namespace StarBlogPublisher.Models;

/// <summary>
/// Markdown 转换为微信公众号内联样式 HTML 后的结果。
/// </summary>
public sealed class WeChatFormatResult {
    public required string Html { get; init; }
    public required string Title { get; init; }
    public required int WordCount { get; init; }
    public required WeChatTheme Theme { get; init; }
}
