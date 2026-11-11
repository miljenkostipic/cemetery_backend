using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using SkiaSharp;

namespace Cemetery.Infrastructure.Imaging;

/// <summary>
/// Decodes an uploaded photo with SkiaSharp, turns it upright from its EXIF orientation and re-encodes
/// it as a full-size and a thumbnail JPEG. Re-encoding writes pixels only, so EXIF (camera, GPS) is dropped.
/// </summary>
public sealed class SkiaImageProcessor : IImageProcessor
{
    public const int FullEdge = 1600;

    public const int ThumbnailEdge = 320;

    public const int JpegQuality = 82;

    /// <summary>Largest decoded image (pixels) accepted; keeps a small file from expanding into gigabytes.</summary>
    public const long MaxPixels = 80_000_000;

    public ProcessedImage Process(byte[] content)
    {
        using var source = Decode(content, out var origin);
        var (width, height) = Upright(source.Width, source.Height, origin);
        var (fullWidth, fullHeight) = Fit(width, height, FullEdge);
        var (thumbWidth, thumbHeight) = Fit(width, height, ThumbnailEdge);
        var full = Render(source, origin, fullWidth, fullHeight);
        var thumbnail = Render(source, origin, thumbWidth, thumbHeight);
        return new ProcessedImage(full, thumbnail, fullWidth, fullHeight);
    }

    private static SKBitmap Decode(byte[] content, out SKEncodedOrigin origin)
    {
        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data) ?? throw new DomainRuleException("photo.type_invalid");
        origin = codec.EncodedOrigin;
        var info = Target(codec);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is SKCodecResult.Success or SKCodecResult.IncompleteInput)
            return bitmap;
        bitmap.Dispose();
        throw new DomainRuleException("photo.type_invalid");
    }

    /// <summary>Decodes straight to a smaller size when the codec can (JPEG, WebP) and the photo is larger than needed.</summary>
    private static SKImageInfo Target(SKCodec codec)
    {
        var size = codec.Info.Size;
        if ((long)size.Width * size.Height > MaxPixels)
            throw new DomainRuleException("photo.too_large");
        var scale = Math.Min(1f, (float)FullEdge / Math.Max(size.Width, size.Height));
        var scaled = codec.GetScaledDimensions(scale);
        if (scaled.Width < 1 || scaled.Height < 1 || (scale < 1f && Math.Max(scaled.Width, scaled.Height) < FullEdge))
            scaled = size;
        return new SKImageInfo(scaled.Width, scaled.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
    }

    private static (int Width, int Height) Upright(int width, int height, SKEncodedOrigin origin) =>
        SwapsAxes(origin) ? (height, width) : (width, height);

    private static bool SwapsAxes(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

    private static (int Width, int Height) Fit(int width, int height, int edge)
    {
        var longest = Math.Max(width, height);
        if (longest <= edge)
            return (width, height);
        var ratio = (double)edge / longest;
        return (Math.Max(1, (int)Math.Round(width * ratio)), Math.Max(1, (int)Math.Round(height * ratio)));
    }

    private static byte[] Render(SKBitmap source, SKEncodedOrigin origin, int width, int height)
    {
        var (uprightWidth, uprightHeight) = Upright(source.Width, source.Height, origin);
        using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.White);
            canvas.Scale((float)width / uprightWidth, (float)height / uprightHeight);
            Orient(canvas, origin, source.Width, source.Height);
            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }

        using var encoded = target.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded.ToArray();
    }

    /// <summary>Maps the stored pixels (<paramref name="width"/> × <paramref name="height"/>) onto the upright canvas.</summary>
    private static void Orient(SKCanvas canvas, SKEncodedOrigin origin, int width, int height)
    {
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                canvas.Translate(width, 0);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.BottomRight:
                canvas.Translate(width, height);
                canvas.RotateDegrees(180);
                break;
            case SKEncodedOrigin.BottomLeft:
                canvas.Translate(0, height);
                canvas.Scale(1, -1);
                break;
            default:
                OrientSideways(canvas, origin, width, height);
                break;
        }
    }

    private static void OrientSideways(SKCanvas canvas, SKEncodedOrigin origin, int width, int height)
    {
        switch (origin)
        {
            case SKEncodedOrigin.LeftTop:
                canvas.RotateDegrees(90);
                canvas.Scale(1, -1);
                break;
            case SKEncodedOrigin.RightTop:
                canvas.Translate(height, 0);
                canvas.RotateDegrees(90);
                break;
            case SKEncodedOrigin.RightBottom:
                canvas.Translate(height, width);
                canvas.RotateDegrees(90);
                canvas.Scale(-1, 1);
                break;
            case SKEncodedOrigin.LeftBottom:
                canvas.Translate(0, width);
                canvas.RotateDegrees(-90);
                break;
        }
    }
}
