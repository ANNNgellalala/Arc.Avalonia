using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>
/// A generated stand-in photo for the demos, so the avatar states render without touching the file system.
/// It is a stylised portrait painted from theme tokens (a gradient wash, a head and shoulders silhouette),
/// not a bitmap asset; the real pick and drop paths replace it with the visitor's own image.
/// </summary>
public static class ArcSamplePhoto
{
    /// <summary>Paints a square sample portrait at the given pixel size.</summary>
    public static RenderTargetBitmap Create(int size = 640)
    {
        size = Math.Max(64, size);
        var bitmap = new RenderTargetBitmap(new PixelSize(size, size), new Vector(96, 96));
        var from = ArcResources.Color(ArcTokens.GradientFrom);
        var to = ArcResources.Color(ArcTokens.GradientTo);
        var series1 = ArcResources.Color(ArcTokens.Series1);
        var series2 = ArcResources.Color(ArcTokens.Series2);
        var series3 = ArcResources.Color(ArcTokens.Series3);
        var series4 = ArcResources.Color(ArcTokens.Series4);
        var surface = ArcResources.Color(ArcTokens.SurfaceRaised);
        var ink = ArcResources.Color(ArcTokens.GradientForeground);

        using (var context = bitmap.CreateDrawingContext())
        {
            var bounds = new Rect(0, 0, size, size);
            var wash = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.1, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.95, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(from, 0),
                    new GradientStop(to, 1)
                }
            };
            context.DrawRectangle(wash, null, bounds);

            // Soft depth shapes behind the portrait.
            context.DrawEllipse(
                new SolidColorBrush(ArcColor.WithAlpha(series1, 0.20)),
                null,
                new Point(size * 0.78, size * 0.22),
                size * 0.34,
                size * 0.34);
            context.DrawEllipse(
                new SolidColorBrush(ArcColor.WithAlpha(series4, 0.16)),
                null,
                new Point(size * 0.14, size * 0.86),
                size * 0.30,
                size * 0.30);

            // Shoulders, neck and head: a flat illustration in three blended tones.
            var body = new SolidColorBrush(ArcColor.Mix(series2, surface, 0.28));
            context.DrawEllipse(body, null, new Point(size * 0.5, size * 1.06), size * 0.35, size * 0.34);

            var neck = new SolidColorBrush(ArcColor.Mix(series3, series2, 0.45));
            context.DrawRectangle(neck, null, new Rect(size * 0.435, size * 0.60, size * 0.13, size * 0.20));

            var skin = new SolidColorBrush(ArcColor.Mix(series3, to, 0.55));
            context.DrawEllipse(skin, null, new Point(size * 0.5, size * 0.46), size * 0.175, size * 0.205);

            var hair = new SolidColorBrush(ArcColor.Mix(series4, ink, 0.30));
            context.DrawEllipse(hair, null, new Point(size * 0.5, size * 0.375), size * 0.20, size * 0.145);

            // A highlight so the photo reads as a surface rather than a flat block.
            context.DrawEllipse(
                new SolidColorBrush(ArcColor.WithAlpha(ink, 0.10)),
                null,
                new Point(size * 0.30, size * 0.18),
                size * 0.22,
                size * 0.14);
        }

        return bitmap;
    }
}
