using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewsOverTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CoffeeId_UserId",
                table: "Reviews");

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "Reviews",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CoffeeId_UserId",
                table: "Reviews",
                columns: new[] { "CoffeeId", "UserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CoffeeId_UserId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CoffeeId_UserId",
                table: "Reviews",
                columns: new[] { "CoffeeId", "UserId" },
                unique: true);
        }
    }
}
