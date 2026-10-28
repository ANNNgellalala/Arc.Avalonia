using System;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Media.Controls;

/// <summary>
/// In-code sample imagery for the media components. Every "photo" is a small procedural scene drawn with
/// <see cref="DrawingContext"/> from a curated OKLCH palette, so the pages render offline with no asset
/// files and stay deterministic for screenshots. The scenes are decorative sample data, not UI chrome:
/// component chrome (borders, labels, controls) still uses semantic Arc tokens.
/// </summary>
internal static class SampleArt
{
    public const int SceneCount = 10;

    private static readonly double[] Hues = { 27, 206, 152, 318, 44, 268, 188, 6, 62, 232 };

    public readonly record struct ArtPalette(
        Color SkyTop,
        Color SkyBottom,
        Color LandNear,
        Color LandFar,
        Color Light,
        Color Ink);

    public static ArtPalette PaletteFor(int seed, double? hueOverride = null)
    {
        var variant = Mod(seed, SceneCount);
        var hue = hueOverride ?? Hues[variant];
        var dark = ArcThemeManager.Scheme == ArcColorScheme.Dark;

        if (!dark)
        {
            return new ArtPalette(
                ArcColor.Oklch(0.955, 0.028, hue),
                ArcColor.Oklch(0.845, 0.072, hue + 10),
                ArcColor.Oklch(0.70, 0.090, hue + 28),
                ArcColor.Oklch(0.535, 0.075, hue + 42),
                ArcColor.Oklch(0.975, 0.030, hue - 22),
                ArcColor.Oklch(0.255, 0.045, hue + 18));
        }

        return new ArtPalette(
            ArcColor.Oklch(0.355, 0.052, hue),
            ArcColor.Oklch(0.225, 0.048, hue + 10),
            ArcColor.Oklch(0.470, 0.082, hue + 28),
            ArcColor.Oklch(0.315, 0.068, hue + 42),
            ArcColor.Oklch(0.845, 0.085, hue - 22),
            ArcColor.Oklch(0.135, 0.035, hue + 18));
    }

    /// <summary>Draws scene <paramref name="seed"/> into <paramref name="rect"/>, clipped to a rounded frame.</summary>
    public static void Draw(DrawingContext context, Rect rect, int seed, double radius = 12, double? hue = null, bool grain = true)
    {
        if (rect.Width < 4 || rect.Height < 4)
        {
            return;
        }

        var palette = PaletteFor(seed, hue);
        using (context.PushClip(new RoundedRect(rect, radius)))
        {
            var variant = Mod(seed, SceneCount);
            switch (variant)
            {
                case 0:
                    Dune(context, rect, palette);
                    break;
                case 1:
                    Coast(context, rect, palette);
                    break;
                case 2:
                    StillLife(context, rect, palette);
                    break;
                case 3:
                    Bloom(context, rect, palette);
                    break;
                case 4:
                    Arches(context, rect, palette);
                    break;
                case 5:
                    Ridge(context, rect, palette);
                    break;
                case 6:
                    Studio(context, rect, palette);
                    break;
                case 7:
                    Field(context, rect, palette);
                    break;
                case 8:
                    Night(context, rect, palette);
                    break;
                default:
                    Fold(context, rect, palette);
                    break;
            }

            // A quiet vignette keeps the edges from competing with component chrome.
            var shade = ArcColor.WithAlpha(palette.Ink, 0.20);
            context.DrawRectangle(
                new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.55, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(ArcColor.WithAlpha(palette.Ink, 0), 0),
                        new GradientStop(ArcColor.WithAlpha(shade, 0), 0.35),
                        new GradientStop(ArcColor.WithAlpha(palette.Ink, 0.22), 1)
                    }
                },
                null,
                rect);

