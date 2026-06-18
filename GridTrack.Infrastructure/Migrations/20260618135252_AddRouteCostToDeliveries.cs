using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GridTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRouteCostToDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RouteCost",
                table: "Deliveries",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RouteDistanceMeters",
                table: "Deliveries",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RouteDurationSeconds",
                table: "Deliveries",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RouteCost",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "RouteDistanceMeters",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "RouteDurationSeconds",
                table: "Deliveries");
        }
    }
}
