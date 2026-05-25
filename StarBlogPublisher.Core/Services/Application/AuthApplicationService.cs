using System;
using System.Threading.Tasks;
using Refit;
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

    /// <summary>
    /// 用户是否已显式登出（用于阻止自动重新登录）
    /// </summary>
    private bool _userExplicitlyLoggedOut;

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
    /// 当前生效的后端地址
    /// </summary>
    public string BackendUrl => string.IsNullOrWhiteSpace(_settings.BackendUrl)
        ? "https://blog.deali.cn/"
        : _settings.BackendUrl;

    /// <summary>
    /// 是否已配置凭据
    /// </summary>
    public bool HasCredentials => !string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password);

    /// <summary>
    /// 获取当前登录状态描述
    /// </summary>
    public string GetStatusMessage() {
        if (_globalState.IsLoggedIn) return "已登录";
        if (HasCredentials) return "未登录 (已配置凭据)";
        return "未登录 (未配置凭据)";
    }

    /// <summary>
    /// 获取详细登录状态信息
    /// </summary>
    public AuthStatusInfo GetStatusInfo() {
        var credentialSource = _globalState.IsLoggedIn
            ? "当前会话令牌"
            : HasCredentials
                ? "已保存凭据"
                : "未配置凭据";

        return new AuthStatusInfo(
            GetStatusMessage(),
            BackendUrl,
            credentialSource,
            _globalState.IsLoggedIn,
            HasCredentials);
    }

    /// <summary>
    /// 使用已保存的凭据登录
    /// </summary>
    public async Task<AuthResult> LoginAsync() {
        if (!HasCredentials) {
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
            _userExplicitlyLoggedOut = false;
            return AuthResult.Ok(resp.Data.Token);
        }
        catch (ApiException ex) {
            return AuthResult.Fail(BuildApiErrorMessage(ex));
        }
        catch (Exception ex) {
            return AuthResult.Fail($"登录失败: {ex.Message}");
        }
    }

    private string BuildApiErrorMessage(ApiException ex) {
        var targetUri = ex.Uri?.ToString() ?? $"{BackendUrl.TrimEnd('/')}/Api/Auth/Login";
        var statusCode = (int)ex.StatusCode;
        var reasonPhrase = string.IsNullOrWhiteSpace(ex.ReasonPhrase)
            ? ex.StatusCode.ToString()
            : ex.ReasonPhrase;

        var message = $"登录失败: 请求 {targetUri} 返回 {statusCode} ({reasonPhrase})";

        if (!string.IsNullOrWhiteSpace(ex.Content)) {
            message += $"，响应内容: {SanitizeErrorContent(ex.Content)}";
        }

        return message;
    }

    private static string SanitizeErrorContent(string content) {
        var flattened = content
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();

        return flattened.Length <= 300
            ? flattened
            : $"{flattened[..300]}...";
    }

    /// <summary>
    /// 登出（仅清理会话状态，保留已保存的凭据）
    /// </summary>
    public void Logout() {
        _globalState.Logout();
        _userExplicitlyLoggedOut = true;
    }

    /// <summary>
    /// 登出并清除已保存的凭据。
    /// </summary>
    public void LogoutAndClearCredentials() {
        Logout();
        _settings.Username = string.Empty;
        _settings.Password = string.Empty;
        _settings.Save();
    }

    /// <summary>
    /// 确保已登录：如果未登录但有凭据，且用户未显式登出，则自动登录。
    /// 仅用于业务命令（如 post publish），不用于 status 等查询命令。
    /// </summary>
    public async Task<AuthResult> EnsureLoggedInAsync() {
        if (_globalState.IsLoggedIn) return AuthResult.Ok();
        if (_userExplicitlyLoggedOut) return AuthResult.Fail("用户已登出，请先执行登录");
        if (!HasCredentials) return AuthResult.Fail("未配置凭据");
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

public sealed record AuthStatusInfo(
    string StatusMessage,
    string BackendUrl,
    string CredentialSource,
    bool IsLoggedIn,
    bool HasSavedCredentials);
