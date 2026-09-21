using System.Collections.Generic;
using System.Text.Json.Serialization;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettingsSnapshot))]
[JsonSerializable(typeof(LegacyAppSettingsSnapshot))]
[JsonSerializable(typeof(ThemeMode))]
[JsonSerializable(typeof(ThemeMode?))]
[JsonSerializable(typeof(AIProfile))]
[JsonSerializable(typeof(List<AIProfile>))]
[JsonSerializable(typeof(WeChatAccountProfile))]
[JsonSerializable(typeof(List<WeChatAccountProfile>))]
internal partial class AppSettingsJsonContext : JsonSerializerContext;
