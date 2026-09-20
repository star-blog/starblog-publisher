using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StarBlogPublisher.ViewModels;

public partial class PublishViewModel {
    [ObservableProperty] private int _publishTargetIndex;
    public string PrimaryPublishLabel => PublishTargetIndex == 0 ? "发布到博客" : "公众号排版与草稿…";
    public string TargetReadinessText => PublishTargetIndex == 0 ? PublishReadinessText
        : string.IsNullOrWhiteSpace(CurrentFilePath) ? "请先保存文章，再进入公众号排版" : "进入排版后选择账号、预览并发送草稿";
    public bool NeedsBlogLogin => PublishTargetIndex == 0 && NeedsLoginToPublish;
    public bool CanPublishToTarget => PublishTargetIndex == 0 ? CanPublish
        : HasLoadedArticle && !IsWorkspaceBusy && !string.IsNullOrWhiteSpace(CurrentFilePath) && !string.IsNullOrWhiteSpace(ArticleContent);
    partial void OnPublishTargetIndexChanged(int value) => NotifyPublishTarget();
    private void NotifyPublishTarget() {
        OnPropertyChanged(nameof(PrimaryPublishLabel)); OnPropertyChanged(nameof(TargetReadinessText));
        OnPropertyChanged(nameof(NeedsBlogLogin)); OnPropertyChanged(nameof(CanPublishToTarget));
    }
    [RelayCommand] private async Task PublishToTarget() {
        if (!CanPublishToTarget) return;
        if (PublishTargetIndex == 0) await PublishCommand.ExecuteAsync(null);
        else ShowWeChatPublisherCommand.Execute(null);
    }
}
