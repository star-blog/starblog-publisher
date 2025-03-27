using Refit;
using StarBlogPublisher.Services.StarBlogApi;
using System;
using System.Net;
using System.Net.Http;
using Newtonsoft.Json;

namespace StarBlogPublisher.Services;

public class ApiService {
    private static ApiService? _instance;

    public static ApiService Instance {
        get {
            _instance ??= new ApiService();
            return _instance;
        }
    }

    private readonly RefitSettings _refitSettings;


    private ApiService() {
        // 确保类型被注册
        RefitTypeRegistration.RegisterTypes();

        // 配置Refit设置
        _refitSettings = new RefitSettings(new NewtonsoftJsonContentSerializer(
            new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.Auto,
                // 禁用反射优化，这在AOT环境中很重要
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple
            }
        ));
    }

    public string BaseUrl {
        get {
            var backendUrl = AppSettings.Instance.BackendUrl;
            return string.IsNullOrWhiteSpace(backendUrl) ? "http://localhost:5038" : backendUrl;
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

    public IAuth Auth => RestService.For<IAuth>(ApiHttpClient, _refitSettings);
    public ICategory Categories => RestService.For<ICategory>(ApiHttpClient, _refitSettings);
    public IBlogPost BlogPost => RestService.For<IBlogPost>(ApiHttpClient, _refitSettings);
}