            if (grain)
            {
                Grain(context, rect, seed, palette);
            }
        }
    }

    private static void Grain(DrawingContext context, Rect rect, int seed, ArtPalette palette)
    {
        var light = SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.16));
        var dark = SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, 0.10));
        var count = Math.Clamp((int)(rect.Width * rect.Height / 2600), 12, 90);
        for (var index = 0; index < count; index++)
        {
            var x = rect.X + SpecialUi.Noise(seed * 31 + index * 7) * rect.Width;
            var y = rect.Y + SpecialUi.Noise(seed * 17 + index * 13) * rect.Height;
            var size = 0.5 + SpecialUi.Noise(seed + index * 3) * 1.3;
            context.DrawEllipse(index % 4 == 0 ? light : dark, null, new Point(x, y), size, size);
        }
    }

    private static void Dune(DrawingContext context, Rect rect, ArtPalette palette)
    {
        Sky(context, rect, palette.SkyTop, palette.SkyBottom);
        Sun(context, rect, 0.68, 0.30, 0.115, palette.Light, 0.9);

        Hill(context, rect, palette.LandFar, 0.68, 0.10, 1.7);
        Hill(context, rect, palette.LandNear, 0.80, 0.13, 4.1);
        Hill(context, rect, ArcColor.Mix(palette.LandNear, palette.Ink, 0.35), 0.94, 0.10, 2.2);
    }

    private static void Coast(DrawingContext context, Rect rect, ArtPalette palette)
    {
        var horizon = rect.Y + rect.Height * 0.58;
        Sky(context, rect, palette.SkyTop, palette.SkyBottom);
        Sun(context, rect, 0.52, 0.46, 0.10, palette.Light, 1);

        var sea = new Rect(rect.X, horizon, rect.Width, rect.Bottom - horizon);
        context.DrawRectangle(
            SpecialUi.Gradient(ArcColor.Mix(palette.SkyBottom, palette.LandNear, 0.45), palette.LandNear),
            null,
            sea);

        var glint = SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.55));
        for (var index = 0; index < 7; index++)
        {
            var y = horizon + 6 + index * (sea.Height / 8);
            var width = rect.Width * (0.06 + 0.035 * index);
            var x = rect.X + rect.Width * 0.52 - width / 2;
            context.DrawRectangle(glint, null, new Rect(x, y, width, Math.Max(1, 1.5 + index * 0.3)));
        }

        var cliff = new[]
        {
            new Point(rect.X, rect.Bottom),
            new Point(rect.X, rect.Y + rect.Height * 0.30),
            new Point(rect.X + rect.Width * 0.16, rect.Y + rect.Height * 0.42),
            new Point(rect.X + rect.Width * 0.22, rect.Y + rect.Height * 0.62),
            new Point(rect.X + rect.Width * 0.30, rect.Bottom)
        };
        context.DrawGeometry(SpecialUi.Solid(palette.Ink), null, SpecialUi.Polygon(cliff));
    }

    private static void StillLife(DrawingContext context, Rect rect, ArtPalette palette)
    {
        Sky(context, rect, palette.SkyTop, palette.SkyBottom);
        var floor = rect.Y + rect.Height * 0.72;
        context.DrawRectangle(
            SpecialUi.Solid(ArcColor.Mix(palette.LandFar, palette.SkyBottom, 0.35)),
            null,
            new Rect(rect.X, floor, rect.Width, rect.Bottom - floor));

        // Plinth.
        var plinth = new Rect(rect.X + rect.Width * 0.24, floor - 12, rect.Width * 0.52, 22);
        context.DrawRectangle(SpecialUi.Solid(palette.LandNear), null, plinth, 3, 3);

        // Soft contact shadow.
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, 0.24)), null,
            new Point(rect.Center.X, plinth.Bottom + 4), rect.Width * 0.22, rect.Height * 0.035);

        // Vessel: a rounded body with a narrow neck.
        var body = new StreamGeometry();
        using (var geometry = body.Open())
        {
            var cx = rect.Center.X;
            var bottom = plinth.Y;
            var top = rect.Y + rect.Height * 0.24;
            var halfWidth = rect.Width * 0.14;
            var neck = rect.Width * 0.055;
            geometry.BeginFigure(new Point(cx - neck, top), true);
            geometry.LineTo(new Point(cx + neck, top));
            geometry.CubicBezierTo(
                new Point(cx + halfWidth * 0.9, top + rect.Height * 0.16),
                new Point(cx + halfWidth, bottom - rect.Height * 0.20),
                new Point(cx + halfWidth * 0.86, bottom));
            geometry.LineTo(new Point(cx - halfWidth * 0.86, bottom));
            geometry.CubicBezierTo(
                new Point(cx - halfWidth, bottom - rect.Height * 0.20),
                new Point(cx - halfWidth * 0.9, top + rect.Height * 0.16),
                new Point(cx - neck, top));
            geometry.EndFigure(true);
        }

        context.DrawGeometry(
            SpecialUi.Gradient(ArcColor.Mix(palette.LandNear, palette.Light, 0.32), palette.LandFar, false),
            null,
            body);

        // Rim and a single highlight.
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.85)), null,
            new Point(rect.Center.X, rect.Y + rect.Height * 0.245), rect.Width * 0.055, rect.Height * 0.016);
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.22)), null,
            new Point(rect.Center.X - rect.Width * 0.06, rect.Y + rect.Height * 0.50),
            rect.Width * 0.022, rect.Height * 0.12);
    }

    private static void Bloom(DrawingContext context, Rect rect, ArtPalette palette)
    {
        context.DrawRectangle(SpecialUi.Solid(ArcColor.Mix(palette.Ink, palette.LandFar, 0.28)), null, rect);

        var center = new Point(rect.Center.X, rect.Y + rect.Height * 0.44);
        var petal = Math.Min(rect.Width, rect.Height) * 0.20;
        for (var index = 0; index < 7; index++)
        {
            var angle = index * Math.PI * 2 / 7 + 0.35;
            var point = new Point(center.X + Math.Cos(angle) * petal, center.Y + Math.Sin(angle) * petal);
            var shade = index % 2 == 0 ? palette.Light : ArcColor.Mix(palette.Light, palette.LandNear, 0.55);
            using (context.PushTransform(Matrix.CreateRotation(angle) * Matrix.CreateTranslation(point.X, point.Y)))
            {
                context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(shade, 0.34)), null, default, petal * 0.85, petal * 0.42);
            }
        }

        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.92)), null, center, petal * 0.30, petal * 0.30);
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, 0.35)), null, center, petal * 0.14, petal * 0.14);

        var stem = new Pen(SpecialUi.Solid(ArcColor.WithAlpha(palette.LandNear, 0.65)), Math.Max(1.5, rect.Width * 0.008));
        context.DrawLine(stem, center, new Point(center.X + rect.Width * 0.06, rect.Bottom));
    }

    private static void Arches(DrawingContext context, Rect rect, ArtPalette palette)
    {
        context.DrawRectangle(SpecialUi.Gradient(palette.SkyBottom, palette.SkyTop, false), null, rect);

        for (var index = 0; index < 2; index++)
        {
            var width = rect.Width * 0.30;
            var height = rect.Height * (index == 0 ? 0.62 : 0.50);
            var x = rect.X + rect.Width * (index == 0 ? 0.12 : 0.55);
            var y = rect.Bottom - height;
            var arch = new StreamGeometry();
            using (var geometry = arch.Open())
            {
                geometry.BeginFigure(new Point(x, rect.Bottom), true);
                geometry.LineTo(new Point(x, y + width / 2));
                geometry.ArcTo(new Point(x + width, y + width / 2), new Size(width / 2, width / 2), 0, false, SweepDirection.Clockwise);
                geometry.LineTo(new Point(x + width, rect.Bottom));
                geometry.EndFigure(true);
            }

            context.DrawGeometry(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, index == 0 ? 0.9 : 0.55)), null, arch);
            context.DrawGeometry(
                null,
                new Pen(SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, index == 0 ? 0.30 : 0.20)), 1),
                arch);
        }

        // Light shaft across the floor.
        var shaft = new[]
        {
            new Point(rect.X + rect.Width * 0.20, rect.Bottom),
            new Point(rect.X + rect.Width * 0.34, rect.Bottom),
            new Point(rect.X + rect.Width * 0.58, rect.Bottom - rect.Height * 0.26),
            new Point(rect.X + rect.Width * 0.42, rect.Bottom - rect.Height * 0.26)
        };
        context.DrawGeometry(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.16)), null, SpecialUi.Polygon(shaft));
    }

    private static void Ridge(DrawingContext context, Rect rect, ArtPalette palette)
    {
        Sky(context, rect, palette.SkyTop, palette.SkyBottom);
        Sun(context, rect, 0.24, 0.24, 0.07, palette.Light, 0.8);

        Mountain(context, rect, palette.LandFar, 0.72, 0.46, 0.0);
        Mountain(context, rect, palette.LandNear, 0.80, 0.36, 0.30);
        Mountain(context, rect, ArcColor.Mix(palette.LandNear, palette.Ink, 0.22), 0.92, 0.24, 0.62);
    }

    private static void Studio(DrawingContext context, Rect rect, ArtPalette palette)
    {
        context.DrawRectangle(SpecialUi.Gradient(palette.SkyTop, palette.SkyBottom, false), null, rect);

        // Drape: soft vertical folds.
        for (var index = 0; index < 6; index++)
        {
            var x = rect.X + rect.Width * index / 6.0;
            var wide = rect.Width / 6.0;
            var shade = index % 2 == 0 ? 0.08 : 0.16;
            context.DrawRectangle(
                SpecialUi.Gradient(
                    ArcColor.WithAlpha(palette.Light, 0),
                    ArcColor.WithAlpha(palette.Light, shade),
                    false),
                null,
                new Rect(x, rect.Y, wide, rect.Height));
        }

        var sphere = new Point(rect.Center.X, rect.Y + rect.Height * 0.52);
        var radius = Math.Min(rect.Width, rect.Height) * 0.24;
        for (var layer = 10; layer >= 1; layer--)
        {
            var t = layer / 10.0;
            var color = ArcColor.Mix(palette.LandFar, palette.Light, Math.Pow(1 - t, 2.2) * 0.85);
            context.DrawEllipse(
                SpecialUi.Solid(color),
                null,
                new Point(sphere.X - radius * 0.22 * t, sphere.Y - radius * 0.28 * t),
                radius * (0.35 + 0.65 * t),
                radius * (0.35 + 0.65 * t));
        }

        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, 0.22)), null,
            new Point(sphere.X, sphere.Y + radius * 1.02), radius * 0.95, radius * 0.16);
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.55)), null,
            new Point(sphere.X - radius * 0.34, sphere.Y - radius * 0.38), radius * 0.16, radius * 0.11);
    }

    private static void Field(DrawingContext context, Rect rect, ArtPalette palette)
    {
        Sky(context, rect, palette.SkyTop, palette.SkyBottom);
        var horizon = rect.Y + rect.Height * 0.46;
        context.DrawRectangle(
            SpecialUi.Gradient(palette.LandNear, ArcColor.Mix(palette.LandNear, palette.Ink, 0.30)),
            null,
            new Rect(rect.X, horizon, rect.Width, rect.Bottom - horizon));

        // Distant tree line.
        for (var index = 0; index < 9; index++)
        {
            var x = rect.X + rect.Width * (0.03 + index * 0.115);
            var height = rect.Height * (0.05 + SpecialUi.Noise(index * 5) * 0.05);
            context.DrawEllipse(
                SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, 0.45)),
                null,
                new Point(x, horizon - height * 0.4),
                height * 0.7,
                height);
        }

        // Grass strokes, seeded so the frame is stable.
        var pen = new Pen(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.28)), Math.Max(1, rect.Width * 0.0035));
        for (var index = 0; index < 26; index++)
        {
            var x = rect.X + SpecialUi.Noise(index * 11 + 3) * rect.Width;
            var y = horizon + SpecialUi.Noise(index * 23 + 7) * (rect.Bottom - horizon);
            var length = rect.Height * (0.03 + SpecialUi.Noise(index * 31) * 0.06);
            context.DrawLine(pen, new Point(x, y), new Point(x + length * 0.35, y - length));
        }
    }

    private static void Night(DrawingContext context, Rect rect, ArtPalette palette)
    {
        context.DrawRectangle(SpecialUi.Gradient(ArcColor.Mix(palette.Ink, palette.SkyTop, 0.18), palette.Ink), null, rect);

        var star = SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.9));
        for (var index = 0; index < 30; index++)
        {
            var x = rect.X + SpecialUi.Noise(index * 13 + 1) * rect.Width;
            var y = rect.Y + SpecialUi.Noise(index * 7 + 5) * rect.Height * 0.7;
            var size = 0.6 + SpecialUi.Noise(index * 3) * 1.2;
            context.DrawEllipse(star, null, new Point(x, y), size, size);
        }

        var moon = new Point(rect.X + rect.Width * 0.72, rect.Y + rect.Height * 0.28);
        var radius = Math.Min(rect.Width, rect.Height) * 0.11;
        for (var layer = 6; layer >= 1; layer--)
        {
            context.DrawEllipse(
                SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.05 * layer / 6)),
                null,
                moon,
                radius * (1 + layer * 0.55),
                radius * (1 + layer * 0.55));
        }

        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.95)), null, moon, radius, radius);
        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(palette.Ink, 0.28)), null,
            new Point(moon.X + radius * 0.3, moon.Y - radius * 0.25), radius * 0.28, radius * 0.22);

        Hill(context, rect, ArcColor.Mix(palette.Ink, palette.LandFar, 0.30), 0.82, 0.08, 2.4);
        Hill(context, rect, ArcColor.Mix(palette.Ink, palette.LandFar, 0.12), 0.94, 0.06, 5.1);
    }

    private static void Fold(DrawingContext context, Rect rect, ArtPalette palette)
    {
        context.DrawRectangle(SpecialUi.Solid(ArcColor.Mix(palette.SkyTop, palette.Light, 0.35)), null, rect);

        // Large paper planes meeting at a diagonal crease.
        var crease = rect.X + rect.Width * 0.58;
        var left = new[]
        {
            rect.TopLeft,
            new Point(crease, rect.Y),
            new Point(crease - rect.Width * 0.12, rect.Bottom),
            rect.BottomLeft
        };
        var right = new[]
        {
            new Point(crease, rect.Y),
            rect.TopRight,
            rect.BottomRight,
            new Point(crease - rect.Width * 0.12, rect.Bottom)
        };

        context.DrawGeometry(
            SpecialUi.Gradient(ArcColor.Mix(palette.LandNear, palette.Light, 0.45), palette.LandNear),
            null,
            SpecialUi.Polygon(left));
        context.DrawGeometry(
            SpecialUi.Gradient(ArcColor.Mix(palette.LandFar, palette.Ink, 0.18), palette.SkyBottom),
            null,
            SpecialUi.Polygon(right));

        context.DrawLine(
            new Pen(SpecialUi.Solid(ArcColor.WithAlpha(palette.Light, 0.5)), Math.Max(1, rect.Width * 0.004)),
            new Point(crease, rect.Y),
            new Point(crease - rect.Width * 0.12, rect.Bottom));
    }

    private static void Sky(DrawingContext context, Rect rect, Color top, Color bottom) =>
        context.DrawRectangle(SpecialUi.Gradient(top, bottom), null, rect);

    private static void Sun(DrawingContext context, Rect rect, double x, double y, double radius, Color color, double glow)
    {
        var center = new Point(rect.X + rect.Width * x, rect.Y + rect.Height * y);
        var size = Math.Min(rect.Width, rect.Height) * radius;
        for (var layer = 4; layer >= 1; layer--)
        {
            context.DrawEllipse(
                SpecialUi.Solid(ArcColor.WithAlpha(color, 0.07 * glow * layer / 4)),
                null,
                center,
                size * (1 + layer * 0.6),
                size * (1 + layer * 0.6));
        }

        context.DrawEllipse(SpecialUi.Solid(ArcColor.WithAlpha(color, 0.95)), null, center, size, size);
    }

    private static void Hill(DrawingContext context, Rect rect, Color color, double baseLine, double amplitude, double phase)
    {
        var y = rect.Y + rect.Height * baseLine;
        var lift = rect.Height * amplitude;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(rect.X, rect.Bottom), true);
            path.LineTo(new Point(rect.X, y));
            path.CubicBezierTo(
                new Point(rect.X + rect.Width * 0.30, y - lift * Math.Sin(phase) - lift * 0.2),
                new Point(rect.X + rect.Width * 0.62, y + lift * Math.Cos(phase)),
                new Point(rect.Right, y - lift * 0.4));
            path.LineTo(new Point(rect.Right, rect.Bottom));
            path.EndFigure(true);
        }

        context.DrawGeometry(SpecialUi.Solid(color), null, geometry);
    }

    private static void Mountain(DrawingContext context, Rect rect, Color color, double baseLine, double amplitude, double offset)
    {
        var y = rect.Y + rect.Height * baseLine;
        var peakX = rect.X + rect.Width * (0.18 + offset);
        var peakY = y - rect.Height * amplitude;
        var points = new[]
        {
            new Point(rect.X - rect.Width * 0.05, rect.Bottom),
            new Point(rect.X - rect.Width * 0.05, y),
            new Point(peakX, peakY),
            new Point(peakX + rect.Width * 0.34, y),
            new Point(rect.Right + rect.Width * 0.05, baseLine > 0.85 ? rect.Bottom : y + rect.Height * 0.02),
            new Point(rect.Right + rect.Width * 0.05, rect.Bottom)
        };
        context.DrawGeometry(SpecialUi.Solid(color), null, SpecialUi.Polygon(points));
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }
}
