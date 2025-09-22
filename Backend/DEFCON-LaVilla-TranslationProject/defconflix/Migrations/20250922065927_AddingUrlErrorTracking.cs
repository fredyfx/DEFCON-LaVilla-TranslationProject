using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class AddingUrlErrorTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProblematicUris",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalUri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SanitizedUri = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FileName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Extension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ErrorType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ErrorDetails = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PostgresqlError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CrawlerJobId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProblematicUris", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProblematicUris_CrawlerJobs_CrawlerJobId",
                        column: x => x.CrawlerJobId,
                        principalTable: "CrawlerJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProblematicUris_CrawlerJobId",
                table: "ProblematicUris",
                column: "CrawlerJobId");

            migrationBuilder.CreateIndex(
                name: "IX_ProblematicUris_CreatedAt",
                table: "ProblematicUris",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProblematicUris_ErrorType",
                table: "ProblematicUris",
                column: "ErrorType");

            migrationBuilder.CreateIndex(
                name: "IX_ProblematicUris_Extension",
                table: "ProblematicUris",
                column: "Extension");

            migrationBuilder.CreateIndex(
                name: "IX_ProblematicUris_IsResolved",
                table: "ProblematicUris",
                column: "IsResolved");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProblematicUris");
        }
    }
}
