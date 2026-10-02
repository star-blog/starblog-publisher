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
            if (resp.Data == null) {
                return CategoryResult.Fail(resp.Message ?? "分类列表为空");
            }
            return CategoryResult.Ok(resp.Data);
        }
        catch (Exception ex) {
            return CategoryResult.Fail($"获取分类失败: {ex.Message}");
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
            if (resp.Data == null) {
                return CategoryResult.Fail(resp.Message ?? "创建分类失败");
            }
            return CategoryResult.Ok(new List<Category> { resp.Data });
        }
        catch (Exception ex) {
            return CategoryResult.Fail($"创建分类失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 获取扁平分类列表（含名称、父级、可见性）
    /// </summary>
    public async Task<CategoryResult> GetAllAsync() {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryResult.Fail(authCheck.ErrorMessage!);

        try {
            var resp = await _api.Categories.GetAll();
            if (resp.Data is { Count: > 0 }) return CategoryResult.Ok(resp.Data);
        }
        catch {
            // GetAll 若未被 ApiResponse 包装，回退到分类树。
        }

        return await GetCategoriesAsync();
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
            if (resp.Data == null) return CategoryMutationResult.Fail(resp.Message ?? "更新分类失败");
            return CategoryMutationResult.Ok(resp.Data);
        }
        catch (Exception ex) {
            return CategoryMutationResult.Fail($"更新分类失败: {ex.Message}");
        }
    }

    public async Task<CategoryMutationResult> DeleteCategoryAsync(int id) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryMutationResult.Fail(authCheck.ErrorMessage!);
        if (id <= 0) return CategoryMutationResult.Fail("分类 ID 无效");

        try {
            var resp = await _api.Categories.Delete(id);
            if (!resp.IsOk) return CategoryMutationResult.Fail(resp.Message ?? "删除分类失败");
            return CategoryMutationResult.Ok(null, resp.Message ?? "已删除");
        }
        catch (Exception ex) {
            return CategoryMutationResult.Fail($"删除分类失败: {ex.Message}");
        }
    }

    public async Task<CategoryMutationResult> SetVisibilityAsync(int id, bool visible) {
        var authCheck = await _authService.EnsureLoggedInAsync();
        if (!authCheck.Success) return CategoryMutationResult.Fail(authCheck.ErrorMessage!);
        if (id <= 0) return CategoryMutationResult.Fail("分类 ID 无效");

        try {
            var resp = visible
                ? await _api.Categories.SetVisible(id)
                : await _api.Categories.SetInvisible(id);
            if (!resp.IsOk) return CategoryMutationResult.Fail(resp.Message ?? "更新可见性失败");
            return CategoryMutationResult.Ok(null, resp.Message ?? (visible ? "已设为可见" : "已设为隐藏"));
        }
        catch (Exception ex) {
            return CategoryMutationResult.Fail($"更新可见性失败: {ex.Message}");
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
