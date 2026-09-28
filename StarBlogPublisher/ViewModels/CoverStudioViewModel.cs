using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class CoverStudioViewModel : ViewModelBase, IDisposable {
    private const int MaxImageBytes = 10 * 1024 * 1024;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CoverComposer _composer = new();
    private readonly Action<PreparedWeChatCover> _applyToWeChat;
    private readonly object _gate = new();
    private CancellationTokenSource? _renderCts;
    private CancellationTokenSource? _randomCts;
    private int _randomRequest;
    private byte[]? _background;
    private string? _unappliedPath;
    private string? _previewFile;
    private int _renderSerial;
    private int _disposed;
    private bool _ready;
    private bool _started;

    public CoverStudioViewModel(IHttpClientFactory httpClientFactory, string? articleTitle, Action<PreparedWeChatCover> applyToWeChat) {
        _httpClientFactory = httpClientFactory;
        _applyToWeChat = applyToWeChat;
        CoverTitle = articleTitle?.Trim() ?? "";
        SelectedRandomCoverProvider = RandomCoverProviders[0];
    }

    public string Title => "制作封面";

    public ObservableCollection<RandomCoverProvider> RandomCoverProviders { get; } = new(RandomCoverCatalog.All);

    [ObservableProperty] private string _coverTitle = "";
    [ObservableProperty] private double _fontSize = CoverComposer.DefaultFontSize;
    [ObservableProperty] private string _textColorId = "white";
    [ObservableProperty] private string _customColorHex = "#FFFFFF";
    [ObservableProperty] private bool _isBold = true;
    [ObservableProperty] private string _textAlignId = "center";
    [ObservableProperty] private string _textPlacementId = "bottom";
    [ObservableProperty] private bool _showScrim = true;
    [ObservableProperty] private string _previewMode = "full";
    [ObservableProperty] private RandomCoverProvider? _selectedRandomCoverProvider;
    [ObservableProperty] private string _backgroundDescription = "尚未选择背景，将使用深色底";
    [ObservableProperty] private string _statusMessage = "选择背景并确认标题";
    [ObservableProperty] private bool _isFetchingRandomBackground;
    [ObservableProperty] private string _outputPath = "";
    [ObservableProperty] private int _effectiveFontSize;
    [ObservableProperty] private Bitmap? _preview;

    public bool HasPreview => Preview != null;
    public bool IsWhiteColor => TextColorId == "white";
    public bool IsBlackColor => TextColorId == "black";
    public bool IsCustomColor => TextColorId == "custom";
    public bool IsRegularWeight => !IsBold;
    public bool IsLeftAlign => TextAlignId == "left";
    public bool IsCenterAlign => TextAlignId == "center";
    public bool IsTopPlacement => TextPlacementId == "top";
    public bool IsCenterPlacement => TextPlacementId == "center";
    public bool IsBottomPlacement => TextPlacementId == "bottom";
    public bool IsFullPreview => PreviewMode == "full";
    public bool IsHeadlinePreview => PreviewMode == "headline";
    public bool IsSecondaryPreview => PreviewMode == "secondary";
    public string PreviewCaption => PreviewMode switch {
        "headline" => "公众号头条裁切预览 · 2.35:1",
        "secondary" => "公众号次条裁切预览 · 1:1",
        _ => "完整画布 1200×900。标题画在头条和次条都会保留的区域。"
    };
    public string FontSizeHint {
        get {
            var requested = RequestedFontSize;
            if (EffectiveFontSize > 0 && EffectiveFontSize < requested) {
                return $"已按安全区缩小到 {EffectiveFontSize}";
            }

            return "超出安全区时会自动缩小";
        }
    }

    public void Start() {
        if (_started) return;
        _started = true;
        _ready = true;
        _ = RenderNowAsync();
        _ = PickRandomBackground();
    }

    [RelayCommand]
    private async Task SelectLocalBackground() {
        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions {
            Title = "选择封面背景",
            AllowMultiple = false,
            FileTypeFilter = [
                new FilePickerFileType("图片") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"] }
            ]
        });
        if (files.Count == 0) return;

        string? failure = null;
        try {
            await using var stream = await files[0].OpenReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            if (buffer.Length > MaxImageBytes) {
                throw new InvalidOperationException("本地图片不能超过 10 MB");
            }

            CancelRandomFetch();
            _background = buffer.ToArray();
            BackgroundDescription = $"本地图片 · {files[0].Name}";
        }
        catch (Exception ex) {
            failure = ex.Message;
            GuiHost.ToastError("读取图片失败", ex.Message);
        }

        if (failure == null || string.IsNullOrWhiteSpace(OutputPath)) await RenderNowAsync();
        if (failure != null) StatusMessage = failure;
    }

    [RelayCommand]
    private async Task PickRandomBackground() {
        var provider = SelectedRandomCoverProvider;
        if (provider == null || _disposed == 1) return;

        int request;
        CancellationToken token;
        lock (_gate) {
            request = Interlocked.Increment(ref _randomRequest);
            _randomCts?.Cancel();
            _randomCts?.Dispose();
            _randomCts = new CancellationTokenSource();
            token = _randomCts.Token;
        }

        IsFetchingRandomBackground = true;
        StatusMessage = "正在获取随机背景…";
        try {
            var uri = provider.CreateUri(
                CoverSafeZone.CanvasWidth,
                CoverSafeZone.CanvasHeight,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var bytes = await DownloadAsync(uri, token);
            lock (_gate) {
                if (request != _randomRequest || _disposed == 1) return;
                _background = bytes;
                BackgroundDescription = $"随机图片 · {provider.Name}";
            }

            await RenderNowAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
        }
        catch (Exception ex) {
            if (!IsCurrentRandomRequest(request)) return;
            if (string.IsNullOrWhiteSpace(OutputPath)) await RenderNowAsync();
            if (!IsCurrentRandomRequest(request)) return;
            StatusMessage = $"获取随机背景失败: {ex.Message}";
            GuiHost.ToastError("获取随机背景失败", ex.Message);
        }
        finally {
            if (IsCurrentRandomRequest(request)) IsFetchingRandomBackground = false;
        }
    }

    [RelayCommand]
    private void SetPreviewMode(string? mode) {
        if (mode is "full" or "headline" or "secondary") PreviewMode = mode;
    }

    [RelayCommand]
    private void SetTextColor(string? color) {
        if (color is "white" or "black" or "custom") TextColorId = color;
    }

    [RelayCommand]
    private void SetTextAlign(string? align) {
        if (align is "left" or "center") TextAlignId = align;
    }

    [RelayCommand]
    private void SetTextPlacement(string? placement) {
        if (placement is "top" or "center" or "bottom") TextPlacementId = placement;
    }

    [RelayCommand]
    private void SetBold(string? value) {
        if (bool.TryParse(value, out var bold)) IsBold = bold;
    }

    [RelayCommand(CanExecute = nameof(HasComposedCover))]
    private async Task SaveImage() {
        if (!HasComposedCover()) return;
        var storage = GuiHost.GetTopLevel()?.StorageProvider;
        if (storage == null) return;

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions {
            Title = "保存封面图",
            SuggestedFileName = SuggestedFileName(),
            DefaultExtension = "jpg",
            FileTypeChoices = [new FilePickerFileType("JPEG") { Patterns = ["*.jpg", "*.jpeg"] }]
        });
        if (file == null) return;

        try {
            var destination = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(destination)) {
                File.Copy(OutputPath, destination, overwrite: true);
            }
            else {
                await using var source = File.OpenRead(OutputPath);
                await using var target = await file.OpenWriteAsync();
                await source.CopyToAsync(target);
            }

            StatusMessage = "封面图已保存";
            GuiHost.ToastSuccess("已保存", "封面图已保存");
        }
        catch (Exception ex) {
            StatusMessage = $"保存封面失败: {ex.Message}";
            GuiHost.ToastError("保存封面失败", ex.Message);
        }
    }

    [RelayCommand(CanExecute = nameof(HasComposedCover))]
    private void ApplyToWeChat() {
        if (!HasComposedCover()) return;
        var path = OutputPath;
        _unappliedPath = null;
        _applyToWeChat(new PreparedWeChatCover(path, CoverSafeZone.CanvasWidth, CoverSafeZone.CanvasHeight));
    }

    public void Dispose() {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        CancelRandomFetch();
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = null;
        var preview = Preview;
        Preview = null;
        preview?.Dispose();
        if (_previewFile != null) TryDelete(_previewFile);
        if (_unappliedPath != null) TryDelete(_unappliedPath);
    }

    partial void OnCoverTitleChanged(string value) => ScheduleRender();
    partial void OnFontSizeChanged(double value) {
        OnPropertyChanged(nameof(FontSizeHint));
        ScheduleRender();
    }
    partial void OnTextColorIdChanged(string value) => NotifyStyle(nameof(IsWhiteColor), nameof(IsBlackColor), nameof(IsCustomColor));
    partial void OnCustomColorHexChanged(string value) => ScheduleRender();
    partial void OnIsBoldChanged(bool value) {
        OnPropertyChanged(nameof(IsRegularWeight));
        ScheduleRender();
    }
    partial void OnTextAlignIdChanged(string value) => NotifyStyle(nameof(IsLeftAlign), nameof(IsCenterAlign));
    partial void OnTextPlacementIdChanged(string value) => NotifyStyle(nameof(IsTopPlacement), nameof(IsCenterPlacement), nameof(IsBottomPlacement));
    partial void OnShowScrimChanged(bool value) => ScheduleRender();
    partial void OnPreviewModeChanged(string value) {
        OnPropertyChanged(nameof(IsFullPreview));
        OnPropertyChanged(nameof(IsHeadlinePreview));
        OnPropertyChanged(nameof(IsSecondaryPreview));
        OnPropertyChanged(nameof(PreviewCaption));
        if (!string.IsNullOrWhiteSpace(OutputPath)) UpdatePreview(Volatile.Read(ref _renderSerial));
    }
    partial void OnPreviewChanged(Bitmap? value) => OnPropertyChanged(nameof(HasPreview));
    partial void OnEffectiveFontSizeChanged(int value) => OnPropertyChanged(nameof(FontSizeHint));
    partial void OnOutputPathChanged(string value) {
        SaveImageCommand.NotifyCanExecuteChanged();
        ApplyToWeChatCommand.NotifyCanExecuteChanged();
    }

    private int RequestedFontSize => (int)Math.Round(FontSize);

    private bool HasComposedCover() => !string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath);

    private void NotifyStyle(params string[] names) {
        foreach (var name in names) OnPropertyChanged(name);
        ScheduleRender();
    }

    private void ScheduleRender() {
        if (!_ready || _disposed == 1) return;
        var serial = Interlocked.Increment(ref _renderSerial);
        var token = ResetRenderCancellation();
        _ = RenderAfterDelayAsync(serial, token);
    }

    private async Task RenderAfterDelayAsync(int serial, CancellationToken token) {
        try {
            await Task.Delay(200, token);
            await RenderCoreAsync(serial, token);
        }
        catch (OperationCanceledException) {
        }
        catch (ObjectDisposedException) {
        }
    }

    private Task RenderNowAsync() {
        if (!_ready || _disposed == 1) return Task.CompletedTask;
        var serial = Interlocked.Increment(ref _renderSerial);
        var token = ResetRenderCancellation();
        return RenderCoreAsync(serial, token);
    }

    private CancellationToken ResetRenderCancellation() {
        lock (_gate) {
            _renderCts?.Cancel();
            _renderCts?.Dispose();
            _renderCts = new CancellationTokenSource();
            return _renderCts.Token;
        }
    }

    private async Task RenderCoreAsync(int serial, CancellationToken token) {
        try {
            var composition = CreateComposition();
            ComposedCover composed;
            if (_background == null) {
                composed = await _composer.ComposeAsync(null, composition, token);
            }
            else {
                await using var stream = new MemoryStream(_background, writable: false);
                composed = await _composer.ComposeAsync(stream, composition, token);
            }

            if (!IsCurrent(serial)) {
                TryDelete(composed.Path);
                return;
            }

            RememberOutput(composed);
            UpdatePreview(serial);
            StatusMessage = composition.Title.Length == 0
                ? "封面已更新，还未填写标题"
                : composed.EffectiveFontSize < RequestedFontSize
                    ? $"封面已更新，标题已缩小到 {composed.EffectiveFontSize}"
                    : "封面已更新";
        }
        catch (OperationCanceledException) {
        }
        catch (ObjectDisposedException) {
        }
        catch (Exception ex) {
            if (!IsCurrent(serial)) return;
            StatusMessage = ex.Message;
        }
    }

    private bool IsCurrent(int serial) =>
        serial == Volatile.Read(ref _renderSerial) && Volatile.Read(ref _disposed) == 0;

    private void RememberOutput(ComposedCover composed) {
        var previous = _unappliedPath;
        _unappliedPath = composed.Path;
        OutputPath = composed.Path;
        EffectiveFontSize = composed.EffectiveFontSize;
        if (previous != null && !string.Equals(previous, composed.Path, StringComparison.OrdinalIgnoreCase)) {
            TryDelete(previous);
        }
    }

    private void UpdatePreview(int serial) {
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdatePreview(serial));
            return;
        }

        if (!IsCurrent(serial) || string.IsNullOrWhiteSpace(OutputPath) || !File.Exists(OutputPath)) return;
        var bytes = _composer.CreatePreviewPng(OutputPath, CurrentFrame);
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StarBlogPublisher", "cover-studio");
        Directory.CreateDirectory(directory);
        var previewPath = System.IO.Path.Combine(directory, $"preview-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(previewPath, bytes);
        var bitmap = new Bitmap(previewPath);
        var previousBitmap = Preview;
        var previousFile = _previewFile;
        _previewFile = previewPath;
        Preview = bitmap;
        previousBitmap?.Dispose();
        if (previousFile != null) TryDelete(previousFile);
    }

    private CoverPreviewFrame CurrentFrame => PreviewMode switch {
        "headline" => CoverPreviewFrame.Headline,
        "secondary" => CoverPreviewFrame.Secondary,
        _ => CoverPreviewFrame.Full
    };

    private CoverComposition CreateComposition() => new() {
        Title = CoverTitle,
        FontSize = RequestedFontSize,
        Color = TextColorId switch {
            "black" => CoverTextColor.Black,
            "custom" => CoverTextColor.Custom,
            _ => CoverTextColor.White
        },
        CustomColorHex = CustomColorHex,
        Bold = IsBold,
        Align = TextAlignId == "left" ? CoverTextHorizontalAlign.Left : CoverTextHorizontalAlign.Center,
        Placement = TextPlacementId switch {
            "top" => CoverTextVerticalPlacement.Top,
            "center" => CoverTextVerticalPlacement.Center,
            _ => CoverTextVerticalPlacement.Bottom
        },
        Scrim = ShowScrim
    };

    private bool IsCurrentRandomRequest(int request) =>
        request == Volatile.Read(ref _randomRequest) && Volatile.Read(ref _disposed) == 0;

    private void CancelRandomFetch() {
        lock (_gate) {
            Interlocked.Increment(ref _randomRequest);
            _randomCts?.Cancel();
            _randomCts?.Dispose();
            _randomCts = null;
        }

        IsFetchingRandomBackground = false;
    }

    private async Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken) {
        using var client = _httpClientFactory.CreateClient(WeChatHttpClientRegistration.ImageDownloadClientName);
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaxImageBytes) {
            throw new InvalidOperationException("在线图片不能超过 10 MB");
        }

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0) {
            total += read;
            if (total > MaxImageBytes) throw new InvalidOperationException("在线图片不能超过 10 MB");
            bounded.Write(buffer, 0, read);
        }

        if (bounded.Length == 0) throw new InvalidOperationException("随机图片是空的");
        return bounded.ToArray();
    }

    private string SuggestedFileName() {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var name = new string(CoverTitle.Trim().Take(24).Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(name) ? "cover.jpg" : name + ".jpg";
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException) {
        }
        catch (UnauthorizedAccessException) {
        }
    }
}
