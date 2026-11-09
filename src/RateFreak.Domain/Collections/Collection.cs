namespace RateFreak.Domain.Collections;

public sealed class Collection
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string Visibility { get; private set; } = "private";
    public long Revision { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private Collection() { }
    public Collection(Guid ownerUserId, string name, string? description, string visibility)
    { OwnerUserId = ownerUserId; Name = name.Trim(); Description = description; Visibility = visibility; }
    public void SetMetadata(string name, string? description, string visibility)
    { Name = name.Trim(); Description = description; Visibility = visibility; }
    public void Advance()
    { Revision = checked(Revision + 1); UpdatedAt = DateTimeOffset.UtcNow; }
}
