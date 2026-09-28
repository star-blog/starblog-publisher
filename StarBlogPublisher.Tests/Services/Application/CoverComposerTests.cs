using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class CoverSafeZoneTests {
    [Fact]
    public void CanvasCrops_MatchTheWeChatCenterWindows() {
        CoverSafeZone.Headline().Should().Be(new CoverPixelRect(0, 194, 1200, 512));
        CoverSafeZone.Secondary().Should().Be(new CoverPixelRect(150, 0, 900, 900));
        CoverSafeZone.Intersection().Should().Be(new CoverPixelRect(150, 194, 900, 512));
        CoverSafeZone.TextArea().Should().Be(new CoverPixelRect(198, 242, 804, 416));
    }

    [Fact]
    public void TextArea_StaysInsideBothCrops() {
        var headline = CoverSafeZone.Headline();
        var secondary = CoverSafeZone.Secondary();
        var text = CoverSafeZone.TextArea();

        text.X.Should().BeGreaterThanOrEqualTo(Math.Max(headline.X, secondary.X));
        text.Y.Should().BeGreaterThanOrEqualTo(Math.Max(headline.Y, secondary.Y));
        text.Right.Should().BeLessThanOrEqualTo(Math.Min(headline.Right, secondary.Right));
        text.Bottom.Should().BeLessThanOrEqualTo(Math.Min(headline.Bottom, secondary.Bottom));
    }
}

public class CoverComposerTests {
    private readonly CoverComposer _composer = new();

    [Fact]
    public async Task RenderAsync_CoverFitsASolidBackground() {
        await using var background = await SolidPngAsync(new Rgba32(0, 170, 0));

        using var rendered = await _composer.RenderAsync(background, new CoverComposition { Title = "", Scrim = false });

        rendered.Image.Width.Should().Be(CoverComposer.CanvasWidth);
        rendered.Image.Height.Should().Be(CoverComposer.CanvasHeight);
        var pixel = rendered.Image[600, 450];
        pixel.G.Should().BeGreaterThan(150);
        pixel.R.Should().BeLessThan(20);
        rendered.Image[8, 8].G.Should().Be(pixel.G);
    }

    [Fact]
    public async Task RenderAsync_RejectsAnUnreadableBackground() {
        await using var background = new MemoryStream([1, 2, 3, 4, 5]);

        var act = async () => await _composer.RenderAsync(background, new CoverComposition { Title = "" });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*背景图片*");
    }

    [Fact]
    public async Task RenderAsync_ShrinksALongTitleAndKeepsInkInsideBothCrops() {
        using var shortTitle = await _composer.RenderAsync(null, new CoverComposition {
            Title = "封面",
            FontSize = 64,
            Scrim = false,
            Bold = true
        });
        using var longTitle = await _composer.RenderAsync(null, new CoverComposition {
            Title = new string('标', 80),
            FontSize = 64,
            Scrim = false,
            Bold = true
        });

        shortTitle.EffectiveFontSize.Should().Be(64);
        shortTitle.FontFamily.Should().NotBeNullOrWhiteSpace();
        longTitle.EffectiveFontSize.Should().BeLessThan(64);
        longTitle.EffectiveFontSize.Should().BeGreaterThanOrEqualTo(CoverComposer.MinFontSize);
        AssertInkInsideIntersection(shortTitle.Image);
    }

    [Fact]
    public async Task RenderAsync_LeftAlignsFurtherLeftThanCenter() {
        using var centered = await _composer.RenderAsync(null, new CoverComposition {
            Title = "封面",
            Align = CoverTextHorizontalAlign.Center,
            Scrim = false
        });
        using var left = await _composer.RenderAsync(null, new CoverComposition {
            Title = "封面",
            Align = CoverTextHorizontalAlign.Left,
            Scrim = false
        });

        AverageInkX(centered.Image).Should().BeGreaterThan(AverageInkX(left.Image) + 80);
    }

    [Fact]
    public async Task RenderAsync_DrawsACustomColorAndDarkensTheBottomScrim() {
        using var plain = await _composer.RenderAsync(null, new CoverComposition {
            Title = "封面",
            Scrim = false,
            Placement = CoverTextVerticalPlacement.Bottom
        });
        using var shaded = await _composer.RenderAsync(null, new CoverComposition {
            Title = "封面",
            Scrim = true,
            Placement = CoverTextVerticalPlacement.Bottom
        });
        using var red = await _composer.RenderAsync(null, new CoverComposition {
            Title = "封面",
            Color = CoverTextColor.Custom,
            CustomColorHex = "FF0000",
            Scrim = false
        });

        shaded.Image[8, CoverComposer.CanvasHeight - 8].R.Should().BeLessThan(plain.Image[8, CoverComposer.CanvasHeight - 8].R);
        CountInk(red.Image, pixel => pixel.R > 200 && pixel.G < 80 && pixel.B < 80).Should().BeGreaterThan(20);
    }

    [Fact]
    public async Task ComposeAsync_WritesAJpegAndCanFrameTheHeadlineCrop() {
        var composed = await _composer.ComposeAsync(null, new CoverComposition { Title = "" });
        try {
            using (var image = await Image.LoadAsync(composed.Path)) {
                image.Width.Should().Be(CoverComposer.CanvasWidth);
                image.Height.Should().Be(CoverComposer.CanvasHeight);
            }

            var png = _composer.CreatePreviewPng(composed.Path, CoverPreviewFrame.Headline);
            using var preview = Image.Load(new MemoryStream(png));
            ((double)preview.Width / preview.Height).Should().BeApproximately(2.35, 0.05);
        }
        finally {
            File.Delete(composed.Path);
        }
    }

    private static async Task<MemoryStream> SolidPngAsync(Rgba32 color) {
        using var image = new Image<Rgba32>(100, 50, color);
        var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        stream.Position = 0;
        return stream;
    }

    private static void AssertInkInsideIntersection(Image<Rgba32> image) {
        var intersection = CoverSafeZone.Intersection(image.Width, image.Height);
        var found = false;
        image.ProcessPixelRows(accessor => {
            for (var y = 0; y < accessor.Height; y++) {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) {
                    if (!IsLightInk(row[x])) continue;
                    found = true;
                    x.Should().BeGreaterThanOrEqualTo(intersection.X);
                    y.Should().BeGreaterThanOrEqualTo(intersection.Y);
                    x.Should().BeLessThan(intersection.Right);
                    y.Should().BeLessThan(intersection.Bottom);
                }
            }
        });
        found.Should().BeTrue();
    }

    private static double AverageInkX(Image<Rgba32> image) {
        long sum = 0;
        var count = 0;
        image.ProcessPixelRows(accessor => {
            for (var y = 0; y < accessor.Height; y++) {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) {
                    if (!IsLightInk(row[x])) continue;
                    sum += x;
                    count++;
                }
            }
        });
        count.Should().BeGreaterThan(10);
        return sum / (double)count;
    }

    private static int CountInk(Image<Rgba32> image, Func<Rgba32, bool> match) {
        var count = 0;
        image.ProcessPixelRows(accessor => {
            for (var y = 0; y < accessor.Height; y++) {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++) {
                    if (match(row[x])) count++;
                }
            }
        });
        return count;
    }

    private static bool IsLightInk(Rgba32 pixel) => pixel.R > 230 && pixel.G > 230 && pixel.B > 230;
}
