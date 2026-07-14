using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GridTrack.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update_AppUser_Table_Name : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                table: "Users");

            migrationBuilder.RenameTable(
                name: "Users",
                newName: "app_users");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Username",
                table: "app_users",
                newName: "IX_app_users_Username");

            migrationBuilder.AddPrimaryKey(
                name: "PK_app_users",
                table: "app_users",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_app_users",
                table: "app_users");

            migrationBuilder.RenameTable(
                name: "app_users",
                newName: "Users");

            migrationBuilder.RenameIndex(
                name: "IX_app_users_Username",
                table: "Users",
                newName: "IX_Users_Username");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "UserId");
        }
    }
}
