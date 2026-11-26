using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexingRunItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexingRunItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IndexingRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    IndexedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexingRunItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexingRunItems_IndexingRuns_IndexingRunId",
                        column: x => x.IndexingRunId,
                        principalTable: "IndexingRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRunItems_DocumentId",
                table: "IndexingRunItems",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRunItems_IndexingRunId_SourceId",
                table: "IndexingRunItems",
                columns: new[] { "IndexingRunId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IndexingRunItems_Status",
                table: "IndexingRunItems",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexingRunItems");
        }
    }
}
