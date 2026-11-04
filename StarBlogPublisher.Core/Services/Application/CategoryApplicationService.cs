using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 分类应用服务
/// 封装分类查询和创建等业务流程
/// </summary>
public class CategoryApplicationService {
    private readonly ApiService _api;
    private readonly AuthApplicationService _authService;

    public CategoryApplicationService(ApiService api, AuthApplicationService authService) {
        _api = api;
        _authService = authService;
    }

    /// <summary>
    /// 获取所有分类（树形结构）
    /// </summary>
    public async Task<CategoryResult> GetCategoriesAsync() {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryResult.Fail(authCheck.ErrorMessage!);

        try {
            var resp = await _api.Categories.GetNodes();
            return CategoryResult.Ok(resp ?? []);
        }
        catch (Exception ex) {
            return CategoryResult.Fail($"获取分类失败: {ApiError.Describe(ex)}");
        }
    }

    /// <summary>
    /// 创建新分类
    /// </summary>
    public async Task<CategoryResult> CreateCategoryAsync(string name, int parentId = 0) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryResult.Fail(authCheck.ErrorMessage!);

        try {
            var resp = await _api.Categories.Add(new Models.Dtos.CategoryCreationDto {
                Name = name,
                ParentId = parentId
            });
            if (resp == null) {
                return CategoryResult.Fail("创建分类失败");
            }
            return CategoryResult.Ok(new List<Category> { resp });
        }
        catch (Exception ex) {
            return CategoryResult.Fail($"创建分类失败: {ApiError.Describe(ex)}");
        }
    }

    /// <summary>
    /// 获取扁平分类列表（含名称、父级、可见性）
    /// </summary>
    public async Task<CategoryResult> GetAllAsync() {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryResult.Fail(authCheck.ErrorMessage!);

        try {
            var categories = new List<Category>();
            var page = 1;
            while (true) {
                var resp = await _api.Categories.GetList(page, 100);
                categories.AddRange(resp.Items);
                if (page >= resp.TotalPages) break;
                page++;
            }
            return CategoryResult.Ok(categories);
        }
        catch (Exception ex) {
            return CategoryResult.Fail($"获取分类失败: {ApiError.Describe(ex)}");
        }
    }

    public async Task<CategoryMutationResult> UpdateCategoryAsync(int id, string name, int parentId, bool visible) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryMutationResult.Fail(authCheck.ErrorMessage!);
        if (id <= 0) return CategoryMutationResult.Fail("分类 ID 无效");
        if (string.IsNullOrWhiteSpace(name)) return CategoryMutationResult.Fail("分类名称不能为空");

        try {
            var resp = await _api.Categories.Update(id, new Models.Dtos.CategoryCreationDto {
                Name = name.Trim(),
                ParentId = parentId,
                Visible = visible
            });
            if (resp == null) return CategoryMutationResult.Fail("更新分类失败");
            return CategoryMutationResult.Ok(resp);
        }
        catch (Exception ex) {
            return CategoryMutationResult.Fail($"更新分类失败: {ApiError.Describe(ex)}");
        }
    }

    public async Task<CategoryMutationResult> DeleteCategoryAsync(int id) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryMutationResult.Fail(authCheck.ErrorMessage!);
        if (id <= 0) return CategoryMutationResult.Fail("分类 ID 无效");

        try {
            await _api.Categories.Delete(id);
            return CategoryMutationResult.Ok(null, "已删除");
        }
        catch (Exception ex) {
            return CategoryMutationResult.Fail($"删除分类失败: {ApiError.Describe(ex)}");
        }
    }

    public async Task<CategoryMutationResult> SetVisibilityAsync(int id, bool visible) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryMutationResult.Fail(authCheck.ErrorMessage!);
        if (id <= 0) return CategoryMutationResult.Fail("分类 ID 无效");

        try {
            // v1 的分类树不包含隐藏分类，也不返回 ParentId；从完整列表读取后再更新。
            var all = await GetAllAsync();
            if (!all.Success) return CategoryMutationResult.Fail(all.ErrorMessage!);
            var category = all.Categories?.FirstOrDefault(item => item.Id == id);
            if (category == null) return CategoryMutationResult.Fail("分类不存在");
            var result = await UpdateCategoryAsync(id, category.DisplayName, category.ParentId, visible);
            return result.Success
                ? CategoryMutationResult.Ok(result.Category, visible ? "已设为可见" : "已设为隐藏")
                : result;
        }
        catch (Exception ex) {
            return CategoryMutationResult.Fail($"更新可见性失败: {ApiError.Describe(ex)}");
        }
    }

    public static IReadOnlyList<Category> Flatten(IEnumerable<Category>? nodes) {
        var list = new List<Category>();
        Append(nodes, list);
        return list;
    }

    private static void Append(IEnumerable<Category>? nodes, List<Category> list) {
        if (nodes == null) return;
        foreach (var node in nodes) {
            list.Add(node);
            Append(node.Nodes, list);
        }
    }
}

public class CategoryResult {
    public bool Success { get; init; }
    public List<Category>? Categories { get; init; }
    public string? ErrorMessage { get; init; }

    public static CategoryResult Ok(List<Category> categories) => new() { Success = true, Categories = categories };
    public static CategoryResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public class CategoryMutationResult {
    public bool Success { get; init; }
    public Category? Category { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }

    public static CategoryMutationResult Ok(Category? category, string? message = null) => new() {
        Success = true,
        Category = category,
        Message = message
    };

    public static CategoryMutationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
