using Azure.Identity;
using EnterpriseAI.Application.Abstractions.AI;
using EnterpriseAI.Application.Abstractions.Chunking;
using EnterpriseAI.Application.Abstractions.Documents;
using EnterpriseAI.Application.Abstractions.Indexing;
using EnterpriseAI.Application.Abstractions.Parsing;
using EnterpriseAI.Application.Abstractions.Persistence;
using EnterpriseAI.Application.Documents.GetBySource;
using EnterpriseAI.Application.Documents.IndexSharePointBatch;
using EnterpriseAI.Application.Documents.IndexSharePointDocument;
using EnterpriseAI.Application.Indexing.GetLatestRuns;
using EnterpriseAI.Application.Indexing.GetRunById;
using EnterpriseAI.Application.Indexing.GetSummary;
using EnterpriseAI.Application.Knowledge.Ask;
using EnterpriseAI.Application.Search.SemanticSearch;
using EnterpriseAI.Infrastructure.AI.Ollama;
using EnterpriseAI.Infrastructure.Documents.Chunking;
using EnterpriseAI.Infrastructure.Documents.Indexing;
using EnterpriseAI.Infrastructure.Documents.Parsing;
using EnterpriseAI.Infrastructure.Documents.Parsing.Docx;
using EnterpriseAI.Infrastructure.Documents.Parsing.Pdf;
using EnterpriseAI.Infrastructure.Documents.Parsing.Xlsx;
using EnterpriseAI.Infrastructure.Documents.SharePoint;
using EnterpriseAI.Infrastructure.Persistence;
using EnterpriseAI.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Graph;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .Validate(
        options =>
            Uri.TryCreate(
                options.BaseUrl,
                UriKind.Absolute,
                out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps),
        "Ollama:BaseUrl must be a valid absolute HTTP or HTTPS URL.")
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Model),
        "Ollama:Model must not be empty.")
    .Validate(
        options => options.TimeoutSeconds is >= 1 and <= 600,
        "Ollama:TimeoutSeconds must be between 1 and 600.")
    .Validate(
        options => options.MaximumOutputTokens is >= 32 and <= 2048,
        "Ollama:MaximumOutputTokens must be between 32 and 2048.")
    .Validate(
        options => options.Temperature is >= 0.0 and <= 2.0,
        "Ollama:Temperature must be between 0.0 and 2.0.")
    .ValidateOnStart();

builder.Services
    .AddOptions<EmbeddingOptions>()
    .Bind(builder.Configuration.GetSection(EmbeddingOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Model),
        "Embedding:Model must not be empty.")
    .Validate(
        options => options.Dimensions > 0,
        "Embedding:Dimensions must be greater than zero.")
    .Validate(
        options => options.BatchSize is >= 1 and <= 128,
        "Embedding:BatchSize must be between 1 and 128.")
    .ValidateOnStart();

builder.Services
    .AddOptions<SharePointOptions>()
    .Bind(
        builder.Configuration.GetSection(SharePointOptions.SectionName))
    .Validate(
        options => Guid.TryParse(
            options.TenantId,
            out _),
        "SharePoint:TenantId must be a valid GUID.")
    .Validate(
        options => Guid.TryParse(
            options.ClientId,
            out _),
            "SharePoint:ClientId must be a valid GUID.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.ClientSecret),
            "SharePoint:ClientSecret must not be empty.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.SiteId),
        "SharePoint:SiteId must not be empty.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(
                options.DriveId),
        "SharePoint:DriveId must not be empty.")
    .Validate(
        options =>
            options.MaximumFileSizeMegabytes
                is >= 1 and <= 500,
        "SharePoint:MaximumFileSizeMegabytes must be between 1 and 500.")
    .Validate(
        options =>
            options.DiscoveryPageSize
                is >= 1 and <= 999,
        "SharePoint:DiscoveryPageSize must be between 1 and 999.")
    .ValidateOnStart();

builder.Services.AddSingleton(
    serviceProvider =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<SharePointOptions>>()
            .Value;

        var credential = new ClientSecretCredential(
            tenantId: options.TenantId,
            clientId: options.ClientId,
            clientSecret: options.ClientSecret);

        string[] scopes =
        [
            "https://graph.microsoft.com/.default"
        ];

        return new GraphServiceClient(credential, scopes);
    });

builder.Services.AddHttpClient<ILlmService, OllamaLlmService>(
    (serviceProvider, client) =>
    {
        var options = serviceProvider
            .GetRequiredService<IOptions<OllamaOptions>>()
            .Value;

        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
    });

builder.Services
    .AddOptions<TextChunkingOptions>()
    .Bind(
        builder.Configuration.GetSection(
            TextChunkingOptions.SectionName))
    .Validate(
        options => options.TargetChunkSize is >= 200 and <= 10000,
        "Chunking:TargetChunkSize must be between 200 and 10000.")
    .Validate(
        options => options.MaximumChunkSize >=
            options.TargetChunkSize,
        "Chunking:MaximumChunkSize must be greater than or equal to TargetChunkSize.")
    .Validate(
        options => options.MaximumChunkSize <= 20000,
        "Chunking:MaximumChunkSize must not exceed 20000.")
    .Validate(
        options => options.OverlapSize >= 0
            && options.OverlapSize <
                options.TargetChunkSize,
        "Chunking:OverlapSize must be non-negative and smaller than TargetChunkSize.")
    .Validate(
        options => options.MinimumChunkSize >= 1
            && options.MinimumChunkSize <=
                options.TargetChunkSize,
        "Chunking:MinimumChunkSize must be between 1 and TargetChunkSize.")
    .Validate(
        options => options.EstimatedCharactersPerToken
            is >= 1 and <= 10,
        "Chunking:EstimatedCharactersPerToken must be between 1 and 10.")
    .ValidateOnStart();

