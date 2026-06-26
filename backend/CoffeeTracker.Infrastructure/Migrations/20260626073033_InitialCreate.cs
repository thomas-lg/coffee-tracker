using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Coffees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Roaster = table.Column<string>(type: "TEXT", nullable: false),
                    Origin = table.Column<string>(type: "TEXT", nullable: false),
                    RoastLevel = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    DateBought = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    PhotoPath = table.Column<string>(type: "TEXT", nullable: true),
                    ShopName = table.Column<string>(type: "TEXT", nullable: true),
                    PurchaseUrl = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coffees", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coffees");
        }
    }
}
