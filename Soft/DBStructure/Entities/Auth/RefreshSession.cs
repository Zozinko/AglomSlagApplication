namespace DBStructure.Entities.Auth;

// Persistence only: this entity must never be used as a GraphQL output.
public sealed class RefreshSession
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public Guid FamilyId { get; set; }
    public required string TokenHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ReplacedById { get; set; }
}
