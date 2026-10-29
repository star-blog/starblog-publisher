namespace StarBlogPublisher.Services.Application;

public sealed record CoverPromptRequest(string Title, string Summary, string Content, string? StyleKey, string AspectRatio, string Reservation);
