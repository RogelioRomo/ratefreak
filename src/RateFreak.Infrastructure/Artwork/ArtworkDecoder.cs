using RateFreak.Application.Catalog;
using SkiaSharp;

namespace RateFreak.Infrastructure.Artwork;

public sealed class ArtworkDecoder : IArtworkDecoder
{
    private static readonly SemaphoreSlim Gate = new(2, 2);
    public async Task<DecodedArtwork> DecodeAsync(Stream input, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            using var bytes = new MemoryStream(); var buffer = new byte[65536]; int count;
            while ((count = await input.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + count > 5 * 1024 * 1024) throw new ArgumentException("Image exceeds 5 MiB.");
                await bytes.WriteAsync(buffer.AsMemory(0, count), ct);
            }
            bytes.Position = 0;
            using var codec = SKCodec.Create(bytes);
            if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) || codec.FrameCount > 1) throw new ArgumentException("A static JPEG, PNG or WebP image is required.");
            var info = codec.Info;
            if (info.Width is < 1 or > 8192 || info.Height is < 1 or > 8192 || (long)info.Width * info.Height > 20_000_000) throw new ArgumentException("Image dimensions exceed the supported bounds.");
            ct.ThrowIfCancellationRequested();
            using var bitmap = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success) throw new ArgumentException("Invalid image content.");
            var orientedWidth = (int)codec.EncodedOrigin >= 5 ? info.Height : info.Width;
            var orientedHeight = (int)codec.EncodedOrigin >= 5 ? info.Width : info.Height;
            var scale = Math.Min(1d, 2048d / Math.Max(orientedWidth, orientedHeight));
            var width = Math.Max(1, (int)Math.Round(orientedWidth * scale)); var height = Math.Max(1, (int)Math.Round(orientedHeight * scale));
            using var resized = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(resized))
            {
                canvas.Scale((float)width / orientedWidth, (float)height / orientedHeight);
                switch ((int)codec.EncodedOrigin)
                {
                    case 2: canvas.Translate(info.Width, 0); canvas.Scale(-1, 1); break;
                    case 3: canvas.Translate(info.Width, info.Height); canvas.RotateDegrees(180); break;
                    case 4: canvas.Translate(0, info.Height); canvas.Scale(1, -1); break;
                    case 5: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                    case 6: canvas.Translate(info.Height, 0); canvas.RotateDegrees(90); break;
                    case 7: canvas.Translate(info.Height, info.Width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                    case 8: canvas.Translate(0, info.Width); canvas.RotateDegrees(270); break;
                }
                canvas.DrawBitmap(bitmap, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            }
            using var image = SKImage.FromBitmap(resized); using var encoded = image.Encode(SKEncodedImageFormat.Webp, 85);
            if (encoded is null) throw new ArgumentException("Image could not be encoded.");
            ct.ThrowIfCancellationRequested(); return new(encoded.ToArray(), "image/webp", width, height);
        }
        finally { Gate.Release(); }
    }
}
