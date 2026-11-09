namespace RateFreak.Domain.Collections;

public sealed class CollectionItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CollectionId { get; private set; }
    public Guid MediaItemId { get; private set; }
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private CollectionItem() { }
    public CollectionItem(Guid collectionId, Guid mediaItemId, int position)
    {
        if (collectionId == Guid.Empty || mediaItemId == Guid.Empty || position < 0) throw new ArgumentException("A membership requires a collection, item and nonnegative position.");
        CollectionId = collectionId; MediaItemId = mediaItemId; Position = position;
    }
    public void Reorder(int position)
    { if (position < 0) throw new ArgumentException("Position must be nonnegative."); Position = position; UpdatedAt = DateTimeOffset.UtcNow; }
}
