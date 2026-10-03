using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using AvaloniaEdit;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

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

        var api = ApiService.Instance;
        var auth = new AuthApplicationService(AppSettings.Instance, GlobalState.Instance, api);
        var editorVm = new SitePostEditorViewModel(shell, new ArticleLibraryApplicationService(api, auth),
            new CategoryApplicationService(api, auth), "preview", _ => { }) {
            Title = "团队 Web 开发规范", Content = "# 团队 Web 开发规范\n\n## 一、总体约定\n\n统一代码风格，保持前后端接口一致。\n\n```csharp\nvar title = \"StarBlog\";\n```\n\n" + string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"### {i}. 开发约定\n\n提交前检查代码与文章内容。")),
            Summary = "团队开发与发布约定。", Slug = "team-web-guide", PostUrl = "https://example.com/p/team-web-guide", IsPublish = true
        };
        var category = new Category { Id = 1, Text = "开发笔记" };
        editorVm.Categories.Add(category);
        editorVm.SelectedCategory = category;
        editorVm.IsLoaded = true;
        editorVm.StatusMessage = "已加载线上文章";
        library.OpenStackPage(editorVm, "编辑文章");
        await Task.Delay(200);
        var editorView = window.GetVisualDescendants().OfType<SitePostEditorView>().First();
        var source = editorView.FindControl<TextEditor>("OnlineEditor")!;
        if (source.Text != editorVm.Content) throw new Exception("Remote content did not reach Markdown editor");
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
