using System.Globalization;
using System.Security.Claims;
using DBStructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace AglomSlagServer.Auth;

public sealed class PasswordChangeService(IDbContextFactory<AuthContext> factory, CredentialService credentials,
    LoginThrottle throttle, IHttpContextAccessor http)
{
    public async Task<bool> ChangeAsync(ClaimsPrincipal principal, string currentPassword, string newPassword)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue("sub"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId))
            throw AuthService.InvalidCredentials();
        // Password verification is also exposed here; every alias consumes the same
        // remote-IP budget as Login, even when an attacker holds a valid access token.
        throttle.Check(http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"UserId\"={userId} FOR UPDATE").SingleOrDefaultAsync();
        if (!await credentials.VerifyAsync(db, user, currentPassword)) throw AuthService.InvalidCredentials();
        user!.PasswordHash = credentials.Hash(user, newPassword);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }
}
