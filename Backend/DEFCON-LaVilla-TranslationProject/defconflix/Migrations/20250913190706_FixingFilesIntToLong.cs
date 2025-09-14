using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class FixingFilesIntToLong : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "size_bytes",
                table: "files",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<long>(
                name: "ProcessedBy",
                table: "files",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "size_bytes",
                table: "files",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<int>(
                name: "ProcessedBy",
                table: "files",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
