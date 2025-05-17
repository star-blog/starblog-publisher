using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class AiSettingsWindowViewModel : ViewModelBase {
    private bool _enableAI;
    private string _aiProvider = "openai";
    private string _aiKey = string.Empty;
    private string _aiModel = string.Empty;
    private string _aiApiBase = string.Empty;
    private bool _showAIKey;
    private bool _isLoadingModels;
    private List<AIProviderInfo> _aiProviders = AIProviderInfo.GetProviders();
    private ObservableCollection<string> _availableModels = new();
    private string _statusMessage = "准备就绪";

    public bool EnableAI {
        get => _enableAI;
        set => SetProperty(ref _enableAI, value);
    }

    public string AIProvider {
        get => _aiProvider;
        set {
            var provider = AIProviders.FirstOrDefault(p => p.DisplayName == value);
            var providerName = provider?.Name ?? value;
            if (SetProperty(ref _aiProvider, providerName)) {
                OnPropertyChanged(nameof(IsCustomProvider));
                OnAIProviderChanged(providerName);
            }
        }
    }

    public List<AIProviderInfo> AIProviders {
        get => _aiProviders;
    }

    public string AIKey {
        get => _aiKey;
        set => SetProperty(ref _aiKey, value);
    }

    public string AIModel {
        get => _aiModel;
        set => SetProperty(ref _aiModel, value);
    }

    public string AIApiBase {
        get => _aiApiBase;
        set => SetProperty(ref _aiApiBase, value);
    }

    public bool ShowAIKey {
        get => _showAIKey;
        set => SetProperty(ref _showAIKey, value);
    }

    public bool IsCustomProvider => AIProvider == "custom";

    public bool IsLoadingModels {
        get => _isLoadingModels;
        set => SetProperty(ref _isLoadingModels, value);
    }

    public ObservableCollection<string> AvailableModels {
        get => _availableModels;
        set => SetProperty(ref _availableModels, value);
    }
    
    public string StatusMessage {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private AIProviderInfo? _currentProvider;

    public AIProviderInfo? CurrentProvider {
        get {
            if (_currentProvider?.Name != AIProvider) {
                _currentProvider = AIProviderInfo.GetProvider(AIProvider);
            }

            return _currentProvider;
        }
    }

    public AiSettingsWindowViewModel() {
        // 加载当前配置
        var settings = AppSettings.Instance;
        LoadSettings(settings);
    }

    private void LoadSettings(AppSettings settings) {
        EnableAI = settings.EnableAI;
        AIProvider = settings.AIProvider;
        AIKey = settings.AIKey;
        AIModel = settings.AIModel;
        AIApiBase = settings.AIApiBase;
        
        // 初始化默认模型列表
        RefreshModels();
    }

    private void OnAIProviderChanged(string value) {
        if (CurrentProvider == null) return;

        if (!IsCustomProvider) {
            if (string.IsNullOrWhiteSpace(AIApiBase)) {
                AIApiBase = CurrentProvider.DefaultApiBase;
            }

            if (string.IsNullOrWhiteSpace(AIModel)) {
                AIModel = CurrentProvider.DefaultModel;
            }
        }
        
        // 切换提供商时刷新模型列表
        RefreshModels();
    }

    [RelayCommand]
    private void ToggleAIKey() {
        ShowAIKey = !ShowAIKey;
    }
    
    [RelayCommand]
    private async Task RefreshModels() {
        if (CurrentProvider == null) return;
        
        IsLoadingModels = true;
        StatusMessage = "正在加载模型列表...";
        
        // 清空当前模型列表
        AvailableModels.Clear();
        
        // 获取模型列表
        var apiBase = IsCustomProvider ? AIApiBase : CurrentProvider.DefaultApiBase;
        
        var result = await CurrentProvider.GetModelsAsync(AIKey, apiBase);
        var models = result.Models;
        
        // 更新模型列表
        foreach (var model in models) {
            AvailableModels.Add(model);
        }
        
        // 如果当前选择的模型不在列表中，且列表不为空
        if (!string.IsNullOrEmpty(AIModel) && !AvailableModels.Contains(AIModel) && AvailableModels.Count > 0) {
            // 如果当前所选模型不在列表中，可以将其添加到列表末尾
            AvailableModels.Add(AIModel);
        }
        
        // 根据成功状态设置消息
        if (result.Success) {
            StatusMessage = $"已加载 {AvailableModels.Count} 个模型";
        } else {
            StatusMessage = $"获取模型列表失败: {result.ErrorMessage}，\n已加载默认模型列表";
            Console.WriteLine($"获取模型列表失败: {result.ErrorMessage}");
        }
        
        IsLoadingModels = false;
    }

    [RelayCommand]
    private void Save() {
        var settings = AppSettings.Instance;

        settings.EnableAI = EnableAI;
        settings.AIProvider = AIProvider;
        settings.AIKey = AIKey;
        settings.AIModel = AIModel;
        settings.AIApiBase = AIApiBase;

        settings.Save();
        CloseWindow();
    }

    [RelayCommand]
    private void Cancel() {
        CloseWindow();
    }

    [RelayCommand]
    private void TestConnection() {
        RefreshModels();
    }

    private void CloseWindow() {
        if (View is Window window) {
            window.Close();
        }
    }
} 