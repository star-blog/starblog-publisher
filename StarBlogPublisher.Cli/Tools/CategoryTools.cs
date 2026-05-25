using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using StarBlogPublisher.Cli.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Tools;

[McpServerToolType]
public static class CategoryTools {
    [McpServerTool, Description("列出所有博客分类")]
    public static async Task<string> CategoryList() {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        var categoryService = new CategoryApplicationService(ApiService.Instance, authService);
        var result = await categoryService.GetCategoriesAsync();

        if (!result.Success) return $"错误: {result.ErrorMessage}";
        if (result.Categories == null || result.Categories.Count == 0) return "暂无分类";

        var items = result.Categories
            .Select(c => new CategorySummaryDto(c.Id.ToString(), c.Text, c.Nodes?.Count ?? 0))
            .ToList();

        return JsonSerializer.Serialize(items, CliJsonContext.Default.ListCategorySummaryDto);
    }

    [McpServerTool, Description("创建新分类")]
    public static async Task<string> CategoryCreate(
        [Description("分类名称")] string name,
        [Description("父分类 ID，默认为 0（顶级）")] int parentId = 0) {

        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        var categoryService = new CategoryApplicationService(ApiService.Instance, authService);
        var result = await categoryService.CreateCategoryAsync(name, parentId);

        return result.Success ? $"分类创建成功: {name}" : $"错误: {result.ErrorMessage}";
    }
}
