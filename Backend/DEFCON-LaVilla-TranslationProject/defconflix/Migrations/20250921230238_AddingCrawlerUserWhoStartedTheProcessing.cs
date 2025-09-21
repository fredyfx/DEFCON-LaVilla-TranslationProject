using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class AddingCrawlerUserWhoStartedTheProcessing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "ProcessedBy",
                table: "files",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AddColumn<int>(
                name: "StartedByUserId",
                table: "CrawlerJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartedByUserId",
                table: "CrawlerJobs");

            migrationBuilder.AlterColumn<long>(
                name: "ProcessedBy",
                table: "files",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
