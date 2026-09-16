using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Models;

public class ApiResponseTests
{
    [Fact]
    public void Default_response_matches_the_StarBlog_API_success_contract()
    {
        var response = new ApiResponse<string>();

        response.StatusCode.Should().Be(200);
        response.Successful.Should().BeTrue();
        response.Message.Should().BeNull();
        response.Data.Should().BeNull();
    }

    [Fact]
    public void Data_constructor_preserves_the_payload()
    {
        var response = new ApiResponse<string>("article");

        response.Data.Should().Be("article");
    }

    [Theory]
    [InlineData("short", 5, "short")]
    [InlineData("summary", 3, "sum")]
    [InlineData("", 0, "")]
    public void Limit_matches_the_previous_extension_contract(string value, int length, string expected)
    {
        value.Limit(length).Should().Be(expected);
    }
}
