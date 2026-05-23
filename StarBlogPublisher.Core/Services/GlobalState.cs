using System;
using StarBlogPublisher.Services.Security;

namespace StarBlogPublisher.Services;

public class GlobalState {
    private static GlobalState? _instance;
    
    public static GlobalState Instance {
        get {
            _instance ??= new GlobalState();
            return _instance;
        }
    }
    
    // 登录状态
    private bool _isLoggedIn;
    public bool IsLoggedIn {
        get => _isLoggedIn;
        private set {
            if (_isLoggedIn != value) {
                _isLoggedIn = value;
                // 触发状态变更事件
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
    
    // 加密后的JWT令牌
    private string _encryptedJwtToken = string.Empty;
    public string EncryptedJwtToken {
        get => _encryptedJwtToken;
        private set => _encryptedJwtToken = value;
    }
    
    // JWT令牌属性（读取时解密）
    public string JwtToken {
        get => EncryptionService.Decrypt(_encryptedJwtToken);
    }
    
    // 状态变更事件
    public event EventHandler? StateChanged;
    
    private GlobalState() {
        // 私有构造函数，防止外部实例化
    }
    
    // 设置登录状态和JWT令牌
    public void SetLoggedIn(string jwtToken) {
        if (string.IsNullOrEmpty(jwtToken)) {
            throw new ArgumentException("JWT令牌不能为空", nameof(jwtToken));
        }
        
        // 加密并存储JWT令牌
        EncryptedJwtToken = EncryptionService.Encrypt(jwtToken);
        IsLoggedIn = true;
    }
    
    // 登出
    public void Logout() {
        EncryptedJwtToken = string.Empty;
        IsLoggedIn = false;
    }
    
    // 检查是否配置了用户名和密码
    public bool HasCredentials() {
        var settings = AppSettings.Instance;
        return !string.IsNullOrEmpty(settings.Username) && 
               !string.IsNullOrEmpty(settings.Password);
    }
}