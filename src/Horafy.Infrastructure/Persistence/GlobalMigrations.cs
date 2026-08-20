using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Horafy.Infrastructure.Persistence;

public static class GlobalMigrations
{
    public static async Task RunAsync(HorafyDbContext db, ILogger logger, CancellationToken ct = default)
    {
        const string sql = """
            ALTER TABLE public.users
                ADD COLUMN IF NOT EXISTS phone VARCHAR(20);

            ALTER TABLE public.users
                ADD COLUMN IF NOT EXISTS password_reset_token_hash VARCHAR(64),
                ADD COLUMN IF NOT EXISTS password_reset_expires_at TIMESTAMPTZ;
            """;

        logger.LogInformation("Running global migrations...");
        await db.Database.ExecuteSqlRawAsync(sql, ct);
        logger.LogInformation("Global migrations complete.");
    }
}
