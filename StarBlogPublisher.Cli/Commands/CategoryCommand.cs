using System.CommandLine;
using StarBlogPublisher.Services;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Cli.Commands;

public static class CategoryCommand {
    public static Command Build() {
        var command = new Command("category", "分类管理");

        // category list
        var listCmd = new Command("list", "列出所有分类");
        listCmd.SetAction(_ => {
            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            var categoryService = new CategoryApplicationService(ApiService.Instance, authService);

            Task.Run(async () => {
                var result = await categoryService.GetCategoriesAsync();
                if (!result.Success) {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                    return;
                }

                if (result.Categories == null || result.Categories.Count == 0) {
                    Console.WriteLine("暂无分类");
                    return;
                }

                foreach (var cat in result.Categories) {
                    PrintCategory(cat, 0);
                }
            }).Wait();
            return Environment.ExitCode;
        });

        // category create
        var nameOpt = new Option<string>("--name") { Description = "分类名称", Required = true };
        var parentIdOpt = new Option<int>("--parent-id") { Description = "父分类 ID", DefaultValueFactory = _ => 0 };

        var createCmd = new Command("create", "创建新分类") { nameOpt, parentIdOpt };
        createCmd.SetAction(parseResult => {
            var name = parseResult.GetValue(nameOpt)!;
            var parentId = parseResult.GetValue(parentIdOpt);

            var authService = new AuthApplicationService(
                AppSettings.Instance, GlobalState.Instance, ApiService.Instance);
            var categoryService = new CategoryApplicationService(ApiService.Instance, authService);

            Task.Run(async () => {
                var result = await categoryService.CreateCategoryAsync(name, parentId);
                if (result.Success) {
                    Console.WriteLine($"分类创建成功: {name}");
                }
                else {
                    Console.Error.WriteLine(result.ErrorMessage);
                    Environment.ExitCode = 1;
                }
            }).Wait();
            return Environment.ExitCode;
        });

        command.Subcommands.Add(listCmd);
        command.Subcommands.Add(createCmd);
        return command;
    }

    private static void PrintCategory(Models.Category cat, int indent) {
        var prefix = new string(' ', indent * 2);
        Console.WriteLine($"{prefix}- [{cat.Id}] {cat.Text}");
        if (cat.Nodes != null) {
            foreach (var child in cat.Nodes) {
                PrintCategory(child, indent + 1);
            }
        }
    }
}
