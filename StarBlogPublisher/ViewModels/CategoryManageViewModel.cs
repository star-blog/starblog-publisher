using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class CategoryManageViewModel : ViewModelBase {
    private readonly CategoryApplicationService _categories;
    private readonly Action? _onChanged;

    public CategoryManageViewModel(CategoryApplicationService categories, Action? onChanged = null) {
        _categories = categories;
        _onChanged = onChanged;
    }

    public ObservableCollection<CategoryManageItem> Items { get; } = new();

    [ObservableProperty] private CategoryManageItem? _selectedItem;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private bool _editVisible = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "加载分类…";

    public bool HasSelection => SelectedItem != null;
    public bool CanSave => HasSelection && !IsBusy && !string.IsNullOrWhiteSpace(EditName);

    partial void OnSelectedItemChanged(CategoryManageItem? value) {
        EditName = value?.Name ?? string.Empty;
        EditVisible = value?.Visible ?? true;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanSave));
    }

    partial void OnEditNameChanged(string value) => OnPropertyChanged(nameof(CanSave));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSave));

    public async Task LoadAsync() {
        IsBusy = true;
        StatusMessage = "正在加载分类…";
        try {
            var selectedId = SelectedItem?.Id;
            var result = await _categories.GetAllAsync();
            Items.Clear();
            if (!result.Success || result.Categories == null) {
                StatusMessage = result.ErrorMessage ?? "分类列表为空";
                return;
            }

            foreach (var item in result.Categories.OrderBy(c => c.ParentId).ThenBy(c => c.Id)) {
                Items.Add(CategoryManageItem.From(item, result.Categories));
            }

            SelectedItem = Items.FirstOrDefault(i => i.Id == selectedId) ?? Items.FirstOrDefault();
            StatusMessage = $"共 {Items.Count} 个分类";
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private async Task Create() {
        var name = await GuiHost.PromptAsync("新建分类", watermark: "分类名称");
        if (string.IsNullOrWhiteSpace(name)) return;
        IsBusy = true;
        try {
            var parentId = SelectedItem?.Id ?? 0;
            var result = await _categories.CreateCategoryAsync(name.Trim(), parentId);
            if (!result.Success) {
                GuiHost.ToastError("创建失败", result.ErrorMessage ?? "创建失败");
                return;
            }

            _onChanged?.Invoke();
            GuiHost.ToastSuccess("已创建", name.Trim());
            await LoadAsync();
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Save() {
        if (SelectedItem == null || !CanSave) return;
        IsBusy = true;
        try {
            var result = await _categories.UpdateCategoryAsync(
                SelectedItem.Id, EditName.Trim(), SelectedItem.ParentId, EditVisible);
            if (!result.Success) {
                GuiHost.ToastError("保存失败", result.ErrorMessage ?? "保存失败");
                StatusMessage = result.ErrorMessage ?? "保存失败";
                return;
            }

            _onChanged?.Invoke();
            GuiHost.ToastSuccess("已保存", EditName.Trim());
            await LoadAsync();
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleVisible() {
        if (SelectedItem == null) return;
        IsBusy = true;
        try {
            var next = !SelectedItem.Visible;
            var result = await _categories.SetVisibilityAsync(SelectedItem.Id, next);
            if (!result.Success) {
                GuiHost.ToastError("更新失败", result.ErrorMessage ?? "更新失败");
                return;
            }

            _onChanged?.Invoke();
            GuiHost.ToastSuccess(next ? "已设为可见" : "已隐藏", SelectedItem.Name);
            await LoadAsync();
        }
        finally {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Delete() {
        if (SelectedItem == null) return;
        if (!await GuiHost.ConfirmAsync("删除分类", $"确定删除「{SelectedItem.Name}」？分类下有文章时无法删除。")) return;
        IsBusy = true;
        try {
            var result = await _categories.DeleteCategoryAsync(SelectedItem.Id);
            if (!result.Success) {
                GuiHost.ToastError("删除失败", result.ErrorMessage ?? "删除失败");
                StatusMessage = result.ErrorMessage ?? "删除失败";
                return;
            }

            _onChanged?.Invoke();
            GuiHost.ToastSuccess("已删除", SelectedItem.Name);
            SelectedItem = null;
            await LoadAsync();
        }
        finally {
            IsBusy = false;
        }
    }
}

public sealed class CategoryManageItem {
    public int Id { get; init; }
    public int ParentId { get; init; }
    public required string Name { get; init; }
    public bool Visible { get; init; }
    public int Depth { get; init; }
    public string VisibilityText => Visible ? "可见" : "隐藏";
    public string IndentedName => new string('　', Depth) + Name;

    public static CategoryManageItem From(Category category, IReadOnlyList<Category> all) {
        var name = category.DisplayName;
        return new CategoryManageItem {
            Id = category.Id,
            ParentId = category.ParentId,
            Name = name,
            Visible = category.Visible,
            Depth = DepthOf(category, all)
        };
    }

    private static int DepthOf(Category category, IReadOnlyList<Category> all) {
        var depth = 0;
        var parentId = category.ParentId;
        var guard = 0;
        while (parentId > 0 && guard++ < 32) {
            var parent = all.FirstOrDefault(c => c.Id == parentId);
            if (parent == null) break;
            depth++;
            parentId = parent.ParentId;
        }
        return depth;
    }
}
