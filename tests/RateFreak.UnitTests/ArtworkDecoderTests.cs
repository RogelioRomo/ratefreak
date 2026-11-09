using RateFreak.Infrastructure.Artwork;
using SkiaSharp;

namespace RateFreak.UnitTests;

public sealed class ArtworkDecoderTests
{
    [Fact]
    public async Task Rejects_oversized_and_non_image_inputs()
    {
        var decoder = new ArtworkDecoder();
        await Assert.ThrowsAsync<ArgumentException>(() => decoder.DecodeAsync(new MemoryStream(new byte[5 * 1024 * 1024 + 1]), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => decoder.DecodeAsync(new MemoryStream("<svg/>"u8.ToArray()), TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task Decodes_static_input_and_bounds_the_output_dimensions()
    {
        using var bitmap = new SKBitmap(3000, 2); bitmap.Erase(SKColors.Red); using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var result = await new ArtworkDecoder().DecodeAsync(new MemoryStream(png.ToArray()), TestContext.Current.CancellationToken);
        Assert.Equal(2048, result.Width); Assert.InRange(result.Height, 1, 2048); Assert.Equal("image/webp", result.ContentType);
        using var decoded = SKBitmap.Decode(result.Bytes); Assert.Equal(2048, decoded.Width);
    }
    [Fact]
    public async Task Preserves_visible_orientation_while_removing_exif_metadata()
    {
        using var bitmap = new SKBitmap(3, 2); bitmap.Erase(SKColors.Red); using var image = SKImage.FromBitmap(bitmap); using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 95);
        byte[] exif = [0xff,0xe1,0x00,0x22,0x45,0x78,0x69,0x66,0,0,0x49,0x49,0x2a,0,8,0,0,0,1,0,0x12,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];
        var original = jpeg.ToArray(); var input = original[..2].Concat(exif).Concat(original[2..]).ToArray();
        var result = await new ArtworkDecoder().DecodeAsync(new MemoryStream(input), TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Width); Assert.Equal(3, result.Height);
    }

}
