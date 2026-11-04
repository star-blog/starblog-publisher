using Refit;
using StarBlogPublisher.Services.StarBlogApi;
using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace StarBlogPublisher.Services;

public class ApiService {
    private static ApiService? _instance;

    public static ApiService Instance {
        get {
            _instance ??= new ApiService();
            return _instance;
        }
    }

    private readonly RefitSettings? _refitSettings;
    private IAuth? _mockAuth;
    private IBlogPost? _mockBlogPost;
    private ICategory? _mockCategories;
    private ISite? _site;
    private readonly string? _baseUrlOverride;

    internal ApiService(IAuth auth, IBlogPost blogPost, ICategory categories) {
        _mockAuth = auth;
        _mockBlogPost = blogPost;
        _mockCategories = categories;
    }

    internal ApiService(HttpClient client) : this(
        RestService.For<IAuth>(client, CreateRefitSettings()),
        RestService.For<IBlogPost>(client, CreateRefitSettings()),
        RestService.For<ICategory>(client, CreateRefitSettings())) {
        _site = RestService.For<ISite>(client, CreateRefitSettings());
        _baseUrlOverride = client.BaseAddress?.ToString();
    }

    private ApiService() {
        // 确保类型被注册
        RefitTypeRegistration.RegisterTypes();

        // 配置Refit设置
        _refitSettings = CreateRefitSettings();
    }

    private static RefitSettings CreateRefitSettings() => new(new NewtonsoftJsonContentSerializer(
            new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.None,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            }
        ));

    public string BaseUrl {
        get {
            var backendUrl = _baseUrlOverride ?? AppSettings.Instance.BackendUrl;
            return string.IsNullOrWhiteSpace(backendUrl) ? "https://blog.deali.cn/" : backendUrl;
        }
    }

    private HttpClient ApiHttpClient {
        get {
            var handler = new HttpClientHandler();

            // 检查是否需要使用代理
            if (AppSettings.Instance.UseProxy && !string.IsNullOrWhiteSpace(AppSettings.Instance.ProxyHost)) {
                var proxyUri =
                    $"{AppSettings.Instance.ProxyType}://{AppSettings.Instance.ProxyHost}:{AppSettings.Instance.ProxyPort}";
                handler.Proxy = new WebProxy(proxyUri);
                handler.UseProxy = true;
            }

            var client = new HttpClient(handler) {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(AppSettings.Instance.BackendTimeout)
            };

            // 如果用户已登录，添加JWT令牌到Authorization头部
            if (GlobalState.Instance.IsLoggedIn) {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {GlobalState.Instance.JwtToken}");
            }

            return client;
        }
    }

    public IAuth Auth => _mockAuth ?? RestService.For<IAuth>(ApiHttpClient, _refitSettings!);
    public ICategory Categories => _mockCategories ?? RestService.For<ICategory>(ApiHttpClient, _refitSettings!);
    public IBlogPost BlogPost => _mockBlogPost ?? RestService.For<IBlogPost>(ApiHttpClient, _refitSettings!);
    public ISite Site => _site ?? RestService.For<ISite>(ApiHttpClient, _refitSettings!);
}
