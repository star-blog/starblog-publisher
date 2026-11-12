using System;
using System.Collections.Generic;
using System.Linq;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>Versioned template geometry. All coordinates are in the 1200 x 900 master.</summary>
public static class CoverTemplateCatalog {
    private const int SplitVersion = 3;
    private const int SplitBoundary = 600;
    private const int SplitTextX = 204;

    public static bool IsSupported(string id, int version) => version == SplitVersion ? id == "split" :
        version is 1 or 2 && CoverTemplate.All.Any(t => t.Id == id);
    public static string Description(string id) => id switch {
        "minimal" => "书刊编辑 · 适合长标题 · 纯色背景", "photo" => "全幅摄影 · 需要背景图片",
        "split" => "科技分区 · 需要背景图片", _ => "杂志栏目 · 图片可选 · 固定栏目"
    };
    public static bool RequiresBackground(CoverDocument d) => d.TemplateVersion >= 2 && d.TemplateId is "photo" or "split";
    public static CoverPixelRect BackgroundArea(CoverDocument d) => (d.TemplateId, d.TemplateVersion) switch {
        ("split", SplitVersion) => new(SplitBoundary, 0, 1200 - SplitBoundary, 900),
        ("split", _) => new(696, 0, 504, 900),
        ("column", 2) => new(0, 0, 1200, 420),
        _ => new(0, 0, 1200, 900)
    };
    public static CoverPixelRect SplitAccent(CoverDocument d, CoverLayout layout, Func<CoverElementId, double>? offsetX = null) {
        if (d.TemplateVersion != SplitVersion) return new(184, 250, 4, 406);
        var left = layout.Objects.Where(o => o.Id is CoverElementId.Title or CoverElementId.Subtitle or CoverElementId.Column)
            .Select(o => o.Bounds.X + (offsetX?.Invoke(o.Id) ?? 0)).DefaultIfEmpty(SplitTextX).Min();
        return new((int)Math.Floor(left) - 24, 270, 4, 360);
    }
    public static CoverDocument ApplyLatest(CoverTemplate template, CoverDocument source) {
        var id = template.Id;
        var split = id == "split";
        var ink = id == "minimal" ? "#24272D" : split ? "#18243A" : id == "column" ? "#FFF8EC" : "#FFFFFF";
        var background = id == "minimal" ? "#F6F1E7" : split ? "#EDF2FA" : id == "column" ? "#542F3E" : "#14202B";
        // Balance the text box within the part of the left panel visible in the centered square crop.
        var x = split ? SplitTextX : 216;
        var width = split ? SplitBoundary - CoverSafeZone.Padding - x : 768;
        var titleSize = id == "minimal" ? 84 : id == "photo" ? 80 : 64;
        CoverTextElement Text(double y, double h, int size, bool bold) => new() {
            X = x, Y = y, Width = width, Height = h, FontSize = size, Color = ink, Bold = bold
        };
        return source with {
            TemplateId = id, TemplateVersion = split ? SplitVersion : 2, BackgroundColor = background, Color = ink,
            FontSize = titleSize,
            Manual = false, Crops = new(), BackgroundX = 0, BackgroundY = 0, Zoom = 1, TextX = 0, TextY = 0,
            Scrim = id == "photo" ? .68 : 0,
            Elements = split ? new() {
                Title = Text(320, 224, titleSize, true),
                Column = Text(270, 34, 22, false),
                Subtitle = Text(560, 68, 24, false),
                Logo = new(x + width - 64, 270, 64, 40)
            } : new() {
                Title = Text(id == "column" ? 474 : 316, id == "column" ? 142 : 242, titleSize, true),
                Column = Text(id == "column" ? 432 : 252, 34, 22, false),
                Subtitle = Text(id == "column" ? 622 : 594, 34, 24, false),
                Logo = new(934, id == "column" ? 432 : 250, 64, 40)
            }
        };
    }
    public static CoverDocument Create(string title = "") => ApplyLatest(CoverTemplate.All[0], new() { Title = title });
    public static string PromptContext(CoverDocument d) => d.TemplateId switch {
        "split" when d.TemplateVersion == SplitVersion => "The photo fills the right half of a 1200x900 cover in a vertical 2:3 panel. Center the subject and leave space around it for the wide and square crops. Do not generate text.",
        "split" => "The photo fills the right 42% of a 1200x900 cover. Keep the left area clear for typography. Do not generate text.",
        "column" when d.TemplateVersion == 2 => "The photo is cropped into a wide 1200x420 top strip. Place the subject within that horizontal strip; the lower panel is reserved for the column title. Do not generate text.",
        "photo" => "Full-bleed photo on a 1200x900 cover. Keep the central lower area quiet for a large, readable title. Do not generate text.",
        _ => "Keep the central area clear for typography on a 1200x900 cover. Do not generate text."
    };
}
