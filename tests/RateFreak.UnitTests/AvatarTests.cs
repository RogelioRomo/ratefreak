using Microsoft.Extensions.Options;
using RateFreak.Infrastructure.Artwork;
using SkiaSharp;

namespace RateFreak.UnitTests;

public sealed class AvatarTests
{
    [Theory]
    [InlineData(1200, 800, 512)]
    [InlineData(40, 80, 40)]
    public async Task Avatar_decoder_center_crops_to_square_without_upscaling(int width, int height, int expected)
    {
        using var bitmap = new SKBitmap(width, height); bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap); using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var result = await new ArtworkDecoder().DecodeAvatarAsync(new MemoryStream(png.ToArray()), TestContext.Current.CancellationToken);
        Assert.Equal(expected, result.Width); Assert.Equal(expected, result.Height); Assert.Equal("image/webp", result.ContentType);
        using var decoded = SKBitmap.Decode(result.Bytes); Assert.Equal(expected, decoded.Width); Assert.Equal(expected, decoded.Height);
    }

    [Fact]
    public async Task Avatar_storage_separates_uploads_and_rejects_directory_symlinks()
    {
        var root = Path.Combine(Path.GetTempPath(), "rf-avatar-" + Guid.NewGuid().ToString("N"));
        var outside = root + "-outside";
        try
        {
            Directory.CreateDirectory(root); Directory.CreateDirectory(outside);
            var storage = new FileAvatarStorage(Options.Create(new ArtworkOptions { Enabled = true, RootPath = root }));
            using (var stage = await storage.StageAsync(new MemoryStream([1, 2, 3]), TestContext.Current.CancellationToken))
            {
                var key = await storage.PromoteAsync(stage, Guid.NewGuid(), TestContext.Current.CancellationToken);
                Assert.True(File.Exists(Path.Combine(root, "avatars", key)));
                Assert.False(File.Exists(Path.Combine(root, key)));
                await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync("../outside.webp", TestContext.Current.CancellationToken));
            }
            Directory.Delete(Path.Combine(root, "avatars"), true);
            Directory.CreateSymbolicLink(Path.Combine(root, "avatars"), outside);
            await Assert.ThrowsAsync<IOException>(() => storage.StageAsync(new MemoryStream([1]), TestContext.Current.CancellationToken));
            Assert.Empty(Directory.EnumerateFiles(outside));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); if (Directory.Exists(outside)) Directory.Delete(outside, true); }
    }
    [Fact]
    public async Task Disposing_a_live_stage_does_not_delete_a_file_through_a_replaced_directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "rf-avatar-lease-" + Guid.NewGuid().ToString("N"));
        var outside = root + "-outside";
        try
        {
            Directory.CreateDirectory(outside);
            var storage = new FileAvatarStorage(Options.Create(new ArtworkOptions { Enabled = true, RootPath = root }));
            var stage = await storage.StageAsync(new MemoryStream([1]), TestContext.Current.CancellationToken);
            var leaf = Path.ChangeExtension(stage.Key, ".lease"); var victim = Path.Combine(outside, leaf);
            await File.WriteAllTextAsync(victim, "keep", TestContext.Current.CancellationToken);
            Directory.Move(Path.Combine(root, "avatars"), Path.Combine(root, "old-avatars"));
            Directory.CreateSymbolicLink(Path.Combine(root, "avatars"), outside);
            stage.Dispose();
            Assert.True(File.Exists(victim));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); if (Directory.Exists(outside)) Directory.Delete(outside, true); }
    }

    [Fact]
    public async Task Avatar_crop_uses_the_center_after_applying_exif_orientation()
    {
        using var bitmap = new SKBitmap(120, 60);
        for (var y=0;y<60;y++) for (var x=0;x<120;x++) bitmap.SetPixel(x,y,x<30?SKColors.Red:x>=90?SKColors.Green:y<30?SKColors.Blue:SKColors.Yellow);
        using var image=SKImage.FromBitmap(bitmap);using var jpeg=image.Encode(SKEncodedImageFormat.Jpeg,100);
        byte[] exif=[0xff,0xe1,0,0x22,0x45,0x78,0x69,0x66,0,0,0x49,0x49,0x2a,0,8,0,0,0,1,0,0x12,1,3,0,1,0,0,0,6,0,0,0,0,0,0,0];
        var bytes=jpeg.ToArray();var input=bytes[..2].Concat(exif).Concat(bytes[2..]).ToArray();
        var result=await new ArtworkDecoder().DecodeAvatarAsync(new MemoryStream(input),TestContext.Current.CancellationToken);
        using var decoded=SKBitmap.Decode(result.Bytes);Assert.Equal(60,decoded.Width);Assert.Equal(60,decoded.Height);
        var left=decoded.GetPixel(10,30);var right=decoded.GetPixel(50,30);
        Assert.True(left.Red>180&&left.Green>180&&left.Blue<70);Assert.True(right.Red<70&&right.Green<70&&right.Blue>180);
    }

}
