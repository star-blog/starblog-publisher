using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StarBlogPublisher.Services.Application;
using StarBlogPublisher.ViewModels;

namespace StarBlogPublisher.Views.Controls;

/// <summary>Display-only guides; image output never contains editing decorations.</summary>
public sealed class CoverGuides : Control {
    public override void Render(DrawingContext context) {
        if (DataContext is not CoverStudioViewModel vm || !vm.ShowGuides) return;
        var scale = System.Math.Min(Bounds.Width / 1200, Bounds.Height / 900);
        var left = (Bounds.Width - 1200 * scale) / 2; var top = (Bounds.Height - 900 * scale) / 2;
        void Draw(CoverPixelRect r, Color color) => context.DrawRectangle(null, new Pen(new SolidColorBrush(color), 1.5),
            new Rect(left + r.X * scale, top + r.Y * scale, r.Width * scale, r.Height * scale));
        var crops = vm.Session.Draft.Crops;
        if (!crops.IsValid) return;
        var head = crops.Window(1200, 900, true); var square = crops.Window(1200, 900, false);
        Draw(head, Colors.Orange); Draw(square, Colors.CornflowerBlue);
        Draw(CoverSafeZone.Inset(CoverSafeZone.Intersect(head, square), 48), Colors.MediumAquamarine);
    }
}
