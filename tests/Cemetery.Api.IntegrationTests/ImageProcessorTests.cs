using System.Buffers.Binary;
using System.Text;
using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Imaging;
using SkiaSharp;

namespace Cemetery.Api.IntegrationTests;

public sealed class ImageProcessorTests
{
    private static readonly SkiaImageProcessor Processor = new();

    [Fact]
    public void A_large_png_becomes_a_1600_full_and_a_320_thumbnail_jpeg()
    {
        var result = Processor.Process(Image(3200, 1600, SKEncodedImageFormat.Png));

        Assert.Equal((1600, 800), (result.Width, result.Height));
        AssertJpeg(result.Full, 1600, 800);
        AssertJpeg(result.Thumbnail, 320, 160);
    }

    [Fact]
    public void A_large_jpeg_is_decoded_at_a_reduced_scale_and_still_fills_1600()
    {
        var result = Processor.Process(Image(3300, 2000, SKEncodedImageFormat.Jpeg));

        Assert.Equal(1600, Math.Max(result.Width, result.Height));
        AssertJpeg(result.Full, result.Width, result.Height);
        AssertJpeg(result.Thumbnail, 320, Math.Max(1, (int)Math.Round(result.Height * 320.0 / result.Width)));
    }

    [Fact]
    public void A_small_webp_is_never_upscaled()
    {
        var result = Processor.Process(Image(100, 50, SKEncodedImageFormat.Webp));

        Assert.Equal((100, 50), (result.Width, result.Height));
        AssertJpeg(result.Full, 100, 50);
        AssertJpeg(result.Thumbnail, 100, 50);
    }

    [Fact]
    public void Transparent_pixels_become_white()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(20, 20, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        var result = Processor.Process(Encode(bitmap, SKEncodedImageFormat.Png));

        using var decoded = SKBitmap.Decode(result.Full);
        var pixel = decoded.GetPixel(10, 10);
        Assert.True(pixel.Red > 240 && pixel.Green > 240 && pixel.Blue > 240, pixel.ToString());
    }

    [Theory]
    [InlineData(1, 40, 20, "left")]
    [InlineData(2, 40, 20, "right")]
    [InlineData(3, 40, 20, "right")]
    [InlineData(4, 40, 20, "left")]
    [InlineData(5, 20, 40, "top")]
    [InlineData(6, 20, 40, "top")]
    [InlineData(7, 20, 40, "bottom")]
    [InlineData(8, 20, 40, "bottom")]
    public void The_exif_orientation_is_applied_so_the_photo_is_upright(ushort orientation, int width, int height, string redSide)
    {
        var result = Processor.Process(WithExif(SplitImage(), orientation));

        Assert.Equal((width, height), (result.Width, result.Height));
        using var decoded = SKBitmap.Decode(result.Full);
        Assert.Equal((width, height), (decoded.Width, decoded.Height));
        var (redX, redY, blueX, blueY) = redSide switch
        {
            "left" => (5, 10, 35, 10),
            "right" => (35, 10, 5, 10),
            "top" => (10, 5, 10, 35),
            _ => (10, 35, 10, 5),
        };
        AssertRed(decoded.GetPixel(redX, redY));
        AssertBlue(decoded.GetPixel(blueX, blueY));
    }

    [Fact]
    public void Exif_and_gps_metadata_are_dropped()
    {
        var input = WithExif(SplitImage(), 6);
        Assert.True(Contains(input, "Exif"u8));
        Assert.True(Contains(input, "GPS-SECRET"u8));

        var result = Processor.Process(input);

        foreach (var output in new[] { result.Full, result.Thumbnail })
        {
            Assert.False(Contains(output, "Exif"u8));
            Assert.False(Contains(output, "GPS-SECRET"u8));
            Assert.False(HasApp1(output));
        }
    }

