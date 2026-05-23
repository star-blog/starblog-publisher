using FluentAssertions;
using Moq;
using CodeLab.Share.ViewModels.Response;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Services.StarBlogApi;

namespace StarBlogPublisher.Tests.Services.Application;

public class AuthApplicationServiceTests {
    private readonly AppSettings _settings;
    private readonly GlobalState _globalState;
    private readonly Mock<IAuth> _mockAuth;
    private readonly ApiService _apiService;
    private readonly AuthApplicationService _service;

    public AuthApplicationServiceTests() {
        _settings = new AppSettings();
        _globalState = new GlobalState();
        _mockAuth = new Mock<IAuth>();
        _apiService = new ApiService(
            _mockAuth.Object,
            Mock.Of<IBlogPost>(),
            Mock.Of<ICategory>()
        );
        _service = new AuthApplicationService(_settings, _globalState, _apiService);
    }

    // === GetStatusMessage ===

    [Fact]
    public void GetStatusMessage_NotLoggedIn_NoCredentials_ReturnsNotConfigured() {
        _service.GetStatusMessage().Should().Contain("未配置凭据");
    }

    [Fact]
    public void GetStatusMessage_NotLoggedIn_HasCredentials_ReturnsNotLoggedIn() {
        _settings.Username = "user";
        _settings.Password = "pass";

        _service.GetStatusMessage().Should().Contain("已配置凭据");
    }

    [Fact]
    public void GetStatusMessage_LoggedIn_ReturnsLoggedIn() {
        _settings.Username = "user";
        _settings.Password = "pass";
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "test-token" }
            });

        _service.LoginAsync("user", "pass").Wait();
        _service.GetStatusMessage().Should().Be("已登录");
    }

    // === LoginAsync ===

    [Fact]
    public async Task LoginAsync_Success_SetsLoggedIn() {
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "jwt-token" }
            });

        var result = await _service.LoginAsync("user", "pass");

        result.Success.Should().BeTrue();
        result.Token.Should().Be("jwt-token");
        _service.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_NoToken_ReturnsFail() {
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "" }
            });

        var result = await _service.LoginAsync("user", "pass");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("登录失败");
    }

    [Fact]
    public async Task LoginAsync_NullData_ReturnsFail() {
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> { Data = null, Message = "Invalid credentials" });

        var result = await _service.LoginAsync("user", "pass");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid credentials");
    }

    [Fact]
    public async Task LoginAsync_ApiThrows_ReturnsFail() {
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ThrowsAsync(new Exception("Network error"));

        var result = await _service.LoginAsync("user", "pass");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task LoginAsync_Parameterless_UsesSettingsCredentials() {
        _settings.Username = "saved-user";
        _settings.Password = "saved-pass";

        _mockAuth.Setup(x => x.Login(It.Is<LoginUser>(
                l => l.Username == "saved-user" && l.Password == "saved-pass")))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "token" }
            });

        var result = await _service.LoginAsync();

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_Parameterless_NoCredentials_ReturnsFail() {
        var result = await _service.LoginAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未配置");
    }

    // === Logout ===

    [Fact]
    public void Logout_ClearsLoginState() {
        // First login
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "token" }
            });
        _service.LoginAsync("user", "pass").Wait();

        _service.Logout();

        _service.IsLoggedIn.Should().BeFalse();
    }

    // === EnsureLoggedInAsync ===

    [Fact]
    public async Task EnsureLoggedInAsync_AlreadyLoggedIn_ReturnsOk() {
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "token" }
            });
        await _service.LoginAsync("user", "pass");

        var result = await _service.EnsureLoggedInAsync();

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureLoggedInAsync_ExplicitlyLoggedOut_DoesNotAutoLogin() {
        // Login then logout
        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "token" }
            });
        await _service.LoginAsync("user", "pass");
        _service.Logout();

        var result = await _service.EnsureLoggedInAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("已登出");
    }

    [Fact]
    public async Task EnsureLoggedInAsync_NoCredentials_ReturnsFail() {
        var result = await _service.EnsureLoggedInAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("未配置凭据");
    }

    [Fact]
    public async Task EnsureLoggedInAsync_HasCredentials_AutoLogins() {
        _settings.Username = "user";
        _settings.Password = "pass";

        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "auto-token" }
            });

        var result = await _service.EnsureLoggedInAsync();

        result.Success.Should().BeTrue();
        _service.IsLoggedIn.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureLoggedInAsync_LoginThenLogoutThenLogin_ClearsExplicitFlag() {
        _settings.Username = "user";
        _settings.Password = "pass";

        _mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> {
                Data = new LoginToken { Token = "token" }
            });

        // Login, logout, then login again
        await _service.LoginAsync("user", "pass");
        _service.Logout();
        await _service.LoginAsync("user", "pass");

        // Now EnsureLoggedIn should work (not be blocked by explicit logout)
        var result = await _service.EnsureLoggedInAsync();
        result.Success.Should().BeTrue();
    }
}
