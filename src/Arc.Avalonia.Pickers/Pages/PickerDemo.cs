using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Pickers.Controls;

namespace Arc.Avalonia.Pickers.Pages;

/// <summary>Small pieces the picker pages share: option rows, floating surface chrome and sample ink.</summary>
internal static class PickerDemo
{
    /// <summary>A preset-rail or time-list row, using the same classes as the real panels.</summary>
    public static ArcButton OptionRow(string text, bool selected = false)
    {
        var option = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Content = text,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            FontWeight = FontWeight.Normal
        };
        option.Classes.Add("picker-option");
        if (selected)
        {
            option.Classes.Add("selected");
        }

        AutomationProperties.SetName(option, text);
        return option;
    }

    /// <summary>The floating panel chrome: surface, hairline border, panel radius, floating shadow.</summary>
    public static Border Surface(Control content, Thickness? padding = null)
    {
        return new Border
        {
            Classes = { "picker-popup" },
            Padding = padding ?? new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = content
        };
    }

    /// <summary>A plausible signature sampled as ink strokes, so the pad shows real ink in the gallery.</summary>
    public static IReadOnlyList<ArcInkStroke> SampleSignature()
    {
        const ArcInkColor ink = ArcInkColor.Violet;
        return new[]
        {
            Curve(ink, new (double, double)[] { (98, 168), (108, 128), (124, 100), (140, 124), (150, 164), (156, 176) }),
            Curve(ink, new (double, double)[] { (96, 148), (122, 152), (150, 146) }),
            Curve(ink, new (double, double)[] { (186, 120), (182, 150), (176, 172), (190, 178), (204, 166), (212, 138), (216, 104) }),
            Curve(ink, new (double, double)[] { (240, 156), (232, 174), (244, 180), (258, 170), (262, 150), (250, 146), (242, 158), (256, 166), (272, 158) }),
            Curve(ink, new (double, double)[] { (300, 156), (294, 178), (308, 184), (324, 174), (330, 150), (318, 144), (310, 158), (326, 166), (344, 158) }),
            Curve(ink, new (double, double)[] { (92, 206), (170, 214), (260, 216), (360, 212), (450, 202), (506, 190) })
        };
    }

    /// <summary>Samples a Catmull-Rom spline through the knots and thins the nib with speed.</summary>
    private static ArcInkStroke Curve(ArcInkColor color, (double X, double Y)[] knots)
    {
        var points = new List<ArcInkPoint>();
        const double width = 2.8;
        for (var index = 0; index < knots.Length - 1; index++)
        {
            var p0 = knots[Math.Max(index - 1, 0)];
            var p1 = knots[index];
            var p2 = knots[index + 1];
            var p3 = knots[Math.Min(index + 2, knots.Length - 1)];
            const int steps = 18;
            for (var step = 0; step < steps; step++)
            {
                var t = step / (double)steps;
                var x = CatmullRom(p0.X, p1.X, p2.X, p3.X, t);
                var y = CatmullRom(p0.Y, p1.Y, p2.Y, p3.Y, t);
                var speed = Math.Abs(CatmullRom(p0.X, p1.X, p2.X, p3.X, t + 0.05) - x)
                            + Math.Abs(CatmullRom(p0.Y, p1.Y, p2.Y, p3.Y, t + 0.05) - y);
                points.Add(new ArcInkPoint(x, y, width * Math.Clamp(1.35 - speed / 26.0, 0.55, 1.25)));
            }
        }

        var last = knots[^1];
        points.Add(new ArcInkPoint(last.X, last.Y, width * 0.6));
        return new ArcInkStroke(color, points);
    }

    private static double CatmullRom(double p0, double p1, double p2, double p3, double t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
    }
}
