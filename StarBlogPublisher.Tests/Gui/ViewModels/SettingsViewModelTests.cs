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
        vm.ShowPassword = true;
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
