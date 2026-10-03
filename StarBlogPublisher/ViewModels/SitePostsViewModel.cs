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
        Posts.CollectionChanged += (_, _) => NotifyListState();
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
    [ObservableProperty] private bool _hasLoadError;
    [ObservableProperty] private bool _isLoadingCategories;
    private bool _refreshPending;
    private bool _loadingFilters;

    public bool CanGoPrevious => Page > 1 && !IsBusy;
    public bool CanGoNext => Page < Math.Max(TotalPages, 1) && !IsBusy;
    public bool HasSelectedPost => SelectedPost != null && !IsBusy;
    public bool NeedsLogin => !_shell.IsUserLoggedIn;
    public bool HasPosts => Posts.Count > 0;
    public string CategorySelectionText => SelectedCategoryFilter?.DisplayName ?? "全部分类";
    public bool ShowEmptyState => !IsBusy && !HasPosts;
    public bool HasActiveFilters => !string.IsNullOrWhiteSpace(SearchText)
        || SelectedCategoryFilter?.Id > 0 || SelectedStatusFilter?.IsPublish != null;
    public string EmptyTitle => NeedsLogin ? "登录后查看站点文章"
        : HasLoadError ? "暂时无法加载文章"
        : HasActiveFilters ? "没有符合条件的文章" : "站点上还没有文章";
    public string EmptyDescription => NeedsLogin ? "连接 StarBlog 账号，管理线上文章和草稿。"
        : HasLoadError ? StatusMessage
        : HasActiveFilters ? "试试其他关键词，或清除筛选条件。" : "在「文章」工作区完成首次发布后，可在这里更新文章。";
    public string PageSummary => TotalCount == 0
        ? "没有文章"
        : $"第 {Page} / {Math.Max(TotalPages, 1)} 页，共 {TotalCount} 篇";

    partial void OnSearchTextChanged(string value) => NotifyListState();
    partial void OnSelectedPostChanged(SitePostListItem? value) => OnPropertyChanged(nameof(HasSelectedPost));
    partial void OnPageChanged(int value) => NotifyPaging();
    partial void OnTotalCountChanged(int value) => NotifyPaging();
    partial void OnTotalPagesChanged(int value) => NotifyPaging();
    partial void OnIsBusyChanged(bool value) { NotifyPaging(); NotifyListState(); }
    partial void OnHasLoadErrorChanged(bool value) => NotifyListState();
    partial void OnSelectedStatusFilterChanged(SitePostStatusFilter? value) {
        NotifyListState();
        if (HasLoaded) _ = SearchFromFirstPage();
    }
    partial void OnSelectedCategoryFilterChanged(Category? value) {
        OnPropertyChanged(nameof(CategorySelectionText));
        NotifyListState();
        if (HasLoaded && !_loadingFilters) _ = SearchFromFirstPage();
    }

    private void NotifyListState() {
        OnPropertyChanged(nameof(HasPosts));
        OnPropertyChanged(nameof(ShowEmptyState));
        OnPropertyChanged(nameof(NeedsLogin));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyDescription));
        OnPropertyChanged(nameof(HasSelectedPost));
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
    private async Task ClearFilters() {
        // Reset as one query, rather than loading each intermediate combination.
        var loaded = HasLoaded;
        HasLoaded = false;
        SearchText = string.Empty;
        SelectedStatusFilter = StatusFilters[0];
        SelectedCategoryFilter = AllCategoriesSentinel;
        HasLoaded = loaded;
        await SearchFromFirstPage();
    }

    [RelayCommand]
    private Task Login() => _shell.EnsureLoggedInAsync();

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
        if (HasSelectedPost) OpenEditor(SelectedPost!.Id);
    }

    [RelayCommand]
    private void OpenCategories() {
        var page = new CategoryManageViewModel(_categories, () => _ = LoadFiltersAsync());
        OpenStackPage(page, "分类管理");
        _ = page.LoadAsync();
    }

    public void OpenEditor(string postId) {
        if (IsBusy) return;
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
    }

    public void RefreshEditorPreviewForThemeChange() {
        if (ActiveStackPage is SitePostEditorViewModel editor) editor.RefreshPreviewForThemeChange();
    }

    public async Task NavigateBreadcrumbAsync(int index) {
        if (index <= 0 && ActiveStackPage is SitePostEditorViewModel editor) {
            if (editor.IsBusy) return;
            if (editor.IsDirty && !await GuiHost.ConfirmAsync("放弃修改", "文章有未保存的修改，确定返回文章列表？")) return;
        }
        NavigateBreadcrumbAt(index);
    }

    private async Task SearchFromFirstPage() {
        Page = 1;
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task LoadFiltersAsync() {
        if (IsLoadingCategories) return;
        IsLoadingCategories = true;
        try {
            var result = await _categories.GetCategoriesAsync();
            if (!result.Success || result.Categories == null) return;
            _loadingFilters = true;
            var selectedId = SelectedCategoryFilter?.Id ?? 0;
            CategoryFilters.Clear();
            CategoryFilters.Add(AllCategoriesSentinel);
            foreach (var category in result.Categories) CategoryFilters.Add(category);
            SelectedCategoryFilter = CategoryApplicationService.Flatten(CategoryFilters)
                .FirstOrDefault(c => c.Id == selectedId) ?? AllCategoriesSentinel;
        }
        finally {
            _loadingFilters = false;
            IsLoadingCategories = false;
        }
    }

    public async Task RefreshAsync() {
        if (IsBusy) { _refreshPending = true; return; }
        if (!_shell.IsUserLoggedIn) {
            Posts.Clear();
            SelectedPost = null;
            TotalCount = 0;
            TotalPages = 0;
            StatusMessage = "登录后可查看站点上的文章。";
            HasLoadError = false;
            NotifyListState();
            return;
        }

        IsBusy = true;
        StatusMessage = "正在加载文章…";
        try {
            do {
                _refreshPending = false;
                var query = new PostListQuery {
                    Page = Math.Max(Page, 1),
                    PageSize = PageSize,
                    Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                    CategoryId = SelectedCategoryFilter?.Id ?? 0,
                    IsPublish = SelectedStatusFilter?.IsPublish
                };
                var result = await _library.ListAsync(query);
                // A filter change during a request must not be dropped or render stale rows.
                if (_refreshPending) continue;
                if (!_shell.IsUserLoggedIn) {
                    Posts.Clear();
                    SelectedPost = null;
                    TotalCount = TotalPages = 0;
                    StatusMessage = "登录后可查看站点上的文章。";
                    HasLoadError = false;
                    break;
                }
                var selectedId = SelectedPost?.Id;
                Posts.Clear();
                SelectedPost = null;
                HasLoadError = !result.Success;
                if (!result.Success) {
                    TotalCount = 0;
                    TotalPages = 0;
                    StatusMessage = result.ErrorMessage ?? "加载失败";
                    return;
                }

                TotalCount = result.Pagination.TotalItemCount;
                TotalPages = Math.Max(result.Pagination.TotalPages, TotalCount == 0 ? 0 : 1);
                if (TotalCount == 0) Page = 1;
                if (Page > TotalPages && TotalPages > 0) {
                    Page = TotalPages;
                    _refreshPending = true;
                    continue;
                }
                foreach (var post in result.Posts) Posts.Add(new SitePostListItem(post));
                SelectedPost = Posts.FirstOrDefault(p => p.Id == selectedId);
                StatusMessage = PageSummary;
            } while (_refreshPending);
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
