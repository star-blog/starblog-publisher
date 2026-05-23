namespace StarBlogPublisher.Models;

/// <summary>
/// 标题优化模板数据模型
/// </summary>
public class TitleOptimizationTemplate
{
    /// <summary>
    /// 模板唯一标识
    /// </summary>
    public string Key { get; set; } = string.Empty;
    
    /// <summary>
    /// 模板显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 模板描述
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// 提示词模板内容
    /// </summary>
    public string Template { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否为默认模板
    /// </summary>
    public bool IsDefault { get; set; } = false;
}