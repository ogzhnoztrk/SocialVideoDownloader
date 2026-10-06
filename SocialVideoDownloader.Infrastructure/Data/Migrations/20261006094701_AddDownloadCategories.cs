using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialVideoDownloader.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryName",
                table: "DownloadJobs",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "Genel");

            migrationBuilder.CreateTable(
                name: "DownloadCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DownloadCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadCategories_Name",
                table: "DownloadCategories",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DownloadCategories");

            migrationBuilder.DropColumn(
                name: "CategoryName",
                table: "DownloadJobs");
        }
    }
}
