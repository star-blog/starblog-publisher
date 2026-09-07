#!/usr/bin/env -S dotnet --
#:property PublishAot=false

using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;

const string projectName = "StarBlogPublisher";
// File-based apps are built into a temporary directory, so AppContext.BaseDirectory
// is not the repository. Run this script from the repository root.
var repositoryDirectory = Directory.GetCurrentDirectory();
var projectDirectory = Path.Combine(repositoryDirectory, projectName);
var dryRun = args.Contains("--dry-run", StringComparer.Ordinal);

var version = GetLatestVersion(repositoryDirectory);
var buildConfigurations = new Dictionary<string, BuildConfiguration>
{
    ["self-contained"] = new(
        ["--self-contained", "true", "-p:PublishSingleFile=true"],
        ["win-x64", "linux-x64", "osx-x64"]),
    ["aot"] = new(
        ["-p:PublishAot=true", "-p:TrimMode=full", "-p:InvariantGlobalization=true",
         "-p:IlcGenerateStackTraceData=false", "-p:IlcOptimizationPreference=Size",
         "-p:IlcFoldIdenticalMethodBodies=true", "-p:JsonSerializerIsReflectionEnabledByDefault=true"],
        GetAotPlatforms())
};

var activeProfiles = new[] { "aot" };

if (Environment.GetEnvironmentVariable("GITHUB_PLATFORM") is { Length: > 0 } githubPlatform)
    buildConfigurations["self-contained"].Platforms = [githubPlatform];

var targetFramework = GetTargetFramework(projectDirectory);
var builds = activeProfiles.SelectMany(profile => buildConfigurations[profile].Platforms.Select(platform => (profile, platform))).ToArray();

if (dryRun)
{
    Console.WriteLine("Dry run; no files will be published or deleted.");
    foreach (var (profile, platform) in builds)
        Console.WriteLine($"dotnet publish -c Release -r {platform} {string.Join(' ', buildConfigurations[profile].Arguments)}");
    return;
}

var distDirectory = Path.Combine(repositoryDirectory, "dist");
if (Directory.Exists(distDirectory))
    Directory.Delete(distDirectory, recursive: true);
Directory.CreateDirectory(distDirectory);

var successCount = 0;
foreach (var (profile, platform) in builds)
{
    if (BuildAndPackage(profile, platform))
        successCount++;
}

Console.WriteLine($"\nBuild complete: {successCount}/{builds.Length}");
Environment.ExitCode = successCount == builds.Length ? 0 : 1;

bool BuildAndPackage(string profile, string platform)
{
    Console.WriteLine($"\nBuilding {profile} - {platform}...");

    try
    {
        var configuration = buildConfigurations[profile];
        if (!configuration.Platforms.Contains(platform))
            throw new InvalidOperationException($"Platform '{platform}' is not supported by '{profile}'.");

        var publishDirectory = Path.Combine(projectDirectory, "bin", "Release", targetFramework, platform, "publish");
        Console.WriteLine($"Publish directory: {publishDirectory}");

        RunProcess("dotnet", ["publish", "-c", "Release", "-r", platform, .. configuration.Arguments], projectDirectory);
        DeleteSymbolFiles(publishDirectory);

        var zipFileName = profile == "aot" && platform == "win-x64"
            ? $"{projectName}-windows-{version}.zip"
            : $"{projectName}_{version}-{platform}-{profile}.zip";
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

static string GetLatestVersion(string workingDirectory)
{
    var result = RunProcess("git", ["tag", "--sort=-v:refname"], workingDirectory, captureOutput: true);
    var latestTag = result.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
        ?? throw new InvalidOperationException("No git tags found; cannot determine package version.");
    var version = latestTag.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? latestTag[1..] : latestTag;
    Console.WriteLine($"Version from git tag: {latestTag} -> {version}");
    return version;
}

static string[] GetAotPlatforms()
{
    if (OperatingSystem.IsWindows()) return ["win-x64"];
    if (OperatingSystem.IsLinux()) return ["linux-x64"];
    if (OperatingSystem.IsMacOS()) return ["osx-x64"];
    throw new PlatformNotSupportedException($"Unsupported host OS: {Environment.OSVersion.Platform}");
}

static string GetTargetFramework(string projectDirectory)
{
    var projectPath = Path.Combine(projectDirectory, "StarBlogPublisher.csproj");
    var content = File.ReadAllText(projectPath);
    var match = Regex.Match(content, @"<TargetFramework>([^<]+)</TargetFramework>");
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

sealed class BuildConfiguration(string[] arguments, string[] platforms)
{
    public string[] Arguments { get; } = arguments;
    public string[] Platforms { get; set; } = platforms;
}

sealed record ProcessResult(string StandardOutput, string StandardError);
