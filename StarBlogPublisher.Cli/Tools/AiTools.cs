using System.ComponentModel;
using ModelContextProtocol.Server;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Tools;

[McpServerToolType]
public static class AiTools {
    private static AiApplicationService CreateAiService() => new(AiService.Instance, AppSettings.Instance);

    [McpServerTool, Description("使用 AI 优化文章标题")]
    public static async Task<string> AiOptimizeTitle(
        [Description("原始标题")] string title,
        [Description("文章内容（用于上下文）")] string? content = null) {

        var ai = CreateAiService();
        if (!ai.IsEnabled) return "错误: AI 功能未启用";
        var result = await ai.RefineTitleAsync(title, content ?? "");
        return result;
    }

    [McpServerTool, Description("使用 AI 生成文章摘要")]
    public static async Task<string> AiGenerateSummary(
        [Description("文章标题")] string title,
        [Description("文章内容")] string content) {

        var ai = CreateAiService();
        if (!ai.IsEnabled) return "错误: AI 功能未启用";
        var result = await ai.GenerateSummaryAsync(title, content);
        return result;
    }

    [McpServerTool, Description("使用 AI 推荐文章标签/关键词")]
    public static async Task<string> AiSuggestTags(
        [Description("文章标题")] string title,
        [Description("文章内容")] string content) {

        var ai = CreateAiService();
        if (!ai.IsEnabled) return "错误: AI 功能未启用";
        var result = await ai.GenerateKeywordsAsync(title, content);
        return result;
    }

    [McpServerTool, Description("使用 AI 生成 URL slug")]
    public static async Task<string> AiGenerateSlug(
        [Description("文章标题")] string title) {

        var ai = CreateAiService();
        if (!ai.IsEnabled) return "错误: AI 功能未启用";
        var result = await ai.GenerateSlugAsync(title);
        return result;
    }

    [McpServerTool, Description("使用 AI 生成封面图提示词")]
    public static async Task<string> AiGenerateCoverPrompt(
        [Description("文章标题")] string title,
        [Description("文章摘要")] string summary,
        [Description("封面风格模板 Key（可选）")] string? styleKey = null) {

        var ai = CreateAiService();
        if (!ai.IsEnabled) return "错误: AI 功能未启用";
        var result = await ai.GenerateCoverPromptAsync(title, summary, styleKey);
        return result;
    }
}
