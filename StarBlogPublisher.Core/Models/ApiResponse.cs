namespace StarBlogPublisher.Models;

/// <summary>
/// Represents the response envelope returned by the StarBlog API.
/// This is intentionally limited to the contract consumed by this client.
/// </summary>
public class ApiResponse<T>
{
    public ApiResponse()
    {
    }

    public ApiResponse(T? data)
    {
        Data = data;
    }

    public int StatusCode { get; set; } = 200;

    public bool Successful { get; set; } = true;

    public string? Message { get; set; }

    public T? Data { get; set; }

    public static implicit operator ApiResponse<T>(T data) => new()
    {
        StatusCode = 200,
        Successful = true,
        Data = data,
        Message = "ok"
    };
}
