using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Models;

public class PageResultTests
{
    [Fact]
    public void Page_response_maps_all_pagination_fields()
    {
        var response = new PageResult<string> {
            Items = ["article"], Page = 2, PageSize = 20, TotalCount = 45, TotalPages = 3
        };
        var pagination = response.ToPagination();
        pagination.PageNumber.Should().Be(2);
        pagination.PageSize.Should().Be(20);
        pagination.TotalItemCount.Should().Be(45);
        pagination.TotalPages.Should().Be(3);
        pagination.HasPreviousPage.Should().BeTrue();
        pagination.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Empty_response_has_no_pages_to_navigate()
    {
        var response = new PageResult<string> { Page = 1, PageSize = 20 };
        response.Items.Should().BeEmpty();
        response.ToPagination().TotalPages.Should().Be(0);
        response.ToPagination().HasPreviousPage.Should().BeFalse();
        response.ToPagination().HasNextPage.Should().BeFalse();
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
