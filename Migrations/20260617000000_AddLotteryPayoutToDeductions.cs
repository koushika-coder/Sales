using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Migrations
{
    public partial class AddLotteryPayoutToDeductions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LotteryPayout",
                table: "Deductions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LotteryPayout",
                table: "AdminReconciliations",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LotteryPayout",
                table: "Deductions");

            migrationBuilder.DropColumn(
                name: "LotteryPayout",
                table: "AdminReconciliations");
        }
    }
}
