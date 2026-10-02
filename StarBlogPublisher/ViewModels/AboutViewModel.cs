using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAvalonia.UI.Controls;
using FluentIcons.Common;
using StarBlogPublisher.Services;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

public partial class AboutViewModel : PageViewModelBase {
    private const string ProjectUrl = "https://github.com/star-blog/starblog-publisher";
    private const string DocsUrl = "https://github.com/star-blog/starblog-publisher#readme";
    private const string ReleasesPageUrl = "https://github.com/star-blog/starblog-publisher/releases";
    private const string IssuesUrl = "https://github.com/star-blog/starblog-publisher/issues";
    private const string StarBlogUrl = "https://github.com/Deali-Axy/StarBlog";
    private const string AuthorUrl = "https://github.com/Deali-Axy";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/star-blog/starblog-publisher/releases/latest";
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public AboutViewModel() : base("关于", Icon.Info) {
    }

    public string AppName { get; } = "StarBlog Publisher";
    public string Tagline { get; } = "面向 StarBlog 的跨平台文章发布客户端";
    public string AppVersion { get; } = ApplicationVersion.Value;
    public string VersionLabel { get; } = $"版本 {ApplicationVersion.Value}";
    public string License { get; } = "Apache-2.0";
    public string Copyright { get; } = "© 2025–2026 DealiAxy · Licensed under Apache-2.0";
    public string Description { get; } =
        "在桌面里同时打开多篇 Markdown，编辑、预览后再发布到 StarBlog 或送进微信公众号草稿箱。同一套发布与 AI 逻辑也提供给 CLI 和 MCP Server。";
    public string RuntimeName { get; } = $".NET {Environment.Version}";
    public string OperatingSystem { get; } = RuntimeInformation.OSDescription;

    public IReadOnlyList<AboutFeatureItem> Features { get; } = [
        new() {
            Title = "文章工作区",
            Description = "多标签编辑、分栏预览、大纲跳转，以及最近文件与上次会话恢复。",
            Symbol = Symbol.Document
        },
        new() {
            Title = "发布到博客",
            Description = "上传正文里的本地图片，发布后给出文章链接和处理后的 Markdown。",
            Symbol = Symbol.Cloud
        },
        new() {
            Title = "公众号排版",
            Description = "33 套主题、浏览器预览、复制富文本，以及转存图片后创建草稿。",
            Symbol = Symbol.TextFont
        },
        new() {
            Title = "封面工作室",
            Description = "在画布上排标题，并按头条与次条安全区预览封面。",
            Symbol = Symbol.Image
        },
        new() {
            Title = "AI 辅助",
            Description = "补全标题、摘要、关键词和 Slug，发布前审校；结果需确认后才会写回。",
            Symbol = Symbol.Sparkle
        },
        new() {
            Title = "CLI 与 MCP",
            Description = "把发布、分类和 AI 接到脚本，或让 Agent 通过标准工具接口调用。",
            Symbol = Symbol.Code
        }
    ];

    public IReadOnlyList<AboutTechItem> TechStack { get; } = [
        new() { Name = ".NET 10", Description = "跨平台运行时" },
        new() { Name = "Avalonia 12", Description = "桌面界面" },
        new() { Name = "FluentAvalonia 3.1", Description = "Fluent 应用壳与主题" },
        new() { Name = "CommunityToolkit.Mvvm", Description = "MVVM 与命令绑定" },
        new() { Name = "Markdig + WebView", Description = "Markdown 编辑与预览" },
        new() { Name = "Microsoft.Extensions.AI", Description = "多服务商 AI 接入" }
    ];

    public IReadOnlyList<AboutLinkItem> Links { get; } = [
        new() { Text = "项目主页", Description = "源码、发行说明与贡献指南", Url = ProjectUrl, Symbol = Symbol.Open },
        new() { Text = "使用文档", Description = "工作区、设置、CLI 与 MCP", Url = DocsUrl, Symbol = Symbol.Library },
        new() { Text = "发布版本", Description = "从 GitHub Releases 下载安装包", Url = ReleasesPageUrl, Symbol = Symbol.Cloud },
        new() { Text = "报告问题", Description = "缺陷、回归或功能建议", Url = IssuesUrl, Symbol = Symbol.QuestionCircle },
        new() { Text = "StarBlog", Description = "配套的博客系统", Url = StarBlogUrl, Symbol = Symbol.Link },
        new() { Text = "作者 DealiAxy", Description = "GitHub @Deali-Axy", Url = AuthorUrl, Symbol = Symbol.Link }
    ];

