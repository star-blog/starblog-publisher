using System;
using System.Threading.Tasks;
using StarBlogPublisher.Models.Dtos;

namespace StarBlogPublisher.Services.Application;

/// <summary>
/// 认证应用服务
/// 封装登录/登出/状态查询等业务流程，供 GUI、CLI、MCP 共用
/// </summary>
public class AuthApplicationService {
    private readonly AppSettings _settings;
    private readonly GlobalState _globalState;
    private readonly ApiService _api;

    public AuthApplicationService(AppSettings settings, GlobalState globalState, ApiService api) {
        _settings = settings;
        _globalState = globalState;
        _api = api;
    }

    /// <summary>
    /// 当前是否已登录
    /// </summary>
    public bool IsLoggedIn => _globalState.IsLoggedIn;

    /// <summary>
    /// 是否已配置凭据
    /// </summary>
    public bool HasCredentials => _globalState.HasCredentials();

    /// <summary>
    /// 获取当前登录状态描述
    /// </summary>
    public string GetStatusMessage() {
        if (_globalState.IsLoggedIn) return "已登录";
        if (_globalState.HasCredentials()) return "未登录 (已配置凭据)";
        return "未登录 (未配置凭据)";
    }

    /// <summary>
    /// 使用已保存的凭据登录
    /// </summary>
    public async Task<AuthResult> LoginAsync() {
        if (!_globalState.HasCredentials()) {
            return AuthResult.Fail("未配置用户名和密码，请先在设置中配置");
        }

        return await LoginAsync(_settings.Username, _settings.Password);
    }

    /// <summary>
    /// 使用指定凭据登录
    /// </summary>
    public async Task<AuthResult> LoginAsync(string username, string password) {
        try {
            var resp = await _api.Auth.Login(new LoginUser {
                Username = username,
                Password = password
            });

            if (string.IsNullOrWhiteSpace(resp.Data?.Token)) {
                return AuthResult.Fail(resp.Message ?? "登录失败，未获取到令牌");
            }

            _globalState.SetLoggedIn(resp.Data.Token);
            return AuthResult.Ok(resp.Data.Token);
        }
        catch (Exception ex) {
            return AuthResult.Fail($"登录失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 登出
    /// </summary>
    public void Logout() {
        _globalState.Logout();
    }

    /// <summary>
    /// 确保已登录：如果未登录但有凭据，自动登录
    /// </summary>
    public async Task<AuthResult> EnsureLoggedInAsync() {
        if (_globalState.IsLoggedIn) return AuthResult.Ok();
        if (!_globalState.HasCredentials()) return AuthResult.Fail("未配置凭据");
        return await LoginAsync();
    }
}

/// <summary>
/// 认证操作结果
/// </summary>
public class AuthResult {
    public bool Success { get; init; }
    public string? Token { get; init; }
    public string? ErrorMessage { get; init; }

    public static AuthResult Ok(string? token = null) => new() { Success = true, Token = token };
    public static AuthResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}
