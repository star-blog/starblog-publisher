using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Views.Controls;

/// <summary>保留 Fluent 菜单模板，只在弹出窗口打开时应用当前材质。</summary>
internal sealed class MaterialMenuItem(WindowMaterialService material) : MenuItem {
    private Popup? _popup;
    private ExperimentalAcrylicBorder? _acrylic;

    protected override Type StyleKeyOverride => typeof(MenuItem);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e) {
        if (_popup != null) _popup.Opened -= OnPopupOpened;
        base.OnApplyTemplate(e);
        _popup = e.NameScope.Find<Popup>("PART_Popup");
        _acrylic = null;
        if (_popup != null) {
            if (_popup.Child is Border surface) {
                _acrylic = new ExperimentalAcrylicBorder {
                    Name = "MenuAcrylicSurface",
                    CornerRadius = surface.CornerRadius,
                    Margin = surface.Margin,
                    IsHitTestVisible = false,
                    IsVisible = false,
                    Material = new ExperimentalAcrylicMaterial {
                        TintColor = Colors.White,
                        FallbackColor = Colors.White
                    }
                };
                // Keep the original menu border and presenter above the material.
                // A separate layer adds Acrylic tint/luminosity/noise without
                // changing menu padding, hit testing or keyboard navigation.
                _popup.Child = null;
                _popup.Child = new Panel { Children = { _acrylic, surface } };
            }
            _popup.Opened += OnPopupOpened;
            if (_popup.IsOpen && _acrylic != null) material.AttachMenuPopup(_popup, _acrylic);
        }
    }

    private void OnPopupOpened(object? sender, EventArgs e) {
        if (sender is Popup popup && _acrylic != null) material.AttachMenuPopup(popup, _acrylic);
    }
}
