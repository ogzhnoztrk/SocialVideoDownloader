using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SocialVideoDownloader.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListenAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ListenAddress",
                table: "ApplicationSettings",
                type: "TEXT",
                maxLength: 45,
                nullable: false,
                defaultValue: "127.0.0.1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ListenAddress",
                table: "ApplicationSettings");
        }
    }
}
