using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>An agent lane on the timeline.</summary>
public sealed class ArcTimelineAgent
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Model { get; set; }

    /// <summary>Blended USD per million tokens, used for the cost in the header.</summary>
    public double PricePerMillion { get; set; }
}

/// <summary>What an agent was doing for a stretch of the run.</summary>
public sealed class ArcTimelineSpan
{
    public string Id { get; set; } = string.Empty;

    public string AgentId { get; set; } = string.Empty;

    /// <summary>thinking, tool or waiting.</summary>
    public string Kind { get; set; } = "thinking";

    public double Start { get; set; }

    public double End { get; set; }

    public string Label { get; set; } = string.Empty;

    public string? Tool { get; set; }

    public double Tokens { get; set; }

    public bool Failed { get; set; }

    public bool Retry { get; set; }
}

/// <summary>Work handed from one agent to another.</summary>
public sealed class ArcTimelineHandoff
{
    public string From { get; set; } = string.Empty;

    public string To { get; set; } = string.Empty;

    public double At { get; set; }

    public string? Label { get; set; }
}

/// <summary>
/// A compact timeline of one orchestrated run: every agent is a lane on a shared time axis, thinking, tool calls
/// and waiting are spans, handoffs draw in as elbow connectors, and a playhead replays the run while the header
/// counts time, tokens and cost. Hover or focus a span to read what it did; press it to pin it.
///
/// Platform approximations: spans are drawn and hit tested by hand rather than being buttons in a roving tab
/// index, so keyboard navigation moves between spans with the arrow keys and Shift plus the arrow keys scrubs
/// time; there is no hover card, its detail appears on one line under the plot.
/// </summary>
public class ArcAgentTimeline : Panel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcAgentTimeline, string>(nameof(Title), "Agent run");

    public static readonly StyledProperty<IEnumerable<ArcTimelineAgent>?> AgentsProperty =
        AvaloniaProperty.Register<ArcAgentTimeline, IEnumerable<ArcTimelineAgent>?>(nameof(Agents));

    public static readonly StyledProperty<IEnumerable<ArcTimelineSpan>?> SpansProperty =
        AvaloniaProperty.Register<ArcAgentTimeline, IEnumerable<ArcTimelineSpan>?>(nameof(Spans));

    public static readonly StyledProperty<IEnumerable<ArcTimelineHandoff>?> HandoffsProperty =
        AvaloniaProperty.Register<ArcAgentTimeline, IEnumerable<ArcTimelineHandoff>?>(nameof(Handoffs));

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<ArcAgentTimeline, double>(nameof(Duration), 26.4);

    public static readonly StyledProperty<bool> IsPlayingProperty =
        AvaloniaProperty.Register<ArcAgentTimeline, bool>(nameof(IsPlaying));

    private readonly SvClock _clock = new();
    private readonly ArcTimelinePlot _plot;
    private TextBlock? _statusText;
    private StackPanel? _detailRow;
    private readonly List<ArcTimelineSpan> _spanList = new();
    private readonly List<ArcTimelineAgent> _agentList = new();
    private readonly List<ArcTimelineHandoff> _handoffList = new();
    private double _time;
    private double _speed = 1;
    private string? _hoverId;
    private string? _pinnedId;
    private string? _selectedId;

    static ArcAgentTimeline()
    {
        AgentsProperty.Changed.AddClassHandler<ArcAgentTimeline>((timeline, _) => timeline.Rebuild());
        SpansProperty.Changed.AddClassHandler<ArcAgentTimeline>((timeline, _) => timeline.Rebuild());
        HandoffsProperty.Changed.AddClassHandler<ArcAgentTimeline>((timeline, _) => timeline.Rebuild());
        DurationProperty.Changed.AddClassHandler<ArcAgentTimeline>((timeline, _) => timeline.Rebuild());
        TitleProperty.Changed.AddClassHandler<ArcAgentTimeline>((timeline, _) => timeline.Rebuild());
        IsPlayingProperty.Changed.AddClassHandler<ArcAgentTimeline>((timeline, _) => timeline.SyncClock());
    }

    public ArcAgentTimeline()
    {
        ClipToBounds = false;
        _plot = new ArcTimelinePlot { Owner = this };
        Rebuild();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IEnumerable<ArcTimelineAgent>? Agents
    {
        get => GetValue(AgentsProperty);
        set => SetValue(AgentsProperty, value);
    }

    public IEnumerable<ArcTimelineSpan>? Spans
    {
        get => GetValue(SpansProperty);
        set => SetValue(SpansProperty, value);
    }

    public IEnumerable<ArcTimelineHandoff>? Handoffs
    {
        get => GetValue(HandoffsProperty);
        set => SetValue(HandoffsProperty, value);
    }

    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public bool IsPlaying
    {
        get => GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    internal IReadOnlyList<ArcTimelineAgent> AgentList => _agentList;

    internal IReadOnlyList<ArcTimelineSpan> SpanList => _spanList;

    internal IReadOnlyList<ArcTimelineHandoff> HandoffList => _handoffList;

    internal double Time => _time;

    internal string? HoverId => _hoverId;

    internal string? PinnedId => _pinnedId;

    internal string? SelectedId => _selectedId;

    private void Rebuild()
    {
        _agentList.Clear();
        _agentList.AddRange(Agents ?? Array.Empty<ArcTimelineAgent>());
        _spanList.Clear();
        _spanList.AddRange((Spans ?? Array.Empty<ArcTimelineSpan>()).OrderBy(span => span.Start));
        _handoffList.Clear();
        _handoffList.AddRange(Handoffs ?? Array.Empty<ArcTimelineHandoff>());

        Children.Clear();
        var root = new StackPanel { Spacing = 8 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(new TextBlock { Text = Title, Classes = { "arc-label" } });
        _statusText = Sv.Numeric(string.Empty, "arc-caption");
        titles.Children.Add(_statusText);
        header.Children.Add(titles);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var play = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Compact = true };
        play.Content = Sv.Icon("play", 15);
        play.Click += (_, _) => TogglePlay();
        AutomationProperties.SetName(play, "Play the replay");
        var restart = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Compact = true, Content = Sv.Icon("rotate-cw", 15) };
        restart.Click += (_, _) => Restart();
        AutomationProperties.SetName(restart, "Restart the replay");
        var speed = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "1×" };
        speed.Click += (_, _) =>
        {
            _speed = Math.Abs(_speed - 1) < 0.01 ? 2 : 1;
            speed.Content = _speed < 1.5 ? "1×" : "2×";
            SyncClock();
        };
        AutomationProperties.SetName(speed, "Playback speed");
        controls.Children.Add(play);
        controls.Children.Add(restart);
        controls.Children.Add(speed);
        Grid.SetColumn(controls, 1);
        header.Children.Add(controls);
        root.Children.Add(header);

        _plot.Owner = this;
        Sv.Detach(_plot);
        root.Children.Add(_plot);

        _detailRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, MinHeight = 19 };
        root.Children.Add(_detailRow);
        Children.Add(root);

        _time = ArcThemeManager.ReducedMotion ? Duration : 0;
        UpdateDetail();
        UpdateStatus();
        SyncClock();
        _plot.InvalidateVisual();
    }

    private void SyncClock()
    {
        _clock.Stop();
        UpdateStatus();
        _plot.InvalidateVisual();
        if (!IsPlaying || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        if (_time >= Duration - 0.001)
        {
            _time = 0;
        }

        _clock.Start(seconds => Advance(seconds));
    }

    private double _lastSeconds;

    private void Advance(double seconds)
    {
        var delta = seconds - _lastSeconds;
        _lastSeconds = seconds;
        _time = Math.Min(Duration, _time + delta * _speed);
        if (_time >= Duration - 0.001)
        {
            _clock.Stop();
            SetCurrentValue(IsPlayingProperty, false);
        }

        UpdateStatus();
        _plot.InvalidateVisual();
    }

    internal void TogglePlay()
    {
        if (IsPlaying)
        {
            SetCurrentValue(IsPlayingProperty, false);
            _clock.Stop();
            return;
        }

        SetCurrentValue(IsPlayingProperty, true);
        _lastSeconds = 0;
        SyncClock();
    }

    private void Restart()
    {
        _time = 0;
        _lastSeconds = 0;
        SetCurrentValue(IsPlayingProperty, true);
        SyncClock();
    }

    internal void ScrubTo(double time)
    {
        _time = Math.Clamp(time, 0, Duration);
        if (IsPlaying)
        {
            SetCurrentValue(IsPlayingProperty, false);
        }

        UpdateStatus();
        _plot.InvalidateVisual();
    }

    internal void SetHover(ArcTimelineSpan? span)
    {
        var id = span?.Id;
        if (_hoverId == id)
        {
            return;
        }

        _hoverId = id;
        UpdateDetail();
        _plot.InvalidateVisual();
    }

    internal void TogglePin(ArcTimelineSpan? span)
    {
        _pinnedId = span is null || _pinnedId == span.Id ? null : span.Id;
        _selectedId = span?.Id ?? _selectedId;
        UpdateDetail();
        _plot.InvalidateVisual();
        AutomationProperties.SetHelpText(_plot, span is null ? "Unpinned" : $"Pinned {span.Label}");
    }

    internal void SelectSpan(ArcTimelineSpan? span)
    {
        _selectedId = span?.Id;
        UpdateDetail();
        _plot.InvalidateVisual();
    }

    private void UpdateStatus()
    {
        if (_statusText is null || _plot is null)
        {
            return;
        }

        var finished = _time >= Duration - 0.001;
        if (!finished)
        {
            var (tokens, cost) = Totals(_time);
            _statusText.Text = $"{_time:0.0}s of {Duration:0.0}s · {Sv.Tokens(tokens)} tokens · {Sv.Money(cost)}";
        }
        else
        {
            var (tokens, cost) = Totals(Duration);
            var work = _spanList.Where(span => span.Kind != "waiting").Sum(span => span.End - span.Start);
            var speedup = Duration <= 0 ? 1 : work / Duration;
            var comparison = speedup >= 1.05 ? $"{speedup:0.0}x faster than sequential" : "parallel across five lanes";
            _statusText.Text = $"{_agentList.Count} agents · {Duration:0.0}s · {comparison} · {Sv.Money(cost)} · {Sv.Tokens(tokens)} tokens";
        }
    }

    internal (double Tokens, double Cost) Totals(double time)
    {
        double tokens = 0, cost = 0;
        foreach (var span in _spanList)
        {
            if (span.Start > time)
            {
                continue;
            }

            var share = Math.Clamp((time - span.Start) / Math.Max(0.001, span.End - span.Start), 0, 1);
            tokens += span.Tokens * share;
            var agent = _agentList.FirstOrDefault(item => item.Id == span.AgentId);
            cost += span.Tokens * share * (agent?.PricePerMillion ?? 1) / 1_000_000.0;
        }

        return (tokens, cost);
    }

    private void UpdateDetail()
    {
        if (_detailRow is null)
        {
            return;
        }

        _detailRow.Children.Clear();
        var span = _spanList.FirstOrDefault(item => item.Id == (_pinnedId ?? _hoverId ?? _selectedId));
        if (span is null)
        {
            _detailRow.Children.Add(Sv.Caption("Hover a span for what it did, or press one to trace its handoff"));
            var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(6, 0, 0, 0) };
            legend.Children.Add(LegendItem(ArcTokens.Accent, "tool", 0.85));
            legend.Children.Add(LegendItem(ArcTokens.Accent, "thinking", 0.28));
            legend.Children.Add(LegendItem(ArcTokens.TextMuted, "waiting", 0.35));
            legend.Children.Add(LegendItem(ArcTokens.Danger, "failed", 0.8));
            _detailRow.Children.Add(legend);
            return;
        }

        var agent = _agentList.FirstOrDefault(item => item.Id == span.AgentId);
        var kind = span.Kind switch { "tool" => span.Tool ?? "Tool call", "waiting" => "Waiting", _ => "Thinking" };
        var duration = span.End - span.Start;
        var parts = new List<string>
        {
            agent?.Name ?? "Agent",
            kind,
            $"{span.Start:0.0}–{span.End:0.0}s ({duration:0.0}s)"
        };
        if (span.Tokens > 0)
        {
            parts.Add($"{Sv.Tokens(span.Tokens)} tokens");
        }

        if (span.Failed)
        {
            parts.Add("Failed");
        }

        if (span.Retry)
        {
            parts.Add("Retry");
        }

        var detail = new TextBlock
        {
            Text = string.Join(" · ", parts),
            FontSize = Sv.Size(ArcTokens.TextXs),
            FontFeatures = Sv.Tabular,
            VerticalAlignment = VerticalAlignment.Center
        };
        detail.Paint(TextBlock.ForegroundProperty, span.Failed ? ArcTokens.Danger : ArcTokens.TextSecondary);
        _detailRow.Children.Add(detail);

        var label = new TextBlock
        {
            Text = span.Label,
            FontSize = Sv.Size(ArcTokens.TextSm),
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 420,
            VerticalAlignment = VerticalAlignment.Center
        };
        _detailRow.Children.Add(label);

        if (_pinnedId is not null)
        {
            var pinned = Sv.Pill(Sv.Text("Pinned", "arc-caption"), ArcTokens.AccentSubtle, ArcTokens.Accent);
            _detailRow.Children.Add(pinned);
        }
    }

    private static Control LegendItem(string token, string label, double alpha)
    {
        var swatch = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(3),
            VerticalAlignment = VerticalAlignment.Center
        };
        swatch.Background = Sv.AlphaBrush(token, alpha);
        return Sv.Row(5, swatch, Sv.Caption(label));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }
}

