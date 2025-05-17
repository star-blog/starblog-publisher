using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Controls;
using StarBlogPublisher.Models;
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
    private ObservableCollection<AIProfile> _profiles = new();
    private AIProfile? _currentProfile;

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
    
    public ObservableCollection<AIProfile> Profiles {
        get => _profiles;
        set => SetProperty(ref _profiles, value);
    }
    
    public AIProfile? CurrentProfile {
        get => _currentProfile;
        set {
            if (SetProperty(ref _currentProfile, value) && value != null) {
                // 将当前选中的配置文件设置应用到UI
                LoadProfileSettings(value);
            }
        }
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
        // 加载配置文件
        LoadProfiles();
    }

    private void LoadProfiles() {
        var settings = AppSettings.Instance;
        
        // 清空并重新加载配置文件列表
        Profiles.Clear();
        foreach (var profile in settings.AIProfiles) {
            Profiles.Add(profile);
        }
        
        // 设置当前配置文件
        var currentProfileName = settings.CurrentAIProfile;
        CurrentProfile = Profiles.FirstOrDefault(p => p.Name == currentProfileName) ?? Profiles.FirstOrDefault();
        
        // 如果没有配置文件，创建一个默认的
        if (CurrentProfile == null && Profiles.Count == 0) {
            var defaultProfile = new AIProfile {
                Name = "默认",
                EnableAI = settings.EnableAI,
                Provider = settings.AIProvider,
                Key = settings.AIKey,
                Model = settings.AIModel,
                ApiBase = settings.AIApiBase
            };
            
            Profiles.Add(defaultProfile);
            CurrentProfile = defaultProfile;
        }
    }
    
    private void LoadProfileSettings(AIProfile profile) {
        // 将配置文件的设置应用到UI
        EnableAI = profile.EnableAI;
        AIProvider = profile.Provider;
        AIKey = profile.Key;
        AIModel = profile.Model;
        AIApiBase = profile.ApiBase;
        
        // 初始化默认模型列表
        RefreshModels();
    }
    
    private void SaveProfileSettings() {
        if (CurrentProfile == null) return;
        
        // 将UI上的设置保存到当前配置文件
        CurrentProfile.EnableAI = EnableAI;
        CurrentProfile.Provider = AIProvider;
        CurrentProfile.Key = AIKey;
        CurrentProfile.Model = AIModel;
        CurrentProfile.ApiBase = AIApiBase;
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
    private async Task AddProfile() {
        if (View is Window window) {
            var result = await DialogWindow.ShowInputDialog(
                window,
                "添加配置文件",
                "新配置",
                "请输入配置文件名称"
            );
            
            if (result.IsConfirmed && !string.IsNullOrWhiteSpace(result.Text)) {
                var profileName = result.Text.Trim();
                
                // 检查是否已存在同名配置
                if (Profiles.Any(p => p.Name == profileName)) {
                    StatusMessage = "已存在同名配置文件，请使用其他名称";
                    return;
                }
                
                // 保存当前配置
                SaveProfileSettings();
                
                // 创建新配置
                var newProfile = new AIProfile {
                    Name = profileName,
                    EnableAI = EnableAI,
                    Provider = AIProvider,
                    Key = AIKey,
                    Model = AIModel,
                    ApiBase = AIApiBase
                };
                
                Profiles.Add(newProfile);
                CurrentProfile = newProfile;
                
                StatusMessage = $"已添加配置文件 \"{profileName}\"";
            }
        }
    }
    
    [RelayCommand]
    private async Task DeleteProfile() {
        if (CurrentProfile == null || View is not Window window) return;
        
        if (Profiles.Count <= 1) {
            StatusMessage = "至少需要保留一个配置文件";
            return;
        }
        
        var confirmed = await DialogWindow.ShowConfirmDialog(
            window,
            "删除配置文件",
            $"确定要删除配置文件 \"{CurrentProfile.Name}\" 吗？"
        );
        
        if (confirmed) {
            var profileName = CurrentProfile.Name;
            var index = Profiles.IndexOf(CurrentProfile);
            
            Profiles.Remove(CurrentProfile);
            
            // 选择下一个配置文件
            if (Profiles.Count > 0) {
                var nextIndex = Math.Min(index, Profiles.Count - 1);
                CurrentProfile = Profiles[nextIndex];
            }
            
            StatusMessage = $"已删除配置文件 \"{profileName}\"";
        }
    }
    
    [RelayCommand]
    private async Task RenameProfile() {
        if (CurrentProfile == null || View is not Window window) return;
        
        var result = await DialogWindow.ShowInputDialog(
            window,
            "重命名配置文件",
            CurrentProfile.Name,
            "请输入新的配置文件名称"
        );
        
        if (result.IsConfirmed && !string.IsNullOrWhiteSpace(result.Text)) {
            var newName = result.Text.Trim();
            
            // 检查是否已存在同名配置
            if (newName != CurrentProfile.Name && Profiles.Any(p => p.Name == newName)) {
                StatusMessage = "已存在同名配置文件，请使用其他名称";
                return;
            }
            
            var oldName = CurrentProfile.Name;
            CurrentProfile.Name = newName;
            
            // 刷新列表
            OnPropertyChanged(nameof(Profiles));
            
            StatusMessage = $"已将配置文件 \"{oldName}\" 重命名为 \"{newName}\"";
        }
    }

    [RelayCommand]
    private void Save() {
        // 保存当前配置设置
        SaveProfileSettings();
        
        var settings = AppSettings.Instance;

        // 保存全局AI设置（兼容旧版本）
        settings.EnableAI = EnableAI;
        settings.AIProvider = AIProvider;
        settings.AIKey = AIKey;
        settings.AIModel = AIModel;
        settings.AIApiBase = AIApiBase;
        
        // 保存配置文件
        settings.AIProfiles.Clear();
        foreach (var profile in Profiles) {
            settings.AIProfiles.Add(profile);
        }
        
        // 保存当前配置文件名称
        if (CurrentProfile != null) {
            settings.CurrentAIProfile = CurrentProfile.Name;
        }

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