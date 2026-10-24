using Avalonia;
using FluentAssertions;
using FluentAvalonia.UI.Controls;
using FluentIcons.Avalonia.Fluent;
using FluentIcons.Common;
using StarBlogPublisher.Converters;
using AppShellPageId = StarBlogPublisher.ViewModels.ShellPageId;

namespace StarBlogPublisher.Tests.Gui;

public class NavigationMenuIconConverterTests {
    static NavigationMenuIconConverterTests() {
        if (Application.Current != null) return;
        AppBuilder.Configure<Application>()
            .UseWin32()
            .UseSkia()
            .UseHarfBuzz()
            .SetupWithoutStarting();
    }

    [Fact]
    public void Convert_WeChatTag_UsesWeChatMark() {
        var icon = NavigationMenuIconConverter.Instance.Convert(
            [Icon.Mail, nameof(AppShellPageId.WeChat)], typeof(FAIconSource), null, System.Globalization.CultureInfo.InvariantCulture);

        var path = icon.Should().BeOfType<FAPathIconSource>().Subject;
        var bounds = path.Data!.Bounds;
        bounds.Width.Should().BeApproximately(bounds.Height, 0.5);
        bounds.Width.Should().BeGreaterThan(500);
    }

    [Fact]
    public void Convert_OtherItems_UseFluentIcon() {
        var icon = NavigationMenuIconConverter.Instance.Convert(
            [Icon.News, nameof(AppShellPageId.SitePosts)], typeof(FAIconSource), null, System.Globalization.CultureInfo.InvariantCulture);

        var fluent = icon.Should().BeOfType<FluentIconSource>().Subject;
        fluent.Icon.Should().Be(Icon.News);
    }
}
