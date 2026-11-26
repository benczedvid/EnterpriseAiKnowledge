using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexingRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexingRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FolderPath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    RequestedMaximumDocuments = table.Column<int>(type: "integer", nullable: false),
                    ForceReindex = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DiscoveredCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessedCount = table.Column<int>(type: "integer", nullable: false),
                    IndexedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedUnchangedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedUnsupportedCount = table.Column<int>(type: "integer", nullable: false),
                    RequiresOcrCount = table.Column<int>(type: "integer", nullable: false),
                    FailedCount = table.Column<int>(type: "integer", nullable: false),
                    NotFoundCount = table.Column<int>(type: "integer", nullable: false),
                    TotalChunkCount = table.Column<int>(type: "integer", nullable: false),
                    FailureMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexingRuns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRuns_SourceType_StartedAt",
                table: "IndexingRuns",
                columns: new[] { "SourceType", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRuns_StartedAt",
                table: "IndexingRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRuns_Status",
                table: "IndexingRuns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexingRuns");
        }
    }
}
