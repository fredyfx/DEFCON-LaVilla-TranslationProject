using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class NewFilesHistoryAndBackground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "last_check_accessible",
                table: "files",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_checked_at",
                table: "files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FileStatusChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileId = table.Column<int>(type: "integer", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: false),
                    IsAccessible = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: true),
                    CheckedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileStatusChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileStatusChecks_Users_CheckedByUserId",
                        column: x => x.CheckedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FileStatusChecks_files_FileId",
                        column: x => x.FileId,
                        principalTable: "files",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileStatusChecks_CheckedAt",
                table: "FileStatusChecks",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FileStatusChecks_CheckedByUserId",
                table: "FileStatusChecks",
                column: "CheckedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FileStatusChecks_FileId",
                table: "FileStatusChecks",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_FileStatusChecks_FileId_CheckedAt",
                table: "FileStatusChecks",
                columns: new[] { "FileId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FileStatusChecks_IsAccessible",
                table: "FileStatusChecks",
                column: "IsAccessible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileStatusChecks");

            migrationBuilder.DropColumn(
                name: "last_check_accessible",
                table: "files");

            migrationBuilder.DropColumn(
                name: "last_checked_at",
                table: "files");
        }
    }
}
