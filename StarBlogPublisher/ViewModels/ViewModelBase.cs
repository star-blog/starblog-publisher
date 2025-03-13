using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;

namespace StarBlogPublisher.ViewModels;

public class ViewModelBase : ObservableObject
{
    public Control? View { get; set; }
}