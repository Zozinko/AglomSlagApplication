using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DBStructure.DbContexts;
using DBStructure.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AglomSlagServer.Auth;

public sealed record IssuedSession(User User, string RefreshToken, DateTime RefreshExpiresAt, Guid FamilyId, Guid SessionId);

public sealed class RefreshSessionService(IDbContextFactory<AuthContext> factory, AuthOptions options)
{
    // Caller owns a transaction and holds the user's FOR UPDATE lock.
    public IssuedSession Create(AuthContext db, User user, Guid? familyId = null)
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;
        var session = new RefreshSession
        {
            Id = Guid.NewGuid(), UserId = user.UserId, FamilyId = familyId ?? Guid.NewGuid(),
            TokenHash = Hash(token), CreatedAt = now, ExpiresAt = now.AddDays(options.RefreshDays)
        };
        db.RefreshSessions.Add(session);
        return new(user, token, session.ExpiresAt, session.FamilyId, session.Id);
    }

    public async Task<IssuedSession> RotateAsync(string token)
    {
        // Our tokens are canonical base64url encodings of exactly 256 random bits.
        if (token.Length != 43 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_'))
            throw AuthService.InvalidCredentials();
        var hash = Hash(token);
        await using var db = await factory.CreateDbContextAsync();
        var userId = await db.RefreshSessions.Where(s => s.TokenHash == hash).Select(s => (int?)s.UserId).SingleOrDefaultAsync();
        if (userId is null) throw AuthService.InvalidCredentials();
        await using var transaction = await db.Database.BeginTransactionAsync();
        // Lock the user first on every mutation. This also serializes password/disabled
        // UPDATEs and their revocation trigger across all API processes.
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"UserId\" = {userId.Value} FOR UPDATE").SingleOrDefaultAsync();
        var session = await db.RefreshSessions.SingleOrDefaultAsync(s => s.TokenHash == hash);
        var now = DateTime.UtcNow;
        if (user is null || session is null) throw AuthService.InvalidCredentials();
        if (user.Disabled || session.RevokedAt is not null || session.ExpiresAt <= now)
        {
            if (user.Disabled) await RevokeUserSessionsAsync(db, user.UserId, now);
            else await RevokeFamilyAsync(db, user.UserId, session.FamilyId, now);
            // Commit replay revocation BEFORE rejecting the request.
            await transaction.CommitAsync();
            throw AuthService.InvalidCredentials();
        }
        var next = Create(db, user, session.FamilyId);
        session.RevokedAt = now;
        session.ReplacedById = next.SessionId;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return next;
    }

    public async Task<bool> LogoutAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue("sub"), out var userId) ||
            !Guid.TryParse(principal.FindFirstValue("sid"), out var family))
            throw AuthService.InvalidCredentials();
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync();
        await RevokeFamilyAsync(db, userId, family, DateTime.UtcNow);
        await transaction.CommitAsync();
        return true;
    }

    public static Task<int> RevokeUserSessionsAsync(AuthContext db, int userId, DateTime nowUtc) =>
        db.RefreshSessions.Where(s => s.UserId == userId && s.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevokedAt, nowUtc));

    private static Task<int> RevokeFamilyAsync(AuthContext db, int userId, Guid family, DateTime nowUtc) =>
        db.RefreshSessions.Where(s => s.UserId == userId && s.FamilyId == family && s.RevokedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(s => s.RevokedAt, nowUtc));

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
}
