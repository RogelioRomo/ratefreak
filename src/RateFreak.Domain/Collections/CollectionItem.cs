namespace RateFreak.Domain.Collections;

public sealed class CollectionItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CollectionId { get; private set; }
    public Guid? MediaItemId { get; private set; }
    public string? Title { get; private set; }
    public string Data { get; private set; } = "{}";
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private CollectionItem() { }
    public CollectionItem(Guid collectionId, Guid? mediaItemId, string? title, string data, int position)
    { CollectionId = collectionId; MediaItemId = mediaItemId; SetValues(title, data, position); }
    public void SetValues(string? title, string data, int position)
    { Title = title?.Trim(); Data = data; Position = position; UpdatedAt = DateTimeOffset.UtcNow; }
}
