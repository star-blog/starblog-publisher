using Avalonia;
using Avalonia.Controls;

namespace StarBlogPublisher.Views.Controls;

/// <summary>Reusable, blocking progress treatment for page and dialog content.</summary>
public partial class BusyOverlay : UserControl {
    public static readonly StyledProperty<bool> IsBusyProperty =
        AvaloniaProperty.Register<BusyOverlay, bool>(nameof(IsBusy));

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<BusyOverlay, string>(nameof(Message), "正在处理...");

    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<BusyOverlay, double>(nameof(Progress));

    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<BusyOverlay, bool>(nameof(IsIndeterminate), true);

    public BusyOverlay() => InitializeComponent();

    public bool IsBusy {
        get => GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public string Message {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public double Progress {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public bool IsIndeterminate {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }
}
