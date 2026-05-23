using System;
using System.Collections.Generic;
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
}

public class CategoryResult {
    public bool Success { get; init; }
    public List<Category>? Categories { get; init; }
    public string? ErrorMessage { get; init; }

    public static CategoryResult Ok(List<Category> categories) => new() { Success = true, Categories = categories };
    public static CategoryResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
