using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace StarBlogPublisher.Views;

public partial class AboutView : UserControl {
    public AboutView() {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateFeatureColumns();
    }

    protected override void OnLoaded(RoutedEventArgs e) {
        base.OnLoaded(e);
        UpdateFeatureColumns();
    }

    private void UpdateFeatureColumns() {
        if (FeatureList.ItemsPanelRoot is UniformGrid grid) {
            grid.Columns = Bounds.Width < 900 ? 2 : 3;
        }
    }
}
