using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// One node of the hierarchy drawn by <see cref="ArcSunburstChart"/>. A branch's value is the sum of
/// its children, so pass <c>value</c> only on leaves (or on a branch that also has a value of its own).
/// </summary>
public sealed class ArcSunburstNode
{
    /// <summary>A leaf: the value is what the arc is sized by.</summary>
    public ArcSunburstNode(string label, double value, params ArcSunburstNode[] children)
    {
        Label = label ?? string.Empty;
        Value = value;
        Children = children ?? Array.Empty<ArcSunburstNode>();
    }

    /// <summary>A branch: its value is the sum of <paramref name="children"/>.</summary>
    public ArcSunburstNode(string label, params ArcSunburstNode[] children)
    {
        Label = label ?? string.Empty;
        Children = children ?? Array.Empty<ArcSunburstNode>();
    }

    /// <summary>Name shown in the legend, the centre readout, and the tooltip.</summary>
    public string Label { get; }

    /// <summary>Explicit value for a leaf. Null means "sum my children".</summary>
    public double? Value { get; }

    /// <summary>Nested nodes, in draw order clockwise from twelve o'clock.</summary>
    public IReadOnlyList<ArcSunburstNode> Children { get; }

    public bool IsLeaf => Children.Count == 0;

    /// <summary>Value of this node: its own value, or the sum of its children.</summary>
    public double Total => Value ?? Children.Sum(child => child.Total);

    /// <summary>Depth of the tree below this node; a leaf is zero.</summary>
    public int Height => Children.Count == 0 ? 0 : 1 + Children.Max(child => child.Height);

    public override string ToString() => $"{Label} ({Total})";
}

/// <summary>A node and the path of nodes from the root down to it, reported by the sunburst.</summary>
public sealed record ArcSunburstSelection(ArcSunburstNode Node, IReadOnlyList<ArcSunburstNode> Path);

/// <summary>One stage of the Sankey flow. Columns run left to right; leave <paramref name="Column"/> null
/// to infer it from the links.</summary>
public sealed record ArcSankeyNode(string Id, string Label, int? Column = null);

/// <summary>A flow between two Sankey nodes, sized in the same unit as every other link.</summary>
public sealed record ArcSankeyLink(string From, string To, double Value);

/// <summary>One stage of the funnel, in order. The first stage is the whole.</summary>
public sealed record ArcFunnelStage(string Label, double Value);

/// <summary>One period of a race bar chart: a label and a value per entity, keyed by entity name.</summary>
public sealed record ArcRaceFrame(string Period, IReadOnlyDictionary<string, double> Values)
{
    public double ValueOf(string entity) => Values.TryGetValue(entity, out var value) ? value : 0;
}

/// <summary>
/// Small colour and number helpers shared by the advanced charts. Every colour still comes from an
/// Arc token; these only pick between tokens or blend them, never invent a value.
/// </summary>
internal static class AdvancedChartInk
{
    /// <summary>Mixes <paramref name="color"/> over <paramref name="backdrop"/>; <paramref name="amount"/> is
    /// how much of the colour survives.</summary>
    public static IBrush Blend(IBrush color, IBrush backdrop, double amount)
    {
        if (color is ISolidColorBrush foreground && backdrop is ISolidColorBrush background)
        {
            var mix = ChartFormat.Clamp01(amount);
            return new SolidColorBrush(Color.FromArgb(
                255,
                (byte)Math.Round(foreground.Color.R * mix + background.Color.R * (1 - mix)),
                (byte)Math.Round(foreground.Color.G * mix + background.Color.G * (1 - mix)),
                (byte)Math.Round(foreground.Color.B * mix + background.Color.B * (1 - mix))));
        }

        return color;
    }

    /// <summary>
    /// Text ink that reads on a filled shape: whichever of the foreground and surface tokens has the
    /// higher contrast against the fill. Both are semantic tokens, so this works in either theme.
    /// </summary>
    public static IBrush OnFill(IBrush fill)
    {
        if (fill is not ISolidColorBrush solid)
        {
            return ChartPalette.Foreground;
        }

        var fillLuminance = Luminance(solid.Color);
        var foreground = ChartPalette.Foreground;
        var surface = ChartPalette.Surface;
        var foregroundLuminance = foreground is ISolidColorBrush f ? Luminance(f.Color) : 0;
        var surfaceLuminance = surface is ISolidColorBrush s ? Luminance(s.Color) : 1;
        return Contrast(surfaceLuminance, fillLuminance) > Contrast(foregroundLuminance, fillLuminance)
            ? surface
            : foreground;
    }

    /// <summary>True when the chart surface is dark, so fills may be lifted toward the foreground.</summary>
    public static bool OnDarkSurface =>
        ChartPalette.Surface is ISolidColorBrush solid && Luminance(solid.Color) < 0.35;

    /// <summary>
    /// A quieter step of a series hue for a deeper level of a hierarchy. In light themes it walks toward
    /// the surface; on a dark surface it walks toward the foreground, so children still read as lighter.
    /// </summary>
    public static IBrush Step(IBrush brush, int depth)
    {
        if (depth <= 1)
        {
            return brush;
        }

        return OnDarkSurface
            ? Blend(brush, ChartPalette.Foreground, Math.Max(0.54, 0.80 - 0.12 * (depth - 2)))
            : Blend(brush, ChartPalette.Surface, Math.Max(0.34, 0.64 - 0.13 * (depth - 2)));
    }

    /// <summary>Relative luminance of a colour, used only to choose between two tokens.</summary>
    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var channel = value / 255.0;
            return channel <= 0.03928 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static double Contrast(double left, double right)
    {
        var high = Math.Max(left, right);
        var low = Math.Min(left, right);
        return (high + 0.05) / (low + 0.05);
    }
}
