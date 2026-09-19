using CommunityToolkit.Mvvm.ComponentModel;

namespace StarBlogPublisher.ViewModels;

/// <summary>Read-only result of the MAF publication-review workflow.</summary>
public partial class PublicationReviewViewModel : ViewModelBase {
    public string Title => "发布前 AI 审校";

    [ObservableProperty] private string _reviewText;

    public PublicationReviewViewModel(string reviewText) {
        ReviewText = reviewText;
    }
}
