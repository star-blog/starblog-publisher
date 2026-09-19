using System;
using System.Collections.Generic;
using System.Linq;

namespace StarBlogPublisher.Services.AI;

/// <summary>Identifies an AI operation independently from any model provider.</summary>
public enum AiCapability {
    TextGeneration,
    StructuredOutput,
    PublicationReview
}

/// <summary>Execution metadata that callers can log without retaining article content.</summary>
public sealed record AiExecutionInfo(
    string Operation,
    string Provider,
    string Model,
    DateTimeOffset StartedAt,
    TimeSpan Duration,
    bool UsedFallback = false);

/// <summary>Successful, typed output together with non-content execution metadata.</summary>
public sealed record AiResult<T>(T Data, AiExecutionInfo Execution, IReadOnlyList<string>? Warnings = null);

public sealed record TitleCandidate(string Title, string? Rationale = null);

/// <summary>
/// The schema used when proposing article metadata. The user must explicitly choose what to apply.
/// </summary>
public sealed record ArticleMetadataDraft(
    IReadOnlyList<TitleCandidate>? Titles,
    string? Summary,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<string>? SlugCandidates,
    IReadOnlyList<string>? Warnings = null) {

    public ArticleMetadataDraft Normalize(Func<string, string> cleanSlug) {
        var titles = (Titles ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Title))
            .Select(item => item with { Title = item.Title.Trim() })
            .DistinctBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();

        var tags = (Tags ?? [])
            .Select(tag => tag?.Trim() ?? string.Empty)
            .Where(tag => tag.Length is > 0 and <= 32)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();

        var slugs = (SlugCandidates ?? [])
            .Select(cleanSlug)
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToArray();

        return this with {
            Titles = titles,
            Summary = Summary?.Trim(),
            Tags = tags,
            SlugCandidates = slugs,
            Warnings = (Warnings ?? []).Where(warning => !string.IsNullOrWhiteSpace(warning)).ToArray()
        };
    }
}

public enum PublicationReviewSeverity {
    Info,
    Warning,
    Error
}

public sealed record PublicationReviewIssue(
    PublicationReviewSeverity Severity,
    string Area,
    string Message,
    string? Suggestion = null);

public sealed record PublicationReview(
    IReadOnlyList<PublicationReviewIssue>? Issues,
    string? OverallAssessment = null) {

    public PublicationReview Normalize() => this with {
        Issues = (Issues ?? [])
            .Where(issue => !string.IsNullOrWhiteSpace(issue.Area) && !string.IsNullOrWhiteSpace(issue.Message))
            .Take(20)
            .ToArray(),
        OverallAssessment = OverallAssessment?.Trim()
    };
}
