using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CoffeeTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RoastLevelEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RoastLevel is now an enum stored as its name. Normalize any pre-existing
            // free-text values (e.g. "medium-dark", "DARK") to the canonical enum names
            // so they round-trip through the value converter — mirrors roastBucket().
            migrationBuilder.Sql(
                @"UPDATE Coffees SET RoastLevel = CASE
                    WHEN lower(RoastLevel) LIKE '%light%' THEN 'Light'
                    WHEN lower(RoastLevel) LIKE '%dark%' THEN 'Dark'
                    ELSE 'Medium' END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
