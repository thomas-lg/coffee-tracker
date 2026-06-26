using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CoffeeTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlavorTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlavorTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CoffeeId = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    TastingNotes = table.Column<string>(type: "TEXT", nullable: true),
                    BrewMethod = table.Column<string>(type: "TEXT", nullable: true),
                    Grind = table.Column<string>(type: "TEXT", nullable: true),
                    Ratio = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_Coffees_CoffeeId",
                        column: x => x.CoffeeId,
                        principalTable: "Coffees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlavorTagReview",
                columns: table => new
                {
                    ReviewsId = table.Column<int>(type: "INTEGER", nullable: false),
                    TagsId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlavorTagReview", x => new { x.ReviewsId, x.TagsId });
                    table.ForeignKey(
                        name: "FK_FlavorTagReview_FlavorTags_TagsId",
                        column: x => x.TagsId,
                        principalTable: "FlavorTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FlavorTagReview_Reviews_ReviewsId",
                        column: x => x.ReviewsId,
                        principalTable: "Reviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "FlavorTags",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Fruity" },
                    { 2, "Berry" },
                    { 3, "Citrus" },
                    { 4, "Floral" },
                    { 5, "Chocolatey" },
                    { 6, "Nutty" },
                    { 7, "Caramel" },
                    { 8, "Spicy" },
                    { 9, "Earthy" },
                    { 10, "Winey" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlavorTagReview_TagsId",
                table: "FlavorTagReview",
                column: "TagsId");

            migrationBuilder.CreateIndex(
                name: "IX_FlavorTags_Name",
                table: "FlavorTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CoffeeId_UserId",
                table: "Reviews",
                columns: new[] { "CoffeeId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlavorTagReview");

            migrationBuilder.DropTable(
                name: "FlavorTags");

            migrationBuilder.DropTable(
                name: "Reviews");
        }
    }
}
