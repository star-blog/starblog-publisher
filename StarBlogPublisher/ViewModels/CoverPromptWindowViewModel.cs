using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using StarBlogPublisher.Services;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

public partial class CoverPromptWindowViewModel : ViewModelBase {
    // AI功能是否启用
    [ObservableProperty] private bool _isAIEnabled = false;

    // 文章封面风格
    [ObservableProperty] private string _coverStyle = "";

    public string ArticleTitle { get; set; }
    public string ArticleDescription { get; set; }
    public string ArticleContent { get; set; }

    public ObservableCollection<CoverStyleOption> CoverStyleOptions { get; } = new() {
        new CoverStyleOption { Display = "极简风 Minimalism", Value = "Minimalism" },
        new CoverStyleOption { Display = "科技感 Tech Vibes", Value = "Tech" },
        new CoverStyleOption { Display = "吸引眼球（美女版）👩✨", Value = "Beauty" },
        new CoverStyleOption { Display = "开源纪念海报风格", Value = "OpenSourcePoster" },
        new CoverStyleOption { Display = "未来感（Future/AIGC）", Value = "Future" },
    };

    public string SelectedCoverStyle { get; set; } // 绑定的实际值

    private CoverStyleOption _selected;

    public CoverStyleOption SelectedCoverStyleOption {
        get => _selected;
        set {
            _selected = value;
            SelectedCoverStyle = value?.Value;
            // OnPropertyChanged 触发通知
        }
    }

    public CoverPromptWindowViewModel() {
        // 初始化AI功能状态
        IsAIEnabled = AppSettings.Instance.EnableAI;
    }

    // 重新生成文章简介命令
    [RelayCommand]
    private async Task GeneratePrompt() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent)) {
            await ShowMessageBox("错误", "无法生成：AI功能未启用或文章内容为空");
            return;
        }

        try {
            var prompt = PromptBuilder
                .Create(PromptTemplates.ArticleDescriptionTechnical)
                .AddParameter("title", ArticleTitle)
                .AddParameter("content", ArticleContent)
                .Build();
            var textStreamAsync = AiService.Instance.GenerateTextStreamAsync(prompt);
            var description = new StringBuilder();

            await foreach (var update in textStreamAsync) {
                description.Append(update.Text);
                ArticleDescription = description.ToString();
            }

            await ShowMessageBox("成功", "已生成AI画图提示词");
        }
        catch (Exception ex) {
            await ShowMessageBox("错误", $"生成AI画图提示词失败: {ex.Message}");
        }
    }

    private async Task<ButtonResult> ShowMessageBox(string title, string text, Icon icon = Icon.None) {
        var msgbox = MessageBoxManager.GetMessageBoxStandard(
            title,
            text,
            ButtonEnum.Ok,
            icon
        );

        return await msgbox.ShowWindowDialogAsync(App.MainWindow);
    }
}

public class CoverStyleOption {
    public string Display { get; set; } // 显示的文字
    public string Value { get; set; } // 实际绑定的值

    public override string ToString() => Display; // 为了调试方便
}