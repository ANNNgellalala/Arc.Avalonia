using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// Contract surface shared by the 22 self-drawn charts (M2-G3a-6).
/// <para>
/// Themeable properties are fed by the ControlThemes in <c>Themes/ArcChartsTheme.axaml</c> (which reference
/// Arc tokens only), and the additive routed events give consumers a bubbling interaction contract
/// (point / item / legend / range). The charts are plain <see cref="Control"/> instances that paint
/// themselves with <see cref="DrawingContext"/>, so Avalonia template parts (<c>[TemplatePart]</c>) do not
/// apply: there is no <c>OnApplyTemplate</c> and no <c>PART_*</c> contract to honour. If a future slice
/// templates them, reserve <c>PART_Plot</c> / <c>PART_Legend</c> / <c>PART_Tooltip</c>.
/// </para>
/// </summary>
public abstract partial class ArcChartBase
{
    /// <summary>Foreground used for labels and foreground marks; the theme points it at <c>ArcForeground</c>.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<ArcChartBase, IBrush?>(nameof(Foreground));

    /// <summary>Optional plot background, painted before the chart marks. The theme keeps it transparent.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        AvaloniaProperty.Register<ArcChartBase, IBrush?>(nameof(Background));

    /// <summary>Base stroke width for themed chart pens (defaults to the 1 px hairline metric).</summary>
    public static readonly StyledProperty<double> StrokeWidthProperty =
        AvaloniaProperty.Register<ArcChartBase, double>(nameof(StrokeWidth), ChartMetrics.Hairline);

    /// <summary>Bubbling event: a data point was activated (line / bar / area / sparkline style charts).</summary>
    public static readonly RoutedEvent<ChartPointEventArgs> PointClickEvent =
        RoutedEvent.Register<ArcChartBase, ChartPointEventArgs>("PointClick", RoutingStrategies.Bubble);

    /// <summary>Bubbling event: a categorical item was activated (donut / waffle / funnel / heatmap / ...).</summary>
    public static readonly RoutedEvent<ChartItemEventArgs> ItemClickEvent =
        RoutedEvent.Register<ArcChartBase, ChartItemEventArgs>("ItemClick", RoutingStrategies.Bubble);

    /// <summary>Bubbling event: a legend entry was activated (after the chart applied its own toggle).</summary>
    public static readonly RoutedEvent<ChartLegendEventArgs> LegendClickEvent =
        RoutedEvent.Register<ArcChartBase, ChartLegendEventArgs>("LegendClick", RoutingStrategies.Bubble);

    /// <summary>Bubbling event: the user completed a range/window selection (brush/overview charts).</summary>
    public static readonly RoutedEvent<ChartRangeEventArgs> RangeSelectEvent =
        RoutedEvent.Register<ArcChartBase, ChartRangeEventArgs>("RangeSelect", RoutingStrategies.Bubble);

    /// <summary>Foreground brush for labels and foreground marks.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Optional plot background painted by the base render pass.</summary>
    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Base stroke width for chart pens.</summary>
    public double StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>Additive CLR surface for <see cref="PointClickEvent"/> (bubbles to ancestors).</summary>
    public event EventHandler<ChartPointEventArgs>? PointClick
    {
        add => AddHandler(PointClickEvent, value);
        remove => RemoveHandler(PointClickEvent, value);
    }

    /// <summary>Additive CLR surface for <see cref="ItemClickEvent"/> (bubbles to ancestors).</summary>
    public event EventHandler<ChartItemEventArgs>? ItemClick
    {
        add => AddHandler(ItemClickEvent, value);
        remove => RemoveHandler(ItemClickEvent, value);
    }

    /// <summary>Additive CLR surface for <see cref="LegendClickEvent"/> (bubbles to ancestors).</summary>
    public event EventHandler<ChartLegendEventArgs>? LegendClick
    {
        add => AddHandler(LegendClickEvent, value);
        remove => RemoveHandler(LegendClickEvent, value);
    }

    /// <summary>Additive CLR surface for <see cref="RangeSelectEvent"/> (bubbles to ancestors).</summary>
    public event EventHandler<ChartRangeEventArgs>? RangeSelect
    {
        add => AddHandler(RangeSelectEvent, value);
        remove => RemoveHandler(RangeSelectEvent, value);
    }

    /// <summary>Effective stroke width, falling back to the hairline metric when a theme sets zero.</summary>
    protected double EffectiveStroke => StrokeWidth > 0 ? StrokeWidth : ChartMetrics.Hairline;

    /// <summary>Foreground brush for labels/marks, falling back to the Arc foreground token.</summary>
    protected IBrush LabelBrush => Foreground ?? ChartPalette.Foreground;

    /// <summary>Paints the themed background before the derived chart draws its marks.</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Background is { } background && Bounds.Width > 0 && Bounds.Height > 0)
        {
            context.FillRectangle(background, new Rect(Bounds.Size));
        }
    }

    /// <summary>Raises <see cref="PointClickEvent"/> with the point details.</summary>
    protected void RaisePointClick(string seriesName, int seriesIndex, int pointIndex, double value, Point position) =>
        RaiseEvent(new ChartPointEventArgs(PointClickEvent)
        {
            SeriesName = seriesName,
            SeriesIndex = seriesIndex,
            PointIndex = pointIndex,
            Value = value,
            Position = position
        });

    /// <summary>Raises <see cref="ItemClickEvent"/> with the item details.</summary>
    protected void RaiseItemClick(int index, string name, double value, Point position) =>
        RaiseEvent(new ChartItemEventArgs(ItemClickEvent)
        {
            Index = index,
            Name = name,
            Value = value,
            Position = position
        });

    /// <summary>Raises <see cref="LegendClickEvent"/>.</summary>
    protected void RaiseLegendClick(int index, string name, bool isVisible) =>
        RaiseEvent(new ChartLegendEventArgs(LegendClickEvent)
        {
            Index = index,
            Name = name,
            IsVisible = isVisible
        });

    /// <summary>Raises <see cref="RangeSelectEvent"/>.</summary>
    protected void RaiseRangeSelect(int startIndex, int endIndex, double startValue, double endValue) =>
        RaiseEvent(new ChartRangeEventArgs(RangeSelectEvent)
        {
            StartIndex = Math.Min(startIndex, endIndex),
            EndIndex = Math.Max(startIndex, endIndex),
            StartValue = startValue,
            EndValue = endValue
        });
}
