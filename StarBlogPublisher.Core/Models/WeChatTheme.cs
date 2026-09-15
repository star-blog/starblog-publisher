namespace StarBlogPublisher.Models;

/// <summary>
/// 微信公众号排版主题。
/// </summary>
public sealed record WeChatTheme(
    string Id,
    string Name,
    string PrimaryColor,
    string AccentColor,
    string BackgroundColor,
    string TextColor
);
