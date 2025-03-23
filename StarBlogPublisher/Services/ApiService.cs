using Refit;
using StarBlogPublisher.Services.StarBlogApi;
using System;
using System.Net;
using System.Net.Http;

namespace StarBlogPublisher.Services;

public class ApiService {
    private static ApiService? _instance;

    public static ApiService Instance {
        get {
            _instance ??= new ApiService();
            return _instance;
        }
    }

    private ApiService() { }

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

            return client;
        }
    }

    public IAuth Auth => RestService.For<IAuth>(ApiHttpClient);
    public ICategory Categories => RestService.For<ICategory>(ApiHttpClient);
    public IBlogPost BlogPost => RestService.For<IBlogPost>(ApiHttpClient);
}