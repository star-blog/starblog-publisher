using System;
using System.Collections.Generic;
using System.Linq;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

internal enum CoverBackgroundMode { None, Optional, Required }

internal sealed record CoverTemplateDefinition(CoverTemplate Template, int Version, string Description) {
    public CoverBackgroundMode Background { get; init; }
    public CoverPixelRect BackgroundArea { get; init; } = new(0, 0, CoverSafeZone.CanvasWidth, CoverSafeZone.CanvasHeight);
    public string BackgroundColor { get; init; } = "#17263C";
    public double Scrim { get; init; }
    public CoverCropSet Crops { get; init; } = new();
    public CoverElements? Elements { get; init; }
    public IReadOnlyList<CoverDecoration> Decorations { get; init; } = [];
    public string PromptContext { get; init; } = CoverTemplateCatalog.DefaultPromptContext;

    public int LegacyFontSize { get; init; } = 64;
    public bool LegacyCentered { get; init; } = true;
    public string LegacyPlacement { get; init; } = "center";
    public double LegacyTextWidth { get; init; } = 1;

    public CoverDocument Apply(CoverDocument source) {
        var design = source with {
            TemplateId = Template.Id, TemplateVersion = Version, BackgroundColor = BackgroundColor, Scrim = Scrim,
            TextX = 0, TextY = 0
        };
        if (Elements is not { } elements) return design with {
            Color = "#FFFFFF", FontSize = LegacyFontSize, Centered = LegacyCentered,
            Placement = LegacyPlacement, TextWidth = LegacyTextWidth
        };
        return design with {
            Color = elements.Title.Color, FontSize = elements.Title.FontSize, Elements = elements,
            Manual = false, Crops = Crops, BackgroundX = 0, BackgroundY = 0, Zoom = 1
        };
    }
}

/// <summary>Versioned template definitions. All rectangles are in master canvas pixels.</summary>
public static class CoverTemplateCatalog {
    internal const string DefaultPromptContext = "Keep the central area clear for typography on a 1200x900 cover. Do not generate text.";

    private static readonly CoverDecoration LegacyAccent = new(new(0, -16, 80, 6), "#52D6B0", 3) {
        Anchor = CoverDecorationAnchor.LegacyTextArea
    };
    private static readonly CoverTemplateDefinition MinimalV1 = new(new("minimal", "纯色大标题"), 1, "书刊编辑 · 适合长标题 · 纯色背景") {
        Decorations = [LegacyAccent]
    };
    private static readonly CoverTemplateDefinition PhotoV1 = new(new("photo", "照片叠字"), 1, "全幅摄影 · 需要背景图片") {
        Background = CoverBackgroundMode.Optional, Scrim = .65, LegacyPlacement = "bottom",
        PromptContext = "Full-bleed photo on a 1200x900 cover. Keep the central lower area quiet for a large, readable title. Do not generate text."
    };
    private static readonly CoverTemplateDefinition SplitV1 = new(new("split", "图文分区"), 1, "科技分区 · 需要背景图片") {
        Background = CoverBackgroundMode.Optional, BackgroundArea = new(696, 0, 504, 900),
        LegacyCentered = false, LegacyTextWidth = .52,
        PromptContext = "The photo fills the right 42% of a 1200x900 cover. Keep the left area clear for typography. Do not generate text."
    };
    private static readonly CoverTemplateDefinition SplitV2 = SplitV1 with {
        Version = 2, Background = CoverBackgroundMode.Required, BackgroundColor = "#EDF2FA",
        Elements = TextElements(216, 448, 72, "#18243A", new(606, 250, 64, 40)),
        Decorations = [new(new(184, 250, 4, 406), "#4169B3")]
    };
    private static readonly CoverTemplateDefinition ColumnV1 = new(new("column", "栏目封面"), 1, "杂志栏目 · 图片可选 · 固定栏目") {
        Background = CoverBackgroundMode.Optional, BackgroundColor = "#41335B", Scrim = .65,
        LegacyFontSize = 60, Decorations = [LegacyAccent]
    };

