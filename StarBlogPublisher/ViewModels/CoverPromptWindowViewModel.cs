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

    public ObservableCollection<PromptTemplate> CoverStyleOptions { get; } =
        new ObservableCollection<PromptTemplate>(PromptTemplates.Cover);

    public PromptTemplate SelectedTemplate { get; set; }

    public CoverPromptWindowViewModel() {
        // 初始化AI功能状态
        IsAIEnabled = AppSettings.Instance.EnableAI;
        SelectedTemplate = CoverStyleOptions.First(e => e.Key == "UrbanElegance");
    }

    // 重新生成文章简介命令
    [RelayCommand]
    private async Task GeneratePrompt() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent)) {
            await ShowMessageBox("错误", "无法生成：AI功能未启用或文章内容为空");
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTemplate.Prompt)) {
            await ShowMessageBox("错误", "未选择风格，或者所选风格的提示词为空！");
            return;
        }

        try {
            var prompt = PromptBuilder
                .Create(SelectedTemplate.Prompt)
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