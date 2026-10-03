using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;
using StarBlogPublisher.Views.Controls;

namespace StarBlogPublisher.DesktopTests;

internal sealed partial class WorkspaceScenarios {
    public async Task SitePostsLayout() {
        // Populate disposable view models: this scenario never contacts or changes a real site.
        var library = shell.SitePostsPage;
        library.HasLoaded = true;
        shell.ActivePage = library;
        await Task.Delay(200);
        var listView = window.GetVisualDescendants().OfType<SitePostsView>().First();
        if (!listView.FindControl<StackPanel>("EmptyState")!.IsVisible)
            throw new Exception("Logged-out library has no empty state");
        Capture(window, Path.Combine(output, "site-posts-login.png"));

        for (var i = 0; i < 28; i++) library.Posts.Add(new SitePostListItem(new BlogPost {
            Id = $"preview-{i}", Title = i == 0 ? "团队 Web 开发规范：DjangoStarter 与 Next.js 的工程约定" : $"第 {i + 1} 篇站点文章",
            Category = new Category { Id = 1, Text = "开发笔记" }, IsPublish = i % 3 != 0,
            LastUpdateTime = new DateTime(2026, 10, 3, 9, 30, 0), Slug = $"preview-{i}"
        }));
        library.TotalCount = 48;
        library.TotalPages = 3;
        library.StatusMessage = "已加载线上文章";
        library.SelectedPost = library.Posts[0];
        await Task.Delay(150);
        if (listView.FindControl<StackPanel>("EmptyState")!.IsVisible)
            throw new Exception("Empty state overlays populated rows");
        await CheckSiteLayout("site-posts-list");
        var childCategory = new Category { Id = 2, Text = "工程实践" };
        var category = new Category { Id = 1, Text = "开发笔记", Nodes = [childCategory] };
        library.HasLoaded = false; // Exercise selection bindings without requesting the real site.
        library.CategoryFilters.Add(category);
        await CheckCategoryPicker(listView.FindControl<Button>("CategoryFilterButton")!, category, childCategory,
            chosen => {
                if (!ReferenceEquals(library.SelectedCategoryFilter, chosen)) throw new Exception("Category tree did not update list filter");
            }, "site-posts-category-filter");
        library.SelectedCategoryFilter = SitePostsViewModel.AllCategoriesSentinel;
        library.HasLoaded = true;

        var api = ApiService.Instance;
        var auth = new AuthApplicationService(AppSettings.Instance, GlobalState.Instance, api);
        var editorVm = new SitePostEditorViewModel(shell, new ArticleLibraryApplicationService(api, auth),
            new CategoryApplicationService(api, auth), "preview", _ => { }) {
            Title = "团队 Web 开发规范", Content = "# 团队 Web 开发规范\n\n## 一、总体约定\n\n统一代码风格，保持前后端接口一致。\n\n```csharp\nvar title = \"StarBlog\";\n```\n\n" + string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"### {i}. 开发约定\n\n提交前检查代码与文章内容。")),
            Summary = "团队开发与发布约定。", Slug = "team-web-guide", PostUrl = "https://example.com/p/team-web-guide", IsPublish = true
        };
        editorVm.Categories.Add(category);
        editorVm.SelectedCategory = childCategory;
        editorVm.IsLoaded = true;
        editorVm.StatusMessage = "已加载线上文章";
        library.OpenStackPage(editorVm, "编辑文章");
        await Task.Delay(200);
        var editorView = window.GetVisualDescendants().OfType<SitePostEditorView>().First();
        var source = editorView.FindControl<TextEditor>("OnlineEditor")!;
        if (source.Text != editorVm.Content) throw new Exception("Remote content did not reach Markdown editor");
        await CheckCategoryPicker(editorView.FindControl<Button>("EditorCategoryButton")!, category, childCategory,
            chosen => {
                if (!ReferenceEquals(editorVm.SelectedCategory, chosen)) throw new Exception("Category tree did not update online article");
            }, "site-posts-editor-category");
        source.Document.Insert(0, "编辑测试\n");
        if (editorVm.Content != source.Text || !editorVm.IsDirty || !editorVm.CanSave)
            throw new Exception("Markdown editor changes did not reach save state");
        await CheckSiteLayout("site-posts-editor");
        editorVm.Content = "# 服务端返回的正文\n\n保存后的内容。";
        if (source.Text != editorVm.Content) throw new Exception("Reloaded content did not update editor");

        var categories = new CategoryManageViewModel(new CategoryApplicationService(api, auth));
        categories.Items.Add(new CategoryManageItem { Id = 1, Name = "开发笔记", Visible = true });
        categories.Items.Add(new CategoryManageItem { Id = 2, ParentId = 1, Depth = 1, Name = "工程实践", Visible = true });
        categories.Items.Add(new CategoryManageItem { Id = 3, Name = "生活记录", Visible = false });
        categories.SelectedItem = categories.Items[0];
        categories.StatusMessage = "共 3 个分类";
        library.OpenStackPage(categories, "分类管理");
        await Task.Delay(150);
        await CheckSiteLayout("site-posts-categories");
        library.ActiveStackPage = null;
        library.IsStackNavigating = false;
        library.Posts.Clear();
        shell.ActivePage = shell.Workspace;
        var localPath = Path.Combine(output, "category-picker.md");
        await File.WriteAllTextAsync(localPath, "# 分类选择验证\n\n共享分类树。");
        await shell.Workspace.OpenPathAsync(localPath);
        shell.PublishPage.Categories = new System.Collections.ObjectModel.ObservableCollection<Category> { category };
        shell.PublishPage.IsLoggedIn = true;
        await Task.Delay(150);
        var inspector = window.GetVisualDescendants().OfType<ArticleInspectorView>().First();
        await CheckCategoryPicker(inspector.FindControl<Button>("WorkspaceCategoryButton")!, category, childCategory,
            chosen => {
                if (!ReferenceEquals(shell.PublishPage.SelectedCategory, chosen)) throw new Exception("Shared tree broke workspace category selection");
            }, "workspace-category", showManagementActions: true);
        shell.PublishPage.IsLoggedIn = false;
    }

    private async Task CheckCategoryPicker(Button button, Category parent, Category child, Action<Category> check, string name, bool showManagementActions = false) {
        var flyout = (Flyout)button.Flyout!;
        flyout.ShowAt(button);
        await Task.Delay(150);
        var picker = (CategoryPicker)flyout.Content!;
        if (picker.ShowManagementActions != showManagementActions) throw new Exception("Picker management actions have the wrong visibility");
        picker.FindControl<TextBox>("CategorySearch")!.Text = child.DisplayName;
        await Task.Delay(150);
        var tree = picker.FindControl<TreeView>("CategoryTree")!;
        var buttons = tree.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("CategoryPickerItem")).ToArray();
        var parentButton = buttons.First(b => b.DataContext is Category c && c.Id == parent.Id);
        if (ReferenceEquals(parentButton.DataContext, parent)) throw new Exception("Search did not retain filtered ancestor branch");
        parentButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(parent); // Searching creates ancestor copies; selection must resolve the original model.
        buttons.First(b => b.DataContext is Category c && c.Id == child.Id).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(child);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)picker.Bounds.Width, (int)picker.Bounds.Height));
        bitmap.Render(picker);
        bitmap.Save(Path.Combine(output, name + ".png"), PngBitmapEncoderOptions.Default);
        flyout.Hide();
        await Task.Delay(100);
    }

    private async Task CheckSiteLayout(string name) {
        foreach (var width in new[] { 1280, 800 }) {
            window.Width = width;
            await Task.Delay(150);
            var controls = window.GetVisualDescendants().OfType<ScrollViewer>().Where(s => s.IsEffectivelyVisible);
            if (controls.Any(s => s.Extent.Width > s.Viewport.Width + 1))
                throw new Exception($"{name} overflows horizontally at {width}px");
            Capture(window, Path.Combine(output, $"{name}-{width}.png"));
        }
        shell.PreviewTheme(ThemeMode.Dark);
        await Task.Delay(150);
        if (window.ActualThemeVariant != ThemeVariant.Dark) throw new Exception("Dark theme was not applied");
        Capture(window, Path.Combine(output, $"{name}-dark.png"));
        shell.PreviewTheme(ThemeMode.Light);
        window.Width = 1280;
        await Task.Delay(100);
    }
}
