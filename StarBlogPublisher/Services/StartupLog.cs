using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace StarBlogPublisher.Services;

/// <summary>
/// Append-only startup phase timing for release profiling. Failures are ignored.
/// </summary>
internal static class StartupLog {
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    private static readonly int ProcessId = Environment.ProcessId;
    private static readonly bool IsColdStart = DetectColdStart();
    private static readonly string LogFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StarBlogPublisher",
        "startup.log");
    private static readonly object Gate = new();
    private static int _webviewNavigationLogged;

    public static void Mark(string phase) {
        try {
            var elapsedMs = Stopwatch.ElapsedMilliseconds;
            var line = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:yyyy-MM-ddTHH:mm:ss.fff}Z  pid={1}  cold={2}   phase={3,-32} ms={4}",
                DateTime.UtcNow,
                ProcessId,
                IsColdStart ? "true" : "false",
                phase,
                elapsedMs);

            lock (Gate) {
                var directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(directory)) {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(LogFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch {
            // Profiling must never affect startup.
        }
    }

    public static void MarkFirstWebViewNavigationCompleted() {
        if (Interlocked.Exchange(ref _webviewNavigationLogged, 1) != 0) {
            return;
        }

        Mark("webview_navigation_completed");
    }

    private static bool DetectColdStart() {
        try {
            var assemblyName = Assembly.GetEntryAssembly()?.GetName().Name ?? "StarBlogPublisher";
            var dotnetTemp = Path.Combine(Path.GetTempPath(), ".net");
            var appExtractRoot = Path.Combine(dotnetTemp, assemblyName);
            if (!Directory.Exists(appExtractRoot)) {
                return true;
            }

            var processStartedUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var freshCutoff = processStartedUtc.AddSeconds(-2);
            var hashDirectories = Directory.EnumerateDirectories(appExtractRoot).ToArray();
            if (hashDirectories.Length == 0) {
                return true;
            }

            // Host may recreate the app root while reusing an older directory timestamp; use hash buckets.
            return hashDirectories.Any(path => Directory.GetCreationTimeUtc(path) >= freshCutoff);
        }
        catch {
            return true;
        }
    }
}
