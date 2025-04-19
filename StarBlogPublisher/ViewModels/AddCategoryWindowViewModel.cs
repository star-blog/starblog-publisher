using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class AddCategoryWindowViewModel : ViewModelBase {
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private ObservableCollection<Category> _categories = new();
    [ObservableProperty] private Category? _selectedParentCategory;

    public AddCategoryWindowViewModel() {
        // 添加顶级分类选项
        Categories.Add(new Category { Text = "[顶级分类]", Id = 0 });
        
        // 从服务器获取分类列表
        _ = InitializeCategories();

        // 默认选择顶级分类
        SelectedParentCategory = Categories[0];
    }

    private async Task InitializeCategories() {
        try {
            // 从服务器获取分类数据
            var resp = await ApiService.Instance.Categories.GetNodes();

            if (resp.Data != null) {
                foreach (var category in resp.Data) {
                    Categories.Add(category);
                }
            }
        }
        catch (Exception ex) {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                "错误",
                $"获取分类列表失败: {ex.Message}",
                ButtonEnum.Ok,
                Icon.Error
            );
            await msgBox.ShowWindowDialogAsync(App.MainWindow);
        }
    }

    [RelayCommand]
    private void Cancel() {
        CloseWindow();
    }

    [RelayCommand]
    private async Task Confirm() {
        if (string.IsNullOrWhiteSpace(CategoryName)) {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                "提示",
                "请输入分类名称",
                ButtonEnum.Ok,
                Icon.Warning
            );
            await msgBox.ShowWindowDialogAsync(App.MainWindow);
            return;
        }

        try {
            // 创建分类
            var resp = await ApiService.Instance.Categories.Add(new CategoryCreationDto {
                Name = CategoryName,
                ParentId = SelectedParentCategory?.Id ?? 0
            });

            if (!resp.Successful || resp.Data == null) {
                throw new Exception(resp.Message ?? "未知错误");
            }

            // 刷新主窗口的分类列表
            var mainViewModel = App.MainWindow.DataContext as MainWindowViewModel;
            await mainViewModel?.RefreshCategoriesCommand.ExecuteAsync(null)!;

            // 关闭窗口
            CloseWindow();
        }
        catch (Exception ex) {
            var msgBox = MessageBoxManager.GetMessageBoxStandard(
                "错误",
                $"添加分类失败: {ex.Message}",
                ButtonEnum.Ok,
                Icon.Error
            );
            await msgBox.ShowWindowDialogAsync(App.MainWindow);
        }
    }
    
    private void CloseWindow() {
        if (View is Window window) {
            window.Close();
        }
    }
}