using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GridTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnomalyTypeToDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AnomalyTypeValue",
                table: "Deliveries",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnomalyTypeValue",
                table: "Deliveries");
        }
    }
}
