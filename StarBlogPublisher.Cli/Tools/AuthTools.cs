using System.ComponentModel;
using ModelContextProtocol.Server;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Tools;

[McpServerToolType]
public static class AuthTools {
    [McpServerTool, Description("登录到 StarBlog 后端")]
    public static async Task<string> AuthLogin(
        [Description("用户名")] string username,
        [Description("密码")] string password,
        [Description("后端地址（可选）")] string? url = null) {

        if (!string.IsNullOrEmpty(url)) {
            AppSettings.Instance.BackendUrl = url;
        }

        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        var result = await authService.LoginAsync(username, password);

        if (result.Success) {
            AppSettings.Instance.Username = username;
            AppSettings.Instance.Password = password;
            AppSettings.Instance.Save();
            return "登录成功";
        }

        return $"登录失败: {result.ErrorMessage}";
    }

    [McpServerTool, Description("查看当前登录状态")]
    public static string AuthStatus() {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        return authService.GetStatusMessage();
    }

    [McpServerTool, Description("登出")]
    public static string AuthLogout() {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        authService.Logout();
        return "已登出";
    }
}
