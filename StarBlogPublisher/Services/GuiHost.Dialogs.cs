using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.Services;

public static partial class GuiHost {
    // Accessed only on the UI thread; entries live only while their window has dialogs.
    private static readonly Dictionary<Window, DialogWebViewState> DialogWindows = new();

    /// <summary>Shows a Fluent overlay without allowing native background browsers to cover it.</summary>
    public static Task<FAContentDialogResult> ShowDialogAsync(FAContentDialog dialog, Window? owner = null) {
        ArgumentNullException.ThrowIfNull(dialog);
        var window = ResolveDialogOwner(owner);
        return ShowOwnedDialogAsync(window, () => dialog.ShowAsync(window), () => {
            if (dialog.IsAttachedToVisualTree()) dialog.Hide();
        }, FAContentDialogResult.None);
    }

    /// <summary>Shows a task dialog, preserving its platform-specific window/overlay presentation.</summary>
    public static Task<object> ShowDialogAsync(FATaskDialog dialog, Window? owner = null) {
        ArgumentNullException.ThrowIfNull(dialog);
        var window = ResolveDialogOwner(owner);
        dialog.XamlRoot = window;
        return ShowOwnedDialogAsync<object>(window, () => dialog.ShowAsync(), () => {
            if (dialog.IsAttachedToVisualTree()) dialog.Hide();
        }, FATaskDialogStandardResult.Cancel);
    }

    private static Window ResolveDialogOwner(Window? owner) {
        Dispatcher.UIThread.VerifyAccess();
        return owner ?? GetMainWindow() ?? throw new InvalidOperationException("无法找到弹窗所属窗口。");
    }

    private static async Task<T> ShowOwnedDialogAsync<T>(Window owner, Func<Task<T>> show, Action hide, T canceledResult) {
        var previousFocus = owner.FocusManager?.GetFocusedElement() as InputElement;
        using var closed = new CancellationTokenSource();
        using var scope = AcquireDialogWebViews(owner);
        void OnOwnerClosed(object? sender, EventArgs e) => closed.Cancel();
        owner.Closed += OnOwnerClosed;
        try {
            return await show().WaitAsync(closed.Token);
        }
        catch (OperationCanceledException) when (closed.IsCancellationRequested) {
            TryHide();
            return canceledResult;
        }
        catch {
            // ShowAsync can fail after attaching its overlay. Preserve the original exception.
            TryHide();
            throw;
        }
        finally {
            owner.Closed -= OnOwnerClosed;
            scope.Dispose();
            if (owner.IsVisible && previousFocus is { IsEffectivelyVisible: true, IsEffectivelyEnabled: true }
                && TopLevel.GetTopLevel(previousFocus) == owner
                && (!DialogWindows.ContainsKey(owner) || IsInsideDialog(previousFocus))) {
                previousFocus.Focus();
            }
        }

        void TryHide() {
            try { hide(); }
            catch { /* Cleanup must not replace a show failure or prevent window teardown. */ }
        }
    }

    private static IDisposable AcquireDialogWebViews(Window owner) {
        if (!DialogWindows.TryGetValue(owner, out var state)) {
            state = new DialogWebViewState(owner);
            DialogWindows.Add(owner, state);
        }
        return state.Acquire();
    }

    private static bool IsInsideDialog(Visual visual) =>
        visual.GetVisualAncestors().Any(ancestor => ancestor is FAContentDialog or FATaskDialog);

    private sealed class DialogWebViewState {
        private readonly Window _owner;
        private readonly Dictionary<NativeWebView, IDisposable> _bindings = new();
        private int _count;
        private bool _refreshing;
        private bool _disposed;

        public DialogWebViewState(Window owner) {
            _owner = owner;
            _owner.LayoutUpdated += OnLayoutUpdated;
            _owner.Closed += OnClosed;
        }

        public IDisposable Acquire() {
            _count++;
            try { Refresh(); }
            catch { Release(); throw; }
            return new DialogWebViewLease(this);
        }

        private void OnLayoutUpdated(object? sender, EventArgs e) => Refresh();
        private void OnClosed(object? sender, EventArgs e) => Dispose();

        private void Refresh() {
            if (_disposed || _refreshing) return;
            _refreshing = true;
            try {
                var browsers = _owner.GetVisualDescendants().OfType<NativeWebView>()
                    .Where(browser => !IsInsideDialog(browser)).ToHashSet();
                foreach (var browser in _bindings.Keys.Where(browser => !browsers.Contains(browser)).ToArray()) {
                    _bindings.Remove(browser, out var binding);
                    binding!.Dispose();
                }
                foreach (var browser in browsers) {
                    if (_bindings.ContainsKey(browser)) continue;
                    // Removing this temporary override restores the original local value or live binding,
                    // including changes it received while the dialog was open. Never write IsVisible=true.
                    _bindings.Add(browser, browser.Bind(Visual.IsVisibleProperty, new Binding {
                        Source = false, Mode = BindingMode.OneWay, Priority = BindingPriority.Animation
                    }));
                }
            }
            finally { _refreshing = false; }
        }

        public void Release() {
            if (--_count == 0) Dispose();
        }

        private void Dispose() {
            if (_disposed) return;
            _disposed = true;
            _owner.LayoutUpdated -= OnLayoutUpdated;
            _owner.Closed -= OnClosed;
            DialogWindows.Remove(_owner);
            foreach (var binding in _bindings.Values) binding.Dispose();
            _bindings.Clear();
        }
    }

    private sealed class DialogWebViewLease(DialogWebViewState state) : IDisposable {
        private DialogWebViewState? _state = state;
        public void Dispose() {
            var current = _state;
            _state = null;
            current?.Release();
        }
    }
}
