using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.AI;

namespace StarBlogPublisher.ViewModels;

/// <summary>Human review surface for a generated metadata proposal. It never writes on creation.</summary>
public partial class AiMetadataDraftViewModel : ViewModelBase, IDialogHostAware {
    private readonly Action<SelectedArticleMetadata> _apply;

    public string Title => "AI 元数据草案";
    public ObservableCollection<TitleCandidate> TitleCandidates { get; }
    public ObservableCollection<string> SlugCandidates { get; }

    [ObservableProperty] private TitleCandidate? _selectedTitleCandidate;
    [ObservableProperty] private string _summary;
    [ObservableProperty] private string _tags;
    [ObservableProperty] private string? _selectedSlug;
    [ObservableProperty] private string _warnings;

    public event Action? CloseRequested;

    public AiMetadataDraftViewModel(ArticleMetadataDraft draft, Action<SelectedArticleMetadata> apply) {
        ArgumentNullException.ThrowIfNull(draft);
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        TitleCandidates = new ObservableCollection<TitleCandidate>(draft.Titles ?? []);
        SlugCandidates = new ObservableCollection<string>(draft.SlugCandidates ?? []);
        SelectedTitleCandidate = TitleCandidates.FirstOrDefault();
        SelectedSlug = SlugCandidates.FirstOrDefault();
        Summary = draft.Summary ?? string.Empty;
        Tags = string.Join(", ", draft.Tags ?? []);
        Warnings = string.Join(Environment.NewLine, draft.Warnings ?? []);
    }

    [RelayCommand]
    private void Apply() {
        _apply(new SelectedArticleMetadata(
            SelectedTitleCandidate?.Title,
            Summary,
            Tags,
            SelectedSlug));
        CloseRequested?.Invoke();
    }
}

public sealed record SelectedArticleMetadata(string? Title, string? Summary, string? Tags, string? Slug);
