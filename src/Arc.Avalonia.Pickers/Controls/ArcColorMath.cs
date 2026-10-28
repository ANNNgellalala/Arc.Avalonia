using System;
using System.Globalization;
using Avalonia.Media;

namespace Arc.Avalonia.Pickers.Controls;

/// <summary>Hue in degrees, saturation and value 0..1, alpha 0..1. Arc's colour picker state.</summary>
internal readonly record struct ArcHsv(double Hue, double Saturation, double Value, double Alpha)
{
    public static ArcHsv FromColor(Color color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        double hue = 0;
        if (delta > 0.0001)
        {
            if (max == r)
            {
                hue = 60 * (((g - b) / delta) % 6);
            }
            else if (max == g)
            {
                hue = 60 * (((b - r) / delta) + 2);
            }
            else
            {
                hue = 60 * (((r - g) / delta) + 4);
            }
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var saturation = max <= 0 ? 0 : delta / max;
        return new ArcHsv(hue, saturation, max, color.A / 255.0);
    }

    public Color ToColor()
    {
        var h = ((Hue % 360) + 360) % 360;
        var s = Math.Clamp(Saturation, 0, 1);
        var v = Math.Clamp(Value, 0, 1);
        var c = v * s;
        var x = c * (1 - Math.Abs(((h / 60) % 2) - 1));
        var m = v - c;

        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromArgb(
            (byte)Math.Round(Math.Clamp(Alpha, 0, 1) * 255),
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }

    public ArcHsv WithHue(double hue) => this with { Hue = hue };

    public ArcHsv WithSaturation(double saturation) => this with { Saturation = Math.Clamp(saturation, 0, 1) };

    public ArcHsv WithValue(double value) => this with { Value = Math.Clamp(value, 0, 1) };

    public ArcHsv WithAlpha(double alpha) => this with { Alpha = Math.Clamp(alpha, 0, 1) };
}

/// <summary>The colour formats the picker's text field can show.</summary>
public enum ArcColorFormat
{
    Hex,
    Rgb,
    Hsl
}

/// <summary>One saved colour in the picker's swatch row.</summary>
public sealed record ArcColorSwatch(string Id, Color Color);

/// <summary>
/// Colour maths for the picker: HSV conversion, hex and functional parsing, WCAG contrast. Kept internal
/// so the public surface stays the documented one.
/// </summary>
internal static class ArcColorMath
{
    public static string ToHex(Color color) => color.A == 255
        ? $"#{color.R:X2}{color.G:X2}{color.B:X2}"
        : $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";

    public static string Format(Color color, ArcColorFormat format) => format switch
    {
        ArcColorFormat.Rgb => color.A == 255
            ? $"rgb({color.R}, {color.G}, {color.B})"
            : $"rgba({color.R}, {color.G}, {color.B}, {color.A / 255.0:0.##})",
        ArcColorFormat.Hsl => FormatHsl(color),
        _ => ToHex(color)
    };

    private static string FormatHsl(Color color)
    {
        var hsv = ArcHsv.FromColor(color);
        var l = hsv.Value * (1 - hsv.Saturation / 2);
        var s = l is <= 0 or >= 1 ? 0 : (hsv.Value - l) / Math.Min(l, 1 - l);
        return color.A == 255
            ? $"hsl({Math.Round(hsv.Hue)}, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%)"
            : $"hsla({Math.Round(hsv.Hue)}, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%, {color.A / 255.0:0.##})";
    }

    /// <summary>Reads hex, rgb(), rgba(), hsl() and hsla() in comma syntax; null when unreadable.</summary>
    public static Color? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.StartsWith('#'))
        {
            return ParseHex(value[1..]);
        }

        var open = value.IndexOf('(');
        var close = value.LastIndexOf(')');
        if (open <= 0 || close <= open)
        {
            return null;
        }

        var name = value[..open].Trim().ToLowerInvariant();
        var body = value[(open + 1)..close];
        var parts = body.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        switch (name)
        {
            case "rgb" or "rgba" when parts.Length >= 3:
            {
                var r = Number(parts[0]);
                var g = Number(parts[1]);
                var b = Number(parts[2]);
                var (a, aPercent) = AlphaPart(parts, 3);
                if (r is null || g is null || b is null || a is null)
                {
                    return null;
                }

                return Color.FromArgb(Alpha(a.Value, aPercent), Byte(r.Value), Byte(g.Value), Byte(b.Value));
            }

            case "hsl" or "hsla" when parts.Length >= 3:
            {
                var h = Number(parts[0]);
                var s = Number(parts[1].Replace("%", string.Empty, StringComparison.Ordinal));
                var l = Number(parts[2].Replace("%", string.Empty, StringComparison.Ordinal));
                var (a, aPercent) = AlphaPart(parts, 3);
                if (h is null || s is null || l is null || a is null)
                {
                    return null;
                }

                return HslToColor(h.Value, s.Value / 100, l.Value / 100, Alpha(a.Value, aPercent));
            }
        }

        return null;
    }

    private static Color HslToColor(double hue, double saturation, double lightness, byte alpha)
    {
        var h = ((hue % 360) + 360) % 360;
        var s = Math.Clamp(saturation, 0, 1);
        var l = Math.Clamp(lightness, 0, 1);
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(((h / 60) % 2) - 1));
        var m = l - c / 2;

        var (r, g, b) = h switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromArgb(alpha, Byte(r + m), Byte(g + m), Byte(b + m));
    }

    private static Color? ParseHex(string hex)
    {
        var text = hex.Trim();
        if (text.Length is not (3 or 4 or 6 or 8) || !IsHex(text))
        {
            return null;
        }

        if (text.Length == 3)
        {
            text = string.Concat(text[0], text[0], text[1], text[1], text[2], text[2]);
        }
        else if (text.Length == 4)
        {
            text = string.Concat(text[0], text[0], text[1], text[1], text[2], text[2], text[3], text[3]);
        }

        var r = Convert.ToByte(text[..2], 16);
        var g = Convert.ToByte(text.Substring(2, 2), 16);
        var b = Convert.ToByte(text.Substring(4, 2), 16);
        var a = text.Length == 8 ? Convert.ToByte(text.Substring(6, 2), 16) : (byte)255;
        return Color.FromArgb(a, r, g, b);
    }

    private static bool IsHex(string text)
    {
        foreach (var c in text)
        {
            if (!Uri.IsHexDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static (double? Value, bool Percent) AlphaPart(string[] parts, int index)
    {
        if (parts.Length <= index)
        {
            return (1, false);
        }

        var text = parts[index];
        var percent = text.EndsWith('%');
        return (Number(text.TrimEnd('%')), percent);
    }

    private static double? Number(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;

    private static byte Byte(double value) => (byte)Math.Round(Math.Clamp(value, 0, 255));

    private static byte Alpha(double value, bool percent) =>
        (byte)Math.Round(Math.Clamp(percent ? value / 100 : value, 0, 1) * 255);

    /// <summary>WCAG relative luminance in sRGB.</summary>
    public static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var c = value / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    /// <summary>WCAG contrast ratio between two colours, 1 to 21.</summary>
    public static double Contrast(Color a, Color b)
    {
        var first = Luminance(a);
        var second = Luminance(b);
        var light = Math.Max(first, second);
        var dark = Math.Min(first, second);
        return (light + 0.05) / (dark + 0.05);
    }

    /// <summary>The WCAG grade for a ratio, using normal-text thresholds.</summary>
    public static string Grade(double ratio) => ratio switch
    {
        >= 7 => "AAA",
        >= 4.5 => "AA",
        >= 3 => "AA Large",
        _ => "Fail"
    };

    public static Color OnWhite(Color color) => Mix(color, Colors.White);

    public static Color OnBlack(Color color) => Mix(color, Colors.Black);

    /// <summary>Renders the colour over a solid backdrop so alpha reads as it will on screen.</summary>
    public static Color Mix(Color color, Color backdrop)
    {
        var alpha = color.A / 255.0;
        return Color.FromArgb(
            255,
            (byte)Math.Round(color.R * alpha + backdrop.R * (1 - alpha)),
            (byte)Math.Round(color.G * alpha + backdrop.G * (1 - alpha)),
            (byte)Math.Round(color.B * alpha + backdrop.B * (1 - alpha)));
    }
}