    [Fact]
    public void Garbage_is_refused_as_an_invalid_type()
    {
        var garbage = Assert.Throws<DomainRuleException>(() => Processor.Process(Encoding.ASCII.GetBytes("definitely not an image")));
        var truncated = Assert.Throws<DomainRuleException>(() => Processor.Process([0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10]));

        Assert.Equal("photo.type_invalid", garbage.Code);
        Assert.Equal("photo.type_invalid", truncated.Code);
    }

    [Fact]
    public void An_image_with_too_many_pixels_is_refused()
    {
        var bomb = ResizedPng(Image(1, 1, SKEncodedImageFormat.Png), 20_000, 20_000);

        var error = Assert.Throws<DomainRuleException>(() => Processor.Process(bomb));

        Assert.Equal("photo.too_large", error.Code);
    }

    private static byte[] Image(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SeaGreen);
            using var paint = new SKPaint { Color = SKColors.Orange };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint);
        }

        return Encode(bitmap, format);
    }

    /// <summary>A 40 × 20 JPEG, red on the left half and blue on the right.</summary>
    private static byte[] SplitImage()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(40, 20, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, 20, 20, paint);
        }

        return Encode(bitmap, SKEncodedImageFormat.Jpeg);
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format)
    {
        using var data = bitmap.Encode(format, 95);
        return data.ToArray();
    }

    /// <summary>Inserts an APP1 Exif segment (big-endian TIFF, one Orientation entry, then stray GPS-looking text) right after SOI.</summary>
    private static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        var tiff = new byte[26];
        "MM"u8.CopyTo(tiff);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(2), 42);
        BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(10), 0x0112);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(12), 3);
        BinaryPrimitives.WriteUInt32BigEndian(tiff.AsSpan(14), 1);
        BinaryPrimitives.WriteUInt16BigEndian(tiff.AsSpan(18), orientation);
        byte[] payload = [.. "Exif\0\0"u8, .. tiff, .. "GPS-SECRET 45.81N 15.98E"u8];
        var segment = new byte[4 + payload.Length];
        segment[0] = 0xFF;
        segment[1] = 0xE1;
        BinaryPrimitives.WriteUInt16BigEndian(segment.AsSpan(2), (ushort)(payload.Length + 2));
        payload.CopyTo(segment, 4);
        return [.. jpeg.AsSpan(0, 2), .. segment, .. jpeg.AsSpan(2)];
    }

    /// <summary>Rewrites the IHDR of a PNG to claim another size (with a valid CRC), like a decompression bomb would.</summary>
    private static byte[] ResizedPng(byte[] png, int width, int height)
    {
        var copy = (byte[])png.Clone();
        BinaryPrimitives.WriteInt32BigEndian(copy.AsSpan(16), width);
        BinaryPrimitives.WriteInt32BigEndian(copy.AsSpan(20), height);
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(29), Crc32(copy.AsSpan(12, 17)));
        return copy;
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    private static void AssertJpeg(byte[] bytes, int width, int height)
    {
        Assert.True(bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF);
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal((width, height), (codec.Info.Width, codec.Info.Height));
    }

    private static void AssertRed(SKColor pixel) =>
        Assert.True(pixel.Red > 180 && pixel.Blue < 90, "expected red, got " + pixel);

    private static void AssertBlue(SKColor pixel) =>
        Assert.True(pixel.Blue > 180 && pixel.Red < 90, "expected blue, got " + pixel);

    private static bool Contains(byte[] haystack, ReadOnlySpan<byte> needle) => haystack.AsSpan().IndexOf(needle) >= 0;

    /// <summary>Walks the JPEG header segments up to the start of scan and reports any APP1 (Exif/XMP) segment.</summary>
    private static bool HasApp1(byte[] jpeg)
    {
        var position = 2;
        while (position + 4 <= jpeg.Length && jpeg[position] == 0xFF)
        {
            var marker = jpeg[position + 1];
            if (marker == 0xE1)
                return true;
            if (marker == 0xDA)
                return false;
            position += 2 + BinaryPrimitives.ReadUInt16BigEndian(jpeg.AsSpan(position + 2));
        }

        return false;
    }
}
