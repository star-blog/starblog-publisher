using Avalonia.Controls;
using FluentAssertions;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.Tests.Gui.Services;

public class WindowMaterialPolicyTests {
    [Fact]
    public void Standard_RequestsNone_AndDoesNotTreatTransparentAsSuccess() {
        var hints = WindowMaterialPolicy.GetTransparencyHints(InterfaceStyle.Standard, forceSolid: false);
        hints.Should().Equal(WindowTransparencyLevel.None);
        WindowMaterialPolicy.IsFrostedMaterial(WindowTransparencyLevel.None).Should().BeFalse();
        WindowMaterialPolicy.IsFrostedMaterial(WindowTransparencyLevel.Transparent).Should().BeFalse();
        WindowMaterialPolicy.ShouldUseTransparentWindowBackground(
            InterfaceStyle.Mica, false, WindowTransparencyLevel.Transparent).Should().BeFalse();
    }

    [Fact]
    public void Mica_CandidateOrder_DoesNotIncludeReverseAcrylicToMica() {
        WindowMaterialPolicy.GetTransparencyHints(InterfaceStyle.Mica, false)
            .Should().Equal(WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur);
        WindowMaterialPolicy.GetTransparencyHints(InterfaceStyle.Acrylic, false)
            .Should().Equal(WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur);
    }

    [Fact]
    public void Accessibility_ForcesSolidHints() {
        WindowMaterialPolicy.GetTransparencyHints(InterfaceStyle.Mica, forceSolid: true)
            .Should().Equal(WindowTransparencyLevel.None);
        WindowMaterialPolicy.ResolvePresentation(InterfaceStyle.Acrylic, true, WindowTransparencyLevel.AcrylicBlur)
            .Should().Be(WindowMaterialPresentation.SolidFallback);
    }

    [Theory]
    [InlineData(InterfaceStyle.Mica, "Mica", WindowMaterialPresentation.NativeMica, "当前使用原生 Mica")]
    [InlineData(InterfaceStyle.Mica, "AcrylicBlur", WindowMaterialPresentation.CompatibilityBlur, "当前使用兼容模糊效果")]
    [InlineData(InterfaceStyle.Mica, "Blur", WindowMaterialPresentation.CompatibilityBlur, "当前使用兼容模糊效果")]
    [InlineData(InterfaceStyle.Acrylic, "AcrylicBlur", WindowMaterialPresentation.NativeAcrylic, "当前使用原生 Acrylic")]
    [InlineData(InterfaceStyle.Acrylic, "Blur", WindowMaterialPresentation.CompatibilityBlur, "当前使用兼容模糊效果")]
    [InlineData(InterfaceStyle.Acrylic, "None", WindowMaterialPresentation.SolidFallback, "当前环境不支持，已使用实色背景")]
    [InlineData(InterfaceStyle.Standard, "Mica", WindowMaterialPresentation.StandardSolid, "当前使用标准实色界面")]
    public void ResolvePresentation_MatchesRequestedStyleAndActualLevel(
        InterfaceStyle requested, string actualName, WindowMaterialPresentation expected, string description) {
        var actual = actualName switch {
            "Mica" => WindowTransparencyLevel.Mica,
            "AcrylicBlur" => WindowTransparencyLevel.AcrylicBlur,
            "Blur" => WindowTransparencyLevel.Blur,
            _ => WindowTransparencyLevel.None
        };
        var status = WindowMaterialPolicy.CreateStatus(requested, false, actual);
        status.Presentation.Should().Be(expected);
        status.Description.Should().Be(description);
    }
}
