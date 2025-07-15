using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    [ObservableProperty] private string _generatedPrompt = "";

    public string ArticleTitle { get; set; }
    public string ArticleDescription { get; set; }
    public string ArticleContent { get; set; }

    public ObservableCollection<CoverStyleOption> CoverStyleOptions { get; } = new() {
        new CoverStyleOption
            { Display = "极简风 Minimalism", Value = "Minimalism", Prompt = PromptTemplates.CoverPromptMinimalism },
        new CoverStyleOption
            { Display = "科技感 Tech Vibes", Value = "Tech", Prompt = PromptTemplates.CoverPromptTechStyle },
        new CoverStyleOption
            { Display = "吸引眼球（美女版）👩✨", Value = "Beauty", Prompt = PromptTemplates.CoverPromptAttractiveFemale },
        new CoverStyleOption
            { Display = "开源纪念海报风格", Value = "OpenSourcePoster", Prompt = PromptTemplates.CoverPromptOpenSourcePoster },
        new CoverStyleOption
            { Display = "未来感（Future/AIGC）", Value = "Future", Prompt = PromptTemplates.CoverPromptFuturistic },
        new CoverStyleOption {
            Display = "Urban Elegance（城市优雅风）",
            Value = "Urban Elegance",
            Prompt = PromptTemplates.CoverPromptUrbanElegance
        }
    };

    public CoverStyleOption SelectedCoverStyleOption { get; set; }

    public CoverPromptWindowViewModel() {
        // 初始化AI功能状态
        IsAIEnabled = AppSettings.Instance.EnableAI;
        SelectedCoverStyleOption = CoverStyleOptions.First(e => e.Value == "Beauty");
    }

    // 重新生成文章简介命令
    [RelayCommand]
    private async Task GeneratePrompt() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent)) {
            await ShowMessageBox("错误", "无法生成：AI功能未启用或文章内容为空");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedCoverStyleOption.Prompt)) {
            await ShowMessageBox("错误", "未选择风格，或者所选风格的提示词为空！");
            return;
        }

        try {
            var prompt = PromptBuilder
                .Create(SelectedCoverStyleOption.Prompt)
                .AddParameter("title", ArticleTitle)
                .AddParameter("summary", ArticleDescription)
                .AddParameter("content", ArticleContent)
                .Build();
            var textStreamAsync = AiService.Instance.GenerateTextStreamAsync(prompt);
            var result = new StringBuilder();

            await foreach (var update in textStreamAsync) {
                result.Append(update.Text);
                GeneratedPrompt = result.ToString();
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
    public string Prompt { get; set; } // 提示词

    public override string ToString() => Display; // 为了调试方便
}