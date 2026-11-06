using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>An event drawn against the time series, such as a launch or an incident.</summary>
/// <param name="Index">Point index in <see cref="ArcBrushChart.Series"/>, the event's place in time.</param>
/// <param name="Label">A word or two, printed beside the marker when there is room.</param>
/// <param name="Tone">Accent uses the second series hue so an event never reads as the metric.</param>
/// <param name="Description">Optional longer note shown in the tooltip.</param>
public sealed record ChartEvent(
    int Index,
    string Label,
    ChartTone Tone = ChartTone.Accent,
    string? Description = null);

/// <summary>
/// A dense time series with an overview strip: the main plot shows the selected window, the strip shows
/// the whole history. Drag the window to pan, its edges to resize, or the empty track to draw a new one.
/// Left and right arrows pan, Shift with the arrows resizes, and Home or End shows the whole series.
/// </summary>
public class ArcBrushChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<double>?> SeriesProperty =
        AvaloniaProperty.Register<ArcBrushChart, IReadOnlyList<double>?>(nameof(Series));

    public static readonly StyledProperty<IReadOnlyList<string>?> LabelsProperty =
        AvaloniaProperty.Register<ArcBrushChart, IReadOnlyList<string>?>(nameof(Labels));

    public static readonly StyledProperty<DateTime?> StartProperty =
        AvaloniaProperty.Register<ArcBrushChart, DateTime?>(nameof(Start));

    public static readonly StyledProperty<IReadOnlyList<ChartEvent>?> EventsProperty =
        AvaloniaProperty.Register<ArcBrushChart, IReadOnlyList<ChartEvent>?>(nameof(Events));

    public static readonly StyledProperty<double> WindowStartProperty =
        AvaloniaProperty.Register<ArcBrushChart, double>(nameof(WindowStart));

    public static readonly StyledProperty<double> WindowEndProperty =
        AvaloniaProperty.Register<ArcBrushChart, double>(nameof(WindowEnd), 1d);

    public static readonly StyledProperty<bool> ShowGridProperty =
        AvaloniaProperty.Register<ArcBrushChart, bool>(nameof(ShowGrid), true);

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcBrushChart, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcBrushChart, Func<double, string>?>(nameof(ValueFormat));

    private const double GutterWidth = 48;
    private const double EventRail = 26;
    private const double AxisHeight = 22;
    private const double StripGap = 10;
    private const double StripRadius = 10;
    private const double HandleZone = 14;
    private const int MinimumWindowPoints = 7;
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private static readonly Cursor CrossCursor = new(StandardCursorType.Cross);
    private static readonly Cursor GrabCursor = new(StandardCursorType.Hand);
    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);

    private readonly List<(Rect Bounds, ChartEvent Event)> _eventHits = new();
    private Rect _plot;
    private Rect _strip;
    private int _activeIndex = -1;
    private ChartEvent? _hoverEvent;
    private DragMode _drag;
    private double _dragOriginX;
    private double _dragAnchor;
    private double _dragFromStart;
    private double _dragFromEnd;
    private bool _dragMoved;
    private bool _keyboardFocus;

    private enum DragMode
    {
        None,
        Start,
        End,
        Pan,
        New
    }

    static ArcBrushChart()
    {
        AffectsRender<ArcBrushChart>(
            SeriesProperty,
            LabelsProperty,
            StartProperty,
            EventsProperty,
            WindowStartProperty,
            WindowEndProperty,
            ShowGridProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
        SeriesProperty.Changed.AddClassHandler<ArcBrushChart>((chart, _) => chart.ResetInteraction());
    }

    public ArcBrushChart()
    {
        Focusable = true;
    }

    /// <summary>Values in time order, oldest first. A few hundred daily points is the intended density.</summary>
    public IReadOnlyList<double>? Series
    {
        get => GetValue(SeriesProperty);
        set => SetValue(SeriesProperty, value);
    }

    /// <summary>Optional time labels, one per point. Without them <see cref="Start"/> plus a day per point is used.</summary>
    public IReadOnlyList<string>? Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    /// <summary>Optional first date, used for axis and tooltip labels when <see cref="Labels"/> is unset.</summary>
    public DateTime? Start
    {
        get => GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    /// <summary>Events drawn as hairlines and dots on the plot, and as marks on the overview strip.</summary>
    public IReadOnlyList<ChartEvent>? Events
    {
        get => GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    /// <summary>Left edge of the selected window as a fraction of the series, from 0 to 1.</summary>
    public double WindowStart
    {
        get => GetValue(WindowStartProperty);
        set => SetValue(WindowStartProperty, value);
    }

    /// <summary>Right edge of the selected window as a fraction of the series, from 0 to 1.</summary>
    public double WindowEnd
    {
        get => GetValue(WindowEndProperty);
        set => SetValue(WindowEndProperty, value);
    }

    /// <summary>Draws quiet value gridlines. The baseline and axis labels stay either way.</summary>
    public bool ShowGrid
    {
        get => GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    /// <summary>Unit appended in the tooltip, such as " users".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Optional custom number format for tooltip values.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    protected override double DefaultHeight => 320;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        if (_drag != DragMode.None)
        {
            if (!_dragMoved && Math.Abs(point.X - _dragOriginX) < 3)
            {
                return;
            }

            _dragMoved = true;
            ApplyDrag(NormAt(point.X));
            e.Handled = true;
            return;
        }

        UpdateCursor(point);
        foreach (var hit in _eventHits)
        {
            if (!hit.Bounds.Contains(point))
            {
                continue;
            }

            SetActive(-1);
            SetHoverEvent(hit.Event);
            return;
        }

        SetHoverEvent(null);
        var count = PointCount();
        if (count < 2 || !_plot.Contains(point))
        {
            SetActive(-1);
            return;
        }

        UpdateReading(point, count);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_drag != DragMode.None)
        {
            return;
        }

        SetActive(-1);
        SetHoverEvent(null);
        Cursor = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (_keyboardFocus)
        {
            _keyboardFocus = false;
            InvalidateVisual();
        }

        var count = PointCount();
        if (count < 2 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        foreach (var hit in _eventHits)
        {
            if (!hit.Bounds.Contains(point))
            {
                continue;
            }

            RaiseItemClick(hit.Event.Index, hit.Event.Label, 0, point);
            e.Handled = true;
            return;
        }

        if (_strip.Contains(point))
        {
            if (e.ClickCount == 2)
            {
                ResetWindow();
                e.Handled = true;
                return;
            }

            BeginDrag(point, e);
            e.Handled = true;
            return;
        }

        if (_plot.Contains(point))
        {
            if (e.ClickCount == 2)
            {
                ResetWindow();
                e.Handled = true;
                return;
            }

            UpdateReading(point, count);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag == DragMode.None)
        {
            return;
        }

        if (_drag == DragMode.New && !_dragMoved)
        {
            var count = PointCount();
            var span = Math.Max(MinSpan(count), _dragFromEnd - _dragFromStart);
            var start = Math.Clamp(_dragAnchor - span / 2, 0, Math.Max(0, 1 - span));
            SetWindow(start, start + span);
        }

        _drag = DragMode.None;
        _dragMoved = false;

        var rangeSeries = Series;
        var rangeCount = rangeSeries?.Count ?? 0;
        if (rangeCount > 0)
        {
            var startIndex = Math.Clamp((int)Math.Round(WindowStart * (rangeCount - 1)), 0, rangeCount - 1);
            var endIndex = Math.Clamp((int)Math.Round(WindowEnd * (rangeCount - 1)), 0, rangeCount - 1);
            RaiseRangeSelect(startIndex, endIndex, rangeSeries![startIndex], rangeSeries[endIndex]);
        }

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _drag = DragMode.None;
        _dragMoved = false;
        Cursor = null;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = PointCount();
        if (count < 2)
        {
            return;
        }

        var (start, end) = EffectiveWindow(count);
        var minimum = MinSpan(count);
        var span = end - start;
        var nextStart = start;
        var nextEnd = end;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            InvalidateVisual();
        }

        switch (e.Key)
        {
            case Key.Left:
            case Key.Right:
            {
                var later = e.Key == Key.Right;
                if (shift)
                {
                    // Shift resizes around the middle of the window: left zooms in, right zooms out.
                    var nextSpan = Math.Clamp(span * (later ? 1.15 : 0.87), minimum, 1);
                    var middle = (start + end) / 2;
                    nextStart = Math.Clamp(middle - nextSpan / 2, 0, 1 - nextSpan);
                    nextEnd = nextStart + nextSpan;
                }
                else
                {
                    var delta = (later ? 1 : -1) * span * 0.1;
                    nextStart = start + delta;
                    nextEnd = end + delta;
                    if (nextStart < 0)
                    {
                        nextEnd -= nextStart;
                        nextStart = 0;
                    }

                    if (nextEnd > 1)
                    {
                        nextStart -= nextEnd - 1;
                        nextEnd = 1;
                    }
                }

                break;
            }

            case Key.PageUp:
            case Key.PageDown:
            {
                var delta = (e.Key == Key.PageDown ? 1 : -1) * span * 0.5;
                nextStart = start + delta;
                nextEnd = end + delta;
                if (nextStart < 0)
                {
                    nextEnd -= nextStart;
                    nextStart = 0;
                }

                if (nextEnd > 1)
                {
                    nextStart -= nextEnd - 1;
                    nextEnd = 1;
                }

                break;
            }

            case Key.OemPlus:
            case Key.Add:
            case Key.OemMinus:
            case Key.Subtract:
            {
                var zoomIn = e.Key is Key.OemPlus or Key.Add;
                var nextSpan = Math.Clamp(span * (zoomIn ? 0.85 : 1 / 0.85), minimum, 1);
                var middle = (start + end) / 2;
                nextStart = Math.Clamp(middle - nextSpan / 2, 0, 1 - nextSpan);
                nextEnd = nextStart + nextSpan;
                break;
            }

            case Key.Home:
            case Key.End:
                nextStart = 0;
                nextEnd = 1;
                break;

            case Key.Escape:
                SetActive(-1);
                SetHoverEvent(null);
                e.Handled = true;
                return;

            default:
                return;
        }

        SetWindow(nextStart, nextEnd);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _eventHits.Clear();
        _plot = default;
        _strip = default;

        var values = Series;
        var count = values?.Count ?? 0;
        if (values is null || count < 2)
        {
            DrawEmpty(context);
            return;
        }

        if (!Layout(out var plot, out var strip, out var axisCenter))
        {
            return;
        }

        _plot = plot;
        _strip = strip;

        var (start, end) = EffectiveWindow(count);
        var firstWindow = Math.Clamp((int)Math.Ceiling(start * (count - 1)), 0, count - 1);
        var lastWindow = Math.Clamp((int)Math.Floor(end * (count - 1)), 0, count - 1);
        if (lastWindow <= firstWindow)
        {
            lastWindow = Math.Min(count - 1, firstWindow + 1);
        }

        var first = Math.Max(0, firstWindow - 1);
        var last = Math.Min(count - 1, lastWindow + 1);
        var peak = 0.0;
        for (var index = first; index <= last; index++)
        {
            peak = Math.Max(peak, values[index]);
        }

        var (top, step) = NiceTop(peak * 1.04);
        var ticks = Math.Clamp((int)Math.Round(top / step), 1, 5);
        var seriesBrush = ChartPalette.Series(0);
        var events = VisibleEvents(count, start, end);

        var gridPen = new Pen(ChartPalette.Border, ChartMetrics.Hairline);
        var baselinePen = new Pen(ChartPalette.BorderStrong, ChartMetrics.Hairline);

        if (ShowGrid)
        {
            for (var tick = 1; tick <= ticks; tick++)
            {
                var y = Crisp(ValueY(tick * step, top, plot));
                context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            }
        }

        DrawEventRules(context, events, count, start, end, plot);

        var points = new List<Point>(last - first + 1);
        for (var index = first; index <= last; index++)
        {
            points.Add(new Point(PlotX(index, count, start, end, plot), ValueY(values[index], top, plot)));
        }

        var pointsPerPixel = (end - start) * (count - 1);
        var pixelsPerPoint = pointsPerPixel <= 0 ? plot.Width : plot.Width / pointsPerPixel;
        using (context.PushClip(plot))
        {
            if (pixelsPerPoint < 3.25)
            {
                // Zoomed out, a centred seven point average and its low to high band read as texture.
                DrawTrend(context, values, first, last, count, start, end, top, plot, seriesBrush);
            }
            else if (points.Count > 1)
            {
                context.DrawGeometry(
                    ChartPalette.Alpha(seriesBrush, 0.10),
                    null,
                    ChartGeometry.Area(points, plot.Bottom, false));
                context.DrawGeometry(null, ChartPalette.Pen(seriesBrush, 1.75), ChartGeometry.Line(points, false));
            }
        }

        context.DrawLine(
            baselinePen,
            new Point(plot.Left, plot.Bottom - 0.5),
            new Point(plot.Right, plot.Bottom - 0.5));

        DrawValueAxis(context, plot, top, step, ticks);
        DrawDateAxis(context, count, firstWindow, lastWindow, start, end, plot, axisCenter);
        DrawEventRail(context, events, count, start, end, plot);
        DrawStrip(context, values, count, start, end);

        if (_hoverEvent is { } hover && InWindow(hover.Index, count, start, end))
        {
            DrawEventTooltip(context, hover, count, start, end, plot);
        }
        else if (_activeIndex >= 0 && _activeIndex < count)
        {
            DrawCrosshair(context, values, count, plot, start, end, top);
        }

        if (_keyboardFocus && IsFocused)
        {
            context.DrawRectangle(
                null,
                new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2),
                new RoundedRect(new Rect(1.5, 1.5, Math.Max(0, Bounds.Width - 3), Math.Max(0, Bounds.Height - 3)), 12));
        }
    }

    private bool Layout(out Rect plot, out Rect strip, out double axisCenter)
    {
        plot = default;
        strip = default;
        axisCenter = 0;
        var stripHeight = Math.Clamp(Bounds.Height * 0.18, 44, 64);
        var plotLeft = 2.0;
        var plotRight = Bounds.Width - GutterWidth;
        var plotWidth = plotRight - plotLeft;
        var stripTop = Bounds.Height - stripHeight;
        var plotBottom = stripTop - StripGap - AxisHeight;
        var plotTop = Math.Min(EventRail, Math.Max(10, plotBottom - 80));
        if (plotWidth < 60 || plotBottom - plotTop < 60)
        {
            return false;
        }

        plot = new Rect(plotLeft, plotTop, plotWidth, plotBottom - plotTop);
        strip = new Rect(plotLeft, stripTop, plotWidth, stripHeight);
        axisCenter = plotBottom + AxisHeight / 2;
        return true;
    }

    private void DrawValueAxis(DrawingContext context, Rect plot, double top, double step, int ticks)
    {
        for (var tick = 0; tick <= ticks; tick++)
        {
            var value = tick * step;
            var y = ValueY(value, top, plot);
            ChartText.Draw(
                context,
                ChartFormat.Compact(value),
                plot.Right + 8,
                y,
                ChartMetrics.LabelFontSize,
                ChartPalette.Muted);
        }
    }

    private void DrawDateAxis(
        DrawingContext context,
        int count,
        int first,
        int last,
        double start,
        double end,
        Rect plot,
        double centerY)
    {
        var span = Math.Max(1, last - first);
        var room = Math.Max(2, (int)(plot.Width / 88));
        var step = Math.Max(1, (int)Math.Ceiling(span / (double)room));
        var drawn = -1;
        for (var index = first; index <= last; index += step)
        {
            DrawDateLabel(index);
            drawn = index;
        }

        if (drawn >= 0 && last != drawn &&
            PlotX(last, count, start, end, plot) - PlotX(drawn, count, start, end, plot) >= 56)
        {
            DrawDateLabel(last);
        }

        void DrawDateLabel(int index)
        {
            var label = LabelAt(index);
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            var text = ChartText.Create(label, ChartMetrics.LabelFontSize, ChartPalette.Muted, maxWidth: 72);
            var x = PlotX(index, count, start, end, plot);
            x = Math.Clamp(
                x,
                plot.Left + text.Width / 2,
                Math.Max(plot.Left + text.Width / 2, plot.Right - text.Width / 2));
            ChartText.Draw(context, text, x, centerY, TextAlignment.Center);
        }
    }

    private void DrawEventRules(
        DrawingContext context,
        IReadOnlyList<ChartEvent> events,
        int count,
        double start,
        double end,
        Rect plot)
    {
        foreach (var item in events)
        {
            var x = Math.Round(PlotX(item.Index, count, start, end, plot)) + 0.5;
            var pen = _hoverEvent?.Index == item.Index
                ? new Pen(EventBrush(item.Tone), ChartMetrics.Hairline)
                : new Pen(ChartPalette.BorderStrong, ChartMetrics.Hairline);
            context.DrawLine(pen, new Point(x, plot.Top - 10), new Point(x, plot.Bottom));
        }
    }

    private void DrawEventRail(
        DrawingContext context,
        IReadOnlyList<ChartEvent> events,
        int count,
        double start,
        double end,
        Rect plot)
    {
        var railY = plot.Top - 10;
        var edge = double.NegativeInfinity;
        var previousX = double.NegativeInfinity;
        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            var x = PlotX(item.Index, count, start, end, plot);
            var nextX = index + 1 < events.Count
                ? PlotX(events[index + 1].Index, count, start, end, plot)
                : plot.Right;
            var active = _hoverEvent?.Index == item.Index;
            var brush = EventBrush(item.Tone);
            var dot = new Point(x, railY);
            context.DrawEllipse(ChartPalette.Surface, null, dot, active ? 5.4 : 4.4, active ? 5.4 : 4.4);
            context.DrawEllipse(brush, null, dot, active ? 4 : 3.2, active ? 4 : 3.2);
            _eventHits.Add((new Rect(x - 12, railY - 12, 24, 24), item));

            var text = ChartText.Create(
                item.Label,
                ChartMetrics.CaptionFontSize,
                active ? ChartPalette.Foreground : ChartPalette.Secondary,
                maxWidth: 104);
            var right = x + 16;
            var limit = Math.Min(plot.Right, nextX - 24);
            if (right >= edge && right + text.Width <= limit)
            {
                ChartText.Draw(context, text, right, railY);
                _eventHits.Add((new Rect(right - 2, railY - 10, text.Width + 4, 20), item));
                edge = right + text.Width + 10;
            }
            else if (x - 16 - text.Width >= Math.Max(Math.Max(edge, plot.Left), previousX + 8))
            {
                ChartText.Draw(context, text, x - 16, railY, TextAlignment.Right);
                _eventHits.Add((new Rect(x - 16 - text.Width - 2, railY - 10, text.Width + 4, 20), item));
                edge = Math.Max(edge, x + 8);
            }
            else
            {
                edge = Math.Max(edge, x + 8);
            }

            previousX = x;
        }
    }

    private void DrawStrip(DrawingContext context, IReadOnlyList<double> values, int count, double start, double end)
    {
        var strip = _strip;
        context.DrawRectangle(
            ChartPalette.Alpha(ChartPalette.Foreground, 0.025),
            null,
            strip,
            StripRadius,
            StripRadius);

        var top = strip.Top + 5;
        var bottom = strip.Bottom - 3;
        var height = Math.Max(1, bottom - top);
        var maximum = 0.0;
        for (var index = 0; index < count; index++)
        {
            maximum = Math.Max(maximum, values[index]);
        }

        maximum = maximum <= 0 ? 1 : maximum * 1.05;

        double Ox(double index)
            => count <= 1 ? strip.Center.X : strip.Left + index / (count - 1) * strip.Width;
        double Oy(double value) => bottom - value / maximum * height;

        var stride = Math.Max(1, count / Math.Max(1, (int)(strip.Width * 2)));
        var points = new List<Point>(count / stride + 2);
        for (var index = 0; index < count; index += stride)
        {
            points.Add(new Point(Ox(index), Oy(values[index])));
        }

        if (points.Count > 0 && points[^1].X < strip.Right - 0.5)
        {
            points.Add(new Point(Ox(count - 1), Oy(values[count - 1])));
        }

        var seriesBrush = ChartPalette.Series(0);
        var mutedLine = Blend(ChartPalette.Foreground, ChartPalette.Surface, 0.30);
        var windowStart = strip.Left + start * strip.Width;
        var windowEnd = strip.Left + end * strip.Width;

        using (context.PushClip(new RoundedRect(strip, StripRadius)))
        {
            if (points.Count > 1)
            {
                context.DrawGeometry(
                    ChartPalette.Alpha(ChartPalette.Foreground, 0.05),
                    null,
                    ChartGeometry.Area(points, strip.Bottom, false));
                context.DrawGeometry(null, ChartPalette.Pen(mutedLine, 1), ChartGeometry.Line(points, false));
            }

            using (context.PushClip(new Rect(
                windowStart,
                strip.Top - 1,
                Math.Max(0, windowEnd - windowStart),
                strip.Height + 2)))
            {
                if (points.Count > 1)
                {
                    context.DrawGeometry(
                        ChartPalette.Alpha(seriesBrush, 0.12),
                        null,
                        ChartGeometry.Area(points, strip.Bottom, false));
                    context.DrawGeometry(null, ChartPalette.Pen(seriesBrush, 1.25), ChartGeometry.Line(points, false));
                }
            }

            if (Events is not null)
            {
                foreach (var item in Events)
                {
                    if (item.Index < 0 || item.Index >= count)
                    {
                        continue;
                    }

                    var mark = new Point(Ox(item.Index), Oy(values[item.Index]));
                    context.DrawEllipse(ChartPalette.Surface, null, mark, 3, 3);
                    context.DrawEllipse(EventBrush(item.Tone), null, mark, 2.2, 2.2);
                }
            }

            var shade = ChartPalette.Alpha(ChartPalette.Surface, 0.45);
            if (windowStart > strip.Left)
            {
                context.DrawRectangle(
                    shade,
                    null,
                    new Rect(strip.Left, strip.Top, windowStart - strip.Left, strip.Height));
            }

            if (windowEnd < strip.Right)
            {
                context.DrawRectangle(
                    shade,
                    null,
                    new Rect(windowEnd, strip.Top, strip.Right - windowEnd, strip.Height));
            }
        }

        var ring = new Rect(
            windowStart + 0.5,
            strip.Top + 0.5,
            Math.Max(0, windowEnd - windowStart - 1),
            Math.Max(0, strip.Height - 1));
        context.DrawRectangle(
            null,
            new Pen(ChartPalette.Alpha(seriesBrush, 0.45), 1),
            new RoundedRect(ring, Math.Max(2, Math.Min(StripRadius, Math.Min(ring.Width, ring.Height) / 2))));

        DrawGrip(context, strip, windowStart);
        DrawGrip(context, strip, windowEnd);
    }

    private static void DrawGrip(DrawingContext context, Rect strip, double x)
    {
        var center = Math.Clamp(x, strip.Left + 5, strip.Right - 5);
        var rect = new RoundedRect(new Rect(center - 3, strip.Center.Y - 11, 6, 22), 3);
        context.DrawRectangle(
            ChartPalette.SurfaceRaised,
            new Pen(ChartPalette.Alpha(ChartPalette.Foreground, 0.26), 1),
            rect);
    }

    private void DrawCrosshair(
        DrawingContext context,
        IReadOnlyList<double> values,
        int count,
        Rect plot,
        double start,
        double end,
        double top)
    {
        var index = _activeIndex;
        var position = index / (double)(count - 1);
        if (position < start || position > end)
        {
            return;
        }

        var x = Math.Round(PlotX(index, count, start, end, plot)) + 0.5;
        var value = values[index];
        var y = ValueY(value, top, plot);
        context.DrawLine(
            new Pen(ChartPalette.Alpha(ChartPalette.Foreground, 0.45), 1),
            new Point(x, plot.Top - 10),
            new Point(x, plot.Bottom));
        context.DrawEllipse(ChartPalette.Surface, null, new Point(x, y), 4.5, 4.5);
        context.DrawEllipse(ChartPalette.Series(0), null, new Point(x, y), 3.1, 3.1);

        var rows = new List<(string Label, string Value, IBrush? Brush)>
        {
            ("Value", FormatValue(value), ChartPalette.Series(0))
        };
        if (index >= 7 && values[index - 7] > 0)
        {
            var change = (value - values[index - 7]) / values[index - 7] * 100;
            rows.Add((
                "vs a week ago",
                (change >= 0 ? "+" : "\u2212") + Math.Abs(change).ToString("0.0", Culture) + "%",
                null));
        }

        var at = EventAt(index, count, start, end);
        if (at is not null)
        {
            rows.Add(("Event", at.Label, EventBrush(at.Tone)));
        }

        ChartTooltip.Draw(
            context,
            Bounds,
            new Point(x, y),
            TooltipLabel(index) ?? $"Point {index + 1}",
            rows);
    }

    private void DrawEventTooltip(
        DrawingContext context,
        ChartEvent item,
        int count,
        double start,
        double end,
        Rect plot)
    {
        var rows = new List<(string Label, string Value, IBrush? Brush)>
        {
            ("Event", item.Label, EventBrush(item.Tone))
        };
        var date = TooltipLabel(item.Index);
        if (!string.IsNullOrEmpty(date))
        {
            rows.Add(("Date", date, null));
        }

        if (!string.IsNullOrWhiteSpace(item.Description))
        {
            rows.Add(("Note", item.Description!, null));
        }

        ChartTooltip.Draw(
            context,
            Bounds,
            new Point(PlotX(item.Index, count, start, end, plot), plot.Top + 34),
            date ?? item.Label,
            rows);
    }

    private static void DrawTrend(
        DrawingContext context,
        IReadOnlyList<double> values,
        int first,
        int last,
        int count,
        double start,
        double end,
        double top,
        Rect plot,
        IBrush brush)
    {
        var average = new double[count];
        var low = new double[count];
        var high = new double[count];
        for (var index = 0; index < count; index++)
        {
            var from = Math.Max(0, index - 3);
            var to = Math.Min(count - 1, index + 3);
            var sum = 0.0;
            var minimum = double.MaxValue;
            var maximum = double.MinValue;
            for (var at = from; at <= to; at++)
            {
                sum += values[at];
                minimum = Math.Min(minimum, values[at]);
                maximum = Math.Max(maximum, values[at]);
            }

            var length = to - from + 1;
            average[index] = sum / length;
            low[index] = minimum;
            high[index] = maximum;
        }

        for (var index = 0; index < count; index++)
        {
            var from = Math.Max(0, index - 3);
            var to = Math.Min(count - 1, index + 3);
            var lowSum = 0.0;
            var highSum = 0.0;
            for (var at = from; at <= to; at++)
            {
                lowSum += low[at];
                highSum += high[at];
            }

            var length = to - from + 1;
            low[index] = lowSum / length;
            high[index] = highSum / length;
        }

        var trend = new List<Point>(last - first + 1);
        var upper = new List<Point>(last - first + 1);
        var lower = new List<Point>(last - first + 1);
        for (var index = first; index <= last; index++)
        {
            var x = PlotX(index, count, start, end, plot);
            trend.Add(new Point(x, ValueY(average[index], top, plot)));
            upper.Add(new Point(x, ValueY(high[index], top, plot)));
            lower.Add(new Point(x, ValueY(low[index], top, plot)));
        }

        context.DrawGeometry(ChartPalette.Alpha(brush, 0.14), null, Band(upper, lower));
        context.DrawGeometry(null, ChartPalette.Pen(brush, 1.75), ChartGeometry.Line(trend, false));
    }

    private static StreamGeometry Band(IReadOnlyList<Point> upper, IReadOnlyList<Point> lower)
    {
        var geometry = new StreamGeometry();
        if (upper.Count < 2 || lower.Count < 2)
        {
            return geometry;
        }

        using var context = geometry.Open();
        context.BeginFigure(upper[0], true);
        for (var index = 1; index < upper.Count; index++)
        {
            context.LineTo(upper[index]);
        }

        for (var index = lower.Count - 1; index >= 0; index--)
        {
            context.LineTo(lower[index]);
        }

        context.EndFigure(true);
        return geometry;
    }

    private void DrawEmpty(DrawingContext context)
    {
        ChartText.Draw(
            context,
            "No data yet",
            Bounds.Width / 2,
            Bounds.Height / 2,
            ChartMetrics.TitleFontSize,
            ChartPalette.Muted,
            alignment: TextAlignment.Center);
    }

    private void BeginDrag(Point point, PointerPressedEventArgs e)
    {
        var count = PointCount();
        var (start, end) = EffectiveWindow(count);
        var strip = _strip;
        var startX = strip.Left + start * strip.Width;
        var endX = strip.Left + end * strip.Width;
        _drag = Math.Abs(point.X - startX) <= HandleZone && point.X <= (startX + endX) / 2
            ? DragMode.Start
            : Math.Abs(point.X - endX) <= HandleZone
                ? DragMode.End
                : point.X > startX && point.X < endX
                    ? DragMode.Pan
                    : DragMode.New;
        _dragOriginX = point.X;
        _dragAnchor = NormAt(point.X);
        _dragFromStart = start;
        _dragFromEnd = end;
        _dragMoved = false;
        e.Pointer.Capture(this);
        SetActive(-1);
        SetHoverEvent(null);
        Cursor = _drag switch
        {
            DragMode.Pan => GrabCursor,
            DragMode.Start or DragMode.End => ResizeCursor,
            _ => CrossCursor
        };
    }

    private void ApplyDrag(double position)
    {
        var count = PointCount();
        var minimum = MinSpan(count);
        var start = _dragFromStart;
        var end = _dragFromEnd;

        switch (_drag)
        {
            case DragMode.Pan:
            {
                var span = end - start;
                var shift = position - _dragAnchor;
                start = ChartFormat.Clamp01(start + shift);
                end = start + span;
                if (end > 1)
                {
                    end = 1;
                    start = 1 - span;
                }

                break;
            }

            case DragMode.Start:
                start = Math.Clamp(position, 0, end - minimum);
                break;

            case DragMode.End:
                end = Math.Clamp(position, start + minimum, 1);
                break;

            case DragMode.New:
            {
                var anchor = _dragAnchor;
                start = Math.Min(anchor, position);
                end = Math.Max(anchor, position);
                if (end - start < minimum)
                {
                    if (position >= anchor)
                    {
                        end = Math.Min(1, start + minimum);
                    }
                    else
                    {
                        start = Math.Max(0, end - minimum);
                    }
                }

                break;
            }
        }

        SetWindow(start, end);
    }

    private void UpdateReading(Point point, int count)
    {
        var (start, end) = EffectiveWindow(count);
        var position = ChartFormat.Clamp01((point.X - _plot.Left) / _plot.Width);
        var time = start + position * (end - start);
        var index = (int)Math.Round(time * (count - 1));
        SetActive(Math.Clamp(index, 0, count - 1));
    }

    private void UpdateCursor(Point point)
    {
        var count = PointCount();
        if (count < 2 || _strip.Width <= 0)
        {
            Cursor = _plot.Contains(point) ? CrossCursor : null;
            return;
        }

        if (_strip.Contains(point))
        {
            var (start, end) = EffectiveWindow(count);
            var startX = _strip.Left + start * _strip.Width;
            var endX = _strip.Left + end * _strip.Width;
            Cursor = Math.Abs(point.X - startX) <= HandleZone || Math.Abs(point.X - endX) <= HandleZone
                ? ResizeCursor
                : point.X > startX && point.X < endX
                    ? GrabCursor
                    : CrossCursor;
            return;
        }

        Cursor = _plot.Contains(point) ? CrossCursor : null;
    }

    private void SetWindow(double start, double end)
    {
        start = ChartFormat.Clamp01(start);
        end = ChartFormat.Clamp01(end);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        SetCurrentValue(WindowStartProperty, start);
        SetCurrentValue(WindowEndProperty, end);
    }

    private void ResetWindow() => SetWindow(0, 1);

    private void SetActive(int index)
    {
        if (_activeIndex == index)
        {
            return;
        }

        _activeIndex = index;
        InvalidateVisual();
    }

    private void SetHoverEvent(ChartEvent? item)
    {
        if (_hoverEvent?.Index == item?.Index && _hoverEvent?.Label == item?.Label)
        {
            return;
        }

        _hoverEvent = item;
        InvalidateVisual();
    }

    private void ResetInteraction()
    {
        _activeIndex = -1;
        _hoverEvent = null;
        _drag = DragMode.None;
        _dragMoved = false;
        InvalidateVisual();
    }

    private (double Start, double End) EffectiveWindow(int count)
    {
        var start = ChartFormat.Clamp01(WindowStart);
        var end = ChartFormat.Clamp01(WindowEnd);
        if (end < start)
        {
            (start, end) = (end, start);
        }

        var minimum = MinSpan(count);
        if (end - start >= minimum)
        {
            return (start, end);
        }

        var middle = (start + end) / 2;
        start = middle - minimum / 2;
        end = middle + minimum / 2;
        if (start < 0)
        {
            end -= start;
            start = 0;
        }

        if (end > 1)
        {
            start -= end - 1;
            end = 1;
        }

        return (Math.Max(0, start), end);
    }

    private static double MinSpan(int count)
        => Math.Min(0.5, MinimumWindowPoints / (double)Math.Max(1, count - 1));

    private List<ChartEvent> VisibleEvents(int count, double start, double end)
    {
        var result = new List<ChartEvent>();
        if (Events is null)
        {
            return result;
        }

        foreach (var item in Events)
        {
            if (InWindow(item.Index, count, start, end))
            {
                result.Add(item);
            }
        }

        result.Sort((left, right) => left.Index.CompareTo(right.Index));
        return result;
    }

    private static bool InWindow(int index, int count, double start, double end)
    {
        if (index < 0 || index >= count)
        {
            return false;
        }

        var position = index / (double)(count - 1);
        return position >= start - 1e-9 && position <= end + 1e-9;
    }

    private ChartEvent? EventAt(int index, int count, double start, double end)
    {
        if (Events is null)
        {
            return null;
        }

        foreach (var item in Events)
        {
            if (item.Index == index && InWindow(item.Index, count, start, end))
            {
                return item;
            }
        }

        return null;
    }

    private double NormAt(double x)
        => _strip.Width <= 0 ? 0 : ChartFormat.Clamp01((x - _strip.Left) / _strip.Width);

    private int PointCount() => Series?.Count ?? 0;

    private string? LabelAt(int index)
    {
        var labels = Labels;
        if (labels is not null && index >= 0 && index < labels.Count)
        {
            return labels[index];
        }

        return Start is { } start
            ? start.AddDays(index).ToString("MMM d", Culture)
            : null;
    }

    private string? TooltipLabel(int index)
    {
        var labels = Labels;
        if (labels is not null && index >= 0 && index < labels.Count)
        {
            return labels[index];
        }

        return Start is { } start
            ? start.AddDays(index).ToString("MMM d, yyyy", Culture)
            : null;
    }

    private string FormatValue(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Number(value, 0);
        return string.IsNullOrEmpty(ValueSuffix) ? formatted : formatted + ValueSuffix;
    }

    private static IBrush EventBrush(ChartTone tone)
        => tone == ChartTone.Accent ? ChartPalette.Series(1) : ChartPalette.Tone(tone);

    private static double PlotX(double index, int count, double start, double end, Rect plot)
    {
        var span = end - start;
        if (count <= 1 || span <= 1e-9)
        {
            return plot.Center.X;
        }

        return plot.Left + (index / (count - 1) - start) / span * plot.Width;
    }

    private static double ValueY(double value, double top, Rect plot)
        => plot.Bottom - value / (top <= 0 ? 1 : top) * plot.Height;

    /// <summary>Clean gridlines from zero: the smallest step of 1, 2, 2.5, or 5 that covers the peak in five rows.</summary>
    private static (double Top, double Step) NiceTop(double high)
    {
        if (double.IsNaN(high) || double.IsInfinity(high) || high <= 0)
        {
            high = 1;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(high / 4)));
        foreach (var factor in new[] { 1.0, 2.0, 2.5, 5.0, 10.0 })
        {
            var step = factor * magnitude;
            var top = Math.Ceiling(high / step) * step;
            if (top / step <= 5 + 1e-9)
            {
                return (Clean(top), Clean(step));
            }
        }

        return (high, high / 4);
    }

    private static double Clean(double value) => Math.Round(value, 10);

    private static IBrush Blend(IBrush color, IBrush backdrop, double amount)
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
}
