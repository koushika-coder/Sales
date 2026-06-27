using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Migrations
{
    public partial class AddUserActiveDateOverride : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserActiveDateOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(nullable: false),
                    ActiveDate = table.Column<DateOnly>(nullable: false),
                    SetAt = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActiveDateOverrides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserActiveDateOverrides_UserId",
                table: "UserActiveDateOverrides",
                column: "UserId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UserActiveDateOverrides");
        }
    }
}
