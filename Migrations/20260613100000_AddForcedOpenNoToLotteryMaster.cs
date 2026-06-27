using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Migrations
{
    public partial class AddForcedOpenNoToLotteryMaster : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ForcedOpenNo",
                table: "LotteryMaster",
                type: "int",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForcedOpenNo",
                table: "LotteryMaster");
        }
    }
}
