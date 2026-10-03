using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PGManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomSharingType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SharingType",
                table: "Rooms",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SharingType",
                table: "Rooms");
        }
    }
}
