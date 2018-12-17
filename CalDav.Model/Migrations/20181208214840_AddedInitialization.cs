using Microsoft.EntityFrameworkCore.Migrations;

namespace CalDav.Model.Migrations
{
    public partial class AddedInitialization : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Initialized",
                table: "Calendars",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Initialized",
                table: "Calendars");
        }
    }
}
