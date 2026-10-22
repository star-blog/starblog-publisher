using FluentAssertions;
using StarBlogPublisher.Utils;

namespace StarBlogPublisher.Tests.Utils;

public class WeChatTextLengthTests {
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("abc", 1.5)]
    [InlineData("A1 #", 2)]
    [InlineData("中文，。", 4)]
    [InlineData("中A1,。 ", 4)]
    [InlineData("\u00FF\u0100", 1.5)]
    [InlineData("\r\n", 1)]
    [InlineData("\U0001F600", 2)]
    [InlineData("e\u0301", 1.5)]
    [InlineData("\U0001F469\u200D\U0001F4BB", 5)]
    public void GetLength_CountsWeightedWords(string? value, double expected) {
        WeChatTextLength.GetLength(value).Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(null, 1, "")]
    [InlineData("", 1, "")]
    [InlineData("abc", 0, "")]
    [InlineData("a", 1, "a")]
    [InlineData("abc", 1, "ab")]
    [InlineData("中a", 1, "中")]
    [InlineData("a中b", 1, "a")]
    [InlineData("a中b", 2, "a中b")]
    [InlineData("\u00FF\u0100", 1, "\u00FF")]
    [InlineData("\U0001F600", 1, "")]
    [InlineData("\U0001F600", 2, "\U0001F600")]
    [InlineData("a\U0001F600b", 2, "a")]
    [InlineData("a\U0001F600b", 3, "a\U0001F600b")]
    [InlineData("e\u0301x", 1, "")]
    [InlineData("e\u0301x", 2, "e\u0301x")]
    [InlineData("a e\u0301z", 2, "a ")]
    [InlineData("\U0001F469\u200D\U0001F4BBx", 4, "")]
    [InlineData("\U0001F469\u200D\U0001F4BBx", 5, "\U0001F469\u200D\U0001F4BB")]
    public void Truncate_KeepsTheLongestPrefixOfCompleteTextElements(string? value, int limit, string expected) {
        WeChatTextLength.Truncate(value, limit).Should().Be(expected);
    }

    [Fact]
    public void Truncate_RejectsNegativeLimits() {
        var action = () => WeChatTextLength.Truncate("text", -1);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
