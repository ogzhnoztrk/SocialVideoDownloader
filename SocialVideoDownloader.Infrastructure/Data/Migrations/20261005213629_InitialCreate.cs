using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialVideoDownloader.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DownloadDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    FileNameTemplate = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    UseDateFolders = table.Column<bool>(type: "INTEGER", nullable: false),
                    DuplicateCheckEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    YtDlpPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    FfmpegPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    AutoUpdateBinaries = table.Column<bool>(type: "INTEGER", nullable: false),
                    WebPort = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenWebOnStartup = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartWithWindows = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DownloadJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Platform = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Uploader = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    VideoId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    OutputDirectory = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Progress = table.Column<double>(type: "REAL", nullable: false),
                    BytesDownloaded = table.Column<long>(type: "INTEGER", nullable: true),
                    TotalBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ThumbnailUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    Resolution = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Format = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IgnoreDuplicate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_CreatedAt",
                table: "DownloadJobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_Status",
                table: "DownloadJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadJobs_VideoId_Status",
                table: "DownloadJobs",
                columns: new[] { "VideoId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "DownloadJobs");
        }
    }
}
