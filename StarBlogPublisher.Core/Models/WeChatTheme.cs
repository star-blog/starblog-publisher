using System.Collections.Generic;

namespace StarBlogPublisher.Models;

/// <summary>
/// 微信公众号排版主题。色板字段供预览/兼容旧调用；实际排版样式来自 Styles。
/// </summary>
public sealed record WeChatTheme(
    string Id,
    string Name,
    string PrimaryColor,
    string AccentColor,
    string BackgroundColor,
    string TextColor
) {
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Category) ? Name : $"{Category} · {Name}";
    public IReadOnlyDictionary<string, string> Styles { get; init; } = EmptyStyles;
    public WeChatCardLayout? Card { get; init; }

    private static readonly IReadOnlyDictionary<string, string> EmptyStyles =
        new Dictionary<string, string>();
}

/// <summary>
/// 卡片布局主题（warm-card / ocean-card / fresh-card）的分卡样式。
/// </summary>
public sealed record WeChatCardLayout(
    string Background,
    string CardBackground,
    string Texture,
    string TextureSize,
    string Border,
    string Shadow,
    string Radius
);
