using System;
using Avalonia.Media;

namespace Arc.Avalonia.Theme;

/// <summary>
/// Colour maths behind the Arc palette. Arc defines every token in OKLCH, so the port keeps the
/// same numbers and converts to sRGB here instead of hard-coding hex approximations.
/// </summary>
public static class ArcColor
{
    /// <summary>Builds a colour from OKLCH coordinates: lightness 0..1, chroma, hue in degrees, alpha 0..1.</summary>
    public static Color Oklch(double lightness, double chroma, double hue, double alpha = 1.0)
    {
        var (r, g, b) = OklchToSrgb(lightness, chroma, hue);
        return Color.FromArgb(AlphaByte(alpha), ToByte(r), ToByte(g), ToByte(b));
    }

    /// <summary>Parses #RRGGBB or #AARRGGBB; an explicit alpha overrides the parsed one.</summary>
    public static Color Hex(string hex, double alpha = -1)
    {
        var text = hex.TrimStart('#');
        byte a = 255, r, g, b;
        if (text.Length == 8)
        {
            a = Convert.ToByte(text[..2], 16);
            text = text[2..];
        }
        r = Convert.ToByte(text[..2], 16);
        g = Convert.ToByte(text.Substring(2, 2), 16);
        b = Convert.ToByte(text.Substring(4, 2), 16);
        return Color.FromArgb(alpha < 0 ? a : AlphaByte(alpha), r, g, b);
    }

    public static Color Rgb(byte r, byte g, byte b, double alpha = 1.0) =>
        Color.FromArgb(AlphaByte(alpha), r, g, b);

    public static Color WithAlpha(Color color, double alpha) =>
        Color.FromArgb(AlphaByte(alpha), color.R, color.G, color.B);

    /// <summary>Mix in OKLab, matching CSS color-mix(in oklab, a, b).</summary>
    public static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        var (l1, a1, b1) = SrgbToOklab(a);
        var (l2, a2, b2) = SrgbToOklab(b);
        var l = l1 + (l2 - l1) * t;
        var aa = a1 + (a2 - a1) * t;
        var bb = b1 + (b2 - b1) * t;
        var (r, g, bl) = OklabToSrgb(l, aa, bb);
        var alpha = (a.A + (b.A - a.A) * t) / 255.0;
        return Color.FromArgb(AlphaByte(alpha), ToByte(r), ToByte(g), ToByte(bl));
    }

    /// <summary>Returns OKLCH coordinates for an sRGB colour (lightness 0..1, hue in degrees).</summary>
    public static (double Lightness, double Chroma, double Hue) ToOklch(Color color)
    {
        var (l, a, b) = SrgbToOklab(color);
        var chroma = Math.Sqrt(a * a + b * b);
        var hue = Math.Atan2(b, a) * 180.0 / Math.PI;
        if (hue < 0)
        {
            hue += 360.0;
        }
        return (l, chroma, hue);
    }

    /// <summary>Rebuilds a colour from OKLCH coordinates, keeping the source alpha.</summary>
    public static Color ReOklch(Color source, double lightness, double chroma, double hue) =>
        Oklch(lightness, chroma, hue, source.A / 255.0);

    internal static (double R, double G, double B) OklchToSrgb(double lightness, double chroma, double hue)
    {
        var h = hue * Math.PI / 180.0;
        var a = chroma * Math.Cos(h);
        var b = chroma * Math.Sin(h);
        return OklabToSrgb(lightness, a, b);
    }

    internal static (double R, double G, double B) OklabToSrgb(double l, double a, double b)
    {
        var l_ = l + 0.3963377774 * a + 0.2158037573 * b;
        var m_ = l - 0.1055613458 * a - 0.0638541728 * b;
        var s_ = l - 0.0894841775 * a - 1.2914855480 * b;

        var ll = l_ * l_ * l_;
        var mm = m_ * m_ * m_;
        var ss = s_ * s_ * s_;

        var r = 4.0767416621 * ll - 3.3077115913 * mm + 0.2309699292 * ss;
        var g = -1.2684380046 * ll + 2.6097574011 * mm - 0.3413193965 * ss;
        var bl = -0.0041960863 * ll - 0.7034186147 * mm + 1.7076147010 * ss;

        return (Gamma(r), Gamma(g), Gamma(bl));
    }

    internal static (double L, double A, double B) SrgbToOklab(Color color)
    {
        var r = Encode(color.R / 255.0);
        var g = Encode(color.G / 255.0);
        var b = Encode(color.B / 255.0);

        var l = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b;
        var m = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b;
        var s = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b;

        var l_ = Math.Cbrt(l);
        var m_ = Math.Cbrt(m);
        var s_ = Math.Cbrt(s);

        return (
            0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_);
    }

    private static double Gamma(double linear)
    {
        if (double.IsNaN(linear))
        {
            return 0;
        }
        var v = Math.Clamp(linear, 0, 1);
        return v <= 0.0031308 ? 12.92 * v : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
    }

    private static double Encode(double srgb)
    {
        var v = Math.Clamp(srgb, 0, 1);
        return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
    }

    private static byte ToByte(double value01) =>
        value01 <= 0 ? (byte)0 : value01 >= 1 ? (byte)255 : (byte)Math.Round(value01 * 255);

    private static byte AlphaByte(double alpha) =>
        alpha <= 0 ? (byte)0 : alpha >= 1 ? (byte)255 : (byte)Math.Round(alpha * 255);
}
