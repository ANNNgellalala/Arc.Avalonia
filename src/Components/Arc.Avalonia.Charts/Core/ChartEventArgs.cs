using System.Collections.Generic;
using Avalonia;
using Avalonia.Interactivity;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// Payload for <see cref="ArcChartBase.PointClick"/>: a single data point was activated on a
/// line/bar/area-style chart. <see cref="Position"/> is in chart (control) coordinates.
/// </summary>
public sealed class ChartPointEventArgs : RoutedEventArgs
{
    public ChartPointEventArgs()
    {
    }

    internal ChartPointEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }

    /// <summary>Series name when the chart has named series; empty for single-value charts.</summary>
    public string SeriesName { get; init; } = string.Empty;

    /// <summary>Index of the series within <c>Series</c>, or 0 for single-series charts.</summary>
    public int SeriesIndex { get; init; }

    /// <summary>Index of the point within the series.</summary>
    public int PointIndex { get; init; }

    /// <summary>The datum value behind the point.</summary>
    public double Value { get; init; }

    /// <summary>Where the point was drawn, in chart coordinates.</summary>
    public Point Position { get; init; }
}

/// <summary>
/// Payload for <see cref="ArcChartBase.ItemClick"/>: a slice / segment / row / cell / node was activated
/// on a categorical chart (donut, waffle, split bar, funnel, heatmap, sankey, sunburst, ...).
/// </summary>
public sealed class ChartItemEventArgs : RoutedEventArgs
{
    public ChartItemEventArgs()
    {
    }

    internal ChartItemEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }

    /// <summary>Item label as drawn (category, slice, node or cell name).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Index of the item within the chart's visible items.</summary>
    public int Index { get; init; }

    /// <summary>The datum value behind the item.</summary>
    public double Value { get; init; }

    /// <summary>Where the item was activated, in chart coordinates.</summary>
    public Point Position { get; init; }
}

/// <summary>
/// Payload for <see cref="ArcChartBase.LegendClick"/>: a legend entry was activated. The chart has already
/// applied its own visibility toggle when it supports one; consumers can react further (e.g. highlight,
/// analytics) without having to re-implement the toggle.
/// </summary>
public sealed class ChartLegendEventArgs : RoutedEventArgs
{
    public ChartLegendEventArgs()
    {
    }

    internal ChartLegendEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }

    /// <summary>Legend entry label / series name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Index of the legend entry.</summary>
    public int Index { get; init; }

    /// <summary>Visibility of the series/segment after the click.</summary>
    public bool IsVisible { get; init; }
}

/// <summary>
/// Payload for <see cref="ArcSunburstChart.NodeSelectedEvent"/>: the additive routed counterpart of the
/// chart's legacy <c>NodeSelected</c> CLR event (both fire on every selection change).
/// </summary>
public sealed class ArcSunburstNodeEventArgs : RoutedEventArgs
{
    public ArcSunburstNodeEventArgs()
    {
    }

    internal ArcSunburstNodeEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }

    /// <summary>The node that became active / focused.</summary>
    public ArcSunburstNode? Node { get; init; }

    /// <summary>The path from the root down to <see cref="Node"/>.</summary>
    public IReadOnlyList<ArcSunburstNode> Path { get; init; } = System.Array.Empty<ArcSunburstNode>();
}

/// <summary>
/// Payload for <see cref="ArcChartBase.RangeSelect"/>: the user completed a range/window selection
/// (brush/overview charts and any future range-capable chart). Indices refer to the chart's columns.
/// </summary>
public sealed class ChartRangeEventArgs : RoutedEventArgs
{
    public ChartRangeEventArgs()
    {
    }

    internal ChartRangeEventArgs(RoutedEvent routedEvent)
        : base(routedEvent)
    {
    }

    /// <summary>First selected column index (always &lt;= <see cref="EndIndex"/>).</summary>
    public int StartIndex { get; init; }

    /// <summary>Last selected column index.</summary>
    public int EndIndex { get; init; }

    /// <summary>Value at the start of the selected range.</summary>
    public double StartValue { get; init; }

    /// <summary>Value at the end of the selected range.</summary>
    public double EndValue { get; init; }
}
