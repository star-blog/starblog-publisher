using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using Markdig;

namespace StarBlogPublisher.ViewModels;

public partial class PublishViewModel {
    private CoverEditSession? _coverSession;
    private CancellationTokenSource? _coverRecoveryCts;
    private string _coverBaseline = "";
    private readonly string _coverRecoveryId = Guid.NewGuid().ToString("N");
    private string? _recoveredSourcePath;
    public string? ArticleSourcePath => CurrentFilePath ?? _recoveredSourcePath;
    public CoverEditSession CoverSession {
        get {
            if (_coverSession != null) return _coverSession;
            _coverSession = new(Path.Combine(_shell.Workspace.CoverDataDirectory, "assets"), ArticleTitle) { ArticlePath = ArticleSourcePath };
            _coverSession.Changed += () => { NotifyDocumentState(); };
            return _coverSession;
        }
    }
    private ArticleSidecar CaptureProperties() => new() {
        Title = ArticleTitle, Description = ArticleDescription, Slug = ArticleSlug, Keywords = ArticleKeywords,
        Category = SelectedCategory == null ? null : new Category { Id = SelectedCategory.Id, Text = SelectedCategory.Text },
        PostId = _sidecarPostId, IsPublish = _sidecarIsPublish, LastSyncedAt = _sidecarLastSyncedAt,
        Cover = _coverSession?.State
    };
    private void OpenCoverStudio(bool ai = false) {
        CoverSession.Initial = CoverSession.Initial with { Title = ArticleTitle };
        var html = Markdig.Markdown.ToHtml(ArticleContent, new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build());
        var studio = new CoverStudioViewModel(_shell.HttpClientFactory, ArticleTitle, _ => { },
            ArticleImageCatalog.FromHtml(html, ArticleSourcePath), CoverSession, () => SaveDocumentAsync(),
            () => ArticleTitle, () => ArticleDescription, () => ArticleContent) { InspectorTab = ai ? 3 : 0 };
        OpenStackPage(studio, studio.Title);
    }
    private void ScheduleCoverRecovery() {
        _coverRecoveryCts?.Cancel();
        _coverRecoveryCts?.Dispose();
        _coverRecoveryCts = null;
        if (_coverSession?.State == null || !IsDocumentDirty || _coverSession.IsClosed) return;
        var cts = _coverRecoveryCts = new();
        var snapshot = new CoverRecovery(_coverRecoveryId, CurrentFilePath, ArticleSourcePath, _coverBaseline, ArticleContent, CaptureProperties());
        _ = WriteRecoveryAsync(snapshot, cts.Token);
    }
    private async Task WriteRecoveryAsync(CoverRecovery snapshot, CancellationToken token) {
        try {
            await Task.Delay(1000, token);
            var portable = await CoverSession.Assets.PersistAsync(snapshot.Properties.Cover, ArticleSourcePath,
                Path.Combine(_shell.Workspace.CoverDataDirectory, "recovery-assets", snapshot.Id + ".md"), token);
            // Recovery uses absolute cache paths so it is independent of the original file location.
            var basePath = Path.Combine(_shell.Workspace.CoverDataDirectory, "recovery-assets", snapshot.Id + ".md");
            CoverDocument Absolute(CoverDocument d) => d with {
                Background = d.Background == null ? null : CoverAssetService.Resolve(d.Background, basePath),
                Logo = d.Logo == null ? null : CoverAssetService.Resolve(d.Logo, basePath)
            };
            snapshot.Properties.Cover = portable == null || !portable.IsSupported ? portable : portable with {
                Draft = Absolute(portable.Draft), Applied = portable.Applied == null ? null : Absolute(portable.Applied)
            };
            token.ThrowIfCancellationRequested();
            await _shell.Workspace.CoverRecovery.WriteAsync(snapshot, token);
            _shell.Workspace.RefreshCoverRecovery();
        } catch (OperationCanceledException) { }
        catch (Exception ex) { StatusMessage = "封面恢复记录未保存：" + ex.Message; }
    }
    internal void ClearCoverRecovery() {
        _coverRecoveryCts?.Cancel(); _coverRecoveryCts?.Dispose(); _coverRecoveryCts = null;
        _shell.Workspace.CoverRecovery.Delete(_coverRecoveryId); _shell.Workspace.RefreshCoverRecovery();
    }
    internal void CloseCoverSession() {
        ClearCoverRecovery();
        DisposeStackPage(); _coverSession?.Close();
    }
    internal void RestoreCoverRecovery(CoverRecovery recovery, bool original) {
        _currentFilePath = original ? recovery.ArticlePath : null;
        _recoveredSourcePath = recovery.SourcePath;
        _coverBaseline = original ? recovery.Baseline : "";
        ArticleContent = recovery.Markdown; ArticleTitle = recovery.Properties.Title;
        ArticleDescription = recovery.Properties.Description; ArticleSlug = recovery.Properties.Slug;
        ArticleKeywords = recovery.Properties.Keywords; SelectedCategory = recovery.Properties.Category;
        CoverSession.ArticlePath = ArticleSourcePath; CoverSession.Load(recovery.Properties.Cover);
        _isNewDocument = !original; HasLoadedArticle = true;
        NotifyDocumentState();
    }
}
