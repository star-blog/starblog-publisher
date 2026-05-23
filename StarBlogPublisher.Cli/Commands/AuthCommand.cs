using System.CommandLine;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Commands;

public static class AuthCommand {
    public static Command Build() {
        var command = new Command("auth", "认证管理");

        // auth login
        var usernameOpt = new Option<string>("--username") { Description = "用户名", Required = true };
        var passwordOpt = new Option<string>("--password") { Description = "密码", Required = true };
        var urlOpt = new Option<string?>("--url") { Description = "后端地址（可选，覆盖已保存配置）" };

        var loginCmd = new Command("login", "登录到 StarBlog 后端") { usernameOpt, passwordOpt, urlOpt };
        loginCmd.SetAction(parseResult => {
            var username = parseResult.GetValue(usernameOpt)!;
            var password = parseResult.GetValue(passwordOpt)!;
            var url = parseResult.GetValue(urlOpt);

            if (!string.IsNullOrEmpty(url)) {
                AppSettings.Instance.BackendUrl = url;
            }

            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);

            Task.Run(async () => {
                var result = await authService.LoginAsync(username, password);
                if (result.Success) {
                    AppSettings.Instance.Username = username;
                    AppSettings.Instance.Password = password;
                    AppSettings.Instance.Save();
                    Console.WriteLine("登录成功");
                }
                else {
                    Console.Error.WriteLine(result.ErrorMessage);
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
            Console.WriteLine(authService.GetStatusMessage());
            return 0;
        });

        // auth logout
        var logoutCmd = new Command("logout", "登出");
        logoutCmd.SetAction(_ => {
            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            authService.Logout();
            Console.WriteLine("已登出");
            return 0;
        });

        command.Subcommands.Add(loginCmd);
        command.Subcommands.Add(statusCmd);
        command.Subcommands.Add(logoutCmd);
        return command;
    }
}
