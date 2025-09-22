using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace defconflix.Migrations
{
    /// <inheritdoc />
    public partial class AddSuccessErrorCounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "CrawlerJobs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancellationRequestedAt",
                table: "CrawlerJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CancelledByUserId",
                table: "CrawlerJobs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FilesSuccessful",
                table: "CrawlerJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FilesWithErrors",
                table: "CrawlerJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancellationRequested",
                table: "CrawlerJobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CrawlerJobs_CancelledByUserId",
                table: "CrawlerJobs",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlerJobs_IsCancellationRequested",
                table: "CrawlerJobs",
                column: "IsCancellationRequested");

            migrationBuilder.CreateIndex(
                name: "IX_CrawlerJobs_StartedByUserId",
                table: "CrawlerJobs",
                column: "StartedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CrawlerJobs_Users_CancelledByUserId",
                table: "CrawlerJobs",
                column: "CancelledByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrawlerJobs_Users_StartedByUserId",
                table: "CrawlerJobs",
                column: "StartedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrawlerJobs_Users_CancelledByUserId",
                table: "CrawlerJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_CrawlerJobs_Users_StartedByUserId",
                table: "CrawlerJobs");

            migrationBuilder.DropIndex(
                name: "IX_CrawlerJobs_CancelledByUserId",
                table: "CrawlerJobs");

            migrationBuilder.DropIndex(
                name: "IX_CrawlerJobs_IsCancellationRequested",
                table: "CrawlerJobs");

            migrationBuilder.DropIndex(
                name: "IX_CrawlerJobs_StartedByUserId",
                table: "CrawlerJobs");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "CrawlerJobs");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "CrawlerJobs");

            migrationBuilder.DropColumn(
                name: "CancelledByUserId",
                table: "CrawlerJobs");

            migrationBuilder.DropColumn(
                name: "FilesSuccessful",
                table: "CrawlerJobs");

            migrationBuilder.DropColumn(
                name: "FilesWithErrors",
                table: "CrawlerJobs");

            migrationBuilder.DropColumn(
                name: "IsCancellationRequested",
                table: "CrawlerJobs");
        }
    }
}
