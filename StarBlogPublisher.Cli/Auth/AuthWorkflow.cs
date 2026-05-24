using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Auth;

internal static class AuthWorkflow {
    public static async Task<AuthWorkflowResult> LoginAsync(
        AuthApplicationService authService,
        string? username,
        string? password,
        string? url,
        bool allowPrompt) {
        if (!string.IsNullOrEmpty(url)) {
            AppSettings.Instance.BackendUrl = url;
        }

        var credentials = ResolveCredentials(authService, username, password, allowPrompt);
        if (!credentials.Success) {
            return AuthWorkflowResult.Fail(credentials.ErrorMessage!);
        }

        var result = credentials.UseSavedCredentials
            ? await authService.LoginAsync()
            : await authService.LoginAsync(credentials.Username!, credentials.Password!);

        if (!result.Success) {
            return AuthWorkflowResult.Fail(result.ErrorMessage ?? "登录失败");
        }

        if (!credentials.UseSavedCredentials) {
            AppSettings.Instance.Username = credentials.Username!;
            AppSettings.Instance.Password = credentials.Password!;
        }

        AppSettings.Instance.Save();
        return AuthWorkflowResult.Ok("登录成功");
    }

    public static string FormatStatus(AuthApplicationService authService, bool includeSavedLoginHint) {
        var status = authService.GetStatusInfo();
        var lines = new List<string> {
            $"状态: {status.StatusMessage}",
            $"后端地址: {status.BackendUrl}",
            $"凭据来源: {status.CredentialSource}"
        };

        if (includeSavedLoginHint && !status.IsLoggedIn && status.HasSavedCredentials) {
            lines.Add("提示: 可直接执行 auth login 复用已保存凭据；如需改用新账号，可执行 auth login --username <用户名> --password <密码>。");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string Logout(AuthApplicationService authService, bool clearCredentials) {
        if (clearCredentials) {
            authService.LogoutAndClearCredentials();
            return "已登出，并清除已保存凭据";
        }

        authService.Logout();
        return "已登出当前会话，已保存凭据仍可用于后续 auth login";
    }

    private static LoginCredentialResolution ResolveCredentials(
        AuthApplicationService authService,
        string? username,
        string? password,
        bool allowPrompt) {
        var hasUsername = !string.IsNullOrWhiteSpace(username);
        var hasPassword = !string.IsNullOrWhiteSpace(password);

        if (!hasUsername && !hasPassword && authService.HasCredentials) {
            return LoginCredentialResolution.UseSaved();
        }

        if ((hasUsername && !hasPassword) || (!hasUsername && hasPassword)) {
            if (!allowPrompt) {
                return LoginCredentialResolution.Fail("请同时提供 --username 和 --password；当前已禁用交互式补全");
            }

            username ??= PromptRequiredValue("用户名: ");
            password ??= PromptRequiredPassword("密码: ");
        }
        else if (!hasUsername && !hasPassword) {
            if (!allowPrompt) {
                return LoginCredentialResolution.Fail("未配置已保存凭据，且已禁用交互式输入，请使用 --username 和 --password");
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

internal sealed record AuthWorkflowResult(bool Success, string Message) {
    public static AuthWorkflowResult Ok(string message) => new(true, message);
    public static AuthWorkflowResult Fail(string message) => new(false, message);
}