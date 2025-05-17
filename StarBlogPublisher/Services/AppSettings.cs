using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Collections.Generic;
using Newtonsoft.Json;
using StarBlogPublisher.Services.Security;
using StarBlogPublisher.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace StarBlogPublisher.Services;

public class AppSettings {
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StarBlogPublisher",
        "settings.json"
    );

    private static AppSettings? _instance;

    public static AppSettings Instance {
        get {
            _instance ??= Load();
            return _instance;
        }
    }

    // 代理设置
    public bool UseProxy { get; set; }
    public string ProxyType { get; set; } = "http";
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; } = 0;
    public int ProxyTimeout { get; set; } = 30;

    // StarBlog后端设置
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;

    // AI设置
    public bool EnableAI { get; set; }
    public string AIProvider { get; set; } = "openai";
    private string _encryptedAIKey = string.Empty;

    [System.Text.Json.Serialization.JsonIgnore]
    public string AIKey {
        get => EncryptionService.Decrypt(_encryptedAIKey);
        set => _encryptedAIKey = EncryptionService.Encrypt(value);
    }

    [JsonPropertyName("AIKey")]
    public string EncryptedAIKey {
        get => _encryptedAIKey;
        set => _encryptedAIKey = value;
    }

    public string AIModel { get; set; } = string.Empty;
    public string AIApiBase { get; set; } = string.Empty;

    // AI配置文件
    public List<AIProfile> AIProfiles { get; set; } = new List<AIProfile>();
    public string CurrentAIProfile { get; set; } = "默认";

    public string Username { get; set; } = string.Empty;

    // 用于存储加密后的密码
    private string _encryptedPassword = string.Empty;

    // 公开属性，读取时解密，设置时不做处理
    [System.Text.Json.Serialization.JsonIgnore]
    public string Password {
        get => EncryptionService.Decrypt(_encryptedPassword);
        set => _encryptedPassword = EncryptionService.Encrypt(value);
    }

    // 用于JSON序列化的属性
    [JsonPropertyName("Password")]
    public string EncryptedPassword {
        get => _encryptedPassword;
        set => _encryptedPassword = value;
    }

    public int BackendTimeout { get; set; } = 30;

    // 主题设置
    public bool IsDarkTheme { get; set; } = false;

    // 配置变更事件
    public event EventHandler? SettingsChanged;

    [System.Text.Json.Serialization.JsonConstructor]
    private AppSettings() { }

    private static AppSettings Load() {
        try {
            if (File.Exists(ConfigPath)) {
                var json = File.ReadAllText(ConfigPath);
                // var settings = JsonSerializer.Deserialize<AppSettings>(json);
                var settings = JsonConvert.DeserializeObject<AppSettings>(json);

                if (settings != null) {
                    // 确保至少有一个默认配置文件
                    if (settings.AIProfiles == null || settings.AIProfiles.Count == 0) {
                        settings.MigrateToProfiles();
                    }
                    
                    return settings;
                }
            }
        }
        catch (Exception ex) {
            // 如果加载失败，返回默认设置
            Console.WriteLine($"Failed to load app settings. {ex}");
        }

        var defaultSettings = new AppSettings();
        defaultSettings.MigrateToProfiles();
        return defaultSettings;
    }

    // 将旧的AI设置迁移到配置文件
    private void MigrateToProfiles() {
        AIProfiles = new List<AIProfile>
        {
            new AIProfile
            {
                Name = "默认",
                EnableAI = this.EnableAI,
                Provider = this.AIProvider,
                Key = this.AIKey,
                Model = this.AIModel,
                ApiBase = this.AIApiBase
            }
        };
        CurrentAIProfile = "默认";
    }

    public void Save() {
        try {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }
            
            // var json = JsonSerializer.Serialize(this, new JsonSerializerOptions {
            //     WriteIndented = true
            // });
            
            var json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);

            // 触发配置变更事件
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception) {
            // todo 处理保存失败的情况
        }
    }
}