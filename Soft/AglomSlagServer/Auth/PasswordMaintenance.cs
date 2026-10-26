using System.Globalization;
using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace AglomSlagServer.Auth;

/// <summary>Explicit operator commands. Never invoked during ordinary API startup.</summary>
public static class PasswordMaintenance
{
    public static Task RunAsync(IServiceProvider services, IConfiguration config) => UpdateAsync(services, config, disable: false);
    public static Task DisableAsync(IServiceProvider services, IConfiguration config) => UpdateAsync(services, config, disable: true);

    private static async Task UpdateAsync(IServiceProvider services, IConfiguration config, bool disable)
    {
        if (!int.TryParse(config["TARGET_USER_ID"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
            throw new InvalidOperationException("Password maintenance requires TARGET_USER_ID.");
        var password = config["NEW_PASSWORD"];
        if (!disable && password is null) throw new InvalidOperationException("Password reset requires NEW_PASSWORD.");
        using var scope = services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuthContext>>().CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"UserId\"={userId} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new InvalidOperationException("Password maintenance refused: account does not exist.");
        if (disable) user.Disabled = true;
        else
        {
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<CredentialService>().Hash(user, password!);
            // An explicit operator reset is the migration path for quarantined legacy users.
            user.Disabled = false;
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
