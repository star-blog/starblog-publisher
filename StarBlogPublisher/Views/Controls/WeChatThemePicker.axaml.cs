using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Views.Controls;

public partial class WeChatThemePicker : UserControl {
    private bool _syncing;
    private WeChatThemeOption? _lastSelectedOption;

    public static readonly StyledProperty<WeChatTheme?> SelectedThemeProperty =
        AvaloniaProperty.Register<WeChatThemePicker, WeChatTheme?>(
            nameof(SelectedTheme),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<string?> SelectedThemeIdProperty =
        AvaloniaProperty.Register<WeChatThemePicker, string?>(
            nameof(SelectedThemeId),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<IList<WeChatTheme>?> ThemesProperty =
        AvaloniaProperty.Register<WeChatThemePicker, IList<WeChatTheme>?>(nameof(Themes));

    static WeChatThemePicker() {
        SelectedThemeProperty.Changed.AddClassHandler<WeChatThemePicker>((picker, e) =>
            picker.OnSelectedThemeChanged(e));
        SelectedThemeIdProperty.Changed.AddClassHandler<WeChatThemePicker>((picker, e) =>
            picker.OnSelectedThemeIdChanged(e));
        ThemesProperty.Changed.AddClassHandler<WeChatThemePicker>((picker, _) =>
            picker.RebuildItems());
    }

    public WeChatThemePicker() {
        InitializeComponent();
        ThemeCombo.SelectionChanged += OnComboSelectionChanged;
        ThemeCombo.ContainerPrepared += OnContainerPrepared;
        Loaded += (_, _) => RebuildItems();
    }

    public WeChatTheme? SelectedTheme {
        get => GetValue(SelectedThemeProperty);
        set => SetValue(SelectedThemeProperty, value);
    }

    public string? SelectedThemeId {
        get => GetValue(SelectedThemeIdProperty);
        set => SetValue(SelectedThemeIdProperty, value);
    }

    public IList<WeChatTheme>? Themes {
        get => GetValue(ThemesProperty);
        set => SetValue(ThemesProperty, value);
    }

    private void OnSelectedThemeChanged(AvaloniaPropertyChangedEventArgs e) {
        if (_syncing || e.NewValue == e.OldValue) {
            return;
        }

        var theme = e.NewValue as WeChatTheme;
        _syncing = true;
        SetCurrentValue(SelectedThemeIdProperty, theme?.Id);
        SyncComboSelection(theme?.Id);
        _syncing = false;
    }

    private void OnSelectedThemeIdChanged(AvaloniaPropertyChangedEventArgs e) {
        if (_syncing) {
            return;
        }

        var id = e.NewValue as string;
        var theme = string.IsNullOrWhiteSpace(id) ? null : WeChatThemeCatalog.Resolve(id);
        if (SelectedTheme?.Id == theme?.Id) {
            SyncComboSelection(theme?.Id);
            return;
        }

        _syncing = true;
        SetCurrentValue(SelectedThemeProperty, theme);
        SyncComboSelection(theme?.Id);
        _syncing = false;
    }

    private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e) {
        if (_syncing) {
            return;
        }

        if (ThemeCombo.SelectedItem is WeChatThemeGroupHeader) {
            _syncing = true;
            ThemeCombo.SelectedItem = _lastSelectedOption;
            _syncing = false;
            return;
        }

        if (ThemeCombo.SelectedItem is not WeChatThemeOption option) {
            return;
        }

        _lastSelectedOption = option;
        _syncing = true;
        SetCurrentValue(SelectedThemeProperty, option.Theme);
        SetCurrentValue(SelectedThemeIdProperty, option.Theme.Id);
        _syncing = false;
    }

    private static void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e) {
        if (e.Container is not ComboBoxItem item) {
            return;
        }

        item.IsEnabled = item.DataContext is not WeChatThemeGroupHeader;
    }

    private void RebuildItems() {
        if (ThemeCombo is null) {
            return;
        }

        IEnumerable<WeChatTheme> themes = Themes is { Count: > 0 } custom
            ? custom
            : WeChatThemeCatalog.All;
        var items = new List<WeChatThemeListItem>();
        string? currentCategory = null;
        foreach (var theme in themes) {
            if (theme.Category != currentCategory) {
                currentCategory = theme.Category;
                if (!string.IsNullOrWhiteSpace(currentCategory)) {
                    items.Add(new WeChatThemeGroupHeader(currentCategory));
                }
            }

            items.Add(new WeChatThemeOption(theme));
        }

        ThemeCombo.ItemsSource = items;
        SyncComboSelection(SelectedTheme?.Id ?? SelectedThemeId);
    }

    private void SyncComboSelection(string? themeId) {
        if (ThemeCombo.ItemsSource is not IEnumerable<WeChatThemeListItem> items) {
            return;
        }

        var normalized = string.IsNullOrWhiteSpace(themeId)
            ? null
            : WeChatThemeCatalog.NormalizeId(themeId);

        WeChatThemeOption? match = null;
        foreach (var item in items) {
            if (item is WeChatThemeOption option &&
                option.Theme.Id.Equals(normalized, StringComparison.OrdinalIgnoreCase)) {
                match = option;
                break;
            }
        }

        _syncing = true;
        ThemeCombo.SelectedItem = match;
        _lastSelectedOption = match;
        _syncing = false;
    }
}
