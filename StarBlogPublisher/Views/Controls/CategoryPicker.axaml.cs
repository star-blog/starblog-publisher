using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StarBlogPublisher.Models;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Views.Controls;

public partial class CategoryPicker : UserControl {
    public static readonly StyledProperty<IEnumerable<Category>?> CategoriesProperty =
        AvaloniaProperty.Register<CategoryPicker, IEnumerable<Category>?>(nameof(Categories));
    public static readonly StyledProperty<Category?> SelectedCategoryProperty =
        AvaloniaProperty.Register<CategoryPicker, Category?>(nameof(SelectedCategory), defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<string> CategorySearchTextProperty =
        AvaloniaProperty.Register<CategoryPicker, string>(nameof(CategorySearchText), string.Empty);
    public static readonly StyledProperty<IEnumerable<Category>?> FilteredCategoriesProperty =
        AvaloniaProperty.Register<CategoryPicker, IEnumerable<Category>?>(nameof(FilteredCategories));
    public static readonly StyledProperty<ICommand?> RefreshCommandProperty =
        AvaloniaProperty.Register<CategoryPicker, ICommand?>(nameof(RefreshCommand));
    public static readonly StyledProperty<bool> IsRefreshingProperty =
        AvaloniaProperty.Register<CategoryPicker, bool>(nameof(IsRefreshing));
    public static readonly StyledProperty<bool> ShowManagementActionsProperty =
        AvaloniaProperty.Register<CategoryPicker, bool>(nameof(ShowManagementActions), true);
    public static readonly StyledProperty<bool> CanManageCategoriesProperty =
        AvaloniaProperty.Register<CategoryPicker, bool>(nameof(CanManageCategories));
    public static readonly StyledProperty<ICommand?> ShowAddCategoryCommandProperty =
        AvaloniaProperty.Register<CategoryPicker, ICommand?>(nameof(ShowAddCategoryCommand));
    public static readonly StyledProperty<ICommand?> ShowWordCloudCommandProperty =
        AvaloniaProperty.Register<CategoryPicker, ICommand?>(nameof(ShowWordCloudCommand));
    public static readonly StyledProperty<string> SelectionDescriptionProperty =
        AvaloniaProperty.Register<CategoryPicker, string>(nameof(SelectionDescription), "选择后会保留在当前文章中");

    public IEnumerable<Category>? Categories { get => GetValue(CategoriesProperty); set => SetValue(CategoriesProperty, value); }
    public Category? SelectedCategory { get => GetValue(SelectedCategoryProperty); set => SetValue(SelectedCategoryProperty, value); }
    public string CategorySearchText { get => GetValue(CategorySearchTextProperty); set => SetValue(CategorySearchTextProperty, value); }
    public IEnumerable<Category>? FilteredCategories { get => GetValue(FilteredCategoriesProperty); private set => SetValue(FilteredCategoriesProperty, value); }
    public ICommand? RefreshCommand { get => GetValue(RefreshCommandProperty); set => SetValue(RefreshCommandProperty, value); }
    public bool IsRefreshing { get => GetValue(IsRefreshingProperty); set => SetValue(IsRefreshingProperty, value); }
    public bool ShowManagementActions { get => GetValue(ShowManagementActionsProperty); set => SetValue(ShowManagementActionsProperty, value); }
    public bool CanManageCategories { get => GetValue(CanManageCategoriesProperty); set => SetValue(CanManageCategoriesProperty, value); }
    public ICommand? ShowAddCategoryCommand { get => GetValue(ShowAddCategoryCommandProperty); set => SetValue(ShowAddCategoryCommandProperty, value); }
    public ICommand? ShowWordCloudCommand { get => GetValue(ShowWordCloudCommandProperty); set => SetValue(ShowWordCloudCommandProperty, value); }
    public string SelectionDescription { get => GetValue(SelectionDescriptionProperty); set => SetValue(SelectionDescriptionProperty, value); }
    private INotifyCollectionChanged? _observedCategories;

    public CategoryPicker() {
        InitializeComponent();
        CategoryTree.Loaded += (_, _) => ExpandMatchingBranches();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change) {
        base.OnPropertyChanged(change);
        if (change.Property == CategoriesProperty) {
            ObserveCategories();
            RefreshTree();
        }
        else if (change.Property == CategorySearchTextProperty) RefreshTree();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
        base.OnAttachedToVisualTree(e);
        ObserveCategories();
        RefreshTree();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
        if (_observedCategories != null) _observedCategories.CollectionChanged -= OnCategoriesChanged;
        _observedCategories = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void ObserveCategories() {
        if (_observedCategories != null) _observedCategories.CollectionChanged -= OnCategoriesChanged;
        _observedCategories = Categories as INotifyCollectionChanged;
        if (_observedCategories != null) _observedCategories.CollectionChanged += OnCategoriesChanged;
    }

    private void OnCategoriesChanged(object? sender, NotifyCollectionChangedEventArgs e) => RefreshTree();

    private void RefreshTree() {
        FilteredCategories = string.IsNullOrWhiteSpace(CategorySearchText) ? Categories
            : CategoryTreeHelper.Filter(Categories, CategorySearchText.Trim());
        Dispatcher.UIThread.Post(ExpandMatchingBranches, DispatcherPriority.Loaded);
    }

    private void OnCategoryClicked(object? sender, RoutedEventArgs e) {
        if (sender is Button { DataContext: Category category }) {
            SetCurrentValue(SelectedCategoryProperty, CategoryTreeHelper.FindById(Categories, category.Id) ?? category);
        }
    }

    private void ExpandMatchingBranches() {
        if (!string.IsNullOrWhiteSpace(CategorySearchText)) ExpandContainers(CategoryTree);
    }

    private static void ExpandContainers(ItemsControl itemsControl) {
        foreach (var item in itemsControl.Items) {
            if (item != null && itemsControl.ContainerFromItem(item) is TreeViewItem treeItem) {
                treeItem.IsExpanded = true;
                ExpandContainers(treeItem);
            }
        }
    }
}
