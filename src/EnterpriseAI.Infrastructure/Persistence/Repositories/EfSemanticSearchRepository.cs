using System.Data;
using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace EnterpriseAI.Infrastructure.Persistence.Repositories;

public sealed class EfSemanticSearchRepository : ISemanticSearchRepository
{
    private const int MaximumResultLimit = 100;
    private readonly EnterpriseAiDbContext _dbContext;
    private readonly string[] _supersededSourceIds;

    public EfSemanticSearchRepository(
        EnterpriseAiDbContext dbContext,
        IOptions<DocumentVersioningOptions> versioningOptions)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(versioningOptions);

        _dbContext = dbContext;
        _supersededSourceIds = versioningOptions.Value
            .GetSupersededSourceIds()
            .ToArray();
    }

    public async Task<IReadOnlyList<SemanticSearchResult>>SearchAsync(
            IReadOnlyList<float> queryEmbedding,
            int limit,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(queryEmbedding);

        if (queryEmbedding.Count != DocumentChunk.EmbeddingDimensions)
        {
            throw new ArgumentException(
                $"Query embedding must contain exactly {DocumentChunk.EmbeddingDimensions} values.",
                nameof(queryEmbedding));
        }

        if (limit is < 1 or > MaximumResultLimit)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit),
                $"Limit must be between 1 and {MaximumResultLimit}.");
        }

        var connection = _dbContext.Database.GetDbConnection();
        var openedConnection = false;

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);

                openedConnection = true;
            }

            await using var command = connection.CreateCommand();

            command.CommandText = """
                SELECT
                    document."Id" AS "DocumentId",
                    chunk."Id" AS "ChunkId",
                    document."SourceId" AS "SourceId",
                    document."Title" AS "DocumentTitle",
                    document."SourceUrl" AS "SourceUrl",
                    chunk."Content" AS "Content",
                    chunk."SectionTitle" AS "SectionTitle",
                    chunk."PageNumber" AS "PageNumber",
                    chunk."ChunkIndex" AS "ChunkIndex",
                    chunk."Embedding" <=> @queryEmbedding
                        AS "Distance"
                FROM "DocumentChunks" AS chunk
                INNER JOIN "Documents" AS document
                    ON document."Id" = chunk."DocumentId"
                WHERE
                    document."ProcessingStatus" =
                        @indexedStatus
                    AND document."IsDeleted" = FALSE
                    AND NOT (
                        document."SourceId" =
                            ANY(@supersededSourceIds))
                ORDER BY
                    chunk."Embedding" <=>
                        @queryEmbedding,
                    document."Id",
                    chunk."ChunkIndex"
                LIMIT @limit;
                """;

            var embeddingParameter = new NpgsqlParameter
                {
                    ParameterName = "queryEmbedding",
                    DataTypeName = "vector",
                    Value = new Vector(queryEmbedding.ToArray())
                };

            var indexedStatusParameter = new NpgsqlParameter<int>
                {
                    ParameterName = "indexedStatus",
                    TypedValue = (int)DocumentProcessingStatus.Indexed
                };

            var limitParameter = new NpgsqlParameter<int>
                {
                    ParameterName = "limit",
                    TypedValue = limit
                };

            /*
             * Az elavult dokumentumváltozatok kizárása a LIMIT
             * előtt történik, így a találati lista helyét nem
             * foglalják el.
             */
            var supersededSourceIdsParameter = new NpgsqlParameter<string[]>
                {
                    ParameterName = "supersededSourceIds",
                    NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text,
                    TypedValue = _supersededSourceIds
                };

            command.Parameters.Add(embeddingParameter);
            command.Parameters.Add(indexedStatusParameter);
            command.Parameters.Add(limitParameter);
            command.Parameters.Add(supersededSourceIdsParameter);

            var results = new List<SemanticSearchResult>(limit);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var documentIdOrdinal = reader.GetOrdinal("DocumentId");
            var chunkIdOrdinal = reader.GetOrdinal("ChunkId");
            var sourceIdOrdinal = reader.GetOrdinal("SourceId");
            var documentTitleOrdinal = reader.GetOrdinal("DocumentTitle");
            var sourceUrlOrdinal = reader.GetOrdinal("SourceUrl");
            var contentOrdinal = reader.GetOrdinal("Content");
            var sectionTitleOrdinal = reader.GetOrdinal("SectionTitle");
            var pageNumberOrdinal = reader.GetOrdinal("PageNumber");
            var chunkIndexOrdinal = reader.GetOrdinal("ChunkIndex");
            var distanceOrdinal = reader.GetOrdinal("Distance");

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new SemanticSearchResult(
                        DocumentId: reader.GetGuid(documentIdOrdinal),
                        ChunkId:reader.GetGuid(chunkIdOrdinal),
                        SourceId: reader.GetString(sourceIdOrdinal),
                        DocumentTitle: reader.GetString(documentTitleOrdinal),
                        SourceUrl: reader.GetString(sourceUrlOrdinal),
                        Content: reader.GetString(contentOrdinal),
                        SectionTitle: reader.IsDBNull(sectionTitleOrdinal) ? null : reader.GetString(sectionTitleOrdinal),
                        PageNumber: reader.IsDBNull(pageNumberOrdinal) ? null : reader.GetInt32(pageNumberOrdinal),
                        ChunkIndex: reader.GetInt32(chunkIndexOrdinal),
                        Distance: reader.GetDouble(distanceOrdinal)));
            }

            return results;
        }
        finally
        {
            if (openedConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}