/// <summary>The drawn plot: agent lanes, spans, handoff elbows, axis and playhead.</summary>
internal sealed class ArcTimelinePlot : Control
{
    private const double LabelWidth = 148;
    private const double RowHeight = 34;
    private const double TopPad = 6;
    private const double AxisHeight = 26;
    private const double SpanHeight = 18;

    private bool _pointerFocus;
    private bool _scrubbing;

    public ArcAgentTimeline? Owner { get; set; }

    public ArcTimelinePlot()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 640 : availableSize.Width;
        return new Size(width, TopPad + RowHeight * Math.Max(1, Owner?.AgentList.Count ?? 5) + AxisHeight + 10);
    }

    private double PlotLeft => LabelWidth;
    private double PlotWidth => Math.Max(40, Bounds.Width - LabelWidth - 14);

    private double X(double time)
    {
        var owner = Owner;
        var duration = Math.Max(0.001, owner?.Duration ?? 1);
        return PlotLeft + Math.Clamp(time / duration, 0, 1) * PlotWidth;
    }

    private double TimeAt(double x)
    {
        var owner = Owner;
        var duration = Math.Max(0.001, owner?.Duration ?? 1);
        return Math.Clamp((x - PlotLeft) / PlotWidth, 0, 1) * duration;
    }

    private double RowY(int index) => TopPad + index * RowHeight;

    public override void Render(DrawingContext context)
    {
        var owner = Owner;
        if (owner is null || Bounds.Width <= 0)
        {
            return;
        }

        var agents = owner.AgentList;
        var spans = owner.SpanList;
        var handoffs = owner.HandoffList;
        var time = owner.Time;
        var border = Sv.Brush(ArcTokens.Border);
        var borderSubtle = Sv.Brush(ArcTokens.BorderSubtle);
        var muted = Sv.Brush(ArcTokens.TextMuted);
        var secondary = Sv.Brush(ArcTokens.TextSecondary);
        var foreground = Sv.Brush(ArcTokens.Foreground);
        var accent = Sv.Brush(ArcTokens.Accent);
        var danger = Sv.Brush(ArcTokens.Danger);

        // Lane hairlines and labels.
        for (var i = 0; i < agents.Count; i++)
        {
            var y = RowY(i);
            var centerY = y + RowHeight / 2;
            context.DrawLine(new Pen(borderSubtle, 1), new Point(PlotLeft, centerY), new Point(Bounds.Width - 10, centerY));

            var dot = i == 0 ? accent : Sv.Brush(SeriesToken(i));
            context.DrawEllipse(dot, null, new Point(4, centerY - 5), 3, 3);
            var name = SvDraw.Text(agents[i].Name, 12, i == 0 ? foreground : secondary, FontWeight.Medium);
            SvDraw.DrawText(context, name, 13, centerY - 14);
            if (!string.IsNullOrWhiteSpace(agents[i].Model))
            {
                var model = SvDraw.Text(agents[i].Model!, 10, muted);
                SvDraw.DrawText(context, model, 13, centerY + 1);
            }
        }

        // Axis ticks and baseline.
        var axisY = TopPad + RowHeight * Math.Max(1, agents.Count);
        context.DrawLine(new Pen(border, 1), new Point(PlotLeft, axisY), new Point(Bounds.Width - 10, axisY));
        var tick = Math.Max(1, Math.Round(owner.Duration / 6));
        for (var t = 0.0; t <= owner.Duration + 0.001; t += tick)
        {
            var x = X(t);
            context.DrawLine(new Pen(borderSubtle, 1), new Point(x, axisY), new Point(x, axisY + 4));
            var label = SvDraw.Text($"{t:0}s", 10, muted);
            SvDraw.DrawText(context, label, x - label.Width / 2, axisY + 6);
        }

        // Spans, clipped at the playhead so they grow with the replay.
        for (var i = 0; i < agents.Count; i++)
        {
            var rowY = RowY(i);
            foreach (var span in spans.Where(item => item.AgentId == agents[i].Id))
            {
                if (span.Start > time)
                {
                    continue;
                }

                var end = Math.Min(span.End, time);
                var width = Math.Max(3, X(end) - X(span.Start));
                var rect = new Rect(X(span.Start), rowY + (RowHeight - SpanHeight) / 2, width, SpanHeight);
                var dim = owner.PinnedId is not null && owner.PinnedId != span.Id;
                var radius = Math.Min(5, rect.Height / 2);
                var active = span.Start <= time && span.End >= time - 0.001;
                var spanToken = i == 0 ? ArcTokens.Accent : SeriesToken(i);

                using (context.PushOpacity(dim ? 0.35 : 1))
                {
                    switch (span.Kind)
                    {
                        case "tool":
                            SvDraw.Fill(context, rect, radius, span.Failed ? Sv.AlphaBrush(ArcTokens.Danger, 0.9) : Sv.AlphaBrush(spanToken, 0.9));
                            break;
                        case "waiting":
                            DrawHatched(context, rect, radius, muted);
                            break;
                        default:
                            SvDraw.Fill(context, rect, radius, Sv.AlphaBrush(spanToken, 0.38));
                            break;
                    }

                    if (span.Retry)
                    {
                        SvDraw.Arc(context, new Point(rect.X + 7, rect.Center.Y), 4.4, -60, 300, new Pen(foreground, 1.2, null, PenLineCap.Round));
                    }

                    if (span.Failed)
                    {
                        SvDraw.Cross(context, new Point(rect.Right - 7, rect.Center.Y), 7, Sv.AlphaBrush(ArcTokens.Surface, 0.9), 1.4);
                    }

                    if (active && owner.IsPlaying)
                    {
                        // A short shine and a square leading edge while the span is open.
                        SvDraw.Fill(context, new Rect(rect.Right - 1.5, rect.Y, 1.5, rect.Height), 0, Sv.Brush(ArcTokens.Surface));
                    }

                    if (owner.HoverId == span.Id || owner.SelectedId == span.Id || owner.PinnedId == span.Id)
                    {
                        SvDraw.Outline(context, rect, radius, owner.PinnedId == span.Id ? accent : Sv.Brush(ArcTokens.FocusOutline), 1.6);
                    }
                }
            }
        }

        // Handoffs: an elbow that drops from the source lane and rises into the target lane.
        foreach (var handoff in handoffs)
        {
            if (handoff.At > time)
            {
                continue;
            }

            var fromIndex = IndexOf(agents, handoff.From);
            var toIndex = IndexOf(agents, handoff.To);
            if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
            {
                continue;
            }

            var x = X(handoff.At);
            var fromY = RowY(fromIndex) + (toIndex > fromIndex ? RowHeight - 3 : 3);
            var toY = RowY(toIndex) + (toIndex > fromIndex ? 3 : RowHeight - 3);
            var pen = new Pen(Sv.AlphaBrush(ArcTokens.TextMuted, 0.55), 1.1);
            var midY = (fromY + toY) / 2;
            context.DrawLine(pen, new Point(x, fromY), new Point(x, midY));
            context.DrawLine(pen, new Point(x, midY), new Point(x + 6, midY));
            context.DrawLine(pen, new Point(x + 6, midY), new Point(x + 6, toY));
            context.DrawLine(pen, new Point(x + 6, toY), new Point(x + 9, toY));
            context.DrawEllipse(Sv.AlphaBrush(ArcTokens.TextSecondary, 0.8), null, new Point(x + 9, toY), 1.8, 1.8);
        }

        // Playhead, with a knob on the axis; it fades out when the run is finished.
        var finished = time >= owner.Duration - 0.001;
        if (!finished && time > 0)
        {
            var x = X(time);
            using (context.PushOpacity(owner.IsPlaying || _scrubbing ? 0.95 : 0.5))
            {
                context.DrawLine(new Pen(accent, 1.4), new Point(x, TopPad - 2), new Point(x, axisY + 4));
                context.DrawEllipse(accent, null, new Point(x, axisY + 2), 3.4, 3.4);
            }
        }

        if (IsFocused && !_pointerFocus)
        {
            context.DrawRectangle(null, new Pen(Sv.Brush(ArcTokens.FocusOutline), 2), new Rect(1, 1, Bounds.Width - 2, Bounds.Height - 2), 12, 12);
        }
    }

    private static void DrawHatched(DrawingContext context, Rect rect, double radius, IBrush brush)
    {
        SvDraw.Fill(context, rect, radius, Sv.AlphaBrush(ArcTokens.TextMuted, 0.12));
        using (context.PushClip(new RoundedRect(rect, radius)))
        {
            var pen = new Pen(Sv.AlphaBrush(ArcTokens.TextMuted, 0.35), 1);
            for (var x = rect.X - rect.Height; x < rect.Right; x += 5)
            {
                context.DrawLine(pen, new Point(x, rect.Bottom), new Point(x + rect.Height, rect.Y));
            }
        }
    }

    private static string SeriesToken(int index) => (index % 4) switch
    {
        1 => ArcTokens.Series2,
        2 => ArcTokens.Series3,
        3 => ArcTokens.Series4,
        _ => ArcTokens.Series1
    };

    private static int IndexOf(IReadOnlyList<ArcTimelineAgent> agents, string id)
    {
        for (var i = 0; i < agents.Count; i++)
        {
            if (agents[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    private ArcTimelineSpan? HitSpan(Point point)
    {
        var owner = Owner;
        if (owner is null)
        {
            return null;
        }

        var agents = owner.AgentList;
        for (var i = 0; i < agents.Count; i++)
        {
            var rowY = RowY(i);
            if (point.Y < rowY || point.Y > rowY + RowHeight)
            {
                continue;
            }

            foreach (var span in owner.SpanList.Where(item => item.AgentId == agents[i].Id))
            {
                var end = Math.Min(span.End, owner.Time);
                if (end < span.Start)
                {
                    continue;
                }

                var x = X(span.Start);
                var width = Math.Max(4, X(end) - x);
                if (point.X >= x - 2 && point.X <= x + width + 2)
                {
                    return span;
                }
            }
        }

        return null;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerFocus = true;
        var point = e.GetPosition(this);
        if (_scrubbing)
        {
            Owner?.ScrubTo(TimeAt(point.X));
            return;
        }

        Owner?.SetHover(HitSpan(point));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Owner?.SetHover(null);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pointerFocus = true;
        Focus();
        var point = e.GetPosition(this);
        var span = HitSpan(point);
        if (span is not null)
        {
            Owner?.TogglePin(span);
        }
        else
        {
            _scrubbing = true;
            e.Pointer.Capture(this);
            Owner?.ScrubTo(TimeAt(point.X));
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _scrubbing = false;
        e.Pointer.Capture(null);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var owner = Owner;
        if (owner is null || owner.SpanList.Count == 0)
        {
            return;
        }

        _pointerFocus = false;
        var ordered = owner.SpanList.ToList();
        var index = owner.SelectedId is null ? -1 : ordered.FindIndex(span => span.Id == owner.SelectedId);

        switch (e.Key)
        {
            case Key.Right when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                index = Math.Min(ordered.Count - 1, index + 1);
                owner.SelectSpan(ordered[Math.Max(0, index)]);
                e.Handled = true;
                break;
            case Key.Left when !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                index = Math.Max(0, index - 1);
                owner.SelectSpan(ordered[index]);
                e.Handled = true;
                break;
            case Key.Right:
                owner.ScrubTo(owner.Time + 0.5);
                e.Handled = true;
                break;
            case Key.Left:
                owner.ScrubTo(owner.Time - 0.5);
                e.Handled = true;
                break;
            case Key.Home:
                owner.SelectSpan(ordered[0]);
                e.Handled = true;
                break;
            case Key.End:
                owner.SelectSpan(ordered[^1]);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                owner.TogglePin(index >= 0 ? ordered[index] : ordered[0]);
                e.Handled = true;
                break;
            case Key.Escape:
                owner.TogglePin(null);
                e.Handled = true;
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }
}
