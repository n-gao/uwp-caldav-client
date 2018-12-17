using Microsoft.EntityFrameworkCore.Migrations;

namespace CalDav.Model.Migrations
{
    public partial class AddedEtag : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Etag",
                table: "Appointments",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Etag",
                table: "Appointments");
        }
    }
}
