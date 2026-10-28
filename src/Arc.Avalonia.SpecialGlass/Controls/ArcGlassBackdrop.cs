using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Arc.Avalonia.Icons;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

/// <summary>The painted scenes used behind glass surfaces.</summary>
public enum ArcGlassScene
{
    Coast,
    Villa,
    Studio,
    Garden,
    Dusk,
    Sand,
    Slate
}

/// <summary>
/// A deterministic, token coloured backdrop painted with layered gradients. The single project ships no
/// photography, so each scene stands in for a photo: a broad sky gradient, three soft colour fields, one
/// light shaft, a horizon and a vignette. Everything is drawn from the accent and series tokens, so the
/// scene changes with the accent and both themes stay legible behind translucent glass.
/// </summary>
public class ArcGlassBackdrop : Control
{
    public static readonly StyledProperty<ArcGlassScene> SceneProperty =
        AvaloniaProperty.Register<ArcGlassBackdrop, ArcGlassScene>(nameof(Scene), ArcGlassScene.Coast);

    public static readonly StyledProperty<double> CornerProperty =
        AvaloniaProperty.Register<ArcGlassBackdrop, double>(nameof(Corner), 26);

    /// <summary>0..1, how strongly the edges fall away from the light.</summary>
    public static readonly StyledProperty<double> VignetteProperty =
        AvaloniaProperty.Register<ArcGlassBackdrop, double>(nameof(Vignette), 0.45);

    /// <summary>Changes the composition without changing the palette.</summary>
    public static readonly StyledProperty<int> SeedProperty =
        AvaloniaProperty.Register<ArcGlassBackdrop, int>(nameof(Seed));

    /// <summary>Optional Lucide glyph painted as a quiet motif, for abstract demo surfaces.</summary>
    public static readonly StyledProperty<string?> MotifProperty =
        AvaloniaProperty.Register<ArcGlassBackdrop, string?>(nameof(Motif));

    static ArcGlassBackdrop()
    {
        AffectsRender<ArcGlassBackdrop>(SceneProperty, CornerProperty, VignetteProperty, SeedProperty, MotifProperty);
    }

