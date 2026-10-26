using DBStructure.DbContexts;
using DBStructure.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace AglomSlagServer.Auth;

public static class UserBootstrap
{
    // Explicit local operator command; never runs during normal API startup.
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var email = config["BOOTSTRAP_EMAIL"]?.Trim().ToLowerInvariant();
        var password = config["BOOTSTRAP_PASSWORD"];
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 || !email.Contains('@') || password is null)
            throw new InvalidOperationException("Configuration error: bootstrap requires an email and a password of 12..1024 characters.");
        using var scope = services.CreateScope();
        await using var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuthContext>>().CreateDbContextAsync();
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserEmail == email);
        if (user is not null) throw new InvalidOperationException("Bootstrap refused: account already exists; use an explicit password reset procedure.");
        user = new User { UserEmail = email, UserName = config["BOOTSTRAP_NAME"], PasswordHash = "" };
        user.PasswordHash = scope.ServiceProvider.GetRequiredService<CredentialService>().Hash(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
