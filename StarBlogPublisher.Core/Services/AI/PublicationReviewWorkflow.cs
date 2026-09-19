using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Agents.AI.Workflows.InProc;
using Microsoft.Extensions.AI;

namespace StarBlogPublisher.Services.AI;

/// <summary>
/// A deliberately small MAF pilot. It performs a read-only review and returns a draft;
/// applying changes or publishing remains outside this workflow and requires UI approval.
/// </summary>
public sealed class PublicationReviewWorkflow {
    private readonly AiService _aiService;

    public PublicationReviewWorkflow(AiService aiService) {
        _aiService = aiService;
    }

    public async Task<AiResult<string>> ReviewAsync(
        string title,
        string content,
        string? summary,
        CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var startedAt = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        var reviewer = _aiService.ChatClient.AsAIAgent(
            instructions: "You are a read-only publication reviewer. Review the supplied Markdown for title/content consistency, Markdown readability, SEO metadata, image alt text, and WeChat readability. Return concise Markdown grouped by severity. Do not claim facts that are not in the article. Do not call tools and do not suggest publishing automatically.",
            name: "publication-reviewer",
            description: "Read-only StarBlog publication reviewer");

        var workflow = AgentWorkflowBuilder.BuildSequential("publication-review", [reviewer]);
        await using var run = await InProcessExecution.OffThread.RunAsync(
            workflow,
            new List<ChatMessage> {
                new(ChatRole.User, BuildPrompt(title, content, summary))
            },
            cancellationToken: cancellationToken);

        var reviewText = string.Join(
            Environment.NewLine,
            run.OutgoingEvents
                .OfType<AgentResponseEvent>()
                .Select(item => item.Response.Text)
                .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(reviewText)) {
            throw new InvalidOperationException("发布前审校工作流没有返回可显示的结果。");
        }

        return new AiResult<string>(
            reviewText,
            new AiExecutionInfo(
                "publication-review",
                AppSettings.Instance.AIProvider,
                AppSettings.Instance.AIModel,
                startedAt,
                stopwatch.Elapsed));
    }

    private static string BuildPrompt(string title, string content, string? summary) =>
        $"""
        Title:
        {title}

        Summary:
        {summary ?? "(not supplied)"}

        Markdown:
        {content}
        """;
}
