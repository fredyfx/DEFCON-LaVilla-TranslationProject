using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class expandingToFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.CreateTable(
            //    name: "files",
            //    columns: table => new
            //    {
            //        id = table.Column<int>(type: "integer", nullable: false)
            //            .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
            //        file_path = table.Column<string>(type: "text", nullable: false),
            //        file_name = table.Column<string>(type: "text", nullable: false),
            //        extension = table.Column<string>(type: "text", nullable: false),
            //        size_bytes = table.Column<int>(type: "integer", nullable: false),
            //        hash = table.Column<string>(type: "text", nullable: false),
            //        status = table.Column<string>(type: "text", nullable: false),
            //        created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            //        updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            //    },
            //    constraints: table =>
            //    {
            //        table.PrimaryKey("PK_files", x => x.id);
            //    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            //migrationBuilder.DropTable(
            //    name: "files");
        }
    }
}
