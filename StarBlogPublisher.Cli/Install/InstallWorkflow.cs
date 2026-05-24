using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StarBlogPublisher.Cli.Install;

public enum InstallFeature {
    Skills,
    Mcp,
}

public sealed record InstallRequest(
    string? AgentId,
    string SkillName,
    string McpServerName,
    string ExecutableCommand,
    string[] ExecutableArgs);

public sealed record InstallResult(bool Success, string Message, string? Location = null);

public static class InstallWorkflow {
    public static int Run(InstallFeature feature, InstallRequest request) {
        try {
            var installer = ResolveInstaller(feature, request.AgentId);
            var result = feature == InstallFeature.Skills
                ? installer.InstallSkills(request)
                : installer.InstallMcp(request);

            if (!result.Success) {
                Console.Error.WriteLine(result.Message);
                return 1;
            }

            Console.WriteLine(result.Message);
            if (!string.IsNullOrWhiteSpace(result.Location)) {
                Console.WriteLine($"位置: {result.Location}");
            }

            return 0;
        }
        catch (Exception ex) {
            Console.Error.WriteLine($"安装失败: {ex.Message}");
            return 1;
        }
    }

    private static AgentInstaller ResolveInstaller(InstallFeature feature, string? agentId) {
        var candidates = AgentRegistry.All
            .Where(agent => agent.Supports(feature))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(agentId)) {
            var matched = candidates.FirstOrDefault(agent => string.Equals(agent.Id, agentId, StringComparison.OrdinalIgnoreCase));
            if (matched is null) {
                var supported = string.Join(", ", candidates.Select(agent => agent.Id));
                throw new InvalidOperationException($"不支持的 Agent: {agentId}。当前可选: {supported}");
            }

            return matched;
        }

        if (Console.IsInputRedirected) {
            var supported = string.Join(", ", candidates.Select(agent => agent.Id));
            throw new InvalidOperationException($"当前为非交互模式，请通过 --agent 指定目标 Agent。可选: {supported}");
        }

        return PromptForAgent(feature, candidates);
    }

    private static AgentInstaller PromptForAgent(InstallFeature feature, IReadOnlyList<AgentInstaller> candidates) {
        Console.WriteLine(feature == InstallFeature.Skills ? "请选择要安装 Skill 的目标 Agent:" : "请选择要安装 MCP 的目标 Agent:");

        for (var index = 0; index < candidates.Count; index++) {
            var installer = candidates[index];
            Console.WriteLine($"  {index + 1}. {installer.DisplayName} ({installer.Id})");
        }

        while (true) {
            Console.Write("输入编号: ");
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out var selectedIndex) && selectedIndex >= 1 && selectedIndex <= candidates.Count) {
                return candidates[selectedIndex - 1];
            }

            Console.WriteLine("无效输入，请重新输入编号。");
        }
    }
}

public static class StarBlogInstallConstants {
    public const string DefaultSkillName = "starblog-publisher";
    public const string DefaultMcpServerName = "starblog";
    public const string DefaultExecutableCommand = "starblog";
}

public sealed class AgentInstaller {
    private readonly Func<InstallRequest, InstallResult>? installSkills;
    private readonly Func<InstallRequest, InstallResult>? installMcp;

    public AgentInstaller(
        string id,
        string displayName,
        Func<InstallRequest, InstallResult>? installSkills,
        Func<InstallRequest, InstallResult>? installMcp) {
        Id = id;
        DisplayName = displayName;
        this.installSkills = installSkills;
        this.installMcp = installMcp;
    }

    public string Id { get; }
    public string DisplayName { get; }

    public bool Supports(InstallFeature feature) {
        return feature switch {
            InstallFeature.Skills => installSkills is not null,
            InstallFeature.Mcp => installMcp is not null,
            _ => false,
        };
    }

    public InstallResult InstallSkills(InstallRequest request) {
        if (installSkills is null) {
            return new InstallResult(false, $"{DisplayName} 暂不支持 skills 安装");
        }

        return installSkills(request);
    }

    public InstallResult InstallMcp(InstallRequest request) {
        if (installMcp is null) {
            return new InstallResult(false, $"{DisplayName} 暂不支持 MCP 安装");
        }

        return installMcp(request);
    }
}

public static class AgentRegistry {
    public static readonly IReadOnlyList<AgentInstaller> All = [
        new AgentInstaller(
            "claude-code",
            "Claude Code",
            request => SkillInstaller.InstallToDirectory(request.SkillName, Path.Combine(HomeDirectory.Path, ".claude", "skills", request.SkillName)),
            request => ClaudeCodeMcpInstaller.Install(request)),
        new AgentInstaller(
            "codex",
            "Codex",
            request => SkillInstaller.InstallToDirectory(request.SkillName, Path.Combine(HomeDirectory.Path, ".agents", "skills", request.SkillName)),
            request => CodexMcpInstaller.Install(request)),
        new AgentInstaller(
            "openclaw",
            "OpenClaw",
            request => SkillInstaller.InstallToDirectory(request.SkillName, Path.Combine(HomeDirectory.Path, ".openclaw", "skills", request.SkillName)),
            null),
    ];
}

