using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Converters;

/// <summary>
/// 侧栏图标。公众号项使用微信标志，其余项使用 Fluent 图标。
/// 绑定值依次为 NavIcon、Tag。
/// </summary>
public sealed class NavigationMenuIconConverter : IMultiValueConverter {
    public static readonly NavigationMenuIconConverter Instance = new();

    // Font Awesome brands "weixin"。图形本身偏宽，侧栏格子又会按边界撑满，
    // 所以把标志放进方形边界里，长边只占格子的一部分，视觉大小才接近 Fluent 图标。
    private const double WeChatInkPortion = 0.8;
    private const string WeChatPath =
        "M385.2 167.6c6.4 0 12.6.3 18.8 1.1C387.4 90.3 303.3 32 207.7 32 100.5 32 13 104.8 13 197.4c0 53.4 29.3 97.5 77.9 131.6l-19.3 58.6 68-34.1c24.4 4.8 43.8 9.7 68.2 9.7 6.2 0 12.1-.3 18.3-.8-4-12.9-6.2-26.6-6.2-40.8-.1-84.9 72.9-154 165.3-154zm-104.5-52.9c14.5 0 24.2 9.7 24.2 24.4 0 14.5-9.7 24.2-24.2 24.2-14.8 0-29.3-9.7-29.3-24.2.1-14.7 14.6-24.4 29.3-24.4zm-136.4 48.6c-14.5 0-29.3-9.7-29.3-24.2 0-14.8 14.8-24.4 29.3-24.4 14.8 0 24.4 9.7 24.4 24.4 0 14.6-9.6 24.2-24.4 24.2zM563 319.4c0-77.9-77.9-141.3-165.4-141.3-92.7 0-165.4 63.4-165.4 141.3S305 460.7 397.6 460.7c19.3 0 38.9-5.1 58.6-9.9l53.4 29.3-14.8-48.6C534 402.1 563 363.2 563 319.4zm-219.1-24.5c-9.7 0-19.3-9.7-19.3-19.6 0-9.7 9.7-19.3 19.3-19.3 14.8 0 24.4 9.7 24.4 19.3 0 10-9.7 19.6-24.4 19.6zm107.1 0c-9.7 0-19.3-9.7-19.3-19.6 0-9.7 9.7-19.3 19.3-19.3 14.5 0 24.4 9.7 24.4 19.3.1 10-9.9 19.6-24.4 19.6z";

    private static readonly Geometry WeChatGeometry = CreateWeChatGeometry();

    private static Geometry CreateWeChatGeometry() {
        var icon = Geometry.Parse(WeChatPath);
        var ink = icon.Bounds;
        var side = Math.Max(ink.Width, ink.Height) / WeChatInkPortion;
        var left = ink.Center.X - side / 2;
        var top = ink.Center.Y - side / 2;

        var padding = new StreamGeometry();
        using (var context = padding.Open()) {
            context.BeginFigure(new Point(left, top), false);
            context.LineTo(new Point(left + 0.01, top));
            context.EndFigure(false);
            context.BeginFigure(new Point(left + side, top + side), false);
            context.LineTo(new Point(left + side - 0.01, top + side));
            context.EndFigure(false);
        }

        var group = new GeometryGroup();
        group.Children.Add(icon);
        group.Children.Add(padding);
        return group;
    }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) {
        if (values.Count > 1 && values[1] is string tag && tag == nameof(ShellPageId.WeChat)) {
            return new FAPathIconSource { Data = WeChatGeometry };
        }

        if (values.Count == 0 || values[0] is not Icon icon) {
            return null;
        }

        return new FluentIconSource {
            Icon = icon,
            IconVariant = IconVariant.Regular
        };
    }
}
