using System;

namespace StarBlogPublisher.Cli.Models;

internal sealed record CategorySummaryDto(
    string Id,
    string? Text,
    int ChildCount);

internal sealed record PublishedPostDto(
    string Id,
    string Title,
    string? Slug,
    string Status);

internal sealed record PostDetailsDto(
    string Id,
    string Title,
    string? Slug,
    string? Category,
    string Status,
    string? Summary,
    DateTime CreationTime,
    DateTime LastUpdateTime);

internal sealed record PostListItemDto(
    string Id,
    string Title,
    string? Slug,
    string Status,
    string? Category,
    DateTime LastUpdateTime);

internal sealed record PostListPageDto(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    List<PostListItemDto> Items);