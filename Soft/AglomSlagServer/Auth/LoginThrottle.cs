using System.Threading.RateLimiting;
using AglomSlagServer.Errors;

namespace AglomSlagServer.Auth;

// Checked in the resolver, so aliases and batched fields each consume a permit.
public sealed class LoginThrottle : IDisposable
{
    private readonly PartitionedRateLimiter<string> limiter;
    public LoginThrottle(IConfiguration config)
    {
        var limit = config.GetValue("AUTH_LOGIN_LIMIT", 20);
        if (limit is < 1 or > 1000) throw new InvalidOperationException("Configuration error: AUTH_LOGIN_LIMIT is invalid.");
        limiter = PartitionedRateLimiter.Create<string, string>(key =>
            RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    }
    public void Check(string key)
    {
        using var lease = limiter.AttemptAcquire(key);
        if (!lease.IsAcquired) throw new ApiException("LOGIN_RATE_LIMITED", "Слишком много попыток входа. Повторите позже.");
    }
    public void Dispose() => limiter.Dispose();
}
