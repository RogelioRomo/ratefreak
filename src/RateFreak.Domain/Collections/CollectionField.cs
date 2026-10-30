namespace RateFreak.Domain.Collections;

public sealed class CollectionField
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CollectionId { get; private set; }
    public string Key { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public string FieldType { get; private set; } = null!;
    public bool Required { get; private set; }
    public string Configuration { get; private set; } = "{}";
    public int Position { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    private CollectionField() { }
    public CollectionField(Guid collectionId, string key, string label, string fieldType, bool required, string configuration, int position)
    { CollectionId = collectionId; Key = key; FieldType = fieldType; SetValues(label, required, configuration, position); }
    public void SetValues(string label, bool required, string configuration, int position)
    { Label = label.Trim(); Required = required; Configuration = configuration; Position = position; UpdatedAt = DateTimeOffset.UtcNow; }
}
