using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.Services.StarBlogApi;

namespace StarBlogPublisher.Tests.Services.Application;

public class CategoryApplicationServiceMutationTests {
    private readonly Mock<ICategory> _mockCategories;
    private readonly CategoryApplicationService _service;

    public CategoryApplicationServiceMutationTests() {
        var settings = new AppSettings { Username = "user", Password = "pass" };
        var globalState = new GlobalState();
        var mockAuth = new Mock<IAuth>();
        _mockCategories = new Mock<ICategory>();
        var api = new ApiService(mockAuth.Object, Mock.Of<IBlogPost>(), _mockCategories.Object);
        var authService = new AuthApplicationService(settings, globalState, api);
        _service = new CategoryApplicationService(api, authService);
        mockAuth.Setup(x => x.Login(It.IsAny<LoginUser>()))
            .ReturnsAsync(new ApiResponse<LoginToken> { Data = new LoginToken { Token = "token" } });
        authService.LoginAsync("user", "pass").Wait();
    }

    [Fact]
    public async Task UpdateCategoryAsync_EmptyName_ReturnsFail() {
        var result = await _service.UpdateCategoryAsync(1, "  ", 0, true);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateCategoryAsync_ReturnsUpdatedCategory() {
        _mockCategories.Setup(x => x.Update(2, It.IsAny<CategoryCreationDto>()))
            .ReturnsAsync(new ApiResponse<Category> { Data = new Category { Id = 2, Name = "Renamed" } });

        var result = await _service.UpdateCategoryAsync(2, "Renamed", 0, true);

        result.Success.Should().BeTrue();
        result.Category!.DisplayName.Should().Be("Renamed");
    }

    [Fact]
    public async Task DeleteCategoryAsync_SurfacesApiFailure() {
        _mockCategories.Setup(x => x.Delete(3))
            .ReturnsAsync(new ApiResponse<object> { Successful = false, StatusCode = 400, Message = "所选分类下有文章，不能删除！" });

        var result = await _service.DeleteCategoryAsync(3);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("不能删除");
    }

    [Fact]
    public void Flatten_WalksNestedNodes() {
        var tree = new List<Category> {
            new() {
                Id = 1, Text = "Root", Nodes = [
                    new Category { Id = 2, Text = "Child" }
                ]
            }
        };

        CategoryApplicationService.Flatten(tree).Select(c => c.Id).Should().Equal(1, 2);
    }
}
