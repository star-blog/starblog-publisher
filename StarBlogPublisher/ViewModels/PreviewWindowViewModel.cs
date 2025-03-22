using CommunityToolkit.Mvvm.ComponentModel;

namespace StarBlogPublisher.ViewModels
{
    public partial class PreviewWindowViewModel : ViewModelBase
    {
        [ObservableProperty] private string _markdownContent = string.Empty;
    }
}