using System.CommandLine;
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
        return command;
    }
}
