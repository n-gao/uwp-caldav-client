using Microsoft.EntityFrameworkCore.Migrations;

namespace CalDav.Model.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Servers",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Host = table.Column<string>(nullable: true),
                    Username = table.Column<string>(nullable: true),
                    Password = table.Column<string>(nullable: true),
                    UserDir = table.Column<string>(nullable: true),
                    CalendarHomeSet = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Servers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Calendars",
                columns: table => new
                {
                    Href = table.Column<string>(nullable: false),
                    Ctag = table.Column<string>(nullable: true),
                    SyncToken = table.Column<string>(nullable: true),
                    DisplayName = table.Column<string>(nullable: true),
                    LocalId = table.Column<string>(nullable: true),
                    ServerId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Calendars", x => x.Href);
                    table.ForeignKey(
                        name: "FK_Calendars_Servers_ServerId",
                        column: x => x.ServerId,
                        principalTable: "Servers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Href = table.Column<string>(nullable: false),
                    CalHref = table.Column<string>(nullable: true),
                    LocalId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Href);
                    table.ForeignKey(
                        name: "FK_Appointments_Calendars_CalHref",
                        column: x => x.CalHref,
                        principalTable: "Calendars",
                        principalColumn: "Href",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CalHref",
                table: "Appointments",
                column: "CalHref");

            migrationBuilder.CreateIndex(
                name: "IX_Calendars_ServerId",
                table: "Calendars",
                column: "ServerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "Calendars");

            migrationBuilder.DropTable(
                name: "Servers");
        }
    }
}
