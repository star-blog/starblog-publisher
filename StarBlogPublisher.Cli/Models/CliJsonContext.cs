using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StarBlogPublisher.Cli.Models;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, WriteIndented = true)]
[JsonSerializable(typeof(CategorySummaryDto))]
[JsonSerializable(typeof(List<CategorySummaryDto>))]
[JsonSerializable(typeof(PublishedPostDto))]
[JsonSerializable(typeof(PostDetailsDto))]
internal partial class CliJsonContext : JsonSerializerContext;