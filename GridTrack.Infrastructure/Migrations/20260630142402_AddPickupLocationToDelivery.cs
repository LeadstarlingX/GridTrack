using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GridTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupLocationToDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "PickupLocation",
                table: "Deliveries",
                type: "geometry (point)",
                nullable: true);

            // Backfill existing rows. The route computed at assignment time goes
            // driver-position -> delivery's-location-at-that-moment, which is the pickup
            // point (CurrentLocation hasn't been overwritten by MarkPickedUp yet). So the
            // highest-Sequence waypoint in delivery_routes is the best available record of
            // the original pickup point. Fall back to CurrentLocation when no route exists.
            migrationBuilder.Sql("""
                UPDATE "Deliveries" d
                SET "PickupLocation" = COALESCE(
                    (SELECT ST_SetSRID(ST_MakePoint(dr."Lng", dr."Lat"), 4326)
                     FROM delivery_routes dr
                     WHERE dr."DeliveryId" = d."DeliveryId"
                     ORDER BY dr."Sequence" DESC
                     LIMIT 1),
                    d."CurrentLocation"
                );
                """);

            migrationBuilder.AlterColumn<Point>(
                name: "PickupLocation",
                table: "Deliveries",
                type: "geometry (point)",
                nullable: false,
                oldClrType: typeof(Point),
                oldType: "geometry (point)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupLocation",
                table: "Deliveries");
        }
    }
}
