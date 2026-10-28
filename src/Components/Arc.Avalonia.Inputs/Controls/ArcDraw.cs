using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Icons;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>Drawing helpers for the controls that paint their own surface (sliders, prices).</summary>
internal static class ArcDraw
{
    private static readonly ConcurrentDictionary<string, Geometry?> Icons = new(StringComparer.Ordinal);

    public static void Fill(DrawingContext context, Rect rect, double radius, IBrush brush) =>
        context.DrawRectangle(brush, null, rect, radius, radius);

    public static void Stroke(DrawingContext context, Rect rect, double radius, IBrush brush, double thickness) =>
        context.DrawRectangle(null, new Pen(brush, thickness), rect, radius, radius);

    public static void Pill(DrawingContext context, Rect rect, IBrush fill) =>
        context.DrawRectangle(fill, null, rect, rect.Height / 2, rect.Height / 2);

    public static void Text(DrawingContext context, FormattedText text, double x, double centerY, bool alignRight = false)
        => context.DrawText(text, new Point(alignRight ? x - text.Width : x, centerY - text.Height / 2));

    public static void Icon(DrawingContext context, string? name, Rect box, IBrush brush, double stroke = 1.75)
    {
        if (string.IsNullOrEmpty(name) || box.Width <= 0 || box.Height <= 0)
        {
            return;
        }

        var geometry = Icons.GetOrAdd(name, static key =>
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
        var pen = new Pen(brush, stroke, null, PenLineCap.Round, PenLineJoin.Round);
        using (context.PushTransform(Matrix.CreateScale(scale, scale) *
                                     Matrix.CreateTranslation(box.X, box.Y)))
        {
            context.DrawGeometry(null, pen, geometry);
        }
    }
}
