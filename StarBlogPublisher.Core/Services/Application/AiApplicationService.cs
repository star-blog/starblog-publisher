using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StarBlogPublisher.Services.AI;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// AI 应用服务
/// 封装 AI 辅助写作相关的业务流程，供 GUI、CLI、MCP 共用
/// </summary>
public class AiApplicationService {
    private readonly AiService _aiService;
    private readonly AppSettings _settings;

    public AiApplicationService(AiService aiService, AppSettings settings) {
        _aiService = aiService;
        _settings = settings;
    }

    /// <summary>
    /// AI 功能是否可用
    /// </summary>
    public bool IsEnabled => _settings.EnableAI;

    /// <summary>
    /// Generates all publication metadata in one structured request. The result is a proposal only;
    /// callers decide which fields to apply.
    /// </summary>
    public async Task<AiResult<ArticleMetadataDraft>> GenerateMetadataDraftAsync(
        string title,
        string content,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var prompt = PromptBuilder
            .Create(PromptTemplates.ArticleMetadataDraft)
            .AddParameter("title", title)
            .AddParameter("content", content)
            .Build();
        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        try {
            var draft = await _aiService.GenerateStructuredAsync<ArticleMetadataDraft>(prompt, cancellationToken);
            var normalized = draft.Normalize(CleanSlug);
            return new AiResult<ArticleMetadataDraft>(
                normalized,
                new AiExecutionInfo(
                    "article-metadata-draft",
                    _settings.AIProvider,
                    _settings.AIModel,
                    startedAt,
                    stopwatch.Elapsed),
                normalized.Warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) {
            // Older OpenAI-compatible gateways often do not implement JSON-schema output.
            // Keep them usable during migration, while making the lower-quality path visible.
            var summary = await GenerateSummaryAsync(title, content);
            var tags = await GenerateKeywordsAsync(title, content);
            var slug = await GenerateSlugAsync(title);
            var legacyDraft = new ArticleMetadataDraft(
                [new TitleCandidate(title, "保留原始标题：当前模型不支持结构化标题候选。")],
                summary,
                tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                [slug],
                ["当前模型或网关不支持结构化输出，已使用兼容模式生成草案。"])
                .Normalize(CleanSlug);

            return new AiResult<ArticleMetadataDraft>(
                legacyDraft,
                new AiExecutionInfo(
                    "article-metadata-draft",
                    _settings.AIProvider,
                    _settings.AIModel,
                    startedAt,
                    stopwatch.Elapsed,
                    UsedFallback: true),
                legacyDraft.Warnings);
        }
    }

    /// <summary>
    /// 生成文章摘要（流式）
    /// </summary>
    public async IAsyncEnumerable<string> GenerateSummaryStreamAsync(string title, string content) {
        var prompt = PromptBuilder
            .Create(PromptTemplates.ArticleDescriptionTechnical)
            .AddParameter("title", title)
            .AddParameter("content", content)
            .Build();

        await foreach (var update in _aiService.GenerateTextStreamAsync(prompt)) {
            if (!string.IsNullOrEmpty(update.Text)) {
                yield return update.Text;
            }
        }
    }

    /// <summary>
    /// 生成文章摘要（一次性返回）
    /// </summary>
    public async Task<string> GenerateSummaryAsync(string title, string content) {
        var sb = new StringBuilder();
        await foreach (var chunk in GenerateSummaryStreamAsync(title, content)) {
            sb.Append(chunk);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 润色标题（流式）
    /// </summary>
    public async IAsyncEnumerable<string> RefineTitleStreamAsync(string title, string content, string? keywords = null, string? templateKey = null) {
        var template = PromptTemplates.TitleOptimizationTemplates.Find(t => t.Key == templateKey)
                       ?? PromptTemplates.TitleOptimizationTemplates.Find(t => t.IsDefault)
                       ?? PromptTemplates.TitleOptimizationTemplates[0];

        var prompt = PromptBuilder
            .Create(template.Template)
            .AddParameter("title", title)
            .AddParameter("keywords", keywords ?? "")
            .AddParameter("content", content)
            .Build();

        await foreach (var update in _aiService.GenerateTextStreamAsync(prompt)) {
            if (!string.IsNullOrEmpty(update.Text)) {
                yield return update.Text;
            }
        }
    }

    /// <summary>
    /// 润色标题（一次性返回）
    /// </summary>
    public async Task<string> RefineTitleAsync(string title, string content, string? keywords = null, string? templateKey = null) {
        var sb = new StringBuilder();
        await foreach (var chunk in RefineTitleStreamAsync(title, content, keywords, templateKey)) {
            sb.Append(chunk);
        }
        var result = sb.ToString().Trim('《', '》', '"', '"', '"', '\n');
        return result;
    }

    /// <summary>
    /// 生成关键词（流式）
    /// </summary>
    public async IAsyncEnumerable<string> GenerateKeywordsStreamAsync(string title, string content) {
        var prompt = PromptBuilder
            .Create(PromptTemplates.KeywordExtraction)
            .AddParameter("title", title)
            .AddParameter("content", content)
            .Build();

        await foreach (var update in _aiService.GenerateTextStreamAsync(prompt)) {
            if (!string.IsNullOrEmpty(update.Text)) {
                yield return update.Text;
            }
        }
    }

    /// <summary>
    /// 生成关键词（一次性返回）
    /// </summary>
    public async Task<string> GenerateKeywordsAsync(string title, string content) {
        var sb = new StringBuilder();
        await foreach (var chunk in GenerateKeywordsStreamAsync(title, content)) {
            sb.Append(chunk);
        }
        return ExtractKeywordsFromJson(sb.ToString());
    }

    /// <summary>
    /// 生成 URL Slug（流式）
    /// </summary>
    public async IAsyncEnumerable<string> GenerateSlugStreamAsync(string title) {
        var prompt = PromptBuilder
            .Create(PromptTemplates.UrlSlugGeneration)
            .AddParameter("title", title)
            .Build();

        await foreach (var update in _aiService.GenerateTextStreamAsync(prompt)) {
            if (!string.IsNullOrEmpty(update.Text)) {
                yield return update.Text;
            }
        }
    }

    /// <summary>
    /// 生成 URL Slug（一次性返回）
    /// </summary>
    public async Task<string> GenerateSlugAsync(string title) {
        var sb = new StringBuilder();
        await foreach (var chunk in GenerateSlugStreamAsync(title)) {
            sb.Append(chunk);
        }
        return CleanSlug(sb.ToString().Trim());
    }

    /// <summary>
    /// 生成封面图提示词（一次性返回）
    /// </summary>
    /// <param name="title">文章标题</param>
    /// <param name="summary">文章摘要</param>
    /// <param name="templateKey">封面风格模板 Key，为空则使用第一个</param>
    public async Task<string> GenerateCoverPromptAsync(string title, string summary, string? templateKey = null) {
        var template = PromptTemplates.Cover.Find(t => t.Key == templateKey)
                       ?? PromptTemplates.Cover[0];

        var prompt = PromptBuilder
            .Create(template.Prompt)
            .AddParameter("title", title)
            .AddParameter("summary", summary)
            .Build();

        return await _aiService.GenerateTextAsync(prompt);
    }

    /// <summary>
    /// 从 AI 返回的 JSON 格式中提取关键词
    /// </summary>
    internal static string ExtractKeywordsFromJson(string jsonOutput) {
        try {
            var startIndex = jsonOutput.IndexOf('[');
            var endIndex = jsonOutput.LastIndexOf(']');

            if (startIndex >= 0 && endIndex > startIndex) {
                var jsonArray = jsonOutput.Substring(startIndex, endIndex - startIndex + 1);
                var keywords = new List<string>();
                var matches = System.Text.RegularExpressions.Regex.Matches(jsonArray, @"""([^""]+)""");

                foreach (System.Text.RegularExpressions.Match match in matches) {
                    var keyword = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(keyword) && !keyword.StartsWith("//")) {
                        keywords.Add(keyword);
                    }
                }

                return string.Join(", ", keywords);
            }
        }
        catch {
            // JSON 解析失败返回空字符串
        }

        return string.Empty;
    }

    /// <summary>
    /// 清理 Slug，确保符合 URL 友好格式
    /// </summary>
    public static string CleanSlug(string slug) {
        var cleaned = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\-+", "-");
        cleaned = cleaned.Trim('-');
        if (cleaned.Length > 50) {
            cleaned = cleaned.Substring(0, 50).TrimEnd('-');
        }
        return cleaned;
    }
}
