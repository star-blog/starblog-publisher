#!/usr/bin/env -S dotnet --
#:property PublishAot=false

using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

// This is a .NET 10 file-based application, not a third-party dotnet-script.
// Run it from the repository root with `dotnet run --file .\build.cs -- [options]`.
// PublishAot is disabled for this utility itself; the aot profile below enables it
// only for StarBlogPublisher, avoiding an unnecessary Native AOT toolchain dependency.
const string ProjectName = "StarBlogPublisher";
var repositoryDirectory = Directory.GetCurrentDirectory();
var projectDirectory = Path.Combine(repositoryDirectory, ProjectName);

try
{
    var options = ParseArguments(args);
    if (options.ShowHelp)
    {
        PrintUsage();
        return;
    }

    var configurations = CreateBuildConfigurations();
    if (options.ListProfiles)
    {
        PrintProfiles(configurations);
        return;
    }

    var version = GetLatestVersion(repositoryDirectory);
    var targetFramework = GetTargetFramework(projectDirectory);
    var builds = ResolveBuilds(options, configurations);

    if (options.DryRun)
    {
        Console.WriteLine("Dry run; no files will be published or deleted.");
        foreach (var build in builds)
            Console.WriteLine(FormatPublishCommand(build));
        return;
    }

    // A normal run intentionally starts with an empty dist directory, so its output
    // always contains only the packages selected by this invocation.
    var distDirectory = Path.Combine(repositoryDirectory, "dist");
    if (Directory.Exists(distDirectory))
        Directory.Delete(distDirectory, recursive: true);
    Directory.CreateDirectory(distDirectory);

    var successCount = 0;
    foreach (var build in builds)
    {
        if (BuildAndPackage(build, targetFramework, projectDirectory, distDirectory, version))
            successCount++;
    }

    Console.WriteLine($"\nBuild complete: {successCount}/{builds.Count}");
    Environment.ExitCode = successCount == builds.Count ? 0 : 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    Console.Error.WriteLine("Run `dotnet run --file .\\build.cs -- --help` for usage.");
    Environment.ExitCode = 2;
}

static Dictionary<string, BuildConfiguration> CreateBuildConfigurations() => new(StringComparer.OrdinalIgnoreCase)
{
    // Single-file publishing requires a RID. IncludeNativeLibrariesForSelfExtract
    // embeds Avalonia/SQLite native DLLs too, producing one distributable EXE. These
    // packages still require the target machine's installed .NET runtime.
    ["framework-dependent"] = new(
        "Framework-dependent single-file",
        ["--self-contained", "false", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true"],
        DefaultRids: ["win-x64", "linux-x64", "osx-x64"],
        AotSupportedRids: null),
    ["self-contained"] = new(
        "Self-contained single-file",
        // Native libraries are embedded and extracted at startup, rather than being
        // shipped alongside the executable as separate DLLs.
        ["--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true"],
        DefaultRids: ["win-x64", "linux-x64", "osx-x64"],
        AotSupportedRids: null),
    // Native AOT compilation needs the native linker for the host OS, therefore the
    // default and accepted RIDs are deliberately limited to the current host family.
    ["aot"] = new(
        "Native AOT",
        ["--self-contained", "true", "-p:PublishAot=true", "-p:TrimMode=full", "-p:InvariantGlobalization=true",
         "-p:IlcGenerateStackTraceData=false", "-p:IlcOptimizationPreference=Size",
         "-p:IlcFoldIdenticalMethodBodies=true", "-p:JsonSerializerIsReflectionEnabledByDefault=true"],
        DefaultRids: GetHostAotRids(),
        AotSupportedRids: GetHostAotRids())
};

static BuildOptions ParseArguments(string[] arguments)
{
    var profiles = new List<string>();
    var rids = new List<string>();
    var options = new BuildOptions(profiles, rids);

    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        switch (argument)
        {
            case "--help" or "-h": options.ShowHelp = true; break;
            case "--dry-run": options.DryRun = true; break;
            case "--compress": options.Compress = true; break;
            case "--list": options.ListProfiles = true; break;
            case "--profile" or "-p": AddValues(profiles, ReadOptionValue(arguments, ref index, argument)); break;
            case "--rid" or "-r": AddValues(rids, ReadOptionValue(arguments, ref index, argument)); break;
            default: throw new ArgumentException($"Unknown option: {argument}");
        }
    }

    return options;
}

static string ReadOptionValue(string[] arguments, ref int index, string option) =>
    ++index < arguments.Length && !arguments[index].StartsWith('-')
        ? arguments[index]
        : throw new ArgumentException($"{option} requires a value.");

