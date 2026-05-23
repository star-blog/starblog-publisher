using FluentAssertions;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Tests.Services.Application;

public class AiApplicationServiceTests {
    // === CleanSlug tests ===

    [Theory]
    [InlineData("hello-world", "hello-world")]
    [InlineData("hello world", "helloworld")]
    [InlineData("hello-world-test", "hello-world-test")]
    [InlineData("a", "a")]
    public void CleanSlug_NormalInput_ReturnsExpected(string input, string expected) {
        AiApplicationService.CleanSlug(input).Should().Be(expected);
    }

    [Fact]
    public void CleanSlug_WithSpecialCharacters_RemovesNonAlphanumeric() {
        AiApplicationService.CleanSlug("hello@world!#test")
            .Should().Be("helloworldtest");
    }

    [Fact]
    public void CleanSlug_Uppercase_RemovesUppercaseLetters() {
        // The regex [^a-z0-9\-] strips uppercase letters
        AiApplicationService.CleanSlug("Hello World")
            .Should().Be("elloorld");
    }

    [Fact]
    public void CleanSlug_WithSpaces_RemovesSpaces() {
        AiApplicationService.CleanSlug("hello world test")
            .Should().Be("helloworldtest");
    }

    [Fact]
    public void CleanSlug_WithChinese_RemovesNonAscii() {
        AiApplicationService.CleanSlug("你好-world-测试")
            .Should().Be("world");
    }

    [Fact]
    public void CleanSlug_ConsecutiveDashes_CollapsesToOne() {
        AiApplicationService.CleanSlug("hello---world")
            .Should().Be("hello-world");
    }

    [Fact]
    public void CleanSlug_LeadingTrailingDashes_Trims() {
        AiApplicationService.CleanSlug("--hello--")
            .Should().Be("hello");
    }

    [Fact]
    public void CleanSlug_EmptyString_ReturnsEmpty() {
        AiApplicationService.CleanSlug("").Should().Be("");
    }

    [Fact]
    public void CleanSlug_AllSpecialChars_ReturnsEmpty() {
        AiApplicationService.CleanSlug("@#$%^&*()").Should().Be("");
    }

    [Fact]
    public void CleanSlug_VeryLongInput_TruncatesTo50() {
        var longSlug = new string('a', 100);
        var result = AiApplicationService.CleanSlug(longSlug);
        result.Length.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void CleanSlug_LongWithDashAtBoundary_DoesNotEndWithDash() {
        // 49 a's + "-b" = 51 chars, should truncate to 50 and not end with dash
        var input = new string('a', 49) + "-b-extra";
        var result = AiApplicationService.CleanSlug(input);
        result.Length.Should().BeLessThanOrEqualTo(50);
        result.Should().NotEndWith("-");
    }

    // === ExtractKeywordsFromJson tests ===

    [Fact]
    public void ExtractKeywordsFromJson_ValidJsonArray_ExtractsKeywords() {
        var json = """["C#", "ASP.NET", "Web API"]""";
        var result = AiApplicationService.ExtractKeywordsFromJson(json);
        result.Should().Be("C#, ASP.NET, Web API");
    }

    [Fact]
    public void ExtractKeywordsFromJson_EmptyArray_ReturnsEmpty() {
        var result = AiApplicationService.ExtractKeywordsFromJson("[]");
        result.Should().Be("");
    }

    [Fact]
    public void ExtractKeywordsFromJson_SingleItem_ReturnsSingleKeyword() {
        var result = AiApplicationService.ExtractKeywordsFromJson("""["dotnet"]""");
        result.Should().Be("dotnet");
    }

    [Fact]
    public void ExtractKeywordsFromJson_WithComments_IgnoresCommentStrings() {
        var json = """["C#", "// this is a comment", "Python"]""";
        var result = AiApplicationService.ExtractKeywordsFromJson(json);
        result.Should().Be("C#, Python");
    }

    [Fact]
    public void ExtractKeywordsFromJson_MalformedInput_ReturnsEmpty() {
        var result = AiApplicationService.ExtractKeywordsFromJson("not json at all");
        result.Should().Be("");
    }

    [Fact]
    public void ExtractKeywordsFromJson_EmptyString_ReturnsEmpty() {
        var result = AiApplicationService.ExtractKeywordsFromJson("");
        result.Should().Be("");
    }

    [Fact]
    public void ExtractKeywordsFromJson_WithSurroundingText_ExtractsArray() {
        var json = """Here are the keywords: ["AI", "Machine Learning"] based on the article.""";
        var result = AiApplicationService.ExtractKeywordsFromJson(json);
        result.Should().Be("AI, Machine Learning");
    }

    [Fact]
    public void ExtractKeywordsFromJson_WithWhitespace_TrimsKeywords() {
        var json = """[ "C#" , "ASP.NET" ]""";
        var result = AiApplicationService.ExtractKeywordsFromJson(json);
        result.Should().Be("C#, ASP.NET");
    }
}
