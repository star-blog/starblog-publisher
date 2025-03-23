using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using StarBlogPublisher.Services.Security;

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
    public string ProxyUrl { get; set; } = string.Empty;
    public int ProxyTimeout { get; set; } = 30;

    // StarBlog后端设置
    public bool UseCustomBackend { get; set; }
    public string BackendUrl { get; set; } = string.Empty;

    // AI设置
    public bool EnableAI { get; set; }
    public string AIProvider { get; set; } = "openai";
    private string _encryptedAIKey = string.Empty;

    [JsonIgnore]
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

    public string Username { get; set; } = string.Empty;

    // 用于存储加密后的密码
    private string _encryptedPassword = string.Empty;

    // 公开属性，读取时解密，设置时不做处理
    [JsonIgnore]
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

    [JsonConstructor]
    private AppSettings() { }

    private static AppSettings Load() {
        try {
            if (File.Exists(ConfigPath)) {
                var json = File.ReadAllText(ConfigPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
        }
        catch (Exception ex) {
            // 如果加载失败，返回默认设置
            Console.WriteLine($"Failed to load app settings. {ex}");
        }

        return new AppSettings();
    }

    public void Save() {
        try {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json);

            // 触发配置变更事件
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception) {
            // todo 处理保存失败的情况
        }
    }
}