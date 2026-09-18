using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace StarBlogPublisher.Services;

/// <summary>Application composition root for shared, pooled HTTP clients.</summary>
internal static class AppHttpClients {
    private static readonly Lazy<ServiceProvider> Provider = new(CreateProvider);

    public static IHttpClientFactory Factory => Provider.Value.GetRequiredService<IHttpClientFactory>();

    private static ServiceProvider CreateProvider() {
        var services = new ServiceCollection();
        services.AddWeChatHttpClients(AppSettings.Instance);
        return services.BuildServiceProvider();
    }
}
