using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class AddingConferenceLanguageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "conference",
                table: "files",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "files",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "conference",
                table: "files");

            migrationBuilder.DropColumn(
                name: "language",
                table: "files");
        }
    }
}
