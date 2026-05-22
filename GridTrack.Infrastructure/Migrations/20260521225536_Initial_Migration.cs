using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace GridTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Migration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentLocation = table.Column<Point>(type: "geometry (point)", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AssignedDriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedEta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ActualEta = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DistrictId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AnomalyFlag = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PickedUpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnomalyReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.DeliveryId);
                });

            migrationBuilder.CreateTable(
                name: "Drivers",
                columns: table => new
                {
                    DriverId = table.Column<Guid>(type: "uuid", nullable: false),
                    Location = table.Column<Point>(type: "geometry (point)", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DistrictId = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drivers", x => x.DriverId);
                });

            migrationBuilder.CreateTable(
                name: "H3District",
                columns: table => new
                {
                    H3Index = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CenterPoint = table.Column<Point>(type: "geometry (point)", nullable: false),
                    BoundaryPolygon = table.Column<Polygon>(type: "geometry (point)", nullable: false),
                    Resolution = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_H3District", x => x.H3Index);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_AssignedDriverId",
                table: "Deliveries",
                column: "AssignedDriverId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_DistrictId",
                table: "Deliveries",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_Status",
                table: "Deliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_DistrictId",
                table: "Drivers",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_IsActive",
                table: "Drivers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_H3District_Resolution",
                table: "H3District",
                column: "Resolution");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "Drivers");

            migrationBuilder.DropTable(
                name: "H3District");
        }
    }
}