    private static readonly CoverTemplateDefinition[] Definitions = [
        MinimalV1,
        MinimalV1 with {
            Version = 2, BackgroundColor = "#F6F1E7", Elements = TextElements(216, 768, 84, "#24272D"),
            Decorations = [new(new(216, 286, 72, 5), "#B65D3A")]
        },
        PhotoV1,
        PhotoV1 with {
            Version = 2, Background = CoverBackgroundMode.Required, BackgroundColor = "#14202B", Scrim = .68,
            Elements = TextElements(216, 768, 80, "#FFFFFF")
        },
        SplitV1,
        SplitV2,
        SplitV2 with {
            Version = 3, BackgroundArea = new(600, 0, 600, 900),
            // The default text box is balanced within the left half of the centered square crop.
            Elements = new() {
                Title = Text(204, 320, 348, 224, 64, "#18243A", bold: true),
                Column = Text(204, 270, 348, 34, 22, "#18243A"),
                Subtitle = Text(204, 560, 348, 68, 24, "#18243A"),
                Logo = new(488, 270, 64, 40)
            },
            Decorations = [new(new(180, 270, 4, 360), "#4169B3") { Anchor = CoverDecorationAnchor.TextLeft, TextLeftOffset = -24 }],
            PromptContext = "The photo fills the right half of a 1200x900 cover in a vertical 2:3 panel. Center the subject and leave space around it for the wide and square crops. Do not generate text."
        },
        ColumnV1,
        ColumnV1 with {
            Version = 2, BackgroundArea = new(0, 0, 1200, 420), BackgroundColor = "#542F3E", Scrim = 0,
            Elements = new() {
                Title = Text(216, 474, 768, 142, 64, "#FFF8EC", bold: true),
                Column = Text(216, 432, 768, 34, 22, "#FFF8EC"),
                Subtitle = Text(216, 622, 768, 34, 24, "#FFF8EC"),
                Logo = new(934, 432, 64, 40)
            },
            Decorations = [new(new(0, 416, 1200, 4), "#D7B477")],
            PromptContext = "The photo is cropped into a wide 1200x420 top strip. Place the subject within that horizontal strip; the lower panel is reserved for the column title. Do not generate text."
        }
    ];
    private static readonly Dictionary<(string Id, int Version), CoverTemplateDefinition> ByVersion =
        Definitions.ToDictionary(d => (d.Template.Id, d.Version));
    private static readonly Dictionary<string, CoverTemplateDefinition> Latest = Definitions.GroupBy(d => d.Template.Id)
        .ToDictionary(group => group.Key, group => group.MaxBy(d => d.Version)!);

    public static IReadOnlyList<CoverTemplate> Templates { get; } = Array.AsReadOnly(Latest.Values.Select(d => d.Template).ToArray());

    internal static CoverTemplateDefinition? Find(string id, int version) => ByVersion.GetValueOrDefault((id, version));
    public static bool IsSupported(string id, int version) => Find(id, version) != null;
    public static bool UsesBackgroundImage(CoverDocument d) => Find(d.TemplateId, d.TemplateVersion)?.Background is
        CoverBackgroundMode.Optional or CoverBackgroundMode.Required;
    public static bool RequiresBackground(CoverDocument d) => Find(d.TemplateId, d.TemplateVersion)?.Background == CoverBackgroundMode.Required;
    public static string Description(string id) => Latest[id].Description;
    public static string PromptContext(CoverDocument d) => Find(d.TemplateId, d.TemplateVersion)?.PromptContext ?? DefaultPromptContext;

    public static CoverDocument ApplyLatest(CoverTemplate template, CoverDocument source) => Latest[template.Id].Apply(source);
    internal static CoverDocument ApplyLegacy(CoverTemplate template, CoverDocument source) =>
        (Find(template.Id, 1) ?? Latest[template.Id]).Apply(source);
    public static CoverDocument Create(string title = "") => ApplyLatest(MinimalV1.Template, new() { Title = title });

    private static CoverElements TextElements(int x, int width, int size, string color, CoverLogoPlacement? logo = null) => new() {
        Title = Text(x, 316, width, 242, size, color, bold: true),
        Column = Text(x, 252, width, 34, 22, color),
        Subtitle = Text(x, 594, width, 34, 24, color),
        Logo = logo ?? new()
    };

    private static CoverTextElement Text(int x, int y, int width, int height, int size, string color, bool bold = false) => new() {
        X = x, Y = y, Width = width, Height = height, FontSize = size, Color = color, Bold = bold
    };
}
