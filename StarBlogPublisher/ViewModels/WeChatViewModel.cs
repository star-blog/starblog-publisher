using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class WeChatViewModel : PageViewModelBase {
    private readonly WeChatFormattingService _formattingService = new();
    private readonly WeChatDraftPublishApplicationService _publishService = new(AppSettings.Instance);
    private string _markdown = string.Empty;
    private string _sourceFilePath = string.Empty;
    private string _summary = string.Empty;

    public WeChatViewModel() : base("公众号排版", "fa-brands fa-weixin") {
        Themes = new ObservableCollection<WeChatTheme>(WeChatFormattingService.Themes);
        SelectedTheme = Themes.FirstOrDefault(theme => theme.Id == AppSettings.Instance.WeChatDefaultTheme) ?? Themes[0];
    }

    public ObservableCollection<WeChatTheme> Themes { get; }

    [ObservableProperty] private WeChatTheme? _selectedTheme;
    [ObservableProperty] private string _articleTitle = string.Empty;
    [ObservableProperty] private string _formattedHtml = string.Empty;
    [ObservableProperty] private string _coverPath = string.Empty;
    [ObservableProperty] private string _draftMediaId = string.Empty;
    [ObservableProperty] private string _statusMessage = "请先在「发布」页加载 Markdown 文件";
    [ObservableProperty] private string _markdownSourceMessage = string.Empty;
    [ObservableProperty] private bool _isPublishing;
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private bool _hasArticle;

    public bool HasDraftMediaId => !string.IsNullOrWhiteSpace(DraftMediaId);

    public void SyncFrom(PublishViewModel publish) {
        if (!publish.HasLoadedArticle || string.IsNullOrWhiteSpace(publish.CurrentFilePath) ||
            string.IsNullOrWhiteSpace(publish.ArticleContent)) {
            HasArticle = false;
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

    [RelayCommand]
    private void GenerateFormat() {
        if (!HasArticle || SelectedTheme == null) return;

        try {
            var result = _formattingService.Format(_markdown, ArticleTitle, SelectedTheme.Id);
            ArticleTitle = result.Title;
            WordCount = result.WordCount;
            FormattedHtml = result.Html;
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

        CoverPath = files[0].Path.LocalPath;
        StatusMessage = $"已选择封面图: {files[0].Name}";
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
        if (string.IsNullOrWhiteSpace(FormattedHtml)) {
            StatusMessage = "没有可预览的排版内容";
            return;
        }

        try {
            var previewDirectory = Path.Combine(Path.GetTempPath(), "StarBlogPublisher", "wechat-preview");
            Directory.CreateDirectory(previewDirectory);
            var previewPath = Path.Combine(previewDirectory, "wechat-preview.html");
            var page =
                $"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>{WebUtility.HtmlEncode(ArticleTitle)}</title></head><body style=\"margin:0;padding:20px;background:#F3F4F6\">{FormattedHtml}</body></html>";
            File.WriteAllText(previewPath, page, Encoding.UTF8);
            Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true });
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
