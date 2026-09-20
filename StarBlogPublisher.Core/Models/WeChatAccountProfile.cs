using System;
using System.ComponentModel;
using System.Text.Json.Serialization;
using StarBlogPublisher.Services.Security;

namespace StarBlogPublisher.Models;

/// <summary>Credentials and publishing defaults for one WeChat Official Account.</summary>
public sealed class WeChatAccountProfile : INotifyPropertyChanged {
    private string _encryptedAppSecret = string.Empty;
    private string _encryptedApiAuthorization = string.Empty;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    public string Name {
        get => _name;
        set {
            if (_name == value) return;
            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public string AppId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.weixin.qq.com/";
    public string Author { get; set; } = string.Empty;

    [JsonIgnore]
    public string AppSecret {
        get => EncryptionService.Decrypt(_encryptedAppSecret);
        set => _encryptedAppSecret = EncryptionService.Encrypt(value);
    }

    public string EncryptedAppSecret {
        get => _encryptedAppSecret;
        set => _encryptedAppSecret = value;
    }

    [JsonIgnore]
    public string ApiAuthorization {
        get => EncryptionService.Decrypt(_encryptedApiAuthorization);
        set => _encryptedApiAuthorization = EncryptionService.Encrypt(value);
    }

    public string EncryptedApiAuthorization {
        get => _encryptedApiAuthorization;
        set => _encryptedApiAuthorization = value;
    }

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "未命名公众号" : Name;

    public WeChatAccountProfile Clone() => new() {
        Id = Id,
        Name = Name,
        AppId = AppId,
        ApiBaseUrl = ApiBaseUrl,
        Author = Author,
        EncryptedAppSecret = EncryptedAppSecret,
        EncryptedApiAuthorization = EncryptedApiAuthorization
    };
}
