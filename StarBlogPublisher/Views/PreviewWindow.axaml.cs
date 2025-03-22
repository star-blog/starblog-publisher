using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views
{
    public partial class PreviewWindow : Window
    {
        public PreviewWindow()
        {
            InitializeComponent();
        }

        public PreviewWindow(string markdownContent) : this()
        {
            DataContext = new PreviewWindowViewModel
            {
                MarkdownContent = markdownContent
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}