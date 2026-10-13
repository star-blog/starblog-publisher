using FluentAssertions;
using Moq;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Tests.Gui.ViewModels;

[Collection("AppSettings")]
public sealed class SettingsViewModelTests {
    private static MainWindowViewModel Shell() => new(Mock.Of<IHttpClientFactory>(), initializeSession: false,
        workspaceHistoryPath: Path.Combine(Path.GetTempPath(), "starblog-settings-tests", Guid.NewGuid() + ".json"));

    [Fact]
    public void DraftChanges_RevertToClean_AndIgnorePresentationState() {
        var vm = Shell().SettingsPage;
        vm.HasChanges.Should().BeFalse();
        var original = vm.Username;
        vm.Username = original + " edit";
        vm.HasChanges.Should().BeTrue();
        vm.Username = original;
        vm.SelectedSection = 2;
        vm.IsWeChatAdvancedExpanded = true;
        vm.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void SwitchingAiProfiles_PreservesDrafts_WithoutMutatingSavedProfiles() {
        var vm = Shell().SettingsPage;
        var original = vm.CurrentProfile!;
        var savedKey = AppSettings.Instance.AIProfiles.FirstOrDefault()?.Key;
        vm.AIKey = "unsaved-key";
        vm.AIModel = "unsaved-model";
        var other = new AIProfile { Name = "Other", Key = "other-key", Model = "other-model" };
        vm.Profiles.Add(other);
        vm.CurrentProfile = other;
        vm.AIKey.Should().Be("other-key");
        vm.CurrentProfile = original;
        vm.AIKey.Should().Be("unsaved-key");
        vm.AIModel.Should().Be("unsaved-model");
        AppSettings.Instance.AIProfiles.FirstOrDefault()?.Key.Should().Be(savedKey);
        vm.CancelCommand.Execute(null);
        vm.Profiles.Should().NotContain(p => p.Name == "Other");
        vm.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void SwitchingAccounts_PreservesEdits_AndRelayChoiceRestoresDraftUrl() {
        var vm = Shell().SettingsPage;
        var first = vm.CurrentWeChatAccount!;
        vm.WeChatAuthor = "Draft author";
        vm.UseWeChatRelay = true;
        vm.WeChatApiBaseUrl = "https://relay.example.com/";
        vm.WeChatApiAuthorization = "Bearer draft-token";
        vm.UseWeChatRelay = false;
        vm.WeChatApiBaseUrl.Should().Be(WeChatHttpClientRegistration.OfficialApiBaseUrl);
        vm.WeChatApiAuthorization.Should().BeEmpty();
        vm.UseWeChatRelay = true;
        vm.WeChatApiBaseUrl.Should().Be("https://relay.example.com/");
        vm.WeChatApiAuthorization.Should().Be("Bearer draft-token");
        var second = new WeChatAccountProfile { Name = "Other" };
        vm.WeChatAccounts.Add(second);
        vm.CurrentWeChatAccount = second;
        vm.CurrentWeChatAccount = first;
        vm.WeChatAuthor.Should().Be("Draft author");
        vm.UseWeChatRelay.Should().BeTrue();
        vm.CancelCommand.Execute(null);
        vm.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void ThemePreview_IsNotPersisted_AndCancelRestoresIt() {
        var shell = Shell();
        var savedMode = AppSettings.Instance.ThemeMode;
        var next = savedMode == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        shell.SettingsPage.ThemeMode = next;
        shell.ThemeMode.Should().Be(next);
        shell.IsDarkTheme.Should().Be(next == ThemeMode.Dark);
        AppSettings.Instance.ThemeMode.Should().Be(savedMode);
        shell.SettingsPage.CancelCommand.Execute(null);
        shell.ThemeMode.Should().Be(savedMode);
        shell.SettingsPage.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void AppearanceDraft_PreviewsNineCombinations_AndCancelRestoresBoth() {
        var shell = Shell();
        var savedMode = AppSettings.Instance.ThemeMode;
        var savedStyle = AppSettings.Instance.InterfaceStyle;
        foreach (var theme in new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark }) {
            foreach (var style in new[] { InterfaceStyle.Standard, InterfaceStyle.Mica, InterfaceStyle.Acrylic }) {
                shell.SettingsPage.ThemeMode = theme;
                shell.SettingsPage.InterfaceStyle = style;
                shell.ThemeMode.Should().Be(theme);
                shell.InterfaceStyle.Should().Be(style);
                AppSettings.Instance.ThemeMode.Should().Be(savedMode);
                AppSettings.Instance.InterfaceStyle.Should().Be(savedStyle);
            }
        }

        shell.SettingsPage.CancelCommand.Execute(null);
        shell.ThemeMode.Should().Be(savedMode);
        shell.InterfaceStyle.Should().Be(savedStyle);
        shell.SettingsPage.ThemeMode.Should().Be(savedMode);
        shell.SettingsPage.InterfaceStyle.Should().Be(savedStyle);
        shell.SettingsPage.HasChanges.Should().BeFalse();
    }

    [Fact]
    public void ToggleTheme_OnSettings_ChangesColorModeOnly() {
        var shell = Shell();
        var savedStyle = AppSettings.Instance.InterfaceStyle;
        shell.ActivePage = shell.SettingsPage;
        var nextStyle = savedStyle == InterfaceStyle.Mica ? InterfaceStyle.Acrylic : InterfaceStyle.Mica;
        shell.SettingsPage.InterfaceStyle = nextStyle;
        var previousTheme = shell.ThemeMode;
        shell.ToggleThemeCommand.Execute(null);
        shell.InterfaceStyle.Should().Be(nextStyle);
        shell.SettingsPage.InterfaceStyle.Should().Be(nextStyle);
        shell.ThemeMode.Should().NotBe(previousTheme);
        AppSettings.Instance.InterfaceStyle.Should().Be(savedStyle);
        shell.SettingsPage.CancelCommand.Execute(null);
    }

    [Fact]
    public void ToggleTheme_AwayFromSettings_DoesNotResetStyle() {
        var shell = Shell();
        var savedTheme = AppSettings.Instance.ThemeMode;
        var savedStyle = AppSettings.Instance.InterfaceStyle;
        shell.PreviewInterfaceStyle(InterfaceStyle.Mica);
        shell.ActivePage = shell.AboutPage;
        var previousTheme = shell.ThemeMode;
        try {
            shell.ToggleThemeCommand.Execute(null);
            shell.InterfaceStyle.Should().Be(InterfaceStyle.Mica);
            shell.ThemeMode.Should().NotBe(previousTheme);
            AppSettings.Instance.InterfaceStyle.Should().Be(savedStyle);
        }
        finally {
            shell.PreviewInterfaceStyle(savedStyle);
            shell.ApplyTheme(savedTheme);
        }
    }

    [Fact]
    public void MaterialStatusHint_DoesNotDirtyDraft() {
        var shell = Shell();
        var vm = shell.SettingsPage;
        vm.HasChanges.Should().BeFalse();
        var original = vm.MaterialStatusDescription;
        shell.MaterialStatusDescription = original == "当前使用兼容模糊效果"
            ? "当前环境不支持，已使用实色背景"
            : "当前使用兼容模糊效果";
        vm.MaterialStatusDescription.Should().Be(shell.MaterialStatusDescription);
        vm.HasChanges.Should().BeFalse();
        vm.InterfaceStyle = vm.InterfaceStyle == InterfaceStyle.Mica ? InterfaceStyle.Acrylic : InterfaceStyle.Mica;
        vm.HasChanges.Should().BeTrue();
        shell.MaterialStatusDescription = original;
        vm.HasChanges.Should().BeTrue();
        vm.CancelCommand.Execute(null);
    }

    [Fact]
    public void RestoreAppearanceDefaults_PreviewsSystemAndStandard() {
        var shell = Shell();
        var savedMode = AppSettings.Instance.ThemeMode;
        var savedStyle = AppSettings.Instance.InterfaceStyle;
        shell.SettingsPage.ThemeMode = ThemeMode.Dark;
        shell.SettingsPage.InterfaceStyle = InterfaceStyle.Acrylic;
        shell.SettingsPage.RestoreAppearanceDefaultsCommand.Execute(null);
        shell.SettingsPage.ThemeMode.Should().Be(ThemeMode.System);
        shell.SettingsPage.InterfaceStyle.Should().Be(InterfaceStyle.Standard);
        shell.ThemeMode.Should().Be(ThemeMode.System);
        shell.InterfaceStyle.Should().Be(InterfaceStyle.Standard);
        AppSettings.Instance.ThemeMode.Should().Be(savedMode);
        AppSettings.Instance.InterfaceStyle.Should().Be(savedStyle);
        shell.SettingsPage.CancelCommand.Execute(null);
        shell.ThemeMode.Should().Be(savedMode);
        shell.InterfaceStyle.Should().Be(savedStyle);
    }

    [Fact]
    public void LeaveDiscard_RestoresInterfaceStyle() {
        var shell = Shell();
        shell.ActivePage = shell.SettingsPage;
        var savedStyle = AppSettings.Instance.InterfaceStyle;
        var next = savedStyle == InterfaceStyle.Mica ? InterfaceStyle.Acrylic : InterfaceStyle.Mica;
        shell.SettingsPage.InterfaceStyle = next;
        shell.SettingsPage.ConfirmLeaveOverride = () => Task.FromResult(SettingsViewModel.LeaveChoice.Discard);
        shell.ActivePage = shell.AboutPage;
        shell.ActivePage.Should().BeSameAs(shell.AboutPage);
        shell.SettingsPage.HasChanges.Should().BeFalse();
        shell.SettingsPage.InterfaceStyle.Should().Be(savedStyle);
        shell.InterfaceStyle.Should().Be(savedStyle);
    }

    [Fact]
    public void InvalidSave_KeepsInterfaceStyleDraft() {
        var vm = Shell().SettingsPage;
        var savedStyle = AppSettings.Instance.InterfaceStyle;
        var next = savedStyle == InterfaceStyle.Mica ? InterfaceStyle.Acrylic : InterfaceStyle.Mica;
        vm.InterfaceStyle = next;
        vm.UseCustomBackend = true;
        vm.BackendUrl = "invalid-url";
        vm.SaveCommand.Execute(null);
        vm.HasChanges.Should().BeTrue();
        vm.HasValidationError.Should().BeTrue();
        vm.InterfaceStyle.Should().Be(next);
        AppSettings.Instance.InterfaceStyle.Should().Be(savedStyle);
        vm.CancelCommand.Execute(null);
    }

    [Fact]
    public void InterfaceStylePreview_DoesNotRegenerateArticleHtml() {
        var shell = Shell();
        shell.Workspace.NewDocumentCommand.Execute(null);
        var document = shell.PublishPage;
        document.ArticleContent = "# 材质预览\n\n正文";
        document.CaretOffset = 4;
        document.EditorMode = MarkdownEditorMode.Split;
        var uri = document.PreviewUri;
        uri.Should().NotBeNull();
        var html = File.ReadAllText(uri!.LocalPath);
        var dirty = document.IsDocumentDirty;
        shell.SettingsPage.InterfaceStyle = InterfaceStyle.Mica;
        document.PreviewUri.Should().Be(uri);
        document.ArticleContent.Should().Be("# 材质预览\n\n正文");
        document.CaretOffset.Should().Be(4);
        document.EditorMode.Should().Be(MarkdownEditorMode.Split);
        document.IsDocumentDirty.Should().Be(dirty);
        File.ReadAllText(uri.LocalPath).Should().Be(html);
        shell.SettingsPage.CancelCommand.Execute(null);
    }

    [Fact]
    public async Task InvalidSave_KeepsDraft_AndSelectsFailingCategory() {
        var vm = Shell().SettingsPage;
        vm.UseCustomBackend = true;
        vm.BackendUrl = "invalid-url";
        vm.SelectedSection = 0;
        vm.ConfirmLeaveOverride = () => Task.FromResult(SettingsViewModel.LeaveChoice.Save);
        (await vm.CanLeaveAsync()).Should().BeFalse();
        vm.SelectedSection.Should().Be(1);
        vm.HasValidationError.Should().BeTrue();
        vm.HasChanges.Should().BeTrue();
        vm.BackendUrl.Should().Be("invalid-url");
    }

    [Fact]
    public async Task Navigation_WaitsForDecision_AndCancelKeepsEditing() {
        var shell = Shell();
        shell.ActivePage = shell.SettingsPage;
        shell.SettingsPage.Username += " draft";
        var pending = new TaskCompletionSource<SettingsViewModel.LeaveChoice>();
        shell.SettingsPage.ConfirmLeaveOverride = () => pending.Task;
        shell.ActivePage = shell.AboutPage;
        shell.ActivePage.Should().BeSameAs(shell.SettingsPage);
        pending.SetResult(SettingsViewModel.LeaveChoice.Cancel);
        await Task.Yield();
        shell.ActivePage.Should().BeSameAs(shell.SettingsPage);
        shell.SettingsPage.HasChanges.Should().BeTrue();
        shell.SettingsPage.ConfirmLeaveOverride = () => Task.FromResult(SettingsViewModel.LeaveChoice.Discard);
        shell.ActivePage = shell.AboutPage;
        shell.ActivePage.Should().BeSameAs(shell.AboutPage);
        shell.SettingsPage.HasChanges.Should().BeFalse();
    }
}
