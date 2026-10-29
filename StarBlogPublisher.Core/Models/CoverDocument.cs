using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Models;

/// <summary>Portable design data only. Asset paths are relative to the article, or managed cache paths before saving.</summary>
public sealed record CoverDocument {
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    public string TemplateId { get; init; } = "minimal";
    [JsonIgnore] public bool UsesBackgroundImage => TemplateId is "photo" or "split" or "column";
    public int TemplateVersion { get; init; } = 1;
    public CoverElements? Elements { get; init; }
    [JsonIgnore] public bool IsSupported => CoverTemplateCatalog.IsSupported(TemplateId, TemplateVersion) && (TemplateVersion == 1 || Elements != null);
    public int Width { get; init; } = 1200;
    public int Height { get; init; } = 900;
    public string? Background { get; init; }
    public string BackgroundName { get; init; } = "背景图片";
    public string? Logo { get; init; }
    public string Title { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Column { get; init; } = "";
    public string Color { get; init; } = "#FFFFFF";
    public string BackgroundColor { get; init; } = "#17263C";
    public int FontSize { get; init; } = 64;
    public bool Bold { get; init; } = true;
    public bool Centered { get; init; } = true;
    public string Placement { get; init; } = "center";
    public double LineHeight { get; init; } = 1.15;
    public double TextWidth { get; init; } = 1;
    public double TextX { get; init; }
    public double TextY { get; init; }
    public double BackgroundX { get; init; }
    public double BackgroundY { get; init; }
    public double Zoom { get; init; } = 1;
    public double Scrim { get; init; } = .55;
    public bool Manual { get; init; }
    public CoverCropSet Crops { get; init; } = new();
    public string Prompt { get; init; } = "";
}

[JsonConverter(typeof(ArticleCoverStateConverter))]
public sealed record ArticleCoverState {
    public int SchemaVersion { get; init; } = 1;
    public CoverDocument Draft { get; init; } = new();
    public CoverDocument? Applied { get; init; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
    [JsonIgnore] public bool IsSupported => Original == null && (SchemaVersion is 1 or 2) && Draft.IsSupported && (Applied?.IsSupported ?? true);
    [JsonIgnore] public JsonElement? Original { get; init; }
}

public sealed class ArticleCoverStateConverter : JsonConverter<ArticleCoverState> {
    public override ArticleCoverState Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) {
        using var json = JsonDocument.ParseValue(ref reader);
        var root = json.RootElement;
        var version = root.TryGetProperty("SchemaVersion", out var v) && v.TryGetInt32(out var number) ? number : 1;
        if (version is not (1 or 2)) return new() { SchemaVersion = version, Original = root.Clone() };
        try {
            var state = new ArticleCoverState {
                SchemaVersion = version,
                Draft = root.GetProperty("Draft").Deserialize(CoverJsonContext.Default.CoverDocument) ?? throw new JsonException("封面草稿缺失"),
                Applied = root.TryGetProperty("Applied", out var applied) && applied.ValueKind != JsonValueKind.Null
                    ? applied.Deserialize(CoverJsonContext.Default.CoverDocument) : null,
                Extra = System.Linq.Enumerable.ToDictionary(System.Linq.Enumerable.Where(root.EnumerateObject(), p => p.Name is not ("SchemaVersion" or "Draft" or "Applied")), p => p.Name, p => p.Value.Clone())
            };
            return state.IsSupported ? state : state with { Original = root.Clone() };
        } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) {
            return new() { SchemaVersion = -1, Original = root.Clone() };
        }
    }
    public override void Write(Utf8JsonWriter writer, ArticleCoverState value, JsonSerializerOptions options) {
        if (value.Original is { } raw) { raw.WriteTo(writer); return; }
        writer.WriteStartObject(); writer.WriteNumber("SchemaVersion", value.SchemaVersion);
        writer.WritePropertyName("Draft"); JsonSerializer.Serialize(writer, value.Draft, CoverJsonContext.Default.CoverDocument);
        if (value.Applied != null) { writer.WritePropertyName("Applied"); JsonSerializer.Serialize(writer, value.Applied, CoverJsonContext.Default.CoverDocument); }
        if (value.Extra != null) foreach (var pair in value.Extra) { writer.WritePropertyName(pair.Key); pair.Value.WriteTo(writer); }
        writer.WriteEndObject();
    }
}

public sealed record CoverCropSet {
    // Offsets along the movable axis. A fixed-ratio window always stays inside the master.
    public double HeadlineOffset { get; init; } = .5;
    public double SecondaryOffset { get; init; } = .5;
    [JsonIgnore] public bool IsValid => double.IsFinite(HeadlineOffset) && double.IsFinite(SecondaryOffset)
        && HeadlineOffset is >= 0 and <= 1 && SecondaryOffset is >= 0 and <= 1;
    public CoverPixelRect Window(int width, int height, bool headline) {
        if (!IsValid || width < 1 || height < 1) throw new InvalidOperationException("封面裁切参数无效");
        var aspect = headline ? 2.35 : 1d;
        var w = Math.Min(width, height * aspect);
        var h = Math.Min(height, width / aspect);
        var offset = headline ? HeadlineOffset : SecondaryOffset;
        var left = (int)Math.Floor((width - w) * offset);
        var top = (int)Math.Floor((height - h) * offset);
        return new(left, top, Math.Min(width, (int)Math.Ceiling(left + w)) - left,
            Math.Min(height, (int)Math.Ceiling(top + h)) - top);
    }
}

public sealed record CoverTemplate(string Id, string Name) {
    public static IReadOnlyList<CoverTemplate> All { get; } = [new("minimal", "纯色大标题"), new("photo", "照片叠字"),
        new("split", "图文分区"), new("column", "栏目封面")];
    public CoverDocument Apply(CoverDocument source) => source with {
        TemplateId = Id, TemplateVersion = 1, FontSize = Id == "column" ? 60 : 64,
        Centered = Id != "split", Placement = Id == "photo" ? "bottom" : "center",
        TextWidth = Id == "split" ? .52 : 1, TextX = 0, TextY = 0,
        Color = "#FFFFFF", BackgroundColor = Id == "column" ? "#41335B" : "#17263C", Scrim = Id is "photo" or "column" ? .65 : 0
    };
}

[JsonSerializable(typeof(ArticleCoverState))]
[JsonSerializable(typeof(CoverDocument))]
[JsonSerializable(typeof(ArticleSidecar))]
[JsonSerializable(typeof(CoverElements))]
public partial class CoverJsonContext : JsonSerializerContext;
