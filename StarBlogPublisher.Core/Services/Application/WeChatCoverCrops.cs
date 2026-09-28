using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// Center-crop windows for a WeChat draft cover. Coordinates are fractions of the uploaded image.
/// </summary>
public static class WeChatCoverCrops {
    public const string HeadlineRatio = "2.35_1";
    public const string SecondaryRatio = "1_1";

    public static WeChatCoverCrop[] Center(int width, int height) {
        if (width <= 0 || height <= 0) return [];
        return [
            Crop(width, height, HeadlineRatio, 2.35),
            Crop(width, height, SecondaryRatio, 1d)
        ];
    }

    private static WeChatCoverCrop Crop(int width, int height, string ratio, double aspect) {
        var imageAspect = (double)width / height;
        double x1, y1, x2, y2;
        if (imageAspect > aspect) {
            var cropWidth = height * aspect;
            var left = (width - cropWidth) / 2d;
            x1 = left / width;
            x2 = (left + cropWidth) / width;
            y1 = 0;
            y2 = 1;
        }
        else {
            var cropHeight = width / aspect;
            var top = (height - cropHeight) / 2d;
            x1 = 0;
            x2 = 1;
            y1 = top / height;
            y2 = (top + cropHeight) / height;
        }

        return new WeChatCoverCrop(ratio, Format(x1), Format(y1), Format(x2), Format(y2));
    }

    private static string Format(double value) =>
        Math.Clamp(value, 0, 1).ToString("0.0000", CultureInfo.InvariantCulture);
}

public readonly record struct WeChatCoverCrop(
    [property: JsonPropertyName("ratio")] string Ratio,
    [property: JsonPropertyName("x1")] string X1,
    [property: JsonPropertyName("y1")] string Y1,
    [property: JsonPropertyName("x2")] string X2,
    [property: JsonPropertyName("y2")] string Y2) {
    [JsonIgnore]
    public string Label => Ratio == WeChatCoverCrops.HeadlineRatio ? "头条 2.35:1" : "次条 1:1";

    [JsonIgnore]
    public string Region => $"{X1}, {Y1} → {X2}, {Y2}";
}
