using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class WeChatViewModel : PageViewModelBase {
    private readonly WeChatFormattingService _formattingService = new();
    private readonly WeChatDraftPublishApplicationService _publishService;
    private readonly WeChatCoverImageService _coverImageService;
    private string _markdown = string.Empty;
    private string _sourceFilePath = string.Empty;
    private string _summary = string.Empty;
    private string _previewPath = string.Empty;

    public WeChatViewModel(IHttpClientFactory httpClientFactory) : base("公众号排版", Icon.Mail) {
        _publishService = new WeChatDraftPublishApplicationService(AppSettings.Instance, httpClientFactory);
        _coverImageService = new WeChatCoverImageService(httpClientFactory);
        Themes = new ObservableCollection<WeChatTheme>(WeChatFormattingService.Themes);
        SelectedTheme = Themes.FirstOrDefault(theme => theme.Id == AppSettings.Instance.WeChatDefaultTheme) ?? Themes[0];
        SelectedCoverSource = CoverSources[0];
        SelectedCoverSize = CoverSizes[0];
        SelectedRandomCoverProvider = RandomCoverProviders[0];
    }

    public ObservableCollection<WeChatTheme> Themes { get; }
    public ObservableCollection<CoverSourceOption> CoverSources { get; } = [
        new("local", "本地图片"),
        new("url", "在线 URL"),
        new("random", "随机图片")
    ];
    public ObservableCollection<CoverSizePreset> CoverSizes { get; } = [
        new("headline", "头条封面", 900, 383, "900 × 383 · 2.35:1"),
        new("secondary", "次条封面", 500, 500, "500 × 500 · 1:1")
    ];
    public ObservableCollection<RandomCoverProvider> RandomCoverProviders { get; } = [
        new("StarBlog PicLib", "https://blog.sblt.deali.cn:9000/Api/PicLib/Random/{0}/{1}?random={2}"),
        new("Lorem Picsum", "https://picsum.photos/{0}/{1}.jpg?random={2}"),
        new("LoremFlickr", "https://loremflickr.com/{0}/{1}?random={2}")
    ];

    [ObservableProperty] private WeChatTheme? _selectedTheme;
    [ObservableProperty] private string _articleTitle = string.Empty;
    [ObservableProperty] private string _formattedHtml = string.Empty;
    [ObservableProperty] private string _coverPath = string.Empty;
    [ObservableProperty] private Bitmap? _coverPreview;
    [ObservableProperty] private string _coverUrl = string.Empty;
    [ObservableProperty] private string _coverSourceDescription = "尚未选择封面图";
    [ObservableProperty] private CoverSourceOption? _selectedCoverSource;
    [ObservableProperty] private CoverSizePreset? _selectedCoverSize;
    [ObservableProperty] private RandomCoverProvider? _selectedRandomCoverProvider;
    [ObservableProperty] private bool _isPreparingCover;
    [ObservableProperty] private Uri? _previewUri;
    [ObservableProperty] private string _draftMediaId = string.Empty;
    [ObservableProperty] private string _statusMessage = "请先在「发布」页加载 Markdown 文件";
    [ObservableProperty] private string _markdownSourceMessage = string.Empty;
    [ObservableProperty] private bool _isPublishing;
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private bool _hasArticle;
    [ObservableProperty] private bool _isInspectorOpen = true;

    public bool HasDraftMediaId => !string.IsNullOrWhiteSpace(DraftMediaId);
    public bool HasFormattedHtml => !string.IsNullOrWhiteSpace(FormattedHtml);
    public bool HasCover => !string.IsNullOrWhiteSpace(CoverPath);
    public bool IsLocalCoverSource => SelectedCoverSource?.Id == "local";
    public bool IsUrlCoverSource => SelectedCoverSource?.Id == "url";
    public bool IsRandomCoverSource => SelectedCoverSource?.Id == "random";
    public bool IsHeadlineCoverSize => SelectedCoverSize?.Id == "headline";
    public bool IsSecondaryCoverSize => SelectedCoverSize?.Id == "secondary";
    public string CoverSizeHint => SelectedCoverSize?.Hint ?? string.Empty;
    public double CoverPreviewHeight => SelectedCoverSize == null
        ? 122
        : Math.Clamp(288d * SelectedCoverSize.Height / SelectedCoverSize.Width, 96, 192);
    public double InspectorPaneWidth => IsInspectorOpen ? 320 : 48;

    public void SyncFrom(PublishViewModel publish) {
        if (!publish.HasLoadedArticle || string.IsNullOrWhiteSpace(publish.CurrentFilePath) ||
            string.IsNullOrWhiteSpace(publish.ArticleContent)) {
            HasArticle = false;
            PreviewUri = null;
            StatusMessage = "请先在「发布」页加载 Markdown 文件";
            return;
        }

        var publishedMarkdown = publish.LastPublishResult?.Success == true
            ? publish.LastPublishResult.MarkdownContent
            : null;
        var usesPublishedMarkdown = !string.IsNullOrWhiteSpace(publishedMarkdown);

        _markdown = usesPublishedMarkdown ? publishedMarkdown! : publish.ArticleContent;
        _sourceFilePath = publish.CurrentFilePath;
        _summary = publish.ArticleDescription;
        ArticleTitle = publish.ArticleTitle;
        MarkdownSourceMessage = usesPublishedMarkdown
            ? "当前使用 StarBlog 发布后返回的 Markdown；其中的图片链接已替换为博客 URL，上传草稿时会再转存到微信 CDN。"
            : "当前使用本地 Markdown；文章尚未在 StarBlog 发布，正文图片会在上传草稿时直接转存到微信 CDN。";
        HasArticle = true;
        GenerateFormat();
    }

    partial void OnSelectedThemeChanged(WeChatTheme? value) {
        if (value != null && HasArticle) GenerateFormat();
    }

    partial void OnDraftMediaIdChanged(string value) {
        OnPropertyChanged(nameof(HasDraftMediaId));
    }

    partial void OnFormattedHtmlChanged(string value) {
        OnPropertyChanged(nameof(HasFormattedHtml));
    }

    partial void OnCoverPathChanged(string value) {
        OnPropertyChanged(nameof(HasCover));
        CoverPreview?.Dispose();
        CoverPreview = null;
        if (string.IsNullOrWhiteSpace(value) || !File.Exists(value)) return;

        try {
            CoverPreview = new Bitmap(value);
        }
        catch (Exception ex) {
            StatusMessage = $"无法加载封面预览: {ex.Message}";
        }
    }

    partial void OnSelectedCoverSourceChanged(CoverSourceOption? value) {
        OnPropertyChanged(nameof(IsLocalCoverSource));
        OnPropertyChanged(nameof(IsUrlCoverSource));
        OnPropertyChanged(nameof(IsRandomCoverSource));
    }

    partial void OnSelectedCoverSizeChanged(CoverSizePreset? value) {
        OnPropertyChanged(nameof(CoverSizeHint));
        OnPropertyChanged(nameof(CoverPreviewHeight));
        OnPropertyChanged(nameof(IsHeadlineCoverSize));
        OnPropertyChanged(nameof(IsSecondaryCoverSize));
        if (!HasCover) return;

        CoverPath = string.Empty;
        CoverSourceDescription = "封面规格已调整，请重新选择图片";
        StatusMessage = "封面规格已变更，请重新准备封面图";
    }

    partial void OnIsInspectorOpenChanged(bool value) {
        OnPropertyChanged(nameof(InspectorPaneWidth));
    }

    [RelayCommand]
    private void ToggleInspector() => IsInspectorOpen = !IsInspectorOpen;

    [RelayCommand]
    private void SetCoverSource(string sourceId) {
        SelectedCoverSource = CoverSources.FirstOrDefault(source => source.Id == sourceId) ?? SelectedCoverSource;
    }

    [RelayCommand]
    private void SetCoverSize(string sizeId) {
        SelectedCoverSize = CoverSizes.FirstOrDefault(size => size.Id == sizeId) ?? SelectedCoverSize;
    }

    [RelayCommand]
    private void GenerateFormat() {
        if (!HasArticle || SelectedTheme == null) return;

        try {
            var result = _formattingService.Format(_markdown, ArticleTitle, SelectedTheme.Id);
            ArticleTitle = result.Title;
            WordCount = result.WordCount;
            FormattedHtml = result.Html;
            RefreshPreview();
            AppSettings.Instance.WeChatDefaultTheme = SelectedTheme.Id;
            StatusMessage = $"已按“{SelectedTheme.Name}”生成排版（约 {WordCount} 字）";
        }
        catch (Exception ex) {
            StatusMessage = $"排版失败: {ex.Message}";
            GuiHost.ToastError("排版失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task SelectCover() {
        var topLevel = GuiHost.GetTopLevel();
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "选择公众号封面图",
            AllowMultiple = false,
            FileTypeFilter = [
                new FilePickerFileType("图片") { Patterns = ["*.jpg", "*.jpeg", "*.png"] }
            ]
        });
        if (files.Count == 0) return;

        SelectedCoverSource = CoverSources[0];
        var filePath = files[0].Path.LocalPath;
        await PrepareCoverAsync(
            () => _coverImageService.PrepareLocalAsync(filePath, CoverWidth, CoverHeight),
            $"本地图片 · {files[0].Name}");
    }

    [RelayCommand]
    private async Task UseCoverUrl() {
        if (!Uri.TryCreate(CoverUrl?.Trim(), UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))) {
            StatusMessage = "请输入有效的 HTTP 或 HTTPS 图片 URL";
            return;
        }

        SelectedCoverSource = CoverSources[1];
        await PrepareCoverAsync(
            () => _coverImageService.DownloadAndPrepareAsync(uri, CoverWidth, CoverHeight),
            $"在线图片 · {uri.Host}");
    }

    [RelayCommand]
    private async Task PickRandomCover() {
        var provider = SelectedRandomCoverProvider;
        if (provider == null) return;

        SelectedCoverSource = CoverSources[2];
        var uri = provider.CreateUri(CoverWidth, CoverHeight, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        await PrepareCoverAsync(
            () => _coverImageService.DownloadAndPrepareAsync(uri, CoverWidth, CoverHeight),
            $"随机图片 · {provider.Name}");
    }

    [RelayCommand]
    private async Task CopyHtml() {
        if (string.IsNullOrWhiteSpace(FormattedHtml)) {
            StatusMessage = "没有可复制的排版内容";
            return;
        }

        var clipboard = GuiHost.GetTopLevel()?.Clipboard;
        if (clipboard == null) {
            StatusMessage = "无法访问系统剪贴板";
            return;
        }

        var item = new DataTransferItem();
        item.SetText(ToPlainText(FormattedHtml));
        item.Set(DataFormat.CreateBytesPlatformFormat("HTML Format"), Encoding.UTF8.GetBytes(CreateCfHtml(FormattedHtml)));

        var dataTransfer = new DataTransfer();
        dataTransfer.Add(item);
        await clipboard.SetDataAsync(dataTransfer);
        StatusMessage = "排版已作为富文本复制，可直接粘贴到公众号正文";
        GuiHost.ToastSuccess("已复制", "可直接粘贴到公众号正文");
    }

    [RelayCommand]
    private async Task CopyDraftMediaId() => await CopyToClipboardAsync(DraftMediaId, "草稿 MediaId 已复制到剪贴板");

    [RelayCommand]
    private void OpenPreview() {
        if (!HasFormattedHtml) {
            StatusMessage = "没有可预览的排版内容";
            return;
        }

        try {
            RefreshPreview();
            if (string.IsNullOrWhiteSpace(_previewPath)) return;

            Process.Start(new ProcessStartInfo(_previewPath) { UseShellExecute = true });
            StatusMessage = "已在浏览器中打开排版预览";
        }
        catch (Exception ex) {
            StatusMessage = $"打开预览失败: {ex.Message}";
            GuiHost.ToastError("打开预览失败", ex.Message);
        }
    }

    [RelayCommand]
    private async Task UploadDraft() {
        if (IsPublishing) return;
        if (string.IsNullOrWhiteSpace(FormattedHtml)) {
            StatusMessage = "请先生成排版内容";
            return;
        }

        var currentTheme = SelectedTheme;
        if (currentTheme == null) return;

        IsPublishing = true;
        DraftMediaId = string.Empty;
        try {
            var formatResult = new WeChatFormatResult {
                Html = FormattedHtml,
                Title = ArticleTitle,
                WordCount = WordCount,
                Theme = currentTheme
            };
            var sourceDirectory = Path.GetDirectoryName(_sourceFilePath) ?? Environment.CurrentDirectory;
            var result = await _publishService.PublishAsync(formatResult, sourceDirectory, _summary, CoverPath,
                (progress, message) => StatusMessage = $"{progress}% · {message}");
            if (result.Success) {
                DraftMediaId = result.DraftMediaId ?? string.Empty;
                FormattedHtml = result.FormattedHtml ?? FormattedHtml;
                RefreshPreview();
                StatusMessage = "草稿已创建并通过校验，可在公众号后台的内容管理 → 草稿箱查看";
                GuiHost.ToastSuccess("草稿已创建", "可在公众号后台草稿箱查看");
            }
            else {
                StatusMessage = result.ErrorMessage ?? "创建公众号草稿失败";
                GuiHost.ToastError("创建草稿失败", result.ErrorMessage ?? "创建公众号草稿失败");
            }
        }
        finally {
            IsPublishing = false;
        }
    }

    private async Task CopyToClipboardAsync(string content, string successMessage) {
        if (string.IsNullOrWhiteSpace(content)) {
            StatusMessage = "没有可复制的内容";
            return;
        }

        var clipboard = GuiHost.GetTopLevel()?.Clipboard;
        if (clipboard == null) {
            StatusMessage = "无法访问系统剪贴板";
            return;
        }

        await clipboard.SetTextAsync(content);
        StatusMessage = successMessage;
        GuiHost.ToastSuccess("已复制", successMessage);
    }

    private int CoverWidth => SelectedCoverSize?.Width ?? 900;
    private int CoverHeight => SelectedCoverSize?.Height ?? 383;

    private async Task PrepareCoverAsync(Func<Task<string>> prepare, string sourceDescription) {
        if (IsPreparingCover) return;

        IsPreparingCover = true;
        try {
            StatusMessage = "正在准备公众号封面图…";
            CoverPath = await prepare();
            CoverSourceDescription = $"{sourceDescription} · {CoverSizeHint}";
            StatusMessage = "封面图已按公众号规格准备完成";
        }
        catch (Exception ex) {
            StatusMessage = $"准备封面图失败: {ex.Message}";
            GuiHost.ToastError("准备封面图失败", ex.Message);
        }
        finally {
            IsPreparingCover = false;
        }
    }

    /// <summary>
    /// Creates a local document for the embedded WebView. The canvas and frame styles
    /// belong only to the desktop preview; the generated fragment itself is unchanged
    /// for clipboard and draft upload operations.
    /// </summary>
    private void RefreshPreview() {
        if (!HasFormattedHtml) {
            PreviewUri = null;
            _previewPath = string.Empty;
            return;
        }

        try {
            var previewDirectory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "wechat-preview");
            Directory.CreateDirectory(previewDirectory);
            _previewPath = Path.Combine(previewDirectory, "index.html");

            var sourceDirectory = string.IsNullOrWhiteSpace(_sourceFilePath)
                ? null
                : Path.GetDirectoryName(_sourceFilePath);
            var baseHref = string.IsNullOrWhiteSpace(sourceDirectory)
                ? string.Empty
                : new Uri(Path.EndsInDirectorySeparator(sourceDirectory)
                    ? sourceDirectory
                    : sourceDirectory + Path.DirectorySeparatorChar).AbsoluteUri;

            var page = $$"""
                <!doctype html>
                <html lang="zh-CN">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1">
                  <base href="{{WebUtility.HtmlEncode(baseHref)}}">
                  <title>{{WebUtility.HtmlEncode(ArticleTitle)}}</title>
                  <style>
                    * { box-sizing: border-box; }
                    html, body { min-height: 100%; }
                    body { margin: 0; padding: 32px 20px 48px; background: #f3f3f3; }
                    .wechat-preview-frame { width: min(100%, 677px); min-height: 100%; margin: 0 auto; background: #fff; box-shadow: 0 2px 10px rgba(0, 0, 0, .08); }
                    @media (max-width: 720px) { body { padding: 0; background: #fff; } .wechat-preview-frame { width: 100%; box-shadow: none; } }
                  </style>
                </head>
                <body><main class="wechat-preview-frame">{{FormattedHtml}}</main></body>
                </html>
                """;

            File.WriteAllText(_previewPath, page, Encoding.UTF8);
            PreviewUri = new Uri($"{new Uri(_previewPath).AbsoluteUri}?v={DateTime.UtcNow.Ticks}");
        }
        catch (Exception ex) {
            PreviewUri = null;
            _previewPath = string.Empty;
            StatusMessage = $"预览生成失败: {ex.Message}";
        }
    }

    private static string CreateCfHtml(string htmlFragment) {
        const string startFragmentMarker = "<!--StartFragment-->";
        const string endFragmentMarker = "<!--EndFragment-->";
        const string headerTemplate =
            "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

        var htmlDocument = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>"
                           + startFragmentMarker + htmlFragment + endFragmentMarker + "</body></html>";
        var provisionalHeader = string.Format(headerTemplate, 0, 0, 0, 0);
        var startHtml = Encoding.UTF8.GetByteCount(provisionalHeader);
        var endHtml = startHtml + Encoding.UTF8.GetByteCount(htmlDocument);
        var startFragment = startHtml + Encoding.UTF8.GetByteCount(
            htmlDocument[..(htmlDocument.IndexOf(startFragmentMarker, StringComparison.Ordinal) + startFragmentMarker.Length)]);
        var endFragment = startHtml + Encoding.UTF8.GetByteCount(
            htmlDocument[..htmlDocument.IndexOf(endFragmentMarker, StringComparison.Ordinal)]);

        return string.Format(headerTemplate, startHtml, endHtml, startFragment, endFragment) + htmlDocument;
    }

    private static string ToPlainText(string html) {
        var withLineBreaks = Regex.Replace(
            html,
            @"<(?:br\s*/?|/p|/h[1-6]|/li|/blockquote|/section)>|</(?:div|tr|table)>",
            "\n",
            RegexOptions.IgnoreCase);
        var withoutTags = Regex.Replace(withLineBreaks, @"<[^>]+>", string.Empty);
        return WebUtility.HtmlDecode(withoutTags).Trim();
    }
}

public sealed record CoverSourceOption(string Id, string Name);

public sealed record CoverSizePreset(string Id, string Name, int Width, int Height, string Hint);

public sealed record RandomCoverProvider(string Name, string UrlTemplate) {
    public Uri CreateUri(int width, int height, long nonce) =>
        new(string.Format(UrlTemplate, width, height, nonce));
}
