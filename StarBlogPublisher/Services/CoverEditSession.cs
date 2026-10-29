using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Services;

/// <summary>Article-owned state. Views subscribe but never own the applied image or undo history.</summary>
public sealed class CoverEditSession {
    private readonly List<ArticleCoverState?> _undo = [];
    private readonly List<ArticleCoverState?> _redo = [];
    private string? _lastGroup;
    private DateTime _lastChange;
    private bool _gesture;
    private readonly List<string> _outputs = [];
    private CoverDocument? _preparedDesign;
    private PreparedWeChatCover? _prepared;
    public CoverEditSession(string cacheDirectory, string title = "") {
        Assets = new(cacheDirectory);
        Initial = CoverTemplateCatalog.Create(title);
    }
    public CoverAssetService Assets { get; }
    public CoverDocument Initial { get; set; }
    public ArticleCoverState? State { get; private set; }
    public CoverDocument Draft => State?.Draft ?? Initial;
    public string? ArticlePath { get; set; }
    public int Revision { get; private set; }
    public bool IsBusy { get; private set; }
    public void SetBusy(bool busy) { IsBusy = busy; Changed?.Invoke(); }
    public bool IsClosed { get; private set; }
    public bool IsSupported => State?.IsSupported ?? true;
    public bool CanUndo => _undo.Count > 0 && IsSupported && !IsBusy;
    public bool CanRedo => _redo.Count > 0 && IsSupported && !IsBusy;
    public event Action? Changed;
    public event Action? BeforeSave;
    public void PrepareToSave() => BeforeSave?.Invoke();
    public void Load(ArticleCoverState? state) { State = state; _undo.Clear(); _redo.Clear(); Revision++; Changed?.Invoke(); }
    public void Rebase(ArticleCoverState? state, string path) {
        CoverDocument Absolute(CoverDocument d) => d with {
            Background = d.Background == null ? null : CoverAssetService.Resolve(d.Background, ArticlePath),
            Logo = d.Logo == null ? null : CoverAssetService.Resolve(d.Logo, ArticlePath)
        };
        ArticleCoverState? Convert(ArticleCoverState? s) => s == null || !s.IsSupported ? s : s with {
            Draft = Absolute(s.Draft), Applied = s.Applied == null ? null : Absolute(s.Applied)
        };
        for (var i = 0; i < _undo.Count; i++) _undo[i] = Convert(_undo[i]);
        for (var i = 0; i < _redo.Count; i++) _redo[i] = Convert(_redo[i]);
        State = state; ArticlePath = path; Revision++; Changed?.Invoke();
    }
    public void Change(CoverDocument draft, string group = "") {
        if (IsClosed || IsBusy || !IsSupported || draft == Draft) return;
        if (group == "" || group != _lastGroup || (!_gesture && (DateTime.UtcNow - _lastChange).TotalMilliseconds > 600)) {
            _undo.Add(State); if (_undo.Count > 100) _undo.RemoveAt(0);
        }
        _lastGroup = group; _lastChange = DateTime.UtcNow; _redo.Clear();
        State = (State ?? new()) with { SchemaVersion = draft.Elements != null ? 2 : State?.SchemaVersion ?? 1, Draft = draft }; Revision++; Changed?.Invoke();
    }
    public void BeginGesture() { _lastGroup = null; _gesture = true; }
    public void EndGesture() { _lastGroup = null; _gesture = false; }
    private void RestoreDraft(ArticleCoverState? historical) {
        // History edits the draft; only ApplyAsync may replace the applied snapshot.
        var applied = State?.Applied;
        State = historical == null && applied == null ? null : (State ?? new()) with { Draft = historical?.Draft ?? Initial, Applied = applied };
    }
    public void Undo() { if (!CanUndo) return; _redo.Add(State); RestoreDraft(_undo[^1]); _undo.RemoveAt(_undo.Count - 1); Revision++; EndGesture(); Changed?.Invoke(); }
    public void Redo() { if (!CanRedo) return; _undo.Add(State); RestoreDraft(_redo[^1]); _redo.RemoveAt(_redo.Count - 1); Revision++; EndGesture(); Changed?.Invoke(); }
    public async Task<PreparedWeChatCover> ApplyAsync(CancellationToken token = default) {
        PrepareToSave();
        if (IsBusy || IsClosed || !IsSupported) throw new InvalidOperationException("当前无法应用封面");
        var snapshot = Draft; var revision = Revision;
        IsBusy = true; Changed?.Invoke();
        try {
            var path = Path.Combine(Assets.CacheDirectory, "applied-" + Guid.NewGuid().ToString("N") + ".jpg");
            var prepared = await Task.Run(() => new CoverExportService().Export(snapshot, ArticlePath, path, weChat: true, token: token), token);
            if (IsClosed || Revision != revision) { File.Delete(path); throw new InvalidOperationException("封面已变化，请重新应用"); }
            State = (State ?? new() { Draft = snapshot, SchemaVersion = snapshot.Elements != null ? 2 : 1 }) with { Applied = snapshot };
            _outputs.Add(prepared.Path); _prepared = prepared; _preparedDesign = snapshot;
            Revision++; Changed?.Invoke(); return prepared;
        } finally { IsBusy = false; Changed?.Invoke(); }
    }
    public async Task<PreparedWeChatCover?> PrepareAppliedAsync(CancellationToken token = default) {
        if (!IsSupported || State?.Applied is not { } snapshot || IsClosed) return null;
        if (_preparedDesign == snapshot && _prepared is { } cached && File.Exists(cached.Path)) return cached;
        var path = Path.Combine(Assets.CacheDirectory, "applied-" + Guid.NewGuid().ToString("N") + ".jpg");
        var result = await Task.Run(() => new CoverExportService().Export(snapshot, ArticlePath, path, weChat: true, token: token), token);
        if (IsClosed || State?.Applied != snapshot) { File.Delete(path); return null; }
        _outputs.Add(path); _prepared = result; _preparedDesign = snapshot; return result;
    }
    public void Close() {
        IsClosed = true; Changed?.Invoke();
        foreach (var path in _outputs) { try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        _outputs.Clear();
    }
}
