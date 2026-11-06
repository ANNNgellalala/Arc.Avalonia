using System;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// Chart drawing metrics, centrally defined so the 22 self-drawn charts share one set of stroke weights,
/// label sizes, radii and paddings. Colours, fonts and radii resolve from Arc tokens at draw time, so a
/// light/dark or accent swap restyles every chart without touching drawing code (R6 token-only).
/// </summary>
internal static class ChartMetrics
{
    /// <summary>1 px stroke used for gridlines, axes and outlines (chart-local metric, no design token).</summary>
    public const double Hairline = 1.0;

    /// <summary>Emphasised 2 px stroke used for active series and highlight rings.</summary>
    public const double Emphasis = 2.0;

    /// <summary>Annotation/label font size, from the Arc <c>ArcTextXs</c> token (fallback 12).</summary>
    public static double LabelFontSize => TokenNumber(ArcTokens.TextXs, 12);

    /// <summary>Caption/legend font size, from the Arc <c>ArcTextXs</c> token (fallback 12).</summary>
    public static double CaptionFontSize => TokenNumber(ArcTokens.TextXs, 12);

    /// <summary>Chart title / emphasised value size, from the Arc <c>ArcTextSm</c> token (fallback 14).</summary>
    public static double TitleFontSize => TokenNumber(ArcTokens.TextSm, 14);

    /// <summary>Tooltip body size, from the Arc <c>ArcTextXs</c> token (fallback 12).</summary>
    public static double TooltipFontSize => TokenNumber(ArcTokens.TextXs, 12);

    /// <summary>Small corner radius (tooltip, legend chips, tooltip swatches) from <c>ArcRadiusControl</c>.</summary>
    public static CornerRadius SmallRadius => TokenRadius(ArcTokens.RadiusControl, 18);

    /// <summary>Panel corner radius from <c>ArcRadiusPanel</c>.</summary>
    public static CornerRadius PanelRadius => TokenRadius(ArcTokens.RadiusPanel, 26);

    /// <summary>Double form of <see cref="SmallRadius"/> for the DrawingContext rectangle overloads.</summary>
    public static double SmallCorner => SmallRadius.TopLeft;

    /// <summary>Tooltip inner padding, from the Arc <c>ArcSpace2</c> spacing token (fallback 8).</summary>
    public static double TooltipPadding => TokenNumber(ArcTokens.Space2, 8);

    /// <summary>Tooltip row height, from the Arc <c>ArcSpace4</c> spacing token (fallback 16).</summary>
    public static double TooltipRowHeight => TokenNumber(ArcTokens.Space4, 16);

    /// <summary>Gap between adjacent data points, in device-independent pixels.</summary>
    public const double PointGap = 2.0;

    private static double TokenNumber(string key, double fallback)
    {
        var value = ArcResources.Double(key);
        return value > 0 ? value : fallback;
    }

    private static CornerRadius TokenRadius(string key, double fallback)
    {
        var radius = ArcResources.Radius(key);
        return radius != default ? radius : new CornerRadius(fallback);
    }
}
