using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class supportingVTT : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VttFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Header = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "WEBVTT"),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VttFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VttCues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VttFileId = table.Column<int>(type: "integer", nullable: false),
                    CueId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Settings = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SequenceOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VttCues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VttCues_VttFiles_VttFileId",
                        column: x => x.VttFileId,
                        principalTable: "VttFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VttCues_EndTime",
                table: "VttCues",
                column: "EndTime");

            migrationBuilder.CreateIndex(
                name: "IX_VttCues_StartTime",
                table: "VttCues",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_VttCues_VttFileId",
                table: "VttCues",
                column: "VttFileId");

            migrationBuilder.CreateIndex(
                name: "IX_VttCues_VttFileId_StartTime",
                table: "VttCues",
                columns: new[] { "VttFileId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_VttFiles_CreatedAt",
                table: "VttFiles",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_VttFiles_FileName",
                table: "VttFiles",
                column: "FileName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VttCues");

            migrationBuilder.DropTable(
                name: "VttFiles");
        }
    }
}
