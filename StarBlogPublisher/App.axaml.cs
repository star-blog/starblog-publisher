using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.ViewModels;
using StarBlogPublisher.Views;

namespace StarBlogPublisher;

public partial class App : Application {
    // 添加静态属性以便在ViewModel中访问MainWindow
    public static MainWindow MainWindow { get; private set; }
    
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            
            // 创建MainWindow并保存引用
            MainWindow = new MainWindow {
                DataContext = new MainWindowViewModel(),
            };
            desktop.MainWindow = MainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation() {
        // 从数据验证插件中移除DataAnnotationsValidationPlugin
        var dataValidationPlugins = BindingPlugins.DataValidators;
        var dataAnnotationsPlugin = dataValidationPlugins
            .OfType<DataAnnotationsValidationPlugin>()
            .FirstOrDefault();
        if (dataAnnotationsPlugin != null)
            dataValidationPlugins.Remove(dataAnnotationsPlugin);
    }
}