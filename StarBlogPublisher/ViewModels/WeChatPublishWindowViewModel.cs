using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

/// <summary>
/// 公众号排版、复制与草稿上传窗口。
/// </summary>
public partial class WeChatPublishWindowViewModel : ViewModelBase {
    private readonly WeChatFormattingService _formattingService = new();
    private readonly WeChatDraftPublishApplicationService _publishService = new(AppSettings.Instance);
    private readonly string _markdown;
    private readonly string _sourceFilePath;
    private readonly string _summary;

    public WeChatPublishWindowViewModel(string markdown, string sourceFilePath, string title, string summary) {
        _markdown = markdown;
        _sourceFilePath = sourceFilePath;
        _summary = summary;
        ArticleTitle = title;
        Themes = new ObservableCollection<WeChatTheme>(WeChatFormattingService.Themes);
        SelectedTheme = Themes.FirstOrDefault(theme => theme.Id == AppSettings.Instance.WeChatDefaultTheme) ?? Themes[0];
        GenerateFormat();
    }

    public ObservableCollection<WeChatTheme> Themes { get; }

    [ObservableProperty] private WeChatTheme? _selectedTheme;
    [ObservableProperty] private string _articleTitle = string.Empty;
    [ObservableProperty] private string _formattedHtml = string.Empty;
    [ObservableProperty] private string _coverPath = string.Empty;
    [ObservableProperty] private string _draftMediaId = string.Empty;
    [ObservableProperty] private string _statusMessage = "请选择主题后即可预览或复制";
    [ObservableProperty] private bool _isPublishing;
    [ObservableProperty] private int _wordCount;

    public bool HasDraftMediaId => !string.IsNullOrWhiteSpace(DraftMediaId);

    partial void OnSelectedThemeChanged(WeChatTheme? value) {
        if (value != null) GenerateFormat();
    }

    partial void OnDraftMediaIdChanged(string value) {
        OnPropertyChanged(nameof(HasDraftMediaId));
    }

    [RelayCommand]
    private void GenerateFormat() {
        if (SelectedTheme == null) return;

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
        }
    }

    [RelayCommand]
    private async Task SelectCover() {
        var topLevel = TopLevel.GetTopLevel(App.MainWindow);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "选择公众号封面图",
            AllowMultiple = false,
            FileTypeFilter = new[] {
                new FilePickerFileType("图片") { Patterns = new[] { "*.jpg", "*.jpeg", "*.png" } }
            }
        });
        if (files.Count == 0) return;

        CoverPath = files[0].Path.LocalPath;
        StatusMessage = $"已选择封面图: {files[0].Name}";
    }

    [RelayCommand]
    private async Task CopyHtml() => await CopyToClipboardAsync(FormattedHtml, "排版 HTML 已复制到剪贴板");

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
            var page = $"<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>{System.Net.WebUtility.HtmlEncode(ArticleTitle)}</title></head><body style=\"margin:0;padding:20px;background:#F3F4F6\">{FormattedHtml}</body></html>";
            File.WriteAllText(previewPath, page, Encoding.UTF8);
            Process.Start(new ProcessStartInfo(previewPath) { UseShellExecute = true });
            StatusMessage = "已在浏览器中打开排版预览";
        }
        catch (Exception ex) {
            StatusMessage = $"打开预览失败: {ex.Message}";
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
            }
            else {
                StatusMessage = result.ErrorMessage ?? "创建公众号草稿失败";
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

        var clipboard = TopLevel.GetTopLevel(App.MainWindow)?.Clipboard;
        if (clipboard == null) {
            StatusMessage = "无法访问系统剪贴板";
            return;
        }

        await clipboard.SetTextAsync(content);
        StatusMessage = successMessage;
    }
}