// Comma-separated values and repeated flags are both supported, e.g.
// --profile self-contained,aot --profile framework-dependent.
static void AddValues(List<string> destination, string value) =>
    destination.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

static List<BuildTarget> ResolveBuilds(BuildOptions options, IReadOnlyDictionary<string, BuildConfiguration> configurations)
{
    var requestedProfiles = options.Profiles.Count == 0 ? ["aot"] : options.Profiles;
    var profiles = requestedProfiles.Contains("all", StringComparer.OrdinalIgnoreCase)
        ? configurations.Keys.ToList()
        : requestedProfiles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    var builds = new List<BuildTarget>();

    foreach (var profile in profiles)
    {
        if (!configurations.TryGetValue(profile, out var configuration))
            throw new ArgumentException($"Unknown profile: {profile}");

        // --rid overrides each profile's defaults. The legacy GITHUB_PLATFORM value
        // remains supported for CI self-contained builds when no CLI RID is supplied.
        var rids = options.Rids.Count > 0
            ? options.Rids
            : profile.Equals("self-contained", StringComparison.OrdinalIgnoreCase)
              && Environment.GetEnvironmentVariable("GITHUB_PLATFORM") is { Length: > 0 } githubPlatform
                ? [githubPlatform]
                : configuration.DefaultRids;

        if (configuration.AotSupportedRids is not null && rids.Any(rid => !configuration.AotSupportedRids.Contains(rid, StringComparer.OrdinalIgnoreCase)))
            throw new ArgumentException($"AOT can only target this host's RID(s): {string.Join(", ", configuration.AotSupportedRids)}.");

        if (rids.Count == 0)
            throw new ArgumentException($"The {profile} profile requires at least one RID.");

        // The .NET SDK only supports EnableCompressionInSingleFile for self-contained
        // apps. AOT is native code; framework-dependent single-file apps are bundled
        // but cannot use this SDK compression option (NETSDK1176).
        var compress = options.Compress && profile.Equals("self-contained", StringComparison.OrdinalIgnoreCase);
        if (options.Compress && !compress)
            Console.WriteLine($"Note: --compress is only supported by self-contained; skipping {profile}.");
        builds.AddRange(rids.Distinct(StringComparer.OrdinalIgnoreCase).Select(rid => new BuildTarget(profile, configuration, rid, compress)));
    }

    return builds;
}

static bool BuildAndPackage(BuildTarget build, string targetFramework, string projectDirectory, string distDirectory, string version)
{
    var targetName = build.Rid;
    Console.WriteLine($"\nBuilding {build.Profile} - {targetName}...");

    try
    {
        var publishDirectory = Path.Combine(projectDirectory, "bin", "Release", targetFramework, build.Rid!, "publish");
        Console.WriteLine($"Publish directory: {publishDirectory}");

        RunProcess("dotnet", CreatePublishArguments(build), projectDirectory);
        DeleteSymbolFiles(publishDirectory);

        var zipFileName = GetPackageFileName(build, version);
        CreateZip(publishDirectory, Path.Combine(distDirectory, zipFileName));
        Console.WriteLine($"Packaged: {zipFileName}");
        return true;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Build failed: {exception.Message}");
        return false;
    }
}

static string FormatPublishCommand(BuildTarget build) => $"dotnet {string.Join(' ', CreatePublishArguments(build))}";

static string[] CreatePublishArguments(BuildTarget build) =>
    build.Compress
        ? ["publish", "-c", "Release", "-r", build.Rid, .. build.Configuration.Arguments, "-p:EnableCompressionInSingleFile=true"]
        : ["publish", "-c", "Release", "-r", build.Rid, .. build.Configuration.Arguments];

static string GetPackageFileName(BuildTarget build, string version)
{
    // Preserve the existing Windows AOT asset name used by the release workflow.
    if (build.Profile.Equals("aot", StringComparison.OrdinalIgnoreCase) && build.Rid == "win-x64")
        return $"{ProjectName}-windows-{version}.zip";

    var targetName = build.Rid!;
    return $"{ProjectName}_{version}-{targetName}-{build.Profile}.zip";
}

static string GetLatestVersion(string workingDirectory)
{
    var result = RunProcess("git", ["tag", "--sort=-v:refname"], workingDirectory, captureOutput: true);
    var latestTag = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
        ?? throw new InvalidOperationException("No git tags found; cannot determine package version.");
    var version = latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? latestTag[1..] : latestTag;
    Console.WriteLine($"Version from git tag: {latestTag} -> {version}");
    return version;
}

