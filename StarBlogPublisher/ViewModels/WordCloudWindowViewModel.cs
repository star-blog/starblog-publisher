using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using StarBlogPublisher.Models;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class WordCloudWindowViewModel : ViewModelBase {
    [ObservableProperty] private List<WordCloud> _wordClouds = new();

    [ObservableProperty] private bool _isLoading;

    public WordCloudWindowViewModel() {
        LoadWordCloudData();
    }

    private async Task LoadWordCloudData() {
        IsLoading = true;
        try {
            var response = await ApiService.Instance.Categories.GetWordCloud();
            if (response is { Successful: true, Data: not null }) {
                // 计算字体大小
                var maxCount = response.Data.Max(w => w.Value);
                var minCount = response.Data.Min(w => w.Value);
                var fontScale = 30.0; // 最大字体大小
                var minFontSize = 12.0; // 最小字体大小

                foreach (var word in response.Data) {
                    // 使用线性插值计算字体大小
                    var normalizedValue = (double)(word.Value - minCount) / (maxCount - minCount);
                    word.Value = (int)(minFontSize + normalizedValue * (fontScale - minFontSize));
                }

                WordClouds = response.Data;
            }
        }
        finally {
            IsLoading = false;
        }
    }
}