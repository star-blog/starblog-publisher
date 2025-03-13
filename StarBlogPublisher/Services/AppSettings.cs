using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarBlogPublisher.Services;

public class AppSettings
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StarBlogPublisher",
        "settings.json"
    );

    private static AppSettings? _instance;
    public static AppSettings Instance
    {
        get
        {
            _instance ??= Load();
            return _instance;
        }
    }

    // 代理设置
    public bool UseProxy { get; set; }
    public string ProxyUrl { get; set; } = string.Empty;
    public int ProxyTimeout { get; set; } = 30;

    // StarBlog后端设置
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int BackendTimeout { get; set; } = 30;

    // 配置变更事件
    public event EventHandler? SettingsChanged;

    private AppSettings() { }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // 如果加载失败，返回默认设置
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);

            // 触发配置变更事件
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // 处理保存失败的情况
        }
    }
}