using Refit;
using StarBlogPublisher.Services.StarBlogApi;

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

    public IAuth Auth => RestService.For<IAuth>(BaseUrl);
}