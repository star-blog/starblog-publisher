using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

public partial class AboutWindowViewModel : ViewModelBase {
    private const string ReleasesPageUrl = "https://github.com/star-blog/starblog-publisher/releases";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/star-blog/starblog-publisher/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    // 软件基本信息
    [ObservableProperty] private string _appName = "StarBlog Publisher";
    [ObservableProperty] private string _appVersion = $"版本 {ApplicationVersion.Value}";
    [ObservableProperty] private string _copyright = "© 2025–2026 DealiAxy · Licensed under Apache-2.0";
    [ObservableProperty] private string _description = "StarBlog Publisher 是面向 StarBlog 的跨平台文章发布工具，提供 Markdown 编辑、预览与发布、图片处理、AI 创作辅助和微信公众号排版。项目同时提供 CLI 与 MCP Server，便于自动化和 AI Agent 集成。";
    [ObservableProperty] private bool _isCheckingForUpdate;
    [ObservableProperty] private string _updateStatus = string.Empty;

    // 技术栈信息
    [ObservableProperty] private ObservableCollection<TechStackItem> _techStack = [
        new() { Name = ".NET 10", Description = "提供跨平台应用运行时" },
        new() { Name = "Avalonia UI 11.3", Description = "构建跨平台桌面界面" },
        new() { Name = "CommunityToolkit.Mvvm", Description = "提供 MVVM 模式与命令绑定" },
        new() { Name = "Markdig + Markdown.Avalonia", Description = "提供 Markdown 编辑与预览" },
        new() { Name = "Microsoft.Extensions.AI", Description = "集成多种 AI 服务" }
    ];

    // 相关链接
    [ObservableProperty] private ObservableCollection<LinkItem> _links = [
        new() { IconClass = "fa-solid fa-globe", IconColor = "#2196F3", Text = "访问项目主页", Url = "https://github.com/star-blog/starblog-publisher" },
        new() { IconClass = "fa-solid fa-book", IconColor = "#4CAF50", Text = "查看使用文档", Url = "https://github.com/star-blog/starblog-publisher#readme" },
        new() { IconClass = "fa-solid fa-download", IconColor = "#9C27B0", Text = "下载发布版本", Url = ReleasesPageUrl },
        new() { IconClass = "fa-solid fa-bug", IconColor = "#F44336", Text = "报告问题", Url = "https://github.com/star-blog/starblog-publisher/issues" }
    ];

    private bool CanCheckForUpdate() => !IsCheckingForUpdate;

    [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
    private async Task CheckForUpdate() {
        IsCheckingForUpdate = true;
        UpdateStatus = "正在检查更新…";

        try {
            using var response = await HttpClient.GetAsync(LatestReleaseApiUrl);
            if (!response.IsSuccessStatusCode) {
                UpdateStatus = "检查更新失败";
                await ShowMessageBox("检查更新失败", $"GitHub 返回了 {(int)response.StatusCode} 状态码，请稍后重试。", Icon.Error);
                return;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            using var release = await JsonDocument.ParseAsync(contentStream);
            var latestVersion = release.RootElement.GetProperty("tag_name").GetString();

            if (string.IsNullOrWhiteSpace(latestVersion)) {
                throw new InvalidOperationException("GitHub 返回的发布版本无效。");
            }

            if (!IsNewerVersion(latestVersion, ApplicationVersion.Value)) {
                UpdateStatus = "当前已是最新版本";
                await ShowMessageBox("检查更新", "当前已是最新版本。", Icon.Success);
                return;
            }

            UpdateStatus = $"发现新版本 {latestVersion}";
            var result = await ShowMessageBox(
                "发现新版本",
                $"发现新版本 {latestVersion}，当前版本为 {ApplicationVersion.Value}。\n\n是否前往发布页面下载？",
                Icon.Info,
                ButtonEnum.YesNo);

            if (result == ButtonResult.Yes) {
                OpenUrl(ReleasesPageUrl);
            }
        }
        catch (HttpRequestException ex) {
            UpdateStatus = "检查更新失败";
            await ShowMessageBox("检查更新失败", $"无法连接到 GitHub，请检查网络后重试。\n\n{ex.Message}", Icon.Error);
        }
        catch (Exception ex) {
            UpdateStatus = "检查更新失败";
            await ShowMessageBox("检查更新失败", $"检查更新时发生错误：{ex.Message}", Icon.Error);
        }
        finally {
            IsCheckingForUpdate = false;
        }
    }

    partial void OnIsCheckingForUpdateChanged(bool value) => CheckForUpdateCommand.NotifyCanExecuteChanged();

    private static HttpClient CreateHttpClient() {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StarBlogPublisher/" + ApplicationVersion.Value);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static bool IsNewerVersion(string latestVersion, string currentVersion) {
        return TryParseVersion(latestVersion, out var latest)
            && TryParseVersion(currentVersion, out var current)
            && latest > current;
    }

    private static bool TryParseVersion(string value, out Version version) {
        var normalized = value.Trim().TrimStart('v', 'V');
        var prereleaseStart = normalized.IndexOfAny(['-', '+']);
        if (prereleaseStart >= 0) {
            normalized = normalized[..prereleaseStart];
        }

        return Version.TryParse(normalized, out version!);
    }

    private static async Task<ButtonResult> ShowMessageBox(
        string title,
        string text,
        Icon icon,
        ButtonEnum buttons = ButtonEnum.Ok) {
        var messageBox = MessageBoxManager.GetMessageBoxStandard(title, text, buttons, icon);
        return await messageBox.ShowWindowDialogAsync(App.MainWindow);
    }

    private static void OpenUrl(string url) {
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
}

// 技术栈项目类
public class TechStackItem {
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

// 链接项目类
public partial class LinkItem : ObservableObject {
    public string IconClass { get; set; } = string.Empty;
    public string IconColor { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    [RelayCommand]
    private void OpenLink(string url) {
        if (string.IsNullOrEmpty(url)) return;
        try {
            var psi = new ProcessStartInfo {
                FileName = url,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch {
            // 处理打开链接失败的情况
            Debug.WriteLine($"Failed to open link: {url}");
        }
    }
}
