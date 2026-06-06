using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchVectorToSummary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Summaries",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "to_tsvector('english', coalesce(\"ShortSummary\", '') || ' ' || coalesce(\"FullSummary\", '') || ' ' || regexp_replace(coalesce(\"Keywords\"::text, ''), '[\\[\\]\",]', ' ', 'g'))",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_SearchVector",
                table: "Summaries",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Summaries_SearchVector",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Summaries");
        }
    }
}
