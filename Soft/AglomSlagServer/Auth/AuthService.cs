using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AglomSlagServer.Errors;
using DBStructure.DbContexts;
using DBStructure.Entities.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AglomSlagServer.Auth;

public sealed record LoginInput(string? Email = null, string? Password = null, string? RefreshToken = null);
public sealed record CurrentUserOutput(int UserId, string? UserName, string UserEmail, bool Admin);
public sealed record LoginPayload(string AccessToken, DateTimeOffset AccessExpiresAt, string RefreshToken, DateTime RefreshExpiresAt, CurrentUserOutput User);

public sealed class AuthService(IDbContextFactory<AuthContext> factory, CredentialService credentials, RefreshSessionService sessions, AuthOptions options, LoginThrottle throttle, IHttpContextAccessor http)
{
    public async Task<LoginPayload> LoginAsync(LoginInput input)
    {
        throttle.Check(http.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (input.RefreshToken is not null)
        {
            if (input.Email is not null || input.Password is not null) throw InvalidCredentials();
            return Issue(await sessions.RotateAsync(input.RefreshToken));
        }
        if (string.IsNullOrWhiteSpace(input.Email) || string.IsNullOrEmpty(input.Password) || input.Email.Length > 254 || input.Password.Length > 1024)
            throw InvalidCredentials();
        await using var db = await factory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var email = input.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"UserEmail\" = {email} FOR UPDATE").SingleOrDefaultAsync();
        var verified = await credentials.VerifyAsync(db, user, input.Password);
        if (!verified || user is null || user.Disabled) throw InvalidCredentials();
        var issued = sessions.Create(db, user);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Issue(issued);
    }

    private LoginPayload Issue(IssuedSession session)
    {
        var user = session.User;
        var now = DateTimeOffset.UtcNow;
        var expiry = now.AddSeconds(options.AccessSeconds);
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience,
            [new Claim("sub", user.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)), new Claim("admin", user.Admin ? "true" : "false"), new Claim("sid", session.FamilyId.ToString("N")), new Claim("jti", Guid.NewGuid().ToString("N"))],
            now.UtcDateTime, expiry.UtcDateTime, new SigningCredentials(options.Key, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expiry, session.RefreshToken, session.RefreshExpiresAt, new(user.UserId, user.UserName, user.UserEmail, user.Admin));
    }

    public static ApiException InvalidCredentials() => new("INVALID_CREDENTIALS", "Неверные учётные данные или сессия.");
}
