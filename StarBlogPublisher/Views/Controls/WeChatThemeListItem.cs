using StarBlogPublisher.Models;

namespace StarBlogPublisher.Views.Controls;

public abstract record WeChatThemeListItem;

public sealed record WeChatThemeGroupHeader(string Title) : WeChatThemeListItem;

public sealed record WeChatThemeOption(WeChatTheme Theme) : WeChatThemeListItem;
