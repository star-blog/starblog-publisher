using System.CommandLine;
using StarBlogPublisher.Models;
using StarBlogPublisher.Models.Dtos;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Commands;

public static class PostCommand {
    public static Command Build() {
        var command = new Command("post", "文章管理");

        // post publish
        var fileArg = new Argument<string>("file") { Description = "Markdown 文件路径" };
        var categoryOpt = new Option<int>("--category") { Description = "分类 ID", Required = true };
        var titleOpt = new Option<string?>("--title") { Description = "文章标题（默认使用文件名）" };
        var summaryOpt = new Option<string?>("--summary") { Description = "文章摘要" };
        var slugOpt = new Option<string?>("--slug") { Description = "URL slug" };
        var draftOpt = new Option<bool>("--draft") { Description = "保存为草稿（不直接发布）" };
        var autoOpt = new Option<bool>("--auto") { Description = "AI 自动生成标题、摘要、Slug（覆盖手动指定的值）" };
        var yesOpt = new Option<bool>("-y") { Description = "跳过确认，直接发布" };

        var publishCmd = new Command("publish", "发布文章") { categoryOpt, titleOpt, summaryOpt, slugOpt, draftOpt, autoOpt, yesOpt };
        publishCmd.Arguments.Add(fileArg);
        publishCmd.SetAction(parseResult => {
            var file = parseResult.GetValue(fileArg)!;
            var categoryId = parseResult.GetValue(categoryOpt);
            var title = parseResult.GetValue(titleOpt);
            var summary = parseResult.GetValue(summaryOpt);
            var slug = parseResult.GetValue(slugOpt);
            var draft = parseResult.GetValue(draftOpt);
            var auto = parseResult.GetValue(autoOpt);
            var skipConfirm = parseResult.GetValue(yesOpt);

            if (!File.Exists(file)) {
                Console.Error.WriteLine($"文件不存在: {file}");
                return 1;
            }

            var content = File.ReadAllText(file);
            string postTitle;

            if (auto) {
                var ai = new AiApplicationService(AiService.Instance, AppSettings.Instance);
                if (!ai.IsEnabled) {
                    Console.Error.WriteLine("AI 功能未启用，请先在设置中配置 AI");
                    return 1;
                }

                try {
                    var originalTitle = title ?? Path.GetFileNameWithoutExtension(file);

                    Console.WriteLine("AI 正在生成标题...");
                    var generatedTitle = Task.Run(() => ai.RefineTitleAsync(originalTitle, content)).Result;
                    if (string.IsNullOrWhiteSpace(generatedTitle)) generatedTitle = originalTitle;

                    Console.WriteLine("AI 正在生成 Slug...");
                    var generatedSlug = Task.Run(() => ai.GenerateSlugAsync(generatedTitle)).Result;

                    Console.WriteLine("AI 正在生成摘要...");
                    var generatedSummary = Task.Run(() => ai.GenerateSummaryAsync(generatedTitle, content)).Result;

                    postTitle = generatedTitle;
                    slug = generatedSlug;
                    summary = generatedSummary;
                }
                catch (Exception ex) {
                    Console.Error.WriteLine($"AI 生成失败: {ex.Message}");
                    return 1;
                }

                while (true) {
                    Console.WriteLine();
                    Console.WriteLine("========== AI 生成结果 ==========");
                    Console.WriteLine($"  标题: {postTitle}");
                    Console.WriteLine($"  摘要: {summary}");
                    Console.WriteLine($"  Slug: {slug}");
                    Console.WriteLine("=================================");

                    if (skipConfirm) {
                        Console.WriteLine("已跳过确认，直接发布。");
                        break;
                    }

                    Console.Write("确认发布？(y=确认, 其他键=编辑) ");
                    var input = Console.ReadLine()?.Trim().ToLower();
                    if (input == "y") break;

                    Console.WriteLine();
                    Console.WriteLine("请选择要修改的字段:");
                    Console.WriteLine("  1. 标题");
                    Console.WriteLine("  2. 摘要");
                    Console.WriteLine("  3. Slug");
                    Console.Write("输入编号 (1/2/3): ");
                    var choice = Console.ReadLine()?.Trim();

                    switch (choice) {
                        case "1":
                            Console.Write($"当前标题: {postTitle}\n新标题: ");
                            var newTitle = Console.ReadLine()?.Trim();
                            if (!string.IsNullOrEmpty(newTitle)) postTitle = newTitle;
                            break;
                        case "2":
                            Console.Write($"当前摘要: {summary}\n新摘要: ");
                            var newSummary = Console.ReadLine()?.Trim();
                            if (!string.IsNullOrEmpty(newSummary)) summary = newSummary;
                            break;
                        case "3":
                            Console.Write($"当前 Slug: {slug}\n新 Slug: ");
                            var newSlug = Console.ReadLine()?.Trim();
                            if (!string.IsNullOrEmpty(newSlug)) slug = newSlug;
                            break;
                        default:
                            Console.WriteLine("无效选择，返回确认。");
                            break;
                    }
                }
            }
            else {
                postTitle = title ?? Path.GetFileNameWithoutExtension(file);
            }

            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            var publishService = new ArticlePublishApplicationService(
                ApiService.Instance, authService, AppSettings.Instance);

            Console.WriteLine($"正在发布文章: {postTitle}");

            Task.Run(async () => {
                var result = await publishService.PublishAsync(
                    file, postTitle, content, summary ?? "",
                    categoryId, slug, !draft,
                    onProgress: (step, msg) => {
                        Console.WriteLine($"  [{step}%] {msg}");
                    });

                if (result.Success && result.Post != null) {
                    Console.WriteLine($"发布成功! 文章 ID: {result.Post.Id}");
                    if (!string.IsNullOrEmpty(result.Post.Slug)) {
                        Console.WriteLine($"URL: {AppSettings.Instance.BackendUrl}/p/{result.Post.Slug}");
                    }
                }
                else {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                }
            }).Wait();
            return Environment.ExitCode;
        });

        // post get
        var idArg = new Argument<string>("id") { Description = "文章 ID" };

        var getCmd = new Command("get", "获取文章详情");
        getCmd.Arguments.Add(idArg);
        getCmd.SetAction(parseResult => {
            var id = parseResult.GetValue(idArg)!;

            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            var publishService = new ArticlePublishApplicationService(
                ApiService.Instance, authService, AppSettings.Instance);

            Task.Run(async () => {
                var result = await publishService.GetPostAsync(id);
                if (result.Success && result.Post != null) {
                    var post = result.Post;
                    Console.WriteLine($"ID:       {post.Id}");
                    Console.WriteLine($"标题:     {post.Title}");
                    Console.WriteLine($"Slug:     {post.Slug}");
                    Console.WriteLine($"分类:     {post.Category?.Text ?? "未分类"}");
                    Console.WriteLine($"状态:     {(post.IsPublish ? "已发布" : "草稿")}");
                    Console.WriteLine($"创建时间: {post.CreationTime}");
                    Console.WriteLine($"更新时间: {post.LastUpdateTime}");
                    if (!string.IsNullOrEmpty(post.Summary)) {
                        Console.WriteLine($"摘要:     {post.Summary}");
                    }
                }
                else {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                }
            }).Wait();
            return Environment.ExitCode;
        });

        command.Subcommands.Add(publishCmd);
        command.Subcommands.Add(getCmd);
        command.Subcommands.Add(BuildListCommand());
        command.Subcommands.Add(BuildUpdateCommand());
        command.Subcommands.Add(BuildDeleteCommand());
        command.Subcommands.Add(BuildPullCommand());
        return command;
    }

