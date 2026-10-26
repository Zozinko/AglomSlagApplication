using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AglomSlagServer.Auth;

public sealed record AuthOptions(string Issuer, string Audience, string SigningKey, int AccessSeconds, int ClockSkewSeconds, int RefreshDays)
{
    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(SigningKey));
    public static AuthOptions Load(IConfiguration config)
    {
        string Required(string name) => !string.IsNullOrWhiteSpace(config[name]) ? config[name]! : throw new InvalidOperationException($"Configuration error: {name} is required.");
        int Number(string name, int fallback, int min, int max) => config[name] is null ? fallback : int.TryParse(config[name], out var v) && v >= min && v <= max ? v : throw new InvalidOperationException($"Configuration error: {name} is invalid.");
        var key = Required("JWT_SIGNING_KEY");
        if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Configuration error: JWT_SIGNING_KEY requires at least 32 UTF-8 bytes.");
        return new(Required("JWT_ISSUER"), Required("JWT_AUDIENCE"), key, Number("JWT_ACCESS_SECONDS", 300, 1, 900), Number("JWT_CLOCK_SKEW_SECONDS", 5, 0, 60), Number("JWT_REFRESH_DAYS", 7, 1, 30));
    }
}
