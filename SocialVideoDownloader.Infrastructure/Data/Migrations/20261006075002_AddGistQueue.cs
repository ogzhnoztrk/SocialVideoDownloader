using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialVideoDownloader.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGistQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedUrl",
                table: "DownloadJobs",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "DownloadJobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "DownloadJobs",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Manual");

            migrationBuilder.AddColumn<DateTime>(
                name: "GistLastCheckedUtc",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GistLastMessage",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "GistLastSucceeded",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GistPollIntervalMinutes",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<bool>(
                name: "GistPollingEnabled",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GistUrl",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_NormalizedUrl",
                table: "DownloadJobs",
                column: "NormalizedUrl");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DownloadJobs_NormalizedUrl",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "NormalizedUrl",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "DownloadJobs");

            migrationBuilder.DropColumn(
                name: "GistLastCheckedUtc",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "GistLastMessage",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "GistLastSucceeded",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "GistPollIntervalMinutes",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "GistPollingEnabled",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "GistUrl",
                table: "ApplicationSettings");
        }
    }
}
