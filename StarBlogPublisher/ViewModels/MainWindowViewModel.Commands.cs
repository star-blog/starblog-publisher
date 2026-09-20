using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class MainWindowViewModel {
    public List<ShellCommand> ShellCommands { get; } = new();
    public Func<string, bool>? CanEdit { get; set; }
    public Func<string, Task>? EditRequested { get; set; }
    public Func<bool, Task>? PaletteRequested { get; set; }
    public Action? ExitRequested { get; set; }
    private PublishViewModel? _commandDocument;

    public ShellCommand GetCommand(string id) => ShellCommands.First(command => command.Id == id);

    private void InitializeCommands() {
        bool DocumentReady() => ActivePage == Workspace && Workspace.HasDocuments && !PublishPage.IsWorkspaceBusy;
        bool AiReady() => DocumentReady() && PublishPage.IsAIEnabled && !PublishPage.IsPreparingAi && !PublishPage.IsRefiningTitle;
        void Add(string id, string group, string label, string? shortcut, Func<Task> action, Func<bool>? enabled = null, Func<bool>? check = null)
            => ShellCommands.Add(new(id, group, label, shortcut, action, enabled, check));
        Task Run(ICommand command, object? parameter = null) {
            if (!command.CanExecute(parameter)) return Task.CompletedTask;
            if (command is IAsyncRelayCommand asyncCommand) return asyncCommand.ExecuteAsync(parameter);
            command.Execute(parameter); return Task.CompletedTask;
        }
        Task ShowWorkspace(ICommand command) { ActivePage = Workspace; return Run(command); }
        Task Act(Action action) { action(); return Task.CompletedTask; }

        Add("file.new", "文件", "新建文章", "Ctrl+N", () => ShowWorkspace(Workspace.NewDocumentCommand));
        Add("file.open", "文件", "打开文章…", "Ctrl+O", () => ShowWorkspace(Workspace.OpenFilesCommand));
        Add("file.quickOpen", "文件", "快速打开…", "Ctrl+P", () => PaletteRequested?.Invoke(false) ?? Task.CompletedTask);
        Add("file.restore", "文件", "恢复上次会话", null, () => ShowWorkspace(Workspace.RestoreSessionCommand), () => Workspace.HasPreviousSession);
        Add("file.save", "文件", "保存", "Ctrl+S", () => Run(Workspace.SaveActiveCommand), DocumentReady);
        Add("file.saveAs", "文件", "另存为…", "Ctrl+Shift+S", () => PublishPage.SaveDocumentAsync(true), DocumentReady);
        Add("file.saveAll", "文件", "全部保存", null, () => Run(Workspace.SaveAllCommand), () => Workspace.HasDocuments);
        Add("file.close", "文件", "关闭文章", "Ctrl+W", () => Run(Workspace.CloseActiveCommand), DocumentReady);
        Add("file.closeAll", "文件", "关闭全部文章", null, () => Run(Workspace.CloseAllCommand), () => Workspace.HasDocuments);
        Add("file.exit", "文件", "退出", null, () => Act(() => ExitRequested?.Invoke()));

        foreach (var (id, label, shortcut) in new[] {
            ("undo", "撤销", "Ctrl+Z"), ("redo", "重做", "Ctrl+Y"), ("cut", "剪切", "Ctrl+X"),
            ("copy", "复制", "Ctrl+C"), ("paste", "粘贴", "Ctrl+V"), ("selectAll", "全选", "Ctrl+A"),
            ("find", "查找…", "Ctrl+F"), ("replace", "替换…", "Ctrl+H") }) {
            Add("edit." + id, "编辑", label, shortcut, () => EditRequested?.Invoke(id) ?? Task.CompletedTask, () => CanEdit?.Invoke(id) == true);
        }
        Add("view.commands", "查看", "命令面板…", "Ctrl+Shift+P", () => PaletteRequested?.Invoke(true) ?? Task.CompletedTask);
        Add("view.sidebar", "查看", "文件侧栏", "Ctrl+B", () => ShowWorkspace(Workspace.ToggleSidebarCommand), check: () => Workspace.IsSidebarOpen);
        Add("view.inspector", "查看", "文章属性", "Ctrl+Alt+B", () => Run(PublishPage.ToggleInspectorCommand), DocumentReady, () => PublishPage.IsInspectorOpen);
        Add("view.tasks", "查看", "任务面板", "Ctrl+J", () => ShowWorkspace(Workspace.ToggleTasksCommand), check: () => Workspace.IsTaskPanelOpen);
        foreach (var mode in new[] { ("Source", "源码"), ("Split", "分栏"), ("Preview", "预览") }) {
            Add("view." + mode.Item1.ToLowerInvariant(), "查看", mode.Item2, null,
                () => Run(PublishPage.SetEditorModeCommand, mode.Item1), DocumentReady,
                () => PublishPage.EditorMode.ToString() == mode.Item1);
        }
        Add("view.next", "查看", "下一个标签", "Ctrl+Tab", () => Run(Workspace.NextDocumentCommand), () => ActivePage == Workspace && Workspace.HasDocuments);
        Add("view.focus", "查看", "专注模式", "Ctrl+Shift+F11", () => ShowWorkspace(Workspace.ToggleFocusCommand), () => Workspace.HasDocuments, () => Workspace.IsFocusMode);
        Add("view.theme", "查看", "深色主题", null, () => Run(ToggleThemeCommand), check: () => IsDarkTheme);
        Add("view.settings", "查看", "设置", "Ctrl+OemComma", () => Act(() => ActivePage = SettingsPage));

        Add("article.title", "文章", "优化标题", null, () => Run(PublishPage.RefineTitleWithAICommand), AiReady);
        Add("article.summary", "文章", "生成摘要", null, () => Run(PublishPage.RegenerateDescriptionCommand), AiReady);
        Add("article.keywords", "文章", "生成关键词", null, () => Run(PublishPage.GenerateKeywordsCommand), AiReady);
        Add("article.slug", "文章", "生成 Slug", null, () => Run(PublishPage.GenerateSlugCommand), AiReady);
        Add("article.metadata", "文章", "补全文章属性", null, () => Run(PublishPage.FillMetadataCommand), AiReady);
        Add("article.images", "文章", "分析文章图片", null, () => Run(PublishPage.AnalyzeImagesCommand), DocumentReady);
        Add("article.cover", "文章", "生成封面", null, () => Run(PublishPage.ShowCoverPromptCommand), DocumentReady);
        Add("article.review", "文章", "发布前审校", null, () => Run(PublishPage.ReviewBeforePublishCommand), AiReady);
        Add("article.reload", "文章", "从磁盘重新加载", null, () => Run(PublishPage.ReloadDocumentCommand), () => DocumentReady() && PublishPage.CurrentFilePath != null);

        Add("publish.blog", "发布", "发布到 StarBlog", null, () => Run(PublishPage.PublishCommand), () => DocumentReady() && PublishPage.CanPublish);
        Add("publish.wechat", "发布", "公众号排版与草稿…", null, () => Run(PublishPage.ShowWeChatPublisherCommand), () => DocumentReady() && PublishPage.CurrentFilePath != null);
        Add("publish.tasks", "发布", "查看任务", null, () => Act(() => { ActivePage = Workspace; Workspace.IsTaskPanelOpen = true; }));
        Add("publish.result", "发布", "查看当前文章发布结果", null, () => Run(PublishPage.ShowPublishResultCommand), () => DocumentReady() && PublishPage.HasPublishResult);
        Add("help.shortcuts", "帮助", "键盘快捷键", null, () => GuiHost.AlertAsync("键盘快捷键",
            string.Join(Environment.NewLine, ShellCommands.Where(c => c.Shortcut != null).Select(c => $"{c.Label}    {c.Shortcut}"))));
        Add("help.usage", "帮助", "工作区使用说明", null, () => GuiHost.AlertAsync("文章工作区",
            "从文件菜单打开或新建文章；Ctrl+P 查找已打开及最近文章，Ctrl+Shift+P 查找命令。\n\n正文保存在 Markdown，文章属性保存在同目录 .starblog.json。未保存文章关闭时会询问。\n\n左侧大纲可跳转章节；右侧设置发布属性；底部任务面板可查看各文章状态。"));
        Add("help.about", "帮助", "关于 StarBlog Publisher", null, () => Act(() => ActivePage = AboutPage));

        Workspace.PropertyChanged += (_, e) => {
            if (e.PropertyName == nameof(Workspace.CurrentDocument)) ObserveCommandDocument();
            RefreshCommands();
        };
        PropertyChanged += (_, _) => RefreshCommands();
        ObserveCommandDocument();
    }

    private void ObserveCommandDocument() {
        if (_commandDocument != null) _commandDocument.PropertyChanged -= OnCommandDocumentChanged;
        _commandDocument = PublishPage;
        _commandDocument.PropertyChanged += OnCommandDocumentChanged;
    }

    private void OnCommandDocumentChanged(object? sender, PropertyChangedEventArgs e) => RefreshCommands();
    public void RefreshCommands() { foreach (var command in ShellCommands) command.Refresh(); }
}
