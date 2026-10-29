using System;
using System.Collections.Generic;
using System.Linq;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>Versioned template geometry. All coordinates are in the 1200 x 900 master.</summary>
public static class CoverTemplateCatalog {
    public static bool IsSupported(string id, int version) => version is 1 or 2 && CoverTemplate.All.Any(t => t.Id == id);
    public static string Description(string id) => id switch {
        "minimal" => "书刊编辑 · 适合长标题 · 纯色背景", "photo" => "全幅摄影 · 需要背景图片",
        "split" => "科技分区 · 需要背景图片", _ => "杂志栏目 · 图片可选 · 固定栏目"
    };
    public static CoverPixelRect BackgroundArea(CoverDocument d) => d.TemplateId == "split" ? new(696, 0, 504, 900) :
        d.TemplateVersion == 2 && d.TemplateId == "column" ? new(0, 0, 1200, 420) : new(0, 0, 1200, 900);
    public static CoverDocument ApplyLatest(CoverTemplate template, CoverDocument source) {
        var id = template.Id;
        var ink = id == "minimal" ? "#24272D" : id == "split" ? "#18243A" : id == "column" ? "#FFF8EC" : "#FFFFFF";
        var background = id == "minimal" ? "#F6F1E7" : id == "split" ? "#EDF2FA" : id == "column" ? "#542F3E" : "#14202B";
        var x = 216d; var width = id == "split" ? 448d : 768d;
        CoverTextElement Text(double y, double h, int size, bool bold) => new() {
            X = x, Y = y, Width = width, Height = h, FontSize = size, Color = ink, Bold = bold
        };
        return source with {
            TemplateId = id, TemplateVersion = 2, BackgroundColor = background, Color = ink,
            FontSize = id == "minimal" ? 84 : id == "photo" ? 80 : id == "split" ? 72 : 64,
            Manual = false, Crops = new(), BackgroundX = 0, BackgroundY = 0, Zoom = 1, TextX = 0, TextY = 0,
            Scrim = id == "photo" ? .68 : 0,
            Elements = new() {
                Title = Text(id == "column" ? 474 : 316, id == "column" ? 142 : 242, id == "minimal" ? 84 : id == "photo" ? 80 : id == "split" ? 72 : 64, true),
                Column = Text(id == "column" ? 432 : 252, 34, 22, false),
                Subtitle = Text(id == "column" ? 622 : 594, 34, 24, false),
                Logo = new(id == "split" ? 606 : 934, id == "column" ? 432 : 250, 64, 40)
            }
        };
    }
    public static CoverDocument Create(string title = "") => ApplyLatest(CoverTemplate.All[0], new() { Title = title });
    public static string PromptContext(CoverDocument d) => d.TemplateId switch {
        "split" => "The photo fills the right 42% of a 1200x900 cover. Keep the left area clear for typography. Do not generate text.",
        "column" when d.TemplateVersion == 2 => "The photo is cropped into a wide 1200x420 top strip. Place the subject within that horizontal strip; the lower panel is reserved for the column title. Do not generate text.",
        "photo" => "Full-bleed photo on a 1200x900 cover. Keep the central lower area quiet for a large, readable title. Do not generate text.",
        _ => "Keep the central area clear for typography on a 1200x900 cover. Do not generate text."
    };
}
