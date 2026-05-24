using System.CommandLine;
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

        var loginCmd = new Command("login", "登录到 StarBlog 后端") { usernameOpt, passwordOpt, urlOpt };
        loginCmd.SetAction(parseResult => {
            var username = parseResult.GetValue(usernameOpt);
            var password = parseResult.GetValue(passwordOpt);
            var url = parseResult.GetValue(urlOpt);

            if (!string.IsNullOrEmpty(url)) {
                AppSettings.Instance.BackendUrl = url;
            }

            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);

            var credentials = ResolveCredentials(authService, username, password);

            if (!credentials.Success) {
                Console.Error.WriteLine(credentials.ErrorMessage);
                return 1;
            }

            Task.Run(async () => {
                var result = credentials.UseSavedCredentials
                    ? await authService.LoginAsync()
                    : await authService.LoginAsync(credentials.Username!, credentials.Password!);

                if (result.Success) {
                    if (!credentials.UseSavedCredentials) {
                        AppSettings.Instance.Username = credentials.Username!;
                        AppSettings.Instance.Password = credentials.Password!;
                    }

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

            if (!authService.IsLoggedIn && authService.HasCredentials) {
                Console.WriteLine("可直接执行 auth login 复用已保存凭据。\n如需改用新账号，可执行 auth login --username <用户名> --password <密码>。");
            }

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

            if (clearCredentials) {
                authService.LogoutAndClearCredentials();
                Console.WriteLine("已登出，并清除已保存凭据");
            }
            else {
                authService.Logout();
                Console.WriteLine("已登出当前会话，已保存凭据仍可用于后续 auth login");
            }

            return 0;
        });

        command.Subcommands.Add(loginCmd);
        command.Subcommands.Add(statusCmd);
        command.Subcommands.Add(logoutCmd);
        return command;
    }

    private static LoginCredentialResolution ResolveCredentials(
        AuthApplicationService authService,
        string? username,
        string? password) {
        var hasUsername = !string.IsNullOrWhiteSpace(username);
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (!hasUsername && !hasPassword && authService.HasCredentials) {
            return LoginCredentialResolution.UseSaved();
        }

        if ((hasUsername && !hasPassword) || (!hasUsername && hasPassword)) {
            if (Console.IsInputRedirected) {
                return LoginCredentialResolution.Fail("请同时提供 --username 和 --password；当前环境不支持交互式补全");
            }

            username ??= PromptRequiredValue("用户名: ");
            password ??= PromptRequiredPassword("密码: ");
        }
        else if (!hasUsername && !hasPassword) {
            if (Console.IsInputRedirected) {
                return LoginCredentialResolution.Fail("未配置已保存凭据，且当前环境不支持交互式输入，请使用 --username 和 --password");
            }

            Console.WriteLine("未检测到已保存凭据，请输入用户名和密码。");
            username = PromptRequiredValue("用户名: ");
            password = PromptRequiredPassword("密码: ");
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) {
            return LoginCredentialResolution.Fail("用户名和密码不能为空");
        }

        return LoginCredentialResolution.UseExplicit(username, password);
    }

    private static string PromptRequiredValue(string prompt) {
        while (true) {
            Console.Write(prompt);
            var input = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(input)) {
                return input;
            }

            Console.WriteLine("输入不能为空，请重试。");
        }
    }

    private static string PromptRequiredPassword(string prompt) {
        while (true) {
            var password = ReadPassword(prompt);
            if (!string.IsNullOrEmpty(password)) {
                return password;
            }

            Console.WriteLine("密码不能为空，请重试。");
        }
    }

    private static string ReadPassword(string prompt) {
        Console.Write(prompt);
        var passwordChars = new List<char>();

        while (true) {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) {
                Console.WriteLine();
                return new string(passwordChars.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace) {
                if (passwordChars.Count > 0) {
                    passwordChars.RemoveAt(passwordChars.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar)) {
                passwordChars.Add(key.KeyChar);
            }
        }
    }

    private sealed record LoginCredentialResolution(
        bool Success,
        bool UseSavedCredentials,
        string? Username,
        string? Password,
        string? ErrorMessage) {
        public static LoginCredentialResolution UseSaved() => new(true, true, null, null, null);

        public static LoginCredentialResolution UseExplicit(string username, string password) =>
            new(true, false, username, password, null);

        public static LoginCredentialResolution Fail(string message) => new(false, false, null, null, message);
    }
}
