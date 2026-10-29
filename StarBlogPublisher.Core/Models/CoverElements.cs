using System;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Models;

public enum CoverElementId { None, Title, Subtitle, Column, Logo, Background }
public enum CoverAlignment { Left, Center, Right }

public sealed record CoverTextElement {
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; } = 760;
    public double Height { get; init; } = 200;
    public int FontSize { get; init; } = 64;
    public string Color { get; init; } = "#FFFFFF";
    public bool Bold { get; init; } = true;
    public double LineHeight { get; init; } = 1.15;
    public CoverAlignment Alignment { get; init; }
}

public sealed record CoverLogoPlacement(double X = 934, double Y = 250, double Width = 64, double Height = 40, bool AlignRight = false);
public sealed record CoverElements {
    public CoverPixelRect? LegacyDecorationArea { get; init; }
    public CoverTextElement Title { get; init; } = new();
    public CoverTextElement Subtitle { get; init; } = new() { FontSize = 26, Bold = false };
    public CoverTextElement Column { get; init; } = new() { FontSize = 22, Bold = false };
    public CoverLogoPlacement Logo { get; init; } = new();
    public CoverTextElement Text(CoverElementId id) => id switch {
        CoverElementId.Subtitle => Subtitle, CoverElementId.Column => Column, _ => Title
    };
    public CoverElements With(CoverElementId id, CoverTextElement value) => id switch {
        CoverElementId.Subtitle => this with { Subtitle = value }, CoverElementId.Column => this with { Column = value }, _ => this with { Title = value }
    };
}

public static class CoverElementContent {
    public static string Content(this CoverDocument d, CoverElementId id) => id switch {
        CoverElementId.Title => d.Title, CoverElementId.Subtitle => d.Subtitle, CoverElementId.Column => d.Column, _ => ""
    };
    public static CoverDocument WithContent(this CoverDocument d, CoverElementId id, string value) => id switch {
        CoverElementId.Title => d with { Title = value }, CoverElementId.Subtitle => d with { Subtitle = value },
        CoverElementId.Column => d with { Column = value }, _ => d
    };
}
