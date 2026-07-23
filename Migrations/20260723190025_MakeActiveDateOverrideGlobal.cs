using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sales.Migrations
{
    /// <inheritdoc />
    public partial class MakeActiveDateOverrideGlobal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The table was previously per-user, so it may currently hold more than one
            // row (one per user who's had an override set that hasn't auto-cleared yet).
            // Once UserId is gone, the app treats this as a single-row table and just
            // grabs the first match — keep only the most-recently-set row so that lookup
            // is unambiguous instead of picking an arbitrary leftover row.
            migrationBuilder.Sql(
                @"DELETE FROM ""UserActiveDateOverrides"" " +
                @"WHERE ""Id"" NOT IN ( " +
                @"    SELECT ""Id"" FROM ""UserActiveDateOverrides"" ORDER BY ""SetAt"" DESC LIMIT 1 " +
                @");");

            // IF EXISTS guards against re-running on a database where a previous,
            // interrupted deploy already dropped the column but didn't get to record
            // the migration in __EFMigrationsHistory (Postgres-specific syntax — fine
            // since this project only targets Postgres).
            migrationBuilder.Sql(
                @"ALTER TABLE ""UserActiveDateOverrides"" " +
                @"DROP COLUMN IF EXISTS ""UserId"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                @"ALTER TABLE ""UserActiveDateOverrides"" " +
                @"ADD COLUMN IF NOT EXISTS ""UserId"" integer NOT NULL DEFAULT 0;");
        }
    }
}
