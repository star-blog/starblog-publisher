using System;
using System.Collections.Generic;
using SkiaSharp;

namespace StarBlogPublisher.Services.Imaging;

/// <summary>
/// Writes one GIF frame. SkiaSharp decodes GIF, but its encoder returns no data for this format.
/// </summary>
internal static class GifEncoder {
    public static byte[] Encode(SKBitmap bitmap) {
        if (bitmap.ColorType != SKColorType.Rgba8888
            || bitmap.Width is < 1 or > 65535
            || bitmap.Height is < 1 or > 65535) {
            throw new ImageDecodeException(ImageDecodeKind.Invalid);
        }

        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = bitmap.GetPixelSpan();
        var rowBytes = bitmap.RowBytes;
        var counts = new long[32768];
        var sumR = new long[32768];
        var sumG = new long[32768];
        var sumB = new long[32768];
        var transparent = false;
        for (var y = 0; y < height; y++) {
            var row = y * rowBytes;
            for (var x = 0; x < width; x++) {
                var index = row + x * 4;
                if (pixels[index + 3] < 128) {
                    transparent = true;
                    continue;
                }

                var key = ((pixels[index] >> 3) << 10) | ((pixels[index + 1] >> 3) << 5) | (pixels[index + 2] >> 3);
                counts[key]++;
                sumR[key] += pixels[index];
                sumG[key] += pixels[index + 1];
                sumB[key] += pixels[index + 2];
            }
        }

        var used = new List<int>();
        for (var key = 0; key < counts.Length; key++) {
            if (counts[key] > 0) used.Add(key);
        }

        used.Sort((left, right) => counts[right].CompareTo(counts[left]));
        var keep = Math.Min(transparent ? 255 : 256, used.Count);
        var colors = new byte[keep * 3];
        for (var i = 0; i < keep; i++) {
            var key = used[i];
            colors[i * 3] = (byte)(sumR[key] / counts[key]);
            colors[i * 3 + 1] = (byte)(sumG[key] / counts[key]);
            colors[i * 3 + 2] = (byte)(sumB[key] / counts[key]);
        }

        var bucket = new byte[32768];
        for (var key = 0; key < bucket.Length; key++) {
            var red = ((key >> 10) & 31) * 255 / 31;
            var green = ((key >> 5) & 31) * 255 / 31;
            var blue = (key & 31) * 255 / 31;
            var best = int.MaxValue;
            var bestIndex = 0;
            for (var i = 0; i < keep; i++) {
                var dr = colors[i * 3] - red;
                var dg = colors[i * 3 + 1] - green;
                var db = colors[i * 3 + 2] - blue;
                var distance = dr * dr + dg * dg + db * db;
                if (distance >= best) continue;
                best = distance;
                bestIndex = i;
                if (distance == 0) break;
            }

            bucket[key] = (byte)bestIndex;
        }

        var colorBase = transparent ? 1 : 0;
        var indices = new byte[width * height];
        var cursor = 0;
        for (var y = 0; y < height; y++) {
            var row = y * rowBytes;
            for (var x = 0; x < width; x++) {
                var index = row + x * 4;
                indices[cursor++] = pixels[index + 3] < 128
                    ? (byte)0
                    : (byte)(colorBase + bucket[((pixels[index] >> 3) << 10) | ((pixels[index + 1] >> 3) << 5) | (pixels[index + 2] >> 3)]);
            }
        }

        var entries = keep + colorBase;
        var tableSize = 2;
        while (tableSize < Math.Max(entries, 2)) tableSize <<= 1;
        var sizeFlag = 0;
        for (var size = tableSize; size > 2; size >>= 1) sizeFlag++;
        var minCodeSize = Math.Max(2, sizeFlag + 1);

        var output = new List<byte>(indices.Length) { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' };
        WriteUInt16(output, width);
        WriteUInt16(output, height);
        output.Add((byte)(0x80 | (7 << 4) | sizeFlag));
        output.Add(0);
        output.Add(0);
        if (transparent) {
            output.Add(0);
            output.Add(0);
            output.Add(0);
        }

        for (var i = 0; i < colors.Length; i++) output.Add(colors[i]);
        for (var i = colorBase + keep; i < tableSize; i++) {
            output.Add(0);
            output.Add(0);
            output.Add(0);
        }

        if (transparent) {
            output.Add(0x21);
            output.Add(0xF9);
            output.Add(4);
            output.Add(1);
            output.Add(0);
            output.Add(0);
            output.Add(0);
            output.Add(0);
        }

        output.Add(0x2C);
        WriteUInt16(output, 0);
        WriteUInt16(output, 0);
        WriteUInt16(output, width);
        WriteUInt16(output, height);
        output.Add(0);
        WriteLzw(output, indices, minCodeSize);
        output.Add(0x3B);
        return output.ToArray();
    }

    private static void WriteUInt16(List<byte> output, int value) {
        output.Add((byte)value);
        output.Add((byte)(value >> 8));
    }

    private static void WriteLzw(List<byte> output, byte[] indices, int minCodeSize) {
        var clearCode = 1 << minCodeSize;
        var eoiCode = clearCode + 1;
        var codeSize = minCodeSize + 1;
        var nextCode = eoiCode + 1;
        var maxCode = (1 << codeSize) - 1;
        var clearFlag = false;
        var dictionary = new Dictionary<int, int>(4096);
        var packed = new List<byte>(indices.Length);
        var accumulator = 0;
        var bits = 0;

        void WriteCode(int code) {
            accumulator = bits == 0 ? code : (accumulator & ((1 << bits) - 1)) | (code << bits);
            bits += codeSize;
            while (bits >= 8) {
                packed.Add((byte)accumulator);
                accumulator >>= 8;
                bits -= 8;
            }

            // The code just written still uses the old width. Grow on the next code.
            if (nextCode > maxCode || clearFlag) {
                if (clearFlag) {
                    codeSize = minCodeSize + 1;
                    maxCode = (1 << codeSize) - 1;
                    clearFlag = false;
                }
                else {
                    codeSize++;
                    maxCode = codeSize == 12 ? 4096 : (1 << codeSize) - 1;
                }
            }

            if (code != eoiCode) return;
            while (bits > 0) {
                packed.Add((byte)accumulator);
                accumulator >>= 8;
                bits -= 8;
            }
        }

        WriteCode(clearCode);
        if (indices.Length == 0) {
            WriteCode(eoiCode);
        }
        else {
            var prefix = (int)indices[0];
            for (var i = 1; i < indices.Length; i++) {
                var value = indices[i];
                var key = (prefix << 8) | value;
                if (dictionary.TryGetValue(key, out var found)) {
                    prefix = found;
                    continue;
                }

                WriteCode(prefix);
                if (nextCode < 4096) {
                    dictionary[key] = nextCode++;
                }
                else {
                    dictionary.Clear();
                    nextCode = eoiCode + 1;
                    clearFlag = true;
                    WriteCode(clearCode);
                }

                prefix = value;
            }

            WriteCode(prefix);
            WriteCode(eoiCode);
        }

        output.Add((byte)minCodeSize);
        var offset = 0;
        while (offset < packed.Count) {
            var count = Math.Min(255, packed.Count - offset);
            output.Add((byte)count);
            for (var i = 0; i < count; i++) output.Add(packed[offset + i]);
            offset += count;
        }

        output.Add(0);
    }
}
