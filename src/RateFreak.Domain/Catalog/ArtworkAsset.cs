namespace RateFreak.Domain.Catalog;

public sealed class ArtworkAsset
{
    public Guid ItemId { get; set; }
    public Guid AssetId { get; set; }
    public string StorageKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteLength { get; set; }
}
