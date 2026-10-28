using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Charts;

/// <summary>
/// A ranking over time: bars overtake each other as the periods play, values count between frames, the
/// period rolls in large type, and a timeline plays, pauses, and scrubs. Rows move on their own eased
/// track, so an overtake reads as one continuous slide rather than a jump.
/// </summary>
/// <remarks>
/// Approximation: Arc Pro autoplays the first time the chart scrolls into view on a spring clock and can
/// group bars under named groups. This port keeps playback explicit (<see cref="IsPlaying"/>), starts on
/// the frame the host asks for, and colours every bar with the first series hue, which is what the public
/// docs describe for an ungrouped race. With reduced motion on, playback is skipped and rows are placed
/// immediately.
/// </remarks>
public class ArcRaceBarChart : ArcChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ArcRaceFrame>?> FramesProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, IReadOnlyList<ArcRaceFrame>?>(nameof(Frames));

    public static readonly StyledProperty<int> TopNProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, int>(nameof(TopN), 6);

    public static readonly StyledProperty<bool> ShowControlsProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, bool>(nameof(ShowControls), true);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, bool>(nameof(IsPlaying));

    public static readonly StyledProperty<double> FrameDurationProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, double>(nameof(FrameDuration), 1400);

    public static readonly StyledProperty<string?> ValueSuffixProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, string?>(nameof(ValueSuffix));

    public static readonly StyledProperty<Func<double, string>?> ValueFormatProperty =
        AvaloniaProperty.Register<ArcRaceBarChart, Func<double, string>?>(nameof(ValueFormat));

    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    private readonly Dictionary<string, double> _rowY = new(StringComparer.Ordinal);
    private readonly List<(Rect Bounds, string Entity)> _barHits = new();
    private DispatcherTimer? _timer;
    private Stopwatch? _watch;
    private double _lastTick;
    private double _position;
    private double _displayMax;
    private Rect _plot;
    private Rect _playRect;
    private Rect _trackRect;
    private bool _scrubbing;
    private bool _playHover;
    private int _hoverIndex = -1;
    private bool _keyboardFocus;

    static ArcRaceBarChart()
    {
        AffectsRender<ArcRaceBarChart>(
            FramesProperty,
            TopNProperty,
            ShowControlsProperty,
            IsPlayingProperty,
            FrameDurationProperty,
            ValueSuffixProperty,
            ValueFormatProperty);
        FramesProperty.Changed.AddClassHandler<ArcRaceBarChart>((chart, _) => chart.ResetFrames());
        IsPlayingProperty.Changed.AddClassHandler<ArcRaceBarChart>((chart, _) => chart.SyncTimer());
    }

    public ArcRaceBarChart()
    {
        Focusable = true;
    }

    /// <summary>One frame per period, in order. Missing entities count as zero.</summary>
    public IReadOnlyList<ArcRaceFrame>? Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
    }

    /// <summary>Bars shown at once. Entities below the cut slide out of view.</summary>
    public int TopN
    {
        get => GetValue(TopNProperty);
        set => SetValue(TopNProperty, value);
    }

    /// <summary>Draws the play button and the timeline scrubber.</summary>
    public bool ShowControls
    {
        get => GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    /// <summary>Whether the race is advancing. Space or the play button toggles it.</summary>
    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>Milliseconds each period is held while playing.</summary>
    public double FrameDuration
    {
        get => GetValue(FrameDurationProperty);
        set => SetValue(FrameDurationProperty, value);
    }

    /// <summary>Unit after values, such as " visits".</summary>
    public string? ValueSuffix
    {
        get => GetValue(ValueSuffixProperty);
        set => SetValue(ValueSuffixProperty, value);
    }

    /// <summary>Formats values on the bars, the axis, and the tooltip.</summary>
    public Func<double, string>? ValueFormat
    {
        get => GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    /// <summary>The period on show. Setting it moves the race there without playing.</summary>
    public int FrameIndex
    {
        get => (int)Math.Round(_position);
        set
        {
            var count = Frames?.Count ?? 0;
            if (count == 0)
            {
                return;
            }

            _position = Math.Clamp(value, 0, count - 1);
            EnsureAnimating();
            InvalidateVisual();
        }
    }

    protected override double DefaultHeight => 244;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SyncTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);

        if (_scrubbing)
        {
            ScrubTo(point.X);
            return;
        }

        var overPlay = _playRect.Contains(point);
        if (overPlay != _playHover)
        {
            _playHover = overPlay;
            InvalidateVisual();
        }

        Cursor = overPlay || _trackRect.Contains(point) ? HandCursor : null;
        var hover = -1;
        for (var index = 0; index < _barHits.Count; index++)
        {
            if (_barHits[index].Bounds.Contains(point))
            {
                hover = index;
                break;
            }
        }

        if (hover != _hoverIndex)
        {
            _hoverIndex = hover;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _playHover = false;
        _hoverIndex = -1;
        Cursor = null;
        InvalidateVisual();
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

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (_playRect.Contains(point))
        {
            TogglePlay();
            e.Handled = true;
            return;
        }

        if (ShowControls && _trackRect.Contains(point))
        {
            _scrubbing = true;
            e.Pointer.Capture(this);
            ScrubTo(point.X);
            e.Handled = true;
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_scrubbing)
        {
            return;
        }

        _scrubbing = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _scrubbing = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Frames?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        if (!_keyboardFocus)
        {
            _keyboardFocus = true;
            InvalidateVisual();
        }

        switch (e.Key)
        {
            case Key.Space:
            case Key.Enter:
                TogglePlay();
                break;
            case Key.Right:
                SetCurrentValue(IsPlayingProperty, false);
                _position = Math.Min(count - 1, Math.Round(_position) + 1);
                EnsureAnimating();
                break;
            case Key.Left:
                SetCurrentValue(IsPlayingProperty, false);
                _position = Math.Max(0, Math.Round(_position) - 1);
                EnsureAnimating();
                break;
            case Key.Home:
                SetCurrentValue(IsPlayingProperty, false);
                _position = 0;
                EnsureAnimating();
                break;
            case Key.End:
                SetCurrentValue(IsPlayingProperty, false);
                _position = count - 1;
                EnsureAnimating();
                break;
            case Key.Up:
            case Key.Down:
            {
                var rows = BuildRows(out _, out var ranked);
                if (ranked.Count == 0)
                {
                    return;
                }

                var next = _hoverIndex < 0 ? 0 : _hoverIndex + (e.Key == Key.Down ? 1 : -1);
                _hoverIndex = Math.Clamp(next, 0, ranked.Count - 1);
                InvalidateVisual();
                break;
            }

            case Key.Escape:
                _hoverIndex = -1;
                InvalidateVisual();
                e.Handled = true;
                return;
            default:
                return;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        _barHits.Clear();
        _playRect = default;
        _trackRect = default;

        var frames = Frames;
        if (frames is null || frames.Count == 0)
        {
            ChartText.Draw(
                context,
                "No data",
                Bounds.Width / 2,
                Bounds.Height / 2,
                13,
                ChartPalette.Muted,
                alignment: TextAlignment.Center);
            return;
        }

        _position = Math.Clamp(_position, 0, frames.Count - 1);
        var periodIndex = Math.Clamp((int)Math.Round(_position), 0, frames.Count - 1);
        var controls = ShowControls;
        var plotBottom = controls ? Bounds.Height - 40 : Bounds.Height - 8;
        _plot = new Rect(6, 34, Math.Max(20, Bounds.Width - 16), Math.Max(20, plotBottom - 34));
        if (_plot.Width < 40 || _plot.Height < 30)
        {
            return;
        }

        var rows = BuildRows(out var maximum, out var ranked);
        var topN = Math.Max(1, TopN);
        var rowHeight = _plot.Height / topN;
        if (_displayMax <= 0 || ArcThemeManager.ReducedMotion)
        {
            _displayMax = maximum;
        }

        // Period in large type, then the value axis along the top edge.
        ChartText.Draw(
            context,
            frames[periodIndex].Period,
            4,
            14,
            18,
            ChartPalette.Foreground,
            FontWeight.Medium,
            maxWidth: Math.Max(60, Bounds.Width * 0.55));

        var scalePen = new Pen(ChartPalette.BorderSubtle, 1);
        var barWidth = Math.Max(40, _plot.Width - 46);
        for (var tick = 0; tick <= 3; tick++)
        {
            var value = _displayMax * tick / 3;
            var x = Crisp(_plot.Left + barWidth * tick / 3);
            if (tick > 0)
            {
                context.DrawLine(scalePen, new Point(x, _plot.Top), new Point(x, _plot.Bottom));
                ChartText.Draw(
                    context,
                    ChartFormat.Compact(value),
                    x,
                    _plot.Top - 8,
                    11,
                    ChartPalette.Muted,
                    alignment: tick == 3 ? TextAlignment.Right : TextAlignment.Center);
            }
        }

        var barBrush = ChartPalette.Series(0);
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var y = ArcThemeManager.ReducedMotion || !_rowY.TryGetValue(row.Entity, out var current)
                ? row.TargetY
                : current;
            var height = Math.Min(rowHeight * 0.62, 22);
            var width = Math.Max(2, row.Value / Math.Max(1e-6, _displayMax) * barWidth);
            var rect = new Rect(_plot.Left, y - height / 2, width, height);
            var hovered = index == _hoverIndex;
            var brush = _hoverIndex >= 0 && !hovered ? ChartPalette.Alpha(barBrush, 0.55) : barBrush;
            context.DrawRectangle(brush, null, new RoundedRect(rect, Math.Min(5, height / 2)));

            var valueText = ChartText.Create(FormatValue(row.Value), 12, ChartPalette.Foreground, FontWeight.Medium);
            var natural = ChartText.Width(row.Entity, 12, FontWeight.Medium);
            if (natural <= width - 18)
            {
                var inside = ChartText.Create(
                    row.Entity,
                    12,
                    AdvancedChartInk.OnFill(brush),
                    FontWeight.Medium,
                    Math.Max(20, width - 18));
                context.DrawText(inside, new Point(rect.X + 9, rect.Center.Y - inside.Height / 2));
                context.DrawText(valueText, new Point(rect.Right + 8, rect.Center.Y - valueText.Height / 2));
                _barHits.Add((new Rect(rect.X, rect.Y, rect.Width + valueText.Width + 14, height), row.Entity));
            }
            else
            {
                var outside = ChartText.Create(
                    row.Entity,
                    12,
                    ChartPalette.Foreground,
                    FontWeight.Medium,
                    Math.Max(24, barWidth - width - valueText.Width - 22));
                context.DrawText(outside, new Point(rect.Right + 8, rect.Center.Y - outside.Height / 2));
                context.DrawText(
                    valueText,
                    new Point(Math.Min(rect.Right + 12 + outside.Width, Bounds.Width - valueText.Width - 4), rect.Center.Y - valueText.Height / 2));
                _barHits.Add((new Rect(rect.X, rect.Y, rect.Width + outside.Width + valueText.Width + 20, height), row.Entity));
            }
        }

        if (controls)
        {
            DrawControls(context, frames.Count);
        }

        if (_hoverIndex >= 0 && _hoverIndex < rows.Count)
        {
            var row = rows[_hoverIndex];
            ChartTooltip.Draw(
                context,
                Bounds,
                new Point(_plot.Left + 40, row.TargetY),
                row.Entity,
                new[]
                {
                    ("Value", FormatValue(row.Value), (IBrush?)barBrush),
                    ("Rank", $"#{_hoverIndex + 1} of {ranked.Count}", null),
                    ("Period", frames[periodIndex].Period, null)
                });
        }

        if (_keyboardFocus && IsFocused)
        {
            context.DrawRectangle(
                null,
                new Pen(ArcResources.Brush(ArcTokens.FocusOutline), 2),
                new RoundedRect(
                    new Rect(1.5, 1.5, Math.Max(0, Bounds.Width - 3), Math.Max(0, Bounds.Height - 3)),
                    12));
        }
    }

    private void DrawControls(DrawingContext context, int frameCount)
    {
        _playRect = new Rect(6, Bounds.Height - 32, 28, 28);
        context.DrawEllipse(
            _playHover ? ChartPalette.Alpha(ChartPalette.BorderSubtle, 0.9) : ChartPalette.Surface,
            new Pen(ChartPalette.Border, 1),
            _playRect.Center,
            13,
            13);
        var iconBrush = ChartPalette.Secondary;
        if (IsPlaying)
        {
            context.DrawRectangle(iconBrush, null, new Rect(_playRect.Center.X - 5, _playRect.Center.Y - 5.5, 3.2, 11), 1, 1);
            context.DrawRectangle(iconBrush, null, new Rect(_playRect.Center.X + 1.8, _playRect.Center.Y - 5.5, 3.2, 11), 1, 1);
        }
        else
        {
            var geometry = new StreamGeometry();
            using (var stream = geometry.Open())
            {
                stream.BeginFigure(new Point(_playRect.Center.X - 4, _playRect.Center.Y - 6), true);
                stream.LineTo(new Point(_playRect.Center.X + 6, _playRect.Center.Y));
                stream.LineTo(new Point(_playRect.Center.X - 4, _playRect.Center.Y + 6));
                stream.EndFigure(true);
            }

            context.DrawGeometry(iconBrush, null, geometry);
        }

        _trackRect = new Rect(44, Bounds.Height - 30, Math.Max(30, Bounds.Width - 54), 24);
        var trackY = _trackRect.Center.Y;
        var track = new Rect(_trackRect.Left, trackY - 2, _trackRect.Width, 4);
        context.DrawRectangle(ChartPalette.BorderSubtle, null, new RoundedRect(track, 2));
        var progress = frameCount <= 1 ? 1 : _position / (frameCount - 1);
        var filled = new Rect(track.X, track.Y, Math.Max(2, track.Width * progress), track.Height);
        context.DrawRectangle(ChartPalette.Series(0), null, new RoundedRect(filled, 2));
        var knob = new Point(track.X + track.Width * progress, trackY);
        context.DrawEllipse(ChartPalette.Surface, null, knob, 6, 6);
        context.DrawEllipse(ChartPalette.Series(0), null, knob, 4, 4);
    }

    private List<RaceRow> BuildRows(out double maximum, out List<(string Entity, double Value)> ranked)
    {
        var frames = Frames ?? Array.Empty<ArcRaceFrame>();
        var entities = new List<string>();
        foreach (var frame in frames)
        {
            foreach (var key in frame.Values.Keys)
            {
                if (!entities.Contains(key, StringComparer.Ordinal))
                {
                    entities.Add(key);
                }
            }
        }

        var values = entities
            .Select(entity => (Entity: entity, Value: Interpolate(frames, entity)))
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .ThenBy(item => item.Entity, StringComparer.Ordinal)
            .ToList();
        ranked = values;
        maximum = values.Count == 0 ? 1 : Math.Max(1, values[0].Value) * 1.06;

        var rows = new List<RaceRow>(Math.Min(values.Count, Math.Max(1, TopN)));
        var rowHeight = _plot.Height / Math.Max(1, TopN);
        for (var index = 0; index < values.Count && index < Math.Max(1, TopN); index++)
        {
            var target = _plot.Top + rowHeight * (index + 0.5);
            rows.Add(new RaceRow(values[index].Entity, values[index].Value, target));
        }

        return rows;
    }

    private double Interpolate(IReadOnlyList<ArcRaceFrame> frames, string entity)
    {
        if (frames.Count == 0)
        {
            return 0;
        }

        var floor = Math.Clamp((int)Math.Floor(_position), 0, frames.Count - 1);
        var ceiling = Math.Min(frames.Count - 1, floor + 1);
        var mix = Math.Clamp(_position - floor, 0, 1);
        var start = frames[floor].ValueOf(entity);
        var end = frames[ceiling].ValueOf(entity);
        return start + (end - start) * mix;
    }

    private void UpdateRows(double deltaMs)
    {
        if (_plot.Height <= 0)
        {
            return;
        }

        var rows = BuildRows(out var maximum, out _);
        var ease = ArcThemeManager.ReducedMotion ? 1 : 1 - Math.Exp(-deltaMs / 90.0);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            seen.Add(row.Entity);
            if (!_rowY.TryGetValue(row.Entity, out var y))
            {
                _rowY[row.Entity] = row.TargetY;
                continue;
            }

            _rowY[row.Entity] = y + (row.TargetY - y) * ease;
        }

        foreach (var key in _rowY.Keys.Where(key => !seen.Contains(key)).ToList())
        {
            _rowY.Remove(key);
        }

        var maxEase = ArcThemeManager.ReducedMotion ? 1 : 1 - Math.Exp(-deltaMs / 180.0);
        _displayMax += (maximum - _displayMax) * maxEase;
    }

    private bool Settled()
    {
        var rows = BuildRows(out var maximum, out _);
        if (Math.Abs(_displayMax - maximum) > 0.5)
        {
            return false;
        }

        return rows.All(row => !_rowY.TryGetValue(row.Entity, out var y) || Math.Abs(y - row.TargetY) < 0.4);
    }

    private void ScrubTo(double x)
    {
        var frames = Frames;
        if (frames is null || frames.Count == 0 || _trackRect.Width <= 0)
        {
            return;
        }

        var progress = ChartFormat.Clamp01((x - _trackRect.Left) / _trackRect.Width);
        SetCurrentValue(IsPlayingProperty, false);
        _position = progress * Math.Max(0, frames.Count - 1);
        EnsureAnimating();
        InvalidateVisual();
    }

    private void TogglePlay()
    {
        var frames = Frames;
        if (frames is null || frames.Count == 0)
        {
            return;
        }

        if (!IsPlaying && _position >= frames.Count - 1)
        {
            _position = 0;
        }

        SetCurrentValue(IsPlayingProperty, !IsPlaying);
        InvalidateVisual();
    }

    private void ResetFrames()
    {
        _rowY.Clear();
        _position = 0;
        _displayMax = 0;
        _hoverIndex = -1;
        SetCurrentValue(IsPlayingProperty, false);
        InvalidateVisual();
    }

    private void SyncTimer()
    {
        if (ArcThemeManager.ReducedMotion)
        {
            StopTimer();
        }
        else if (IsPlaying)
        {
            StartTimer();
        }
        else
        {
            EnsureAnimating();
        }

        InvalidateVisual();
    }

    private void EnsureAnimating()
    {
        if (ArcThemeManager.ReducedMotion || VisualRoot is null)
        {
            InvalidateVisual();
            return;
        }

        StartTimer();
    }

    private void StartTimer()
    {
        if (_timer is not null)
        {
            return;
        }

        _watch = Stopwatch.StartNew();
        _lastTick = 0;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnTick();
        _timer.Start();
    }

    private void OnTick()
    {
        if (_watch is null)
        {
            return;
        }

        var now = _watch.Elapsed.TotalMilliseconds;
        var delta = Math.Clamp(now - _lastTick, 1, 120);
        _lastTick = now;
        var frames = Frames;
        if (frames is null || frames.Count == 0)
        {
            StopTimer();
            return;
        }

        if (IsPlaying)
        {
            var duration = Math.Max(120, FrameDuration);
            _position += delta / duration;
            if (_position >= frames.Count - 1)
            {
                _position = frames.Count - 1;
                SetCurrentValue(IsPlayingProperty, false);
            }
        }

        UpdateRows(delta);
        InvalidateVisual();
        if (!IsPlaying && Settled())
        {
            StopTimer();
        }
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        _watch = null;
        _lastTick = 0;
        if (!ArcThemeManager.ReducedMotion)
        {
            // Land the settled state now that the loop has stopped.
            UpdateRows(1000);
        }
        else
        {
            UpdateRows(60);
        }

        InvalidateVisual();
    }

    private string FormatValue(double value)
    {
        var formatted = ValueFormat?.Invoke(value) ?? ChartFormat.Compact(value);
        return string.IsNullOrEmpty(ValueSuffix) ? formatted : formatted + ValueSuffix;
    }

    private sealed record RaceRow(string Entity, double Value, double TargetY);
}
