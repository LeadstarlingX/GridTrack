using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GridTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverShiftAndCapacity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ShiftEndsAt",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShiftStartedAt",
                table: "Drivers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "VehicleCapacityKg",
                table: "Drivers",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShiftEndsAt",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ShiftStartedAt",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "VehicleCapacityKg",
                table: "Drivers");
        }
    }
}