    [ObservableProperty] private bool _isCheckingForUpdate;
    [ObservableProperty] private bool _hasUpdateStatus;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private string _updateTitle = string.Empty;
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private FAInfoBarSeverity _updateSeverity = FAInfoBarSeverity.Informational;

    private bool CanCheckForUpdate() => !IsCheckingForUpdate;

    [RelayCommand]
    private void OpenProject() => OpenUrl(ProjectUrl);

    [RelayCommand]
    private void OpenReleases() => OpenUrl(ReleasesPageUrl);

    [RelayCommand]
    private async Task CopyVersion() {
        var clipboard = GuiHost.GetTopLevel()?.Clipboard;
        if (clipboard == null) {
            GuiHost.ToastWarning("无法复制", "当前没有可用的剪贴板。");
            return;
        }

        await clipboard.SetTextAsync($"StarBlog Publisher {ApplicationVersion.Value}");
        GuiHost.ToastSuccess("已复制", $"版本 {ApplicationVersion.Value}");
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
    private async Task CheckForUpdate() {
        IsCheckingForUpdate = true;
        SetUpdateStatus("正在检查更新", "正在查询 GitHub Releases…", FAInfoBarSeverity.Informational);

        try {
            using var response = await HttpClient.GetAsync(LatestReleaseApiUrl);
            if (!response.IsSuccessStatusCode) {
                SetUpdateStatus("检查更新失败",
                    $"GitHub 返回了 {(int)response.StatusCode} 状态码，请稍后重试。",
                    FAInfoBarSeverity.Error);
                return;
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            using var release = await JsonDocument.ParseAsync(contentStream);
            var latestVersion = release.RootElement.GetProperty("tag_name").GetString();

            if (string.IsNullOrWhiteSpace(latestVersion)) {
                throw new InvalidOperationException("GitHub 返回的发布版本无效。");
            }

            if (!IsNewerVersion(latestVersion, ApplicationVersion.Value)) {
                SetUpdateStatus("已是最新版本",
                    $"当前版本 {ApplicationVersion.Value}，无需更新。",
                    FAInfoBarSeverity.Success);
                return;
            }

            SetUpdateStatus("发现新版本",
                $"最新版本 {latestVersion}，当前为 {ApplicationVersion.Value}。",
                FAInfoBarSeverity.Informational,
                available: true);
        }
        catch (HttpRequestException ex) {
            SetUpdateStatus("检查更新失败", $"无法连接到 GitHub，请检查网络后重试。{ex.Message}",
                FAInfoBarSeverity.Error);
        }
        catch (Exception ex) {
            SetUpdateStatus("检查更新失败", ex.Message, FAInfoBarSeverity.Error);
        }
        finally {
            IsCheckingForUpdate = false;
        }
    }

    partial void OnIsCheckingForUpdateChanged(bool value) => CheckForUpdateCommand.NotifyCanExecuteChanged();

    private void SetUpdateStatus(string title, string message, FAInfoBarSeverity severity, bool available = false) {
        UpdateTitle = title;
        UpdateStatus = message;
        UpdateSeverity = severity;
        IsUpdateAvailable = available;
        HasUpdateStatus = true;
    }

    private static HttpClient CreateHttpClient() {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("StarBlogPublisher/" + ApplicationVersion.Value);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    public static bool IsNewerVersion(string latestVersion, string currentVersion) {
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

    internal static void OpenUrl(string url) {
        if (string.IsNullOrWhiteSpace(url)) return;
        try {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) {
            Debug.WriteLine($"Failed to open link {url}: {ex.Message}");
        }
    }
}

public class AboutFeatureItem {
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required Symbol Symbol { get; init; }
}

public class AboutTechItem {
    public required string Name { get; init; }
    public required string Description { get; init; }
}

public partial class AboutLinkItem : ObservableObject {
    public required string Text { get; init; }
    public required string Description { get; init; }
    public required string Url { get; init; }
    public Symbol Symbol { get; init; } = Symbol.Link;

    [RelayCommand]
    private void Open() => AboutViewModel.OpenUrl(Url);
}
