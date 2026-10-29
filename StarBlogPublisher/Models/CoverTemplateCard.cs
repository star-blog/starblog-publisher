using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using StarBlogPublisher.Services.Application;

namespace StarBlogPublisher.Models;

public sealed record CoverAlignmentChoice(CoverAlignment Value, string Name);

public partial class CoverTemplateCard(string id, string name, string description, CoverTemplate? builtIn = null, CoverPreset? preset = null) : ObservableObject, IDisposable {
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Description { get; } = description;
    public CoverTemplate? BuiltIn { get; } = builtIn;
    public CoverPreset? Preset { get; } = preset;
    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private bool _isSelected;
    public void Dispose() { var image = Thumbnail; Thumbnail = null; image?.Dispose(); }
}
