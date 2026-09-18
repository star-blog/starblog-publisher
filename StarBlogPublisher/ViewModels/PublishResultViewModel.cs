using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class PublishResultViewModel : ViewModelBase {
    public PublishResultViewModel(PublishResult result) {
        ArticleTitle = result.Post?.Title ?? string.Empty;
        ArticleId = result.Post?.Id ?? string.Empty;
        ArticleUrl = result.PostUrl ?? string.Empty;
        MarkdownContent = result.MarkdownContent ?? result.Post?.Content ?? string.Empty;
    }

    [ObservableProperty] private string _articleTitle;
    [ObservableProperty] private string _articleId;
    [ObservableProperty] private string _articleUrl;
    [ObservableProperty] private string _markdownContent;
    [ObservableProperty] private string _statusMessage = "发布结果已就绪";

    [RelayCommand]
    private async Task CopyUrl() => await CopyToClipboardAsync(ArticleUrl, "文章 URL");

    [RelayCommand]
    private async Task CopyMarkdown() => await CopyToClipboardAsync(MarkdownContent, "Markdown 内容");

    [RelayCommand]
    private async Task CopyTitle() => await CopyToClipboardAsync(ArticleTitle, "文章标题");

    [RelayCommand]
    private void OpenArticle() {
        if (string.IsNullOrWhiteSpace(ArticleUrl)) {
            StatusMessage = "文章 URL 不可用";
            return;
        }

        try {
            Process.Start(new ProcessStartInfo(ArticleUrl) { UseShellExecute = true });
            StatusMessage = "已在浏览器中打开文章";
        }
        catch (Exception ex) {
            StatusMessage = $"打开文章失败: {ex.Message}";
        }
    }

    private async Task CopyToClipboardAsync(string content, string contentName) {
        if (string.IsNullOrWhiteSpace(content)) {
            StatusMessage = $"没有可复制的{contentName}";
            return;
        }

        try {
            var clipboard = GuiHost.GetTopLevel()?.Clipboard;
            if (clipboard == null) {
                StatusMessage = "无法访问剪贴板";
                return;
            }

            await clipboard.SetTextAsync(content);
            StatusMessage = $"{contentName}已复制到剪贴板";
            GuiHost.ToastSuccess("已复制", $"{contentName}已复制到剪贴板");
        }
        catch (Exception ex) {
            StatusMessage = $"复制失败: {ex.Message}";
        }
    }
}