    private static ArticleLibraryApplicationService CreateLibrary() {
        var authService = new AuthApplicationService(
            AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
        return new ArticleLibraryApplicationService(ApiService.Instance, authService);
    }

    private static Command BuildListCommand() {
        var pageOpt = new Option<int>("--page") { Description = "页码", DefaultValueFactory = _ => 1 };
        var sizeOpt = new Option<int>("--page-size") { Description = "每页条数", DefaultValueFactory = _ => 20 };
        var searchOpt = new Option<string?>("--search") { Description = "按标题搜索" };
        var categoryOpt = new Option<int>("--category") { Description = "分类 ID", DefaultValueFactory = _ => 0 };
        var publishedOpt = new Option<bool?>("--published") { Description = "true 仅已发布，false 仅草稿" };

        var listCmd = new Command("list", "列出站点文章") { pageOpt, sizeOpt, searchOpt, categoryOpt, publishedOpt };
        listCmd.SetAction(parseResult => {
            var library = CreateLibrary();
            Task.Run(async () => {
                var result = await library.ListAsync(new PostListQuery {
                    Page = parseResult.GetValue(pageOpt),
                    PageSize = parseResult.GetValue(sizeOpt),
                    Search = parseResult.GetValue(searchOpt),
                    CategoryId = parseResult.GetValue(categoryOpt),
                    IsPublish = parseResult.GetValue(publishedOpt)
                });
                if (!result.Success) {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                    return;
                }

                if (result.Posts.Count == 0) {
                    Console.WriteLine("暂无文章");
                    return;
                }

                Console.WriteLine($"第 {result.Pagination.PageNumber} 页，共 {result.Pagination.TotalItemCount} 篇");
                foreach (var post in result.Posts) {
                    var status = post.IsPublish ? "已发布" : "草稿";
                    Console.WriteLine($"- [{post.Id}] {post.Title} ({status}) {post.Slug}");
                }
            }).Wait();
            return Environment.ExitCode;
        });
        return listCmd;
    }

    private static Command BuildUpdateCommand() {
        var fileArg = new Argument<string>("file") { Description = "Markdown 文件路径" };
        var idOpt = new Option<string?>("--id") { Description = "文章 ID（默认读取同目录 .starblog.json）" };
        var categoryOpt = new Option<int?>("--category") { Description = "分类 ID" };
        var titleOpt = new Option<string?>("--title") { Description = "文章标题" };
        var summaryOpt = new Option<string?>("--summary") { Description = "文章摘要" };
        var slugOpt = new Option<string?>("--slug") { Description = "URL slug" };
        var draftOpt = new Option<bool>("--draft") { Description = "保存为草稿" };

        var updateCmd = new Command("update", "更新已有文章") { idOpt, categoryOpt, titleOpt, summaryOpt, slugOpt, draftOpt };
        updateCmd.Arguments.Add(fileArg);
        updateCmd.SetAction(parseResult => {
            var file = parseResult.GetValue(fileArg)!;
            if (!File.Exists(file)) {
                Console.Error.WriteLine($"文件不存在: {file}");
                return 1;
            }

            Task.Run(async () => {
                var sidecar = await ArticleSidecar.ReadAsync(file);
                var id = parseResult.GetValue(idOpt) ?? sidecar?.PostId;
                if (string.IsNullOrWhiteSpace(id)) {
                    Console.Error.WriteLine("未提供文章 ID，也没有在 .starblog.json 中找到 PostId");
                    Environment.ExitCode = 1;
                    return;
                }

                var content = await File.ReadAllTextAsync(file);
                var categoryId = parseResult.GetValue(categoryOpt) ?? sidecar?.Category?.Id ?? 0;
                if (categoryId <= 0) {
                    Console.Error.WriteLine("请通过 --category 或 sidecar 指定分类");
                    Environment.ExitCode = 1;
                    return;
                }

                var publish = !parseResult.GetValue(draftOpt);
                var library = CreateLibrary();
                var result = await library.UpdateAsync(id, new PostUpdateDto {
                    Id = id,
                    Title = parseResult.GetValue(titleOpt) ?? sidecar?.Title ?? Path.GetFileNameWithoutExtension(file),
                    Summary = parseResult.GetValue(summaryOpt) ?? sidecar?.Description ?? "",
                    Slug = parseResult.GetValue(slugOpt) ?? sidecar?.Slug,
                    Content = content,
                    CategoryId = categoryId,
                    IsPublish = publish
                });
                if (!result.Success || result.Post == null) {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                    return;
                }

                Console.WriteLine($"已更新 {result.Post.Id} ({(result.Post.IsPublish ? "已发布" : "草稿")})");
            }).Wait();
            return Environment.ExitCode;
        });
        return updateCmd;
    }

    private static Command BuildDeleteCommand() {
        var idArg = new Argument<string>("id") { Description = "文章 ID" };
        var deleteCmd = new Command("delete", "删除文章");
        deleteCmd.Arguments.Add(idArg);
        deleteCmd.SetAction(parseResult => {
            var id = parseResult.GetValue(idArg)!;
            Task.Run(async () => {
                var result = await CreateLibrary().DeleteAsync(id);
                if (!result.Success) {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine(result.Message ?? $"已删除 {id}");
            }).Wait();
            return Environment.ExitCode;
        });
        return deleteCmd;
    }

    private static Command BuildPullCommand() {
        var idArg = new Argument<string>("id") { Description = "文章 ID" };
        var outOpt = new Option<string>("--out") { Description = "保存的 Markdown 路径", Required = true };
        var pullCmd = new Command("pull", "把线上文章下载为本地 Markdown") { outOpt };
        pullCmd.Arguments.Add(idArg);
        pullCmd.SetAction(parseResult => {
            var id = parseResult.GetValue(idArg)!;
            var path = parseResult.GetValue(outOpt)!;
            Task.Run(async () => {
                var result = await CreateLibrary().PullAsync(id, path);
                if (!result.Success) {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine($"已保存 {path}");
            }).Wait();
            return Environment.ExitCode;
        });
        return pullCmd;
    }
}
