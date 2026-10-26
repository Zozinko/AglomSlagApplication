using System.Buffers.Binary;
using System.Security.Cryptography;
using AglomSlagServer.Errors;
using DBStructure.DbContexts;
using DBStructure.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace AglomSlagServer.Auth;

/// <summary>Server-side Identity V3 PBKDF2 credentials; never accepts the former plaintext seed.</summary>
public sealed class CredentialService(IPasswordHasher<User> hasher)
{
    public const int IterationCount = 210_000;
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 1024;

    private static readonly User Dummy = new() { UserEmail = "", PasswordHash = "" };
    // A single process-local hash uses exactly the production cost, including for disabled,
    // unknown and malformed accounts. No deterministic credential is stored or logged.
    private static readonly string DummyHash = new PasswordHasher<User>(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = IterationCount
    })).HashPassword(Dummy, Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

    public string Hash(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumPasswordLength || password.Length > MaximumPasswordLength)
            throw new ApiException(PublicErrorCodes.ValidationFailed, "Пароль должен содержать от 12 до 1024 символов и не может состоять только из пробелов.");
        return hasher.HashPassword(user, password);
    }

    /// <summary>Caller holds the user row lock in a transaction before verification and issuing a session.</summary>
    public async Task<bool> VerifyAsync(AuthContext db, User? user, string password)
    {
        var usable = user is { Disabled: false } && IsSupportedHash(user.PasswordHash);
        var candidate = password.Length <= MaximumPasswordLength ? password : "";
        PasswordVerificationResult result;
        try
        {
            result = hasher.VerifyHashedPassword(usable ? user! : Dummy, usable ? user!.PasswordHash : DummyHash, candidate);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or CryptographicException)
        {
            // Invalid stored credentials are reset by an operator; never fall back to plaintext.
            _ = hasher.VerifyHashedPassword(Dummy, DummyHash, candidate);
            return false;
        }
        if (!usable || result == PasswordVerificationResult.Failed || password.Length > MaximumPasswordLength) return false;
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user!.PasswordHash = hasher.HashPassword(user, password);
            // The database trigger also revokes older refresh sessions on a successful upgrade.
            await db.SaveChangesAsync();
        }
        return true;
    }

    private static bool IsSupportedHash(string hash)
    {
        // Identity's verifier understands the cryptography; this envelope check bounds work
        // and excludes legacy plaintext, V2 and truncated/corrupt values before invoking it.
        if (hash.Length is < 60 or > 1024) return false;
        Span<byte> bytes = stackalloc byte[768];
        if (!Convert.TryFromBase64String(hash, bytes, out var length) || length < 45 || bytes[0] != 1) return false;
        var prf = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(1, 4));
        var iterations = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(5, 4));
        var saltLength = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(9, 4));
        return prf <= 2 && iterations is > 0 and <= 2_000_000 && saltLength >= 16 && saltLength <= (uint)(length - 29);
    }
}