static string[] GetHostAotRids()
{
    if (OperatingSystem.IsWindows()) return ["win-x64"];
    if (OperatingSystem.IsLinux()) return ["linux-x64"];
    if (OperatingSystem.IsMacOS()) return ["osx-x64"];
    throw new PlatformNotSupportedException($"Unsupported host OS: {Environment.OSVersion.Platform}");
}

static string GetTargetFramework(string projectDirectory)
{
    var projectPath = Path.Combine(projectDirectory, "StarBlogPublisher.csproj");
    var match = Regex.Match(File.ReadAllText(projectPath), @"<TargetFramework>([^<]+)</TargetFramework>");
    if (!match.Success)
        throw new InvalidOperationException($"Could not read TargetFramework from {projectPath}.");

    var framework = match.Groups[1].Value.Trim();
    Console.WriteLine($"Target framework: {framework}");
    return framework;
}

static void DeleteSymbolFiles(string publishDirectory)
{
    if (!Directory.Exists(publishDirectory))
        throw new DirectoryNotFoundException($"Publish directory not found: {publishDirectory}");

    var symbolExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdb", ".dbg", ".dSYM", ".dwarf", ".sym" };
    foreach (var file in Directory.EnumerateFiles(publishDirectory, "*", SearchOption.AllDirectories))
    {
        if (!symbolExtensions.Contains(Path.GetExtension(file))) continue;
        Console.WriteLine($"Removing symbol file: {file}");
        File.Delete(file);
    }
}

static void CreateZip(string sourceDirectory, string destinationPath)
{
    if (!Directory.Exists(sourceDirectory))
        throw new DirectoryNotFoundException($"Publish directory not found: {sourceDirectory}");
    if (!Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories).Any())
        throw new InvalidOperationException($"Publish directory is empty; refusing to package: {sourceDirectory}");

    ZipFile.CreateFromDirectory(sourceDirectory, destinationPath, CompressionLevel.SmallestSize, includeBaseDirectory: false);
}

static ProcessResult RunProcess(string fileName, IEnumerable<string> arguments, string workingDirectory, bool captureOutput = false)
{
    var startInfo = new ProcessStartInfo(fileName)
    {
        WorkingDirectory = workingDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = captureOutput,
        RedirectStandardError = captureOutput
    };
    foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Could not start {fileName}.");
    var standardOutput = captureOutput ? process.StandardOutput.ReadToEnd() : string.Empty;
    var standardError = captureOutput ? process.StandardError.ReadToEnd() : string.Empty;
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}: {standardError.Trim()}");

    return new ProcessResult(standardOutput, standardError);
}

static void PrintProfiles(IReadOnlyDictionary<string, BuildConfiguration> configurations)
{
    foreach (var (name, configuration) in configurations)
    {
        var rids = string.Join(", ", configuration.DefaultRids);
        Console.WriteLine($"{name,-20} {configuration.DisplayName} (default: {rids})");
    }
}

static void PrintUsage() => Console.WriteLine("""
Usage: dotnet run --file .\build.cs -- [options]

Profiles:
  aot                  Native AOT package for the current host RID (default).
  framework-dependent  Single-file package that uses the target's installed .NET runtime.
  self-contained       Single-file packages for win-x64, linux-x64, and osx-x64.
  all                  Build every profile above.

Options:
  -p, --profile <name> Select a profile. Repeat or separate names with commas.
  -r, --rid <rid>      Target RID(s); overrides the profile default RID(s).
      --dry-run        Print publish commands without deleting or creating files.
      --compress       Compress embedded managed assemblies in self-contained packages.
      --list           List profiles and their default target RIDs.
  -h, --help           Show this help.

Examples:
  dotnet run --file .\build.cs --
  dotnet run --file .\build.cs -- --profile framework-dependent
  dotnet run --file .\build.cs -- -p self-contained -r win-x64,linux-x64
  dotnet run --file .\build.cs -- -p framework-dependent,self-contained --compress
  dotnet run --file .\build.cs -- --profile all
""");

sealed class BuildOptions(List<string> profiles, List<string> rids)
{
    public List<string> Profiles { get; } = profiles;
    public List<string> Rids { get; } = rids;
    public bool DryRun { get; set; }
    public bool Compress { get; set; }
    public bool ListProfiles { get; set; }
    public bool ShowHelp { get; set; }
}

sealed record BuildConfiguration(string DisplayName, string[] Arguments, IReadOnlyList<string> DefaultRids, IReadOnlyList<string>? AotSupportedRids);
sealed record BuildTarget(string Profile, BuildConfiguration Configuration, string Rid, bool Compress);
sealed record ProcessResult(string StandardOutput, string StandardError);