builder.Services
    .AddOptions<IndexingOptions>()
    .Bind(
        builder.Configuration.GetSection(
            IndexingOptions.SectionName))
    .Validate(
        options => options.MaximumBatchSize
            is >= 1 and <= 10000,
        "Indexing:MaximumBatchSize must be " +
        "between 1 and 10000.")
    .Validate(
        options => options.DefaultBatchSize >= 1
            && options.DefaultBatchSize
            <= options.MaximumBatchSize,
        "Indexing:DefaultBatchSize must be between 1 " +
        "and MaximumBatchSize.")
    .ValidateOnStart();

builder.Services
    .AddOptions<DocumentVersioningOptions>()
    .Bind(
        builder.Configuration.GetSection(
            DocumentVersioningOptions.SectionName))
    .Validate(
        options => options.SupersededDocuments.All(
            document =>
                !string.IsNullOrWhiteSpace(document.SourceId)
                && !string.IsNullOrWhiteSpace(
                    document.SupersededBySourceId)),
        "DocumentVersioning:SupersededDocuments entries must have " +
        "both SourceId and SupersededBySourceId.")
    .Validate(
        options => options.SupersededDocuments.All(
            document => !string.Equals(
                document.SourceId.Trim(),
                document.SupersededBySourceId.Trim(),
                StringComparison.Ordinal)),
        "DocumentVersioning:SupersededDocuments entries must not " +
        "supersede themselves.")
    .Validate(
        options =>
        {
            /*
             * Az aktuális változat nem lehet maga is elavult,
             * különben a dokumentumcsalád minden változata
             * kiesne a keresésből.
             */
            var supersededSourceIds = options
                .GetSupersededSourceIds()
                .ToHashSet(StringComparer.Ordinal);

            return options.SupersededDocuments.All(
                document => !supersededSourceIds.Contains(
                    document.SupersededBySourceId.Trim()));
        },
        "DocumentVersioning:SupersededDocuments must not point to " +
        "a SupersededBySourceId that is itself superseded.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>(
    (serviceProvider, client) =>
    {
        var ollamaOptions = serviceProvider
            .GetRequiredService<IOptions<OllamaOptions>>()
            .Value;

        client.BaseAddress = new Uri(
            ollamaOptions.BaseUrl.TrimEnd('/') + "/");

        client.Timeout = TimeSpan.FromSeconds(
            ollamaOptions.TimeoutSeconds);
    });

var connectionString = builder.Configuration
    .GetConnectionString("EnterpriseAI");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Connection string 'EnterpriseAI' is not configured.");
}

builder.Services.AddDbContext<EnterpriseAiDbContext>(
    options =>
    {
        options.UseNpgsql(
            connectionString,
            npgsqlOptions => npgsqlOptions.UseVector());
    });


builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();


builder.Services.AddScoped<GetDocumentBySourceHandler>();
builder.Services.AddScoped<SemanticSearchHandler>();
builder.Services.AddScoped<AskKnowledgeHandler>();
builder.Services.AddScoped<IndexSharePointDocumentHandler>();
builder.Services.AddScoped<IndexSharePointDocumentHandler>();
builder.Services.AddScoped<IndexSharePointBatchHandler>();
builder.Services.AddScoped<GetLatestIndexingRunsHandler>();
builder.Services.AddScoped<GetIndexingRunByIdHandler>();
builder.Services.AddScoped<GetIndexingSummaryHandler>();
builder.Services.AddScoped<StartSharePointBatchHandler>();
builder.Services.AddScoped<ProcessSharePointIndexingJobHandler>();

builder.Services.AddScoped<IDocumentRepository, EfDocumentRepository>();
builder.Services.AddScoped<ISemanticSearchRepository, EfSemanticSearchRepository>();
builder.Services.AddScoped<IIndexingRunRepository, EfIndexingRunRepository>();
builder.Services.AddScoped<IIndexingSummaryRepository, EfIndexingSummaryRepository>();

builder.Services.AddScoped<IDocumentSource,SharePointDocumentSource>();
builder.Services.AddScoped<IDocumentParserResolver, DocumentParserResolver>();
builder.Services.AddScoped<IDocumentParser, PdfDocumentParser>();
builder.Services.AddScoped<IDocumentParser, DocxDocumentParser>();
builder.Services.AddScoped<IDocumentParser, XlsxDocumentParser>();
builder.Services.AddScoped<IChunkingService, TextChunkingService>();

builder.Services.AddSingleton<ISharePointDocumentIndexer, ScopedSharePointDocumentIndexer>();
builder.Services.AddSingleton<ISharePointIndexingQueue, SharePointIndexingQueue>();

builder.Services.AddHostedService<SharePointIndexingBackgroundService>();


builder.Services.AddCors(
    options =>
    {
        options.AddPolicy(
            "AngularDevelopment",
            policy =>
            {
                policy
                    .WithOrigins(
                        "http://localhost:4200")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
    });
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors("AngularDevelopment");
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();