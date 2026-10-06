using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialVideoDownloader.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryGistUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GistUrl",
                table: "DownloadCategories",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GistUrl",
                table: "DownloadCategories");
        }
    }
}
