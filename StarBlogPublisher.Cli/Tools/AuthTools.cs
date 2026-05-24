using System.ComponentModel;
using ModelContextProtocol.Server;
using StarBlogPublisher.Cli.Auth;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Tools;

[McpServerToolType]
public static class AuthTools {
    [McpServerTool, Description("登录到 StarBlog 后端")]
    public static async Task<string> AuthLogin(
        [Description("用户名（可选，未提供时复用已保存凭据）")] string? username = null,
        [Description("密码（可选，未提供时复用已保存凭据）")] string? password = null,
        [Description("后端地址（可选）")] string? url = null) {

        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        var result = await AuthWorkflow.LoginAsync(authService, username, password, url, allowPrompt: false);
        return result.Success ? result.Message : $"登录失败: {result.Message}";
    }

    [McpServerTool, Description("查看当前登录状态")]
    public static string AuthStatus() {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        return AuthWorkflow.FormatStatus(authService, includeSavedLoginHint: false);
    }

    [McpServerTool, Description("登出")]
    public static string AuthLogout(
        [Description("是否同时清除已保存的用户名和密码")] bool clearCredentials = false) {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        return AuthWorkflow.Logout(authService, clearCredentials);
    }
}
