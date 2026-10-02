using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class SitePostsViewModel : PageViewModelBase {
    private readonly MainWindowViewModel _shell;
    private readonly ArticleLibraryApplicationService _library;
    private readonly CategoryApplicationService _categories;

    public SitePostsViewModel(
        MainWindowViewModel shell,
        ArticleLibraryApplicationService? library = null,
        CategoryApplicationService? categories = null) : base("站点文章", Icon.News) {
        _shell = shell;
        var auth = shell.AuthService;
        _library = library ?? new ArticleLibraryApplicationService(ApiService.Instance, auth);
        _categories = categories ?? new CategoryApplicationService(ApiService.Instance, auth);
        StatusFilters = [
            new SitePostStatusFilter("all", "全部", null),
            new SitePostStatusFilter("published", "已发布", true),
            new SitePostStatusFilter("draft", "草稿", false)
        ];
        SelectedStatusFilter = StatusFilters[0];
        CategoryFilters.Add(AllCategoriesSentinel);
        SelectedCategoryFilter = AllCategoriesSentinel;
    }

    public ObservableCollection<SitePostListItem> Posts { get; } = new();
    public ObservableCollection<StackBreadcrumb> Breadcrumbs { get; } = new();
    public ObservableCollection<Category> CategoryFilters { get; } = new();
    public IReadOnlyList<SitePostStatusFilter> StatusFilters { get; }

    public static Category AllCategoriesSentinel { get; } = new() { Id = 0, Text = "全部分类" };

    [ObservableProperty] private SitePostListItem? _selectedPost;
    [ObservableProperty] private SitePostStatusFilter? _selectedStatusFilter;
    [ObservableProperty] private Category? _selectedCategoryFilter;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _page = 1;
    [ObservableProperty] private int _pageSize = 20;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "登录后可查看站点上的文章。";
    [ObservableProperty] private object? _activeStackPage;
    [ObservableProperty] private bool _isStackNavigating;
    [ObservableProperty] private bool _hasLoaded;

    public bool CanGoPrevious => Page > 1 && !IsBusy;
    public bool CanGoNext => Page < Math.Max(TotalPages, 1) && !IsBusy;
    public bool HasSelectedPost => SelectedPost != null;
    public string PageSummary => TotalCount == 0
        ? "没有文章"
        : $"第 {Page} / {Math.Max(TotalPages, 1)} 页，共 {TotalCount} 篇";

    partial void OnSearchTextChanged(string value) => NotifyPaging();
    partial void OnSelectedPostChanged(SitePostListItem? value) => OnPropertyChanged(nameof(HasSelectedPost));
    partial void OnPageChanged(int value) => NotifyPaging();
    partial void OnTotalCountChanged(int value) => NotifyPaging();
    partial void OnTotalPagesChanged(int value) => NotifyPaging();
    partial void OnIsBusyChanged(bool value) => NotifyPaging();
    partial void OnSelectedStatusFilterChanged(SitePostStatusFilter? value) {
        if (HasLoaded) _ = SearchFromFirstPage();
    }
    partial void OnSelectedCategoryFilterChanged(Category? value) {
        if (HasLoaded) _ = SearchFromFirstPage();
    }

    private void NotifyPaging() {
        OnPropertyChanged(nameof(CanGoPrevious));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(PageSummary));
    }

    public async Task EnsureLoadedAsync() {
        if (HasLoaded || IsBusy) return;
        await LoadFiltersAsync();
        await RefreshAsync();
        HasLoaded = true;
    }

    public void ReloadAfterLogin() {
        HasLoaded = false;
        _ = EnsureLoadedAsync();
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    [RelayCommand]
    private Task Search() => SearchFromFirstPage();

    [RelayCommand]
    private async Task GoPrevious() {
        if (!CanGoPrevious) return;
        Page--;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task GoNext() {
        if (!CanGoNext) return;
        Page++;
        await RefreshAsync();
    }

    [RelayCommand]
    private void OpenSelected() {
        if (SelectedPost != null) OpenEditor(SelectedPost.Id);
    }

    [RelayCommand]
    private void OpenCategories() {
        var page = new CategoryManageViewModel(_categories, () => _ = LoadFiltersAsync());
        OpenStackPage(page, "分类管理");
        _ = page.LoadAsync();
    }

    public void OpenEditor(string postId) {
        var page = new SitePostEditorViewModel(_shell, _library, _categories, postId, OnEditorClosed);
        OpenStackPage(page, "编辑文章");
        _ = page.LoadAsync();
    }

    public void OpenStackPage(object page, string title) {
        ActiveStackPage = page;
        IsStackNavigating = true;
        Breadcrumbs.Clear();
        Breadcrumbs.Add(new StackBreadcrumb { Title = Title, Target = null });
        Breadcrumbs.Add(new StackBreadcrumb { Title = title, Target = page });
    }

    public void NavigateBreadcrumbAt(int index) {
        if (index <= 0) {
            ActiveStackPage = null;
            IsStackNavigating = false;
            Breadcrumbs.Clear();
            _ = RefreshAsync();
            return;
        }

        if (index >= Breadcrumbs.Count) return;
        while (Breadcrumbs.Count > index + 1) Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        ActiveStackPage = Breadcrumbs[index].Target;
        IsStackNavigating = ActiveStackPage != null;
    }

    private void OnEditorClosed(bool changed) {
        NavigateBreadcrumbAt(0);
        if (changed) _ = RefreshAsync();
    }

    private async Task SearchFromFirstPage() {
        Page = 1;
        await RefreshAsync();
    }

    public async Task LoadFiltersAsync() {
        var result = await _categories.GetCategoriesAsync();
        if (!result.Success || result.Categories == null) return;

        var selectedId = SelectedCategoryFilter?.Id ?? 0;
        CategoryFilters.Clear();
        CategoryFilters.Add(AllCategoriesSentinel);
        foreach (var category in CategoryApplicationService.Flatten(result.Categories)) {
            CategoryFilters.Add(category);
        }

        SelectedCategoryFilter = CategoryFilters.FirstOrDefault(c => c.Id == selectedId) ?? AllCategoriesSentinel;
    }

    public async Task RefreshAsync() {
        if (IsBusy) return;
        if (!_shell.IsUserLoggedIn) {
            Posts.Clear();
            TotalCount = 0;
            TotalPages = 0;
            StatusMessage = "登录后可查看站点上的文章。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在加载文章…";
        try {
            var query = new PostListQuery {
                Page = Math.Max(Page, 1),
                PageSize = PageSize,
                Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                CategoryId = SelectedCategoryFilter?.Id ?? 0,
                IsPublish = SelectedStatusFilter?.IsPublish
            };
            var result = await _library.ListAsync(query);
            Posts.Clear();
            if (!result.Success) {
                TotalCount = 0;
                TotalPages = 0;
                StatusMessage = result.ErrorMessage ?? "加载失败";
                return;
            }

            foreach (var post in result.Posts) Posts.Add(new SitePostListItem(post));
            TotalCount = result.Pagination.TotalItemCount;
            TotalPages = Math.Max(result.Pagination.TotalPages, TotalCount == 0 ? 0 : 1);
            if (Page > TotalPages && TotalPages > 0) {
                Page = TotalPages;
            }
            StatusMessage = PageSummary;
        }
        finally {
            IsBusy = false;
        }
    }
}

public sealed class SitePostListItem(BlogPost post) {
    public BlogPost Post { get; } = post;
    public string Id => Post.Id;
    public string Title => string.IsNullOrWhiteSpace(Post.Title) ? "(无标题)" : Post.Title;
    public string StatusText => Post.IsPublish ? "已发布" : "草稿";
    public string CategoryName => Post.Category?.DisplayName ?? string.Empty;
    public string UpdatedAt => Post.LastUpdateTime == default
        ? string.Empty
        : Post.LastUpdateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string Slug => Post.Slug ?? string.Empty;
}

public sealed record SitePostStatusFilter(string Id, string Label, bool? IsPublish) {
    public override string ToString() => Label;
}
