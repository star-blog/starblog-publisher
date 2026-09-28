using Avalonia.Controls;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views;

public partial class CoverStudioView : UserControl {
    public CoverStudioView() {
        InitializeComponent();
        DataContextChanged += (_, _) => {
            if (DataContext is CoverStudioViewModel studio) studio.Start();
        };
    }
}
