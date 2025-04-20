using System;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Sdcb.WordClouds;
using SkiaSharp;
using StarBlogPublisher.Services;

namespace StarBlogPublisher.ViewModels;

public partial class WordCloudWindowViewModel : ViewModelBase {
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _errorMessage;
    [ObservableProperty] private Bitmap _wordCloudImage;

    public WordCloudWindowViewModel() {
        _ = GenerateWordCloudImage();
    }

    private async Task<List<WordScore>?> GetWordScores() {
        try {
            var response = await ApiService.Instance.Categories.GetWordCloud();
            if (response.Data == null) {
                throw new Exception("获取词云数据失败：服务器返回数据为空");
            }

            var originalScores = response.Data.Select(e => new WordScore(Score: e.Value, Word: e.Name)).ToList();

            // 扩充数据，复制原始数据以增加词云密度
            var extendedScores = new List<WordScore>();
            foreach (var score in originalScores) {
                // 根据需要调整复制次数
                for (var i = 0; i < 10; i++) {
                    extendedScores.Add(score);
                }
            }

            return extendedScores;
        }
        catch (Exception e) {
            HasError = true;
            ErrorMessage = $"获取词云数据失败：{e.Message}";
            return null;
        }
    }

    private async Task GenerateWordCloudImage() {
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        
        try {
            var wordScores = await GetWordScores();
            if (wordScores == null || !wordScores.Any()) {
                HasError = true;
                ErrorMessage = "没有可用的词云数据";
                return;
            }
            
            var wc = WordCloud.Create(new WordCloudOptions(900, 900, wordScores) {
                FontManager = new FontManager([SKTypeface.FromFamilyName("Times New Roman")]),
                Mask = MaskOptions.CreateWithForegroundColor(SKBitmap.Decode(
                        await new HttpClient().GetByteArrayAsync(
                            "https://io.starworks.cc:88/cv-public/2024/alice_mask.png")
                    ),
                    SKColors.White
                )
            });

            using var skImage = wc.ToSKBitmap();
            using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            WordCloudImage = new Bitmap(stream);
        }
        catch (Exception ex) {
            HasError = true;
            ErrorMessage = $"生成词云图片失败：{ex.Message}";
        }
        finally {
            IsLoading = false;
        }
    }
}