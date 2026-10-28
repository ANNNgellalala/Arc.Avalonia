using System;
using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Icons;

namespace Arc.Avalonia.Special.Media.Controls;

/// <summary>
/// Draws the shared Lucide icon set into an arbitrary rect inside a <see cref="DrawingContext"/> call.
/// Self-drawn components still use Arc's stroke weight and round joins, so icons inside a canvas look the
/// same as an <c>ArcIcon</c> in a template.
/// </summary>
internal static class SpecialIcons
{
    private static readonly ConcurrentDictionary<string, Geometry?> Cache = new(StringComparer.Ordinal);

    public static void Draw(
        DrawingContext context,
        string name,
        Rect box,
        Color color,
        double stroke = 1.75,
        double? angle = null) =>
        Draw(context, name, box, new SolidColorBrush(color), stroke, angle);

    public static void Draw(
        DrawingContext context,
        string name,
        Rect box,
        IBrush brush,
        double stroke = 1.75,
        double? angle = null)
    {
        var geometry = Cache.GetOrAdd(name, static key =>
        {
            if (!ArcIconData.Paths.TryGetValue(key, out var data) || string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            try
            {
                return Geometry.Parse(data);
            }
            catch
            {
                return null;
            }
        });

        if (geometry is null)
        {
            return;
        }

        var scale = Math.Min(box.Width, box.Height) / 24.0;
        var centerX = box.X + box.Width / 2;
        var centerY = box.Y + box.Height / 2;
        var matrix = Matrix.CreateTranslation(-12, -12) * Matrix.CreateScale(scale, scale);
        if (angle is not null)
        {
            matrix *= Matrix.CreateRotation(angle.Value * Math.PI / 180);
        }

        matrix *= Matrix.CreateTranslation(centerX, centerY);

        using (context.PushTransform(matrix))
        {
            context.DrawGeometry(null, new Pen(brush, stroke, null, PenLineCap.Round, PenLineJoin.Round), geometry);
        }
    }

    /// <summary>Skip forward/back: a triangle plus a bar, drawn on the same 24 unit grid as the icon set.</summary>
    public static void DrawSkip(DrawingContext context, Rect box, IBrush brush, bool forward)
    {
        var scale = Math.Min(box.Width, box.Height) / 24.0;
        var matrix = Matrix.CreateTranslation(-12, -12)
                     * Matrix.CreateScale(scale, scale)
                     * Matrix.CreateTranslation(box.X + box.Width / 2, box.Y + box.Height / 2);
        using (context.PushTransform(matrix))
        {
            var triangle = forward
                ? new[] { new Point(6, 5.5), new Point(6, 18.5), new Point(16.5, 12) }
                : new[] { new Point(18, 5.5), new Point(18, 18.5), new Point(7.5, 12) };
            context.DrawGeometry(brush, null, SpecialUi.Polygon(triangle));
            var bar = forward ? new Rect(16.5, 5.5, 2.2, 13) : new Rect(5.3, 5.5, 2.2, 13);
            context.DrawRectangle(brush, null, bar, 1, 1);
        }
    }
}
