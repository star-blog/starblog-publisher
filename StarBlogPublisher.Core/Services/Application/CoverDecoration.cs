using System;
using System.Collections.Generic;
using System.Linq;
using StarBlogPublisher.Models;

namespace StarBlogPublisher.Services.Application;

internal enum CoverDecorationAnchor { Fixed, TextLeft, LegacyTextArea }

/// <summary>A rectangle shared by the editor and export. Template bounds are the fallback when no text is present.</summary>
public sealed record CoverDecoration(CoverPixelRect Bounds, string Color, int CornerRadius = 0) {
    internal CoverDecorationAnchor Anchor { get; init; }
    internal int TextLeftOffset { get; init; }

    internal CoverDecoration Arrange(CoverPixelRect legacyArea, IReadOnlyList<CoverLayoutObject> objects) => Anchor switch {
        CoverDecorationAnchor.LegacyTextArea => this with {
            Bounds = Bounds with { X = Bounds.X + legacyArea.X, Y = Bounds.Y + legacyArea.Y }, Anchor = CoverDecorationAnchor.Fixed
        },
        CoverDecorationAnchor.TextLeft => this with { Bounds = ResolveBounds(objects) },
        _ => this
    };

    public CoverPixelRect ResolveBounds(IReadOnlyList<CoverLayoutObject> objects, Func<CoverElementId, double>? offsetX = null) {
        if (Anchor != CoverDecorationAnchor.TextLeft) return Bounds;
        var left = objects.Where(o => CoverObjectLayout.TextIds.Contains(o.Id))
            .Select(o => o.Bounds.X + (offsetX?.Invoke(o.Id) ?? 0)).DefaultIfEmpty(Bounds.X - TextLeftOffset).Min();
        return Bounds with { X = (int)Math.Floor(left) + TextLeftOffset };
    }
}
