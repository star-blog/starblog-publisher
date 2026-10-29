using SkiaSharp;
using StarBlogPublisher.Services.Imaging;

namespace StarBlogPublisher.Tests.Services.Imaging;

internal static class SkiaFixture {
    public static async Task SaveAsync(string path, int width, int height, byte r, byte g, byte b, byte a, string format) {
        await File.WriteAllBytesAsync(path, EncodeSolid(width, height, r, g, b, a, format));
    }

    public static byte[] EncodeSolid(int width, int height, byte r, byte g, byte b, byte a, string format) {
        using var bitmap = NewBitmap(width, height, a);
        bitmap.Erase(new SKColor(r, g, b, a));
        return Encode(bitmap, format);
    }

    public static Task SaveRandomAsync(string path, int width, int height, bool transparent, int seed, string format) =>
        File.WriteAllBytesAsync(path, EncodeRandom(width, height, transparent, seed, format));

    public static byte[] EncodeRandom(int width, int height, bool transparent, int seed, string format) {
        using var bitmap = NewBitmap(width, height, transparent ? (byte)128 : (byte)255);
        var random = new Random(seed);
        var pixels = bitmap.GetPixelSpan();
        var rowBytes = bitmap.RowBytes;
        for (var y = 0; y < height; y++) {
            var row = y * rowBytes;
            for (var x = 0; x < width; x++) {
                var index = row + x * 4;
                pixels[index] = (byte)random.Next(256);
                pixels[index + 1] = (byte)random.Next(256);
                pixels[index + 2] = (byte)random.Next(256);
                pixels[index + 3] = transparent ? (byte)random.Next(256) : (byte)255;
            }
        }

        return Encode(bitmap, format);
    }

    public static (int Width, int Height) Size(byte[] bytes) {
        using var bitmap = Decode(bytes);
        return (bitmap.Width, bitmap.Height);
    }

    public static (int Width, int Height) SizeOfFile(string path) => Size(File.ReadAllBytes(path));

    public static byte Alpha(byte[] bytes, int x, int y) {
        using var bitmap = Decode(bytes);
        return bitmap.GetPixelSpan()[y * bitmap.RowBytes + x * 4 + 3];
    }

    public static CoverSample Sample(string path, int x, int y) {
        using var bitmap = Decode(File.ReadAllBytes(path));
        var index = y * bitmap.RowBytes + x * 4;
        var pixels = bitmap.GetPixelSpan();
        return new CoverSample(bitmap.Width, bitmap.Height, pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
    }

    public static byte[] EncodeTwoFrameGif() {
        var output = new List<byte>();
        output.AddRange("GIF89a"u8.ToArray());
        WriteUInt16(output, 1);
        WriteUInt16(output, 1);
        output.Add(0xF0);
        output.Add(0);
        output.Add(0);
        output.AddRange([255, 0, 0, 0, 0, 255]);
        output.AddRange([
            0x21, 0xFF, 0x0B,
            (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E',
            (byte)'2', (byte)'.', (byte)'0',
            3, 1, 0, 0, 0
        ]);
        WriteGifFrame(output, [0x02, 0x02, 0x44, 0x01, 0x00]);
        WriteGifFrame(output, [0x02, 0x02, 0x4C, 0x01, 0x00]);
        output.Add(0x3B);
        return output.ToArray();
    }

    public static int FrameCount(byte[] bytes) {
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidOperationException("fixture is not an image");
        return codec.FrameCount;
    }

    public static string Detect(byte[] bytes) {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return "PNG";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return "JPEG";
        if (bytes.Length >= 6 && bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F') return "GIF";
        if (bytes.Length >= 12 && bytes[0] == (byte)'R' && bytes[8] == (byte)'W' && bytes[9] == (byte)'E') return "WEBP";
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M') return "BMP";
        return "UNKNOWN";
    }

    private static SKBitmap Decode(byte[] bytes) {
        using var stream = new MemoryStream(bytes, writable: false);
        using var codec = SKCodec.Create(stream) ?? throw new InvalidOperationException("fixture is not an image");
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        var result = codec.GetPixels(info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) {
            bitmap.Dispose();
            throw new InvalidOperationException($"fixture decode failed: {result}");
        }

        return bitmap;
    }

    private static SKBitmap NewBitmap(int width, int height, byte alpha) =>
        new(new SKImageInfo(width, height, SKColorType.Rgba8888, alpha == 255 ? SKAlphaType.Opaque : SKAlphaType.Unpremul));

    private static byte[] Encode(SKBitmap bitmap, string format) {
        if (format is "gif") return GifEncoder.Encode(bitmap);
        if (format is "bmp") return EncodeBmp(bitmap);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(ToFormat(format), format is "jpg" or "jpeg" ? 95 : 100);
        return (data ?? throw new InvalidOperationException($"cannot encode {format}")).ToArray();
    }

    private static byte[] EncodeBmp(SKBitmap bitmap) {
        var stride = (bitmap.Width * 3 + 3) & ~3;
        var bytes = new byte[54 + stride * bitmap.Height];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        WriteInt(bytes, 2, bytes.Length);
        WriteInt(bytes, 10, 54);
        WriteInt(bytes, 14, 40);
        WriteInt(bytes, 18, bitmap.Width);
        WriteInt(bytes, 22, bitmap.Height);
        bytes[26] = 1;
        bytes[28] = 24;
        var pixels = bitmap.GetPixelSpan();
        for (var y = 0; y < bitmap.Height; y++) {
            var source = (bitmap.Height - 1 - y) * bitmap.RowBytes;
            var destination = 54 + y * stride;
            for (var x = 0; x < bitmap.Width; x++) {
                var index = source + x * 4;
                bytes[destination++] = pixels[index + 2];
                bytes[destination++] = pixels[index + 1];
                bytes[destination++] = pixels[index];
            }
        }

        return bytes;
    }

    private static void WriteGifFrame(List<byte> output, byte[] lzw) {
        output.AddRange([0x21, 0xF9, 0x04, 0x00, 0x0A, 0x00, 0x00, 0x00]);
        output.Add(0x2C);
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        WriteUInt16(output, 1);
        WriteUInt16(output, 1);
        output.Add(0);
        output.AddRange(lzw);
    }

    private static void WriteUInt16(List<byte> output, int value) {
        output.Add((byte)value);
        output.Add((byte)(value >> 8));
    }

    private static void WriteInt(byte[] bytes, int offset, int value) {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static SKEncodedImageFormat ToFormat(string format) => format switch {
        "png" => SKEncodedImageFormat.Png,
        "jpg" or "jpeg" => SKEncodedImageFormat.Jpeg,
        "webp" => SKEncodedImageFormat.Webp,
        _ => throw new ArgumentException(format)
    };
}

internal readonly record struct CoverSample(int Width, int Height, byte R, byte G, byte B, byte A);
