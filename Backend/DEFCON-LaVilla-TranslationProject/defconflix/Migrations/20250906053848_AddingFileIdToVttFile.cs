using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class AddingFileIdToVttFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FileId",
                table: "VttFiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileId",
                table: "VttFiles");
        }
    }
}
