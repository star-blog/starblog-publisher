using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// Loads WeChat typography themes from embedded wechat-pub JSON files.
/// </summary>
public static class WeChatThemeCatalog {
    public const string DefaultThemeId = "newspaper";
    private const string ResourcePrefix = ".Resources.WeChatThemes.";
    private static readonly JsonSerializerOptions JsonOptions = new() {
        PropertyNameCaseInsensitive = true
    };

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["tech"] = "github"
        };

    private static readonly string[] DisplayOrder = [
        "newspaper", "warm-card", "ocean-card", "fresh-card",
        "magazine", "ink", "coffee-house",
        "github", "bytedance", "sspai", "midnight",
        "terracotta", "mint-fresh", "sunset-amber", "lavender-dream",
        "sports", "bauhaus", "chinese", "wechat-native",
        "minimal-gold", "minimal-blue", "minimal-gray", "minimal-navy", "minimal-red",
        "focus-blue", "focus-gold", "focus-red",
        "elegant-green", "elegant-blue", "elegant-navy",
        "bold-blue", "bold-green", "bold-navy"
    ];

    private static readonly IReadOnlyDictionary<string, string> Categories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["warm-card"] = "卡片系列",
            ["fresh-card"] = "卡片系列",
            ["ocean-card"] = "卡片系列",
            ["newspaper"] = "深度长文",
            ["magazine"] = "深度长文",
            ["ink"] = "深度长文",
            ["coffee-house"] = "深度长文",
            ["bytedance"] = "科技产品",
            ["github"] = "科技产品",
            ["sspai"] = "科技产品",
            ["midnight"] = "科技产品",
            ["terracotta"] = "文艺随笔",
            ["mint-fresh"] = "文艺随笔",
            ["sunset-amber"] = "文艺随笔",
            ["lavender-dream"] = "文艺随笔",
            ["sports"] = "活力动态",
            ["bauhaus"] = "活力动态",
            ["chinese"] = "活力动态",
            ["wechat-native"] = "活力动态",
            ["minimal-gold"] = "模板布局",
            ["minimal-blue"] = "模板布局",
            ["minimal-gray"] = "模板布局",
            ["minimal-navy"] = "模板布局",
            ["minimal-red"] = "模板布局",
            ["focus-blue"] = "模板布局",
            ["focus-gold"] = "模板布局",
            ["focus-red"] = "模板布局",
            ["elegant-green"] = "模板布局",
            ["elegant-blue"] = "模板布局",
            ["elegant-navy"] = "模板布局",
            ["bold-blue"] = "模板布局",
            ["bold-green"] = "模板布局",
            ["bold-navy"] = "模板布局"
        };

    public static IReadOnlyList<WeChatTheme> All { get; } = Load();

    public static string NormalizeId(string? themeId) {
        if (string.IsNullOrWhiteSpace(themeId)) return DefaultThemeId;
        if (Aliases.TryGetValue(themeId, out var alias)) return alias;
        return All.Any(theme => theme.Id.Equals(themeId, StringComparison.OrdinalIgnoreCase))
            ? All.First(theme => theme.Id.Equals(themeId, StringComparison.OrdinalIgnoreCase)).Id
            : DefaultThemeId;
    }

    public static WeChatTheme Resolve(string? themeId) {
        var id = NormalizeId(themeId);
        return All.FirstOrDefault(theme => theme.Id == id) ?? All[0];
    }

    private static IReadOnlyList<WeChatTheme> Load() {
        var assembly = typeof(WeChatThemeCatalog).Assembly;
        var loaded = new Dictionary<string, WeChatTheme>(StringComparer.OrdinalIgnoreCase);

        foreach (var resourceName in assembly.GetManifestResourceNames()) {
            var marker = resourceName.IndexOf(ResourcePrefix, StringComparison.Ordinal);
            if (marker < 0 || !resourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;

            var id = resourceName[(marker + ResourcePrefix.Length)..^".json".Length];
            using var stream = assembly.GetManifestResourceStream(resourceName)
                               ?? throw new InvalidOperationException($"Missing WeChat theme resource: {resourceName}");
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            loaded[id] = Parse(id, json);
        }

        if (loaded.Count == 0) {
            throw new InvalidOperationException("No WeChat typography themes were embedded.");
        }

        var ordered = new List<WeChatTheme>();
        foreach (var id in DisplayOrder) {
            if (loaded.TryGetValue(id, out var theme)) ordered.Add(theme);
        }

        foreach (var theme in loaded.Values.OrderBy(item => item.Id, StringComparer.Ordinal)) {
            if (ordered.All(item => item.Id != theme.Id)) ordered.Add(theme);
        }

        return ordered;
    }

    private static WeChatTheme Parse(string id, string json) {
        var dto = JsonSerializer.Deserialize<ThemeDto>(json, JsonOptions)
                  ?? throw new InvalidOperationException($"Invalid WeChat theme JSON: {id}");

        var colors = dto.Colors ?? new Dictionary<string, string>();
        var primary = Color(colors, "primary", "#202020");
        var accent = Color(colors, "accent", "#B48A52");
        var background = Color(colors, "background", "#FFFFFF");
        var styles = new Dictionary<string, string>(StringComparer.Ordinal);
        if (dto.Styles != null) {
            foreach (var (key, properties) in dto.Styles) {
                if (properties == null || properties.Count == 0) continue;
                styles[key] = string.Join(";", properties.Select(pair =>
                    $"{pair.Key.Replace('_', '-')}:{pair.Value}"));
            }
        }

        WeChatCardLayout? card = null;
        if (string.Equals(dto.Layout, "card", StringComparison.OrdinalIgnoreCase) && dto.Card != null) {
            card = new WeChatCardLayout(
                dto.Card.Bg ?? background,
                dto.Card.CardBg ?? "#FFFFFF",
                dto.Card.CardTexture ?? "none",
                dto.Card.CardTextureSize ?? "auto",
                dto.Card.CardBorder ?? "none",
                dto.Card.CardShadow ?? "none",
                dto.Card.CardRadius ?? "0"
            );
        }

        Categories.TryGetValue(id, out var category);
        return new WeChatTheme(id, dto.Name ?? id, primary, accent, background, primary) {
            Description = dto.Description ?? string.Empty,
            Category = category ?? "其他",
            Styles = styles,
            Card = card
        };
    }

    private static string Color(IReadOnlyDictionary<string, string> colors, string key, string fallback) =>
        colors.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private sealed class ThemeDto {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public Dictionary<string, string>? Colors { get; set; }
        public Dictionary<string, Dictionary<string, string>>? Styles { get; set; }
        public string? Layout { get; set; }
        public CardDto? Card { get; set; }
    }

    private sealed class CardDto {
        [JsonPropertyName("bg")] public string? Bg { get; set; }
        [JsonPropertyName("card_bg")] public string? CardBg { get; set; }
        [JsonPropertyName("card_texture")] public string? CardTexture { get; set; }
        [JsonPropertyName("card_texture_size")] public string? CardTextureSize { get; set; }
        [JsonPropertyName("card_border")] public string? CardBorder { get; set; }
        [JsonPropertyName("card_shadow")] public string? CardShadow { get; set; }
        [JsonPropertyName("card_radius")] public string? CardRadius { get; set; }
    }
}
