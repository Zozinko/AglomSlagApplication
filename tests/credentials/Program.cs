using System.Buffers.Binary;
using System.Security.Cryptography;
using AglomSlagServer.Auth;
using AglomSlagServer.Errors;
using DBStructure.Entities.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

// Executable assertions avoid a separate test-framework dependency. API/PostgreSQL
// integration tests cover persistence, transactional rehash and session revocation.
var spy = new RecordingHasher();
var credentials = new CredentialService(spy);
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
var user = new User { UserEmail = "unit@example.invalid", PasswordHash = "" };
user.PasswordHash = credentials.Hash(user, password);
Check(await credentials.VerifyAsync(null!, user, password), "Correct password rejected");
Check(!await credentials.VerifyAsync(null!, user, password + "wrong"), "Wrong password accepted");
var realHash = spy.LastHash!;
var realCost = Cost(realHash);
foreach (var candidate in new User?[]
{
    null,
    new() { UserEmail = user.UserEmail, PasswordHash = user.PasswordHash, Disabled = true },
    new() { UserEmail = user.UserEmail, PasswordHash = "Password" },
    new() { UserEmail = user.UserEmail, PasswordHash = "!invalid-base64!" },
    new() { UserEmail = user.UserEmail, PasswordHash = Convert.ToBase64String(new byte[64]) }
})
{
    spy.Calls = 0;
    Check(!await credentials.VerifyAsync(null!, candidate, password), "Unusable credential accepted");
    Check(spy.Calls == 1, "Unusable credential must execute exactly one password verification");
    Check(spy.LastHash != realHash, "Unusable account must use the dummy credential");
    Check(Cost(spy.LastHash!) == realCost, "Unknown, disabled and malformed users must use the configured KDF cost");
}
Check(realCost == (2u, (uint)CredentialService.IterationCount), "Production KDF parameters differ");
foreach (var invalid in new[] { "", "short", new string(' ', 12), new string('x', 1025) })
{
    var refused = false;
    try { credentials.Hash(user, invalid); }
    catch (ApiException exception) when (exception.Code == PublicErrorCodes.ValidationFailed) { refused = true; }
    Check(refused, "Invalid password was hashed");
}
Console.WriteLine("Credential checks passed: configured KDF, correct/incorrect verification, dummy cost and password bounds.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static (uint Prf, uint Iterations) Cost(string hash)
{
    var bytes = Convert.FromBase64String(hash);
    Check(bytes.Length >= 45 && bytes[0] == 1, "Expected Identity V3 envelope");
    return (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(1, 4)), BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(5, 4)));
}

sealed class RecordingHasher : IPasswordHasher<User>
{
    private readonly PasswordHasher<User> inner = new(Options.Create(new PasswordHasherOptions
    {
        CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
        IterationCount = CredentialService.IterationCount
    }));
    public int Calls { get; set; }
    public string? LastHash { get; private set; }
    public string HashPassword(User user, string password) => inner.HashPassword(user, password);
    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword)
    {
        Calls++;
        LastHash = hashedPassword;
        return inner.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
