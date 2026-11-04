using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Refit;

namespace StarBlogPublisher.Services;

/// <summary>Readable errors from the v1 ProblemDetails contract, with a plain HTTP fallback.</summary>
internal static class ApiError {
    public static string Describe(Exception exception) {
        if (exception is not ApiException api) return exception.Message;
        var detail = api.ReasonPhrase ?? api.StatusCode.ToString();
        if (!string.IsNullOrWhiteSpace(api.Content)) {
            try {
                var problem = JObject.Parse(api.Content);
                detail = problem.Value<string>("detail") ?? problem.Value<string>("title") ?? detail;
            }
            catch (JsonException) {
                // Proxies may return HTML or plain text instead of ProblemDetails.
                detail = api.Content.Replace('\r', ' ').Replace('\n', ' ').Trim();
            }
        }
        if (detail.Length > 300) detail = detail[..300] + "...";
        return $"HTTP {(int)api.StatusCode}: {detail}";
    }
}
