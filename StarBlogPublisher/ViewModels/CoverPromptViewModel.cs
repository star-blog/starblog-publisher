using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls.Notifications;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.ViewModels;

public partial class CoverPromptViewModel : ViewModelBase {
    public string Title => "封面提示词";

    [ObservableProperty] private bool _isAIEnabled;
    [ObservableProperty] private string _generatedPrompt = "";
    [ObservableProperty] private PromptTemplate? _selectedTemplate;

    public string ArticleTitle { get; set; } = string.Empty;
    public string ArticleDescription { get; set; } = string.Empty;
    public string ArticleContent { get; set; } = string.Empty;

    public ObservableCollection<PromptTemplate> CoverStyleOptions { get; } = new(PromptTemplates.Cover);

    public CoverPromptViewModel() {
        IsAIEnabled = AppSettings.Instance.EnableAI;
        SelectedTemplate = CoverStyleOptions.FirstOrDefault(e => e.Key == "BeCreativeGirl")
                           ?? CoverStyleOptions.FirstOrDefault();
    }

    [RelayCommand]
    private async Task GeneratePrompt() {
        if (!IsAIEnabled || string.IsNullOrEmpty(ArticleContent)) {
            await GuiHost.AlertAsync("无法生成", "AI功能未启用或文章内容为空", NotificationType.Warning);
            return;
        }

        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(SelectedTemplate.Prompt)) {
            await GuiHost.AlertAsync("无法生成", "未选择风格，或者所选风格的提示词为空！", NotificationType.Warning);
            return;
        }

        try {
            var prompt = PromptBuilder
                .Create(SelectedTemplate.Prompt)
                .AddParameter("title", ArticleTitle)
                .AddParameter("summary", ArticleDescription)
                .AddParameter("content", ArticleContent)
                .Build();
            var result = new StringBuilder();
            await foreach (var update in AiService.Instance.GenerateTextStreamAsync(prompt)) {
                result.Append(update.Text);
                GeneratedPrompt = result.ToString();
            }

            GuiHost.ToastSuccess("封面提示词", "已生成 AI 画图提示词");
        }
        catch (Exception ex) {
            await GuiHost.AlertAsync("生成失败", $"生成AI画图提示词失败: {ex.Message}", NotificationType.Error);
        }
    }
}
