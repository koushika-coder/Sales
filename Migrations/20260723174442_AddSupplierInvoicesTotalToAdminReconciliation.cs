using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoicesTotalToAdminReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS guards against re-running on a database where a previous,
            // interrupted deploy already applied this column but didn't get to record
            // the migration in __EFMigrationsHistory (Postgres-specific syntax — fine
            // since this project only targets Postgres).
            migrationBuilder.Sql(
                @"ALTER TABLE ""AdminReconciliations"" " +
                @"ADD COLUMN IF NOT EXISTS ""SupplierInvoicesTotal"" numeric(18,2) NOT NULL DEFAULT 0.0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""AdminReconciliations"" " +
                @"DROP COLUMN IF EXISTS ""SupplierInvoicesTotal"";");
        }
    }
}
