using System.Reflection;

namespace StarBlogPublisher.Utils;

/// <summary>
/// Exposes the version embedded in the GUI assembly at build time.
/// </summary>
public static class ApplicationVersion
{
    public static string Value { get; } = GetValue();

    private static string GetValue()
    {
        var informationalVersion = typeof(ApplicationVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // SourceLink or other build tooling can append build metadata after '+'.
            var metadataStart = informationalVersion.IndexOf('+');
            return metadataStart >= 0 ? informationalVersion[..metadataStart] : informationalVersion;
        }

        return typeof(ApplicationVersion).Assembly.GetName().Version?.ToString() ?? "开发版本";
    }
}
