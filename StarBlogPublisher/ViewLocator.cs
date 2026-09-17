using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher;

public class ViewLocator : IDataTemplate {
    public Control? Build(object? param) {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name) ?? typeof(ViewLocator).Assembly.GetType(name);

        if (type != null) {
            try {
                return (Control)Activator.CreateInstance(type)!;
            }
            catch (Exception ex) {
                var message = ex.InnerException?.Message ?? ex.Message;
                return new TextBlock { Text = $"Failed to create {name}: {message}", TextWrapping = TextWrapping.Wrap };
            }
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) {
        return data is ViewModelBase;
    }
}