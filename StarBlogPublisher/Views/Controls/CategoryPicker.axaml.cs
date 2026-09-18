using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views.Controls;

public partial class CategoryPicker : UserControl {
    private PublishViewModel? _viewModel;

    public CategoryPicker() {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        CategoryTree.Loaded += (_, _) => ExpandMatchingBranches();
    }

    private void OnDataContextChanged(object? sender, EventArgs e) {
        if (_viewModel != null) {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PublishViewModel;
        if (_viewModel != null) {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName is nameof(PublishViewModel.CategorySearchText)
            or nameof(PublishViewModel.FilteredCategories)) {
            Dispatcher.UIThread.Post(ExpandMatchingBranches, DispatcherPriority.Loaded);
        }
    }

    private void ExpandMatchingBranches() {
        if (string.IsNullOrWhiteSpace(_viewModel?.CategorySearchText)) {
            return;
        }

        ExpandContainers(CategoryTree);
    }

    private static void ExpandContainers(ItemsControl itemsControl) {
        foreach (var item in itemsControl.Items) {
            if (item is null) {
                continue;
            }

            if (itemsControl.ContainerFromItem(item) is TreeViewItem treeItem) {
                treeItem.IsExpanded = true;
                ExpandContainers(treeItem);
            }
        }
    }
}
