using System.Net.Mail;
using DBStructure.DbContexts;
using DBStructure.Entities.Auth;
using Microsoft.EntityFrameworkCore;

namespace AglomSlagServer.Auth;

public static class DemoUserBootstrap
{
    public const int UserId = -1;
    public const int AdminId = -2;

    // Idempotent startup setup for the demo; existing accounts keep their passwords.
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var ordinary = ReadAccount("DEMO_USER", UserId, false);
        var admin = ReadAccount("DEMO_ADMIN", AdminId, true);
        if (ordinary.Email == admin.Email)
            throw new InvalidOperationException("Demo bootstrap refused: the two accounts require distinct emails.");

        using var scope = services.CreateScope();
        var scoped = scope.ServiceProvider;
        await using var db = await scoped.GetRequiredService<IDbContextFactory<AuthContext>>().CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var credentials = scoped.GetRequiredService<CredentialService>();
        foreach (var account in new[] { ordinary, admin })
        {
            var existing = await db.Users.SingleOrDefaultAsync(x => x.UserId == account.Id);
            var collision = await db.Users.AnyAsync(x => x.UserEmail == account.Email && x.UserId != account.Id);
            if (collision)
                throw new InvalidOperationException("Demo bootstrap refused: email belongs to a different account.");

            if (existing is not null)
            {
                // SecureCredentials deliberately disabled the original plaintext demo -1.
                // Only this exact legacy account may be revived by the explicit command.
                var legacy = account.Id == UserId && existing.Disabled && !existing.Admin && existing.PasswordHash == "";
                if (!legacy)
                {
                    if (existing.Disabled || existing.Admin != account.Admin || existing.UserEmail != account.Email)
                        throw new InvalidOperationException("Demo bootstrap refused: an existing fixed-ID account has different identity or role.");
                    continue; // Idempotent: never silently reset a configured account's password or role.
                }
            }
            else
            {
                existing = new User { UserId = account.Id, UserEmail = account.Email, PasswordHash = "" };
                db.Users.Add(existing);
            }
            existing.UserEmail = account.Email;
            existing.UserName = account.Admin ? "Demo administrator" : "Demo user";
            existing.Admin = account.Admin;
            existing.Disabled = false;
            existing.PasswordHash = credentials.Hash(existing, account.Password);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private static (int Id, bool Admin, string Email, string Password) ReadAccount(string prefix, int id, bool admin)
    {
        // Environment only: never accept JSON configuration containing demo credentials.
        var email = Environment.GetEnvironmentVariable(prefix + "_EMAIL")?.Trim().ToLowerInvariant();
        var password = Environment.GetEnvironmentVariable(prefix + "_PASSWORD");
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254 ||
            !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email ||
            string.IsNullOrWhiteSpace(password) || password.Length is < 12 or > 1024)
            throw new InvalidOperationException("Configuration error: demo bootstrap requires two valid emails and passwords of 12..1024 characters in environment variables.");
        return (id, admin, email, password);
    }
}
