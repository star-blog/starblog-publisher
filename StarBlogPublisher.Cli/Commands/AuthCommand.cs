using System.CommandLine;
using StarBlogPublisher.Cli.Auth;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Commands;

public static class AuthCommand {
    public static Command Build() {
        var command = new Command("auth", "认证管理");

        // auth login
        var usernameOpt = new Option<string?>("--username") { Description = "用户名（可选，未提供时使用已保存凭据）" };
        var passwordOpt = new Option<string?>("--password") { Description = "密码（可选，未提供时使用已保存凭据）" };
        var urlOpt = new Option<string?>("--url") { Description = "后端地址（可选，覆盖已保存配置）" };
        var noPromptOpt = new Option<bool>("--no-prompt") { Description = "禁用交互式输入；适用于 CI 或脚本环境" };

        var loginCmd = new Command("login", "登录到 StarBlog 后端") { usernameOpt, passwordOpt, urlOpt, noPromptOpt };
        loginCmd.SetAction(parseResult => {
            var username = parseResult.GetValue(usernameOpt);
            var password = parseResult.GetValue(passwordOpt);
            var url = parseResult.GetValue(urlOpt);
            var noPrompt = parseResult.GetValue(noPromptOpt);

            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);

            Task.Run(async () => {
                var result = await AuthWorkflow.LoginAsync(
                    authService,
                    username,
                    password,
                    url,
                    allowPrompt: !noPrompt && !Console.IsInputRedirected);

                if (result.Success) {
                    Console.WriteLine(result.Message);
                }
                else {
                    Console.Error.WriteLine(result.Message);
                    Environment.ExitCode = 1;
                }
            }).Wait();
            return Environment.ExitCode;
        });

        // auth status
        var statusCmd = new Command("status", "查看登录状态");
        statusCmd.SetAction(_ => {
            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            Console.WriteLine(AuthWorkflow.FormatStatus(authService, includeSavedLoginHint: true));

            return 0;
        });

        // auth logout
        var clearCredentialsOpt = new Option<bool>("--clear-credentials") { Description = "同时清除已保存的用户名和密码" };
        clearCredentialsOpt.Aliases.Add("--clear");
        var logoutCmd = new Command("logout", "登出") { clearCredentialsOpt };
        logoutCmd.SetAction(parseResult => {
            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            var clearCredentials = parseResult.GetValue(clearCredentialsOpt);

            Console.WriteLine(AuthWorkflow.Logout(authService, clearCredentials));

            return 0;
        });

        command.Subcommands.Add(loginCmd);
        command.Subcommands.Add(statusCmd);
        command.Subcommands.Add(logoutCmd);
        return command;
    }
}
