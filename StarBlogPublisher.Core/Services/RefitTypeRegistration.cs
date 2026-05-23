using Newtonsoft.Json;
using StarBlogPublisher.Models;
using System.Collections.Generic;
using CodeLab.Share.ViewModels.Response;

namespace StarBlogPublisher.Services;

/// <summary>
/// 为AOT编译预注册Refit使用的类型
/// </summary>
public static class RefitTypeRegistration {
    /// <summary>
    /// 在应用启动时调用此方法，确保所有类型都被预注册
    /// </summary>
    public static void RegisterTypes() {
        // 注册常用的响应类型
        JsonConvert.DefaultSettings = () => new JsonSerializerSettings {
            TypeNameHandling = TypeNameHandling.Auto,
            // 添加自定义转换器如果需要
            Converters = new List<JsonConverter> {
                // 可以添加自定义转换器
            }
        };

        // 预热类型 - 确保这些类型在AOT编译时被包含
        var types = new[] {
            typeof(ApiResponse<>),
            typeof(ApiResponse<List<Category>>),
            typeof(ApiResponse<List<WordCloud>>),
            // 添加其他API响应类型
            typeof(List<Category>),
            typeof(Category),
            typeof(WordCloud),
            // 添加所有模型类型
        };

        // 触发类型加载
        foreach (var type in types) {
            var _ = type.FullName;
        }
    }
}