    public ArcGlassScene Scene
    {
        get => GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public double Corner
    {
        get => GetValue(CornerProperty);
        set => SetValue(CornerProperty, value);
    }

    public double Vignette
    {
        get => GetValue(VignetteProperty);
        set => SetValue(VignetteProperty, value);
    }

    public int Seed
    {
        get => GetValue(SeedProperty);
        set => SetValue(SeedProperty, value);
    }

    public string? Motif
    {
        get => GetValue(MotifProperty);
        set => SetValue(MotifProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? 320 : availableSize.Height;
        return new Size(width, height);
    }

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (rect.Width <= 1 || rect.Height <= 1)
        {
            return;
        }

        var clip = new RoundedRect(rect, Corner);
        using (context.PushClip(clip))
        {
            var palette = BuildPalette();

            // Sky and floor: one diagonal gradient, slightly deeper at the bottom.
            context.DrawRectangle(
                ArcGlass.Gradient(
                    palette.Angle,
                    new GradientStop(palette.Top, 0),
                    new GradientStop(palette.Middle, 0.55),
                    new GradientStop(palette.Bottom, 1)),
                null,
                rect);

            // Three soft colour fields, placed from the seed so cards look different but stable.
            var jitter = (Seed % 7) * 0.035;
            Blob(context, rect, new Point(0.18 + jitter, 0.24), new Size(0.55, 0.62), palette.Glow, 0.55);
            Blob(context, rect, new Point(0.82 - jitter, 0.32), new Size(0.46, 0.54), palette.AccentField, 0.48);
            Blob(context, rect, new Point(0.56 + jitter, 0.92), new Size(0.70, 0.52), palette.Floor, 0.55);

            // A light shaft, the cue that sells the surface as a photograph.
            DrawShaft(context, rect, palette.Light, palette.Angle);

            // Horizon: a soft band low in the frame.
            var horizon = new Rect(0, rect.Height * (0.58 + (Seed % 5) * 0.02), rect.Width, rect.Height * 0.42);
            context.DrawRectangle(
                ArcGlass.Gradient(
                    180,
                    new GradientStop(Color.FromArgb(0, palette.Floor.R, palette.Floor.G, palette.Floor.B), 0),
                    new GradientStop(ArcColor.WithAlpha(palette.Floor, 0.42), 1)),
                null,
                horizon);

            if (!string.IsNullOrWhiteSpace(Motif))
            {
                DrawMotif(context, rect, palette);
            }

            // Vignette keeps the glass panel and overlaid text readable.
            if (Vignette > 0)
            {
                var shade = ArcGlass.Dark ? ArcGlass.Color(ArcTokens.Background) : ArcGlass.Color(ArcTokens.Foreground);
                var vignette = new RadialGradientBrush
                {
                    Center = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
                    GradientOrigin = new RelativePoint(0.5, 0.42, RelativeUnit.Relative),
                    RadiusX = new RelativeScalar(0.78, RelativeUnit.Relative),
                    RadiusY = new RelativeScalar(0.92, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0, shade.R, shade.G, shade.B), 0.35),
                        new GradientStop(ArcColor.WithAlpha(shade, 0.38 * Vignette), 1)
                    }
                };
                context.DrawRectangle(vignette, null, rect);
            }
        }
    }

    private void DrawMotif(DrawingContext context, Rect rect, Palette palette)
    {
        var name = Motif;
        if (string.IsNullOrWhiteSpace(name) || !ArcIconData.Paths.TryGetValue(name!, out var path) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Geometry geometry;
        try
        {
            geometry = Geometry.Parse(path);
        }
        catch
        {
            return;
        }

        var size = Math.Min(rect.Width, rect.Height) * 0.34;
        var scale = size / 24.0;
        var translate = Matrix.CreateTranslation(
            (rect.Width / 2) - (12 * scale),
            (rect.Height * 0.42) - (12 * scale));
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * translate))
        {
            context.DrawGeometry(null, new Pen(ArcGlass.Solid(ArcColor.WithAlpha(palette.Light, 0.65)), 1.1), geometry);
        }
    }

    private static void Blob(DrawingContext context, Rect rect, Point center, Size size, Color color, double alpha)
    {
        var point = new Point(rect.Width * center.X, rect.Height * center.Y);
        var radiusX = rect.Width * size.Width * 0.5;
        var radiusY = rect.Height * size.Height * 0.5;
        var brush = new RadialGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            GradientOrigin = new RelativePoint(0.38, 0.36, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ArcColor.WithAlpha(color, alpha), 0),
                new GradientStop(ArcColor.WithAlpha(color, alpha * 0.45), 0.55),
                new GradientStop(ArcColor.WithAlpha(color, 0), 1)
            }
        };
        context.DrawEllipse(brush, null, point, radiusX, radiusY);
    }

    private static void DrawShaft(DrawingContext context, Rect rect, Color light, double angle)
    {
        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            var band = rect.Width * 0.30;
            var x = rect.Width * (0.52 + (angle % 9) * 0.01);
            stream.BeginFigure(new Point(x, 0), true);
            stream.LineTo(new Point(x + band, 0));
            stream.LineTo(new Point(x + band - rect.Width * 0.22, rect.Height));
            stream.LineTo(new Point(x - rect.Width * 0.22, rect.Height));
            stream.EndFigure(true);
        }

        var brush = ArcGlass.Gradient(
            105,
            new GradientStop(ArcColor.WithAlpha(light, 0.30), 0),
            new GradientStop(ArcColor.WithAlpha(light, 0.06), 0.55),
            new GradientStop(ArcColor.WithAlpha(light, 0), 1));
        context.DrawGeometry(brush, null, geometry);
    }

    private readonly record struct Palette(
        Color Top,
        Color Middle,
        Color Bottom,
        Color Glow,
        Color AccentField,
        Color Floor,
        Color Light,
        double Angle);

    private Palette BuildPalette()
    {
        var accent = ArcGlass.Color(ArcTokens.Accent);
        var series1 = ArcGlass.Color(ArcTokens.Series1);
        var series2 = ArcGlass.Color(ArcTokens.Series2);
        var series3 = ArcGlass.Color(ArcTokens.Series3);
        var series4 = ArcGlass.Color(ArcTokens.Series4);
        var surface = ArcGlass.Color(ArcTokens.Surface);
        var foreground = ArcGlass.Color(ArcTokens.Foreground);
        var dark = ArcGlass.Dark;

        Color Tone(Color color, double lightness) =>
            ArcColor.ReOklch(color, Math.Clamp(lightness, 0.06, 0.96), Math.Clamp(ArcColor.ToOklch(color).Chroma * 0.9, 0.01, 0.20), ArcColor.ToOklch(color).Hue);

        return Scene switch
        {
            ArcGlassScene.Coast => new Palette(
                Tone(series2, dark ? 0.40 : 0.82),
                Tone(series2, dark ? 0.34 : 0.70),
                Tone(series3, dark ? 0.30 : 0.60),
                ArcGlass.Turn(series3, 12),
                ArcGlass.Turn(accent, 6),
                Tone(series4, dark ? 0.26 : 0.52),
                ArcGlass.Lift(ArcColor.Mix(series3, surface, dark ? 0.1 : 0.4), 0.18),
                160),
            ArcGlassScene.Villa => new Palette(
                Tone(series3, dark ? 0.38 : 0.84),
                Tone(series4, dark ? 0.32 : 0.70),
                Tone(series4, dark ? 0.26 : 0.55),
                ArcGlass.Turn(series3, -8),
                ArcGlass.Turn(accent, 18),
                Tone(series2, dark ? 0.22 : 0.46),
                ArcGlass.Lift(series3, dark ? 0.28 : 0.20),
                140),
            ArcGlassScene.Studio => new Palette(
                Tone(accent, dark ? 0.30 : 0.58),
                Tone(series2, dark ? 0.24 : 0.44),
                Tone(series2, dark ? 0.18 : 0.32),
                ArcGlass.Turn(series3, 24),
                ArcGlass.Turn(series4, -18),
                Tone(foreground, dark ? 0.14 : 0.24),
                ArcGlass.Lift(ArcColor.Mix(accent, surface, 0.5), 0.22),
                120),
            ArcGlassScene.Garden => new Palette(
                Tone(series4, dark ? 0.34 : 0.78),
                Tone(series4, dark ? 0.28 : 0.62),
                Tone(series2, dark ? 0.22 : 0.44),
                ArcGlass.Turn(series3, -14),
                ArcGlass.Turn(accent, 10),
                Tone(series1, dark ? 0.20 : 0.38),
                ArcGlass.Lift(ArcColor.Mix(series4, surface, 0.45), 0.20),
                150),
            ArcGlassScene.Dusk => new Palette(
                Tone(series2, dark ? 0.24 : 0.46),
                Tone(accent, dark ? 0.22 : 0.42),
                Tone(series2, dark ? 0.14 : 0.30),
                ArcGlass.Turn(series3, 30),
                ArcGlass.Turn(series4, -24),
                Tone(foreground, dark ? 0.10 : 0.20),
                ArcGlass.Lift(ArcColor.Mix(accent, surface, 0.35), 0.30),
                175),
            ArcGlassScene.Sand => new Palette(
                Tone(series3, dark ? 0.34 : 0.84),
                Tone(series3, dark ? 0.29 : 0.72),
                Tone(series4, dark ? 0.22 : 0.52),
                ArcGlass.Turn(series4, 8),
                ArcGlass.Turn(accent, 26),
                Tone(series1, dark ? 0.18 : 0.38),
                ArcGlass.Lift(series3, dark ? 0.26 : 0.18),
                135),
            _ => new Palette(
                Tone(surface, dark ? 0.30 : 0.80),
                Tone(series2, dark ? 0.26 : 0.66),
                Tone(foreground, dark ? 0.16 : 0.40),
                ArcGlass.Turn(accent, 0, 0.6),
                ArcGlass.Turn(series2, 20, 0.7),
                Tone(foreground, dark ? 0.12 : 0.30),
                ArcGlass.Lift(ArcColor.Mix(surface, foreground, 0.2), 0.25),
                155)
        };
    }
}



