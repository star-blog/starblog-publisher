using FluentAssertions;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Utils;

public class PromptTemplatesTests {
    [Fact]
    public void TitleOptimizationTemplates_IsNotEmpty() {
        PromptTemplates.TitleOptimizationTemplates.Should().NotBeEmpty();
    }

    [Fact]
    public void TitleOptimizationTemplates_HasDefault() {
        PromptTemplates.TitleOptimizationTemplates
            .Should().Contain(t => t.IsDefault);
    }

    [Fact]
    public void TitleOptimizationTemplates_AllHaveRequiredFields() {
        foreach (var template in PromptTemplates.TitleOptimizationTemplates) {
            template.Key.Should().NotBeNullOrWhiteSpace();
            template.Name.Should().NotBeNullOrWhiteSpace();
            template.Template.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Cover_IsNotEmpty() {
        PromptTemplates.Cover.Should().NotBeEmpty();
    }

    [Fact]
    public void Cover_AllHaveRequiredFields() {
        foreach (var template in PromptTemplates.Cover) {
            template.Key.Should().NotBeNullOrWhiteSpace();
            template.Name.Should().NotBeNullOrWhiteSpace();
            template.Prompt.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void KeywordExtraction_IsNotEmpty() {
        PromptTemplates.KeywordExtraction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void UrlSlugGeneration_IsNotEmpty() {
        PromptTemplates.UrlSlugGeneration.Should().NotBeNullOrWhiteSpace();
    }
}
