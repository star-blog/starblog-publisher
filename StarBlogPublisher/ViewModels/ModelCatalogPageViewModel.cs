using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.AI;

namespace StarBlogPublisher.ViewModels;

public sealed record ModelCatalogSnapshot(IReadOnlyList<AIModelDescriptor> Models, string StatusMessage);

/// <summary>Full-page model catalog, kept outside the sidebar because it is contextual to AI settings.</summary>
public partial class ModelCatalogPageViewModel : PageViewModelBase {
    private readonly SettingsViewModel _settings;
    private readonly Action _returnToSettings;
    private List<AIModelDescriptor> _allModels = [];

    public ObservableCollection<AIModelDescriptor> FilteredModels { get; } = [];

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private AIModelDescriptor? _selectedModel;
    [ObservableProperty] private string _customModelId = string.Empty;
    [ObservableProperty] private string _statusMessage = "\u6b63\u5728\u51c6\u5907\u6a21\u578b\u76ee\u5f55\u2026";
    [ObservableProperty] private bool _isRefreshing;

    public string ProviderName => _settings.CurrentProvider?.DisplayName ?? _settings.AIProvider;
    public string ModelCountText => $"{FilteredModels.Count} \u4e2a\u6a21\u578b";

    public ModelCatalogPageViewModel(SettingsViewModel settings, Action returnToSettings)
        : base("\u6a21\u578b\u76ee\u5f55", Icon.Database) {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _returnToSettings = returnToSettings ?? throw new ArgumentNullException(nameof(returnToSettings));
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedModelChanged(AIModelDescriptor? value) {
        if (value != null) CustomModelId = string.Empty;
    }

    public async Task RefreshAsync() {
        IsRefreshing = true;
        try {
            var snapshot = await _settings.RefreshModelCatalogAsync();
            _allModels = snapshot.Models.ToList();
            StatusMessage = snapshot.StatusMessage;
            SelectedModel = _allModels.FirstOrDefault(item => string.Equals(item.Id, _settings.AIModel, StringComparison.OrdinalIgnoreCase));
            CustomModelId = SelectedModel == null ? _settings.AIModel : string.Empty;
            ApplyFilter();
            OnPropertyChanged(nameof(ProviderName));
        }
        finally {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => RefreshAsync();

    [RelayCommand]
    private void ApplyModel() {
        var modelId = string.IsNullOrWhiteSpace(CustomModelId) ? SelectedModel?.Id : CustomModelId.Trim();
        if (string.IsNullOrWhiteSpace(modelId)) {
            GuiHost.ToastWarning("AI \u6a21\u578b", "\u8bf7\u4ece\u76ee\u5f55\u4e2d\u9009\u62e9\u6a21\u578b\uff0c\u6216\u8f93\u5165\u81ea\u5b9a\u4e49\u6a21\u578b ID\u3002");
            return;
        }

        _settings.SelectModel(modelId);
        _returnToSettings();
    }

    [RelayCommand]
    private void ReturnToSettings() => _returnToSettings();

    private void ApplyFilter() {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allModels
            : _allModels.Where(item => item.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        FilteredModels.Clear();
        foreach (var model in filtered) FilteredModels.Add(model);
        OnPropertyChanged(nameof(ModelCountText));
    }
}
