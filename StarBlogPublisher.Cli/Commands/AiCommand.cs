using System.CommandLine;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Commands;

public static class AiCommand {
    public static Command Build() {
        var command = new Command("ai", "AI 辅助功能");

        // ai generate-summary
        var summaryFileArg = new Argument<string>("file") { Description = "Markdown 文件路径" };

        var summaryCmd = new Command("generate-summary", "生成文章摘要");
        summaryCmd.Arguments.Add(summaryFileArg);
        summaryCmd.SetAction(parseResult => {
            var file = parseResult.GetValue(summaryFileArg)!;
            if (!File.Exists(file)) {
                Console.Error.WriteLine($"文件不存在: {file}");
                return 1;
            }

            var ai = new AiApplicationService(AiService.Instance, AppSettings.Instance);
            if (!ai.IsEnabled) {
                Console.Error.WriteLine("AI 功能未启用，请先在设置中配置 AI");
                return 1;
            }

            var content = File.ReadAllText(file);
            var title = Path.GetFileNameWithoutExtension(file);
            Console.WriteLine("正在生成摘要...");

            Task.Run(async () => {
                var summary = await ai.GenerateSummaryAsync(title, content);
                Console.WriteLine(summary);
            }).Wait();
            return 0;
        });

        // ai optimize-title
        var titleArg = new Argument<string>("title") { Description = "原始标题" };
        var contentOpt = new Option<string?>("--content") { Description = "文章内容（用于上下文）" };

        var titleCmd = new Command("optimize-title", "AI 优化标题") { contentOpt };
        titleCmd.Arguments.Add(titleArg);
        titleCmd.SetAction(parseResult => {
            var title = parseResult.GetValue(titleArg)!;
            var content = parseResult.GetValue(contentOpt) ?? "";

            var ai = new AiApplicationService(AiService.Instance, AppSettings.Instance);
            if (!ai.IsEnabled) {
                Console.Error.WriteLine("AI 功能未启用");
                return 1;
            }

            Console.WriteLine("正在优化标题...");
            Task.Run(async () => {
                var refined = await ai.RefineTitleAsync(title, content);
                Console.WriteLine(refined);
            }).Wait();
            return 0;
        });

        // ai suggest-tags
        var tagsFileArg = new Argument<string>("file") { Description = "Markdown 文件路径" };

        var tagsCmd = new Command("suggest-tags", "推荐标签");
        tagsCmd.Arguments.Add(tagsFileArg);
        tagsCmd.SetAction(parseResult => {
            var file = parseResult.GetValue(tagsFileArg)!;
            if (!File.Exists(file)) {
                Console.Error.WriteLine($"文件不存在: {file}");
                return 1;
            }

            var ai = new AiApplicationService(AiService.Instance, AppSettings.Instance);
            if (!ai.IsEnabled) {
                Console.Error.WriteLine("AI 功能未启用");
                return 1;
            }

            var content = File.ReadAllText(file);
            var title = Path.GetFileNameWithoutExtension(file);
            Console.WriteLine("正在推荐标签...");

            Task.Run(async () => {
                var tags = await ai.GenerateKeywordsAsync(title, content);
                Console.WriteLine(tags);
            }).Wait();
            return 0;
        });

        // ai generate-slug
        var slugTitleArg = new Argument<string>("title") { Description = "文章标题" };

        var slugCmd = new Command("generate-slug", "生成 URL Slug");
        slugCmd.Arguments.Add(slugTitleArg);
        slugCmd.SetAction(parseResult => {
            var title = parseResult.GetValue(slugTitleArg)!;

            var ai = new AiApplicationService(AiService.Instance, AppSettings.Instance);
            if (!ai.IsEnabled) {
                Console.Error.WriteLine("AI 功能未启用");
                return 1;
            }

            Console.WriteLine("正在生成 Slug...");
            Task.Run(async () => {
                var slug = await ai.GenerateSlugAsync(title);
                Console.WriteLine(slug);
            }).Wait();
            return 0;
        });

        command.Subcommands.Add(summaryCmd);
        command.Subcommands.Add(titleCmd);
        command.Subcommands.Add(tagsCmd);
        command.Subcommands.Add(slugCmd);
        return command;
    }
}
