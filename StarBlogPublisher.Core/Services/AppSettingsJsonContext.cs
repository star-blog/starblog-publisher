using System.Collections.Generic;
using System.Text.Json.Serialization;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, WriteIndented = true)]
[JsonSerializable(typeof(AppSettingsSnapshot))]
[JsonSerializable(typeof(LegacyAppSettingsSnapshot))]
[JsonSerializable(typeof(AIProfile))]
[JsonSerializable(typeof(List<AIProfile>))]
internal partial class AppSettingsJsonContext : JsonSerializerContext;