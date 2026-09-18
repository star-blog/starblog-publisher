using System;
using System.Collections;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace StarBlogPublisher.Views.Controls;

/// <summary>Reusable host for a root page and its breadcrumb-backed secondary page stack.</summary>
public partial class StackPageHost : UserControl {
    public static readonly StyledProperty<IEnumerable?> BreadcrumbsProperty =
        AvaloniaProperty.Register<StackPageHost, IEnumerable?>(nameof(Breadcrumbs));

    public static readonly StyledProperty<bool> IsNavigatingProperty =
        AvaloniaProperty.Register<StackPageHost, bool>(nameof(IsNavigating));

    public static readonly StyledProperty<object?> RootContentProperty =
        AvaloniaProperty.Register<StackPageHost, object?>(nameof(RootContent));

    public static readonly StyledProperty<object?> StackContentProperty =
        AvaloniaProperty.Register<StackPageHost, object?>(nameof(StackContent));

    public StackPageHost() => InitializeComponent();

    public event EventHandler<int>? BreadcrumbClicked;

    public IEnumerable? Breadcrumbs {
        get => GetValue(BreadcrumbsProperty);
        set => SetValue(BreadcrumbsProperty, value);
    }

    public bool IsNavigating {
        get => GetValue(IsNavigatingProperty);
        set => SetValue(IsNavigatingProperty, value);
    }

    public object? RootContent {
        get => GetValue(RootContentProperty);
        set => SetValue(RootContentProperty, value);
    }

    public object? StackContent {
        get => GetValue(StackContentProperty);
        set => SetValue(StackContentProperty, value);
    }

    private void OnBreadcrumbItemClicked(FABreadcrumbBar sender, FABreadcrumbBarItemClickedEventArgs args) =>
        BreadcrumbClicked?.Invoke(this, args.Index);
}
