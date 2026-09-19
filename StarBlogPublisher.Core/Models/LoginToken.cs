using System;

namespace StarBlogPublisher.Models;

public class LoginToken {
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
}
