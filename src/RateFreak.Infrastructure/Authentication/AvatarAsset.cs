namespace RateFreak.Infrastructure.Authentication;

public sealed class AvatarAsset
{
    public Guid UserId { get; set; }
    public Guid AssetId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/webp";
    public int Width { get; set; }
    public int Height { get; set; }
    public long ByteLength { get; set; }
}