internal static class HomeDirectory {
    public static string Path => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}

internal static class SkillInstaller {
    public static InstallResult InstallToDirectory(string skillName, string skillDirectory) {
        Directory.CreateDirectory(skillDirectory);

        var targetPath = System.IO.Path.Combine(skillDirectory, "SKILL.md");
        File.WriteAllText(targetPath, SkillTemplateBuilder.Build(skillName), new UTF8Encoding(false));

        return new InstallResult(true, $"已安装 Skill: {skillName}", targetPath);
    }
}

public static class SkillTemplateBuilder {
    public static string Build(string skillName) {
        var body = EmbeddedSkillBodyLoader.Load();
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"name: {skillName}");
        builder.AppendLine("description: 使用 StarBlog Publisher 的 CLI 与 MCP 能力完成博客发布、分类管理和 AI 辅助创作。适用于发布 Markdown、生成标题/摘要/Slug、查询或创建分类等任务。");
        builder.AppendLine("---");
        builder.AppendLine();
        builder.Append(body.Trim());
        builder.AppendLine();
        return builder.ToString();
    }
}

internal static class EmbeddedSkillBodyLoader {
    private const string ResourceName = "StarBlogPublisher.Cli.Resources.StarBlogSkillBody.md";

    public static string Load() {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"找不到内置 Skill 资源: {ResourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

internal static class ClaudeCodeMcpInstaller {
    public static InstallResult Install(InstallRequest request) {
        var configPath = System.IO.Path.Combine(HomeDirectory.Path, ".claude.json");
        var root = LoadJsonObject(configPath);
        var servers = root["mcpServers"] as JsonObject ?? new JsonObject();

        servers[request.McpServerName] = new JsonObject {
            ["type"] = "stdio",
            ["command"] = request.ExecutableCommand,
            ["args"] = BuildJsonArray(request.ExecutableArgs),
            ["env"] = new JsonObject(),
        };

        root["mcpServers"] = servers;
        SaveJsonObject(configPath, root);

        return new InstallResult(true, $"已为 Claude Code 安装 MCP Server: {request.McpServerName}", configPath);
    }

    private static JsonObject LoadJsonObject(string path) {
        if (!File.Exists(path)) {
            return new JsonObject();
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) {
            return new JsonObject();
        }

        return JsonNode.Parse(text)?.AsObject() ?? new JsonObject();
    }

    private static void SaveJsonObject(string path, JsonObject root) {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
    }

    private static JsonArray BuildJsonArray(IEnumerable<string> values) {
        var array = new JsonArray();
        foreach (var value in values) {
            array.Add(value);
        }

        return array;
    }
}

internal static class CodexMcpInstaller {
    public static InstallResult Install(InstallRequest request) {
        var configPath = System.IO.Path.Combine(HomeDirectory.Path, ".codex", "config.toml");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(configPath)!);

        var block = BuildBlock(request);
        var existing = File.Exists(configPath) ? File.ReadAllText(configPath) : string.Empty;
        var updated = TomlBlockUpdater.UpsertMcpServer(existing, request.McpServerName, block);

        File.WriteAllText(configPath, updated, new UTF8Encoding(false));
        return new InstallResult(true, $"已为 Codex 安装 MCP Server: {request.McpServerName}", configPath);
    }

    private static string BuildBlock(InstallRequest request) {
        var args = string.Join(", ", request.ExecutableArgs.Select(TomlEscaping.Quote));
        var builder = new StringBuilder();
        builder.AppendLine($"[mcp_servers.{request.McpServerName}]");
        builder.AppendLine($"command = {TomlEscaping.Quote(request.ExecutableCommand)}");
        builder.AppendLine($"args = [{args}]");
        return builder.ToString();
    }
}

internal static class TomlBlockUpdater {
    public static string UpsertMcpServer(string content, string serverName, string block) {
        if (string.IsNullOrWhiteSpace(content)) {
            return block;
        }

        var lines = content.Replace("\r\n", "\n").Split('\n');
        var header = $"[mcp_servers.{serverName}]";
        var prefix = $"[mcp_servers.{serverName}.";
        var start = -1;

        for (var index = 0; index < lines.Length; index++) {
            if (string.Equals(lines[index].Trim(), header, StringComparison.Ordinal)) {
                start = index;
                break;
            }
        }

        if (start < 0) {
            var trimmed = content.TrimEnd();
            return trimmed + Environment.NewLine + Environment.NewLine + block;
        }

        var end = lines.Length;
        for (var index = start + 1; index < lines.Length; index++) {
            var trimmedLine = lines[index].Trim();
            if (!trimmedLine.StartsWith("[", StringComparison.Ordinal)) {
                continue;
            }

            if (trimmedLine.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            end = index;
            break;
        }

        var before = lines.Take(start);
        var after = lines.Skip(end);
        var rebuilt = string.Join(
            Environment.NewLine,
            before
                .Concat(block.Replace("\r\n", "\n").TrimEnd().Split('\n'))
                .Concat(after));

        return rebuilt.TrimEnd() + Environment.NewLine;
    }
}

internal static class TomlEscaping {
    public static string Quote(string value) {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}