using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.ViewModels;

public partial class AddCategoryViewModel : ViewModelBase, IDialogHostAware {
    private readonly Action _onAdded;
    private readonly CategoryApplicationService _categoryService = new(ApiService.Instance,
        new AuthApplicationService(AppSettings.Instance, GlobalState.Instance, ApiService.Instance));

    public event Action? CloseRequested;

    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedParentCategory;

    public AddCategoryViewModel(Action onAdded) {
        _onAdded = onAdded;
        Categories.Add(new Category { Text = "[顶级分类]", Id = 0 });
        SelectedParentCategory = Categories[0];
        _ = InitializeCategories();
    }

    private async Task InitializeCategories() {
        try {
            var resp = await _categoryService.GetCategoriesAsync();
            if (!resp.Success) throw new Exception(resp.ErrorMessage);
            if (resp.Categories != null) {
                foreach (var category in resp.Categories) {
                    Categories.Add(category);
                }
            }
        }
        catch (Exception ex) {
            await GuiHost.AlertAsync("错误", $"获取分类列表失败: {ex.Message}", NotificationType.Error);
        }
    }

    [RelayCommand]
    private async Task Confirm() {
        if (string.IsNullOrWhiteSpace(CategoryName)) {
            await GuiHost.AlertAsync("提示", "请输入分类名称", NotificationType.Warning);
            return;
        }

        try {
            var resp = await _categoryService.CreateCategoryAsync(CategoryName, SelectedParentCategory?.Id ?? 0);

            if (!resp.Success) {
                throw new Exception(resp.ErrorMessage ?? "未知错误");
            }

            _onAdded();
            GuiHost.ToastSuccess("添加分类", $"已添加「{CategoryName}」");
            CloseRequested?.Invoke();
        }
        catch (Exception ex) {
            await GuiHost.AlertAsync("错误", $"添加分类失败: {ex.Message}", NotificationType.Error);
        }
    }
}
