using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// Data flow illustration: three sources on the left, a transform core in the middle, three destinations
/// on the right. Raw records leave as hollow dots, turn into solid accent squares inside the core, then fan
/// out along their destination's curve; alerts receives every third record. A live count ticks under the
/// core. Hovering a node lights its path and shows the one line detail.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public data-flow documentation. Approximation: Arc samples a cubic
/// arc length table per layout so records move at a mathematically even pace; here the curve is walked with
/// a smoothed parameter, which is visually even at this size. The record set is a pure function of the
/// phase (six records per 6.6 s loop), so the loop is seamless and the reduced-motion frame is a real
/// mid-story frame.
/// </remarks>
public class ArcDataFlow : ArcExplainer
{
    private sealed class Node
    {
        public string Label = string.Empty;
        public string Meta = string.Empty;
        public string Detail = string.Empty;
        public string Icon = string.Empty;
        public int Series;
        public Rect Rect;
        public Point Anchor;
    }

    private readonly List<Node> _sources = new()
    {
        new Node { Label = "App", Meta = "Web and iOS", Detail = "1,284 events a minute", Icon = "panel-top", Series = 0 },
        new Node { Label = "Database", Meta = "Postgres", Detail = "Change data capture", Icon = "database", Series = 1 },
        new Node { Label = "Events", Meta = "Stripe webhooks", Detail = "About 320 a minute", Icon = "zap", Series = 2 }
    };

    private readonly List<Node> _destinations = new()
    {
        new Node { Label = "Warehouse", Meta = "Every record", Detail = "Snowflake, three tables", Icon = "box", Series = 0 },
        new Node { Label = "Dashboard", Meta = "Live metrics", Detail = "Rolled up every 30 s", Icon = "layout-dashboard", Series = 2 },
        new Node { Label = "Alerts", Meta = "Slack", Detail = "About every third record", Icon = "bell", Series = 3 }
    };

    private const double RecordLife = 4.0;
    private const int RecordsPerCycle = 6;

    private Rect _core = default;
    private int _hover = -1;

    public ArcDataFlow()
    {
        Describe(
            "Data flow illustration: three sources feed a transform core that fans records out to a warehouse, a dashboard and alerts",
            "Hollow dots are raw records, solid squares are processed records. Press Space to pause or resume the flow.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 6.6;

    protected override double PosterPhase => 0.30;

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p)
        {
            for (var i = 0; i < _sources.Count; i++)
            {
                if (_sources[i].Rect.Contains(p))
                {
                    next = i;
                    break;
                }
            }

            if (next < 0 && _core.Contains(p))
            {
                next = 3;
            }

            if (next < 0)
            {
                for (var i = 0; i < _destinations.Count; i++)
                {
                    if (_destinations[i].Rect.Contains(p))
                    {
                        next = 4 + i;
                        break;
                    }
                }
            }
        }

        if (next == _hover)
        {
            return false;
        }

        _hover = next;
        return true;
    }

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        var pad = 24.0;
        var nodeWidth = 186.0;
        var nodeHeight = 56.0;
        var rows = new[] { 64.0, 152.0, 240.0 };
        var destinationX = width - pad - nodeWidth;

        for (var i = 0; i < 3; i++)
        {
            _sources[i].Rect = new Rect(pad, rows[i], nodeWidth, nodeHeight);
            _sources[i].Anchor = new Point(pad + nodeWidth, rows[i] + nodeHeight / 2);
            _destinations[i].Rect = new Rect(destinationX, rows[i], nodeWidth, nodeHeight);
            _destinations[i].Anchor = new Point(destinationX, rows[i] + nodeHeight / 2);
        }

        _core = new Rect(width * 0.5 - 98, 132, 196, 86);
        var coreLeft = new Point(_core.X, _core.Center.Y);
        var coreRight = new Point(_core.Right, _core.Center.Y);

        Ink.Label(context, "Sources", pad, 42, 11, Ink.B(ArcTokens.TextMuted), FontWeight.Medium);
        Ink.Label(context, "Destinations", width - pad, 42, 11, Ink.B(ArcTokens.TextMuted), FontWeight.Medium, Align.Right);

        var accent = Ink.B(ArcTokens.Accent);
        var idle = Ink.A(Ink.B(ArcTokens.BorderStrong), 0.55);

        // Wires first: every source into the core, then the core out to every destination.
        for (var i = 0; i < 3; i++)
        {
            var sourceHot = _hover == i || _hover == 3;
            var sourceBrush = _hover < 0 || sourceHot ? Ink.A(Series(_sources[i].Series), sourceHot ? 1 : 0.42) : Ink.A(idle, 0.5);
            context.DrawGeometry(null, Ink.Stroke(sourceBrush, sourceHot ? 2 : 1.2), Ink.Flow(_sources[i].Anchor, coreLeft));

            var destHot = _hover == 4 + i || _hover == 3;
            var destinationBrush = _hover < 0 || destHot ? Ink.A(Series(_destinations[i].Series), destHot ? 1 : 0.42) : Ink.A(idle, 0.5);
            context.DrawGeometry(null, Ink.Stroke(destinationBrush, destHot ? 2 : 1.2), Ink.Flow(coreRight, _destinations[i].Anchor));
        }

        // The core: a dashed ring that turns with the story, the label, and the live count underneath.
        var ringRect = _core.Inflate(9);
        context.DrawRectangle(null, Ink.Dashed(Ink.A(accent, 0.42), 1.2, 5, 5, -Story * 14), ringRect, 22, 22);
        Ink.Card(context, _core, 18, Ink.B(ArcTokens.SurfaceRaised), _hover == 3 ? accent : Ink.B(ArcTokens.Border));
        var coreIcon = new Rect(_core.X + 14, _core.Center.Y - 16, 32, 32);
        context.DrawRectangle(Ink.A(accent, 0.14), null, coreIcon, 9, 9);
        Ink.Icon(context, "layers", coreIcon.Center.X, coreIcon.Center.Y, 17, accent);
        Ink.Label(context, "Transform", _core.X + 56, _core.Center.Y - 10, 14, Ink.B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: _core.Right - _core.X - 66);
        Ink.Label(context, "Dedupe, enrich", _core.X + 56, _core.Center.Y + 9, 11, Ink.B(ArcTokens.TextMuted), maxWidth: _core.Right - _core.X - 66);

        var processed = 12480 + (int)(Story / 1.1);
        Ink.Label(context, Ink.Number(processed), _core.Center.X, _core.Bottom + 30, 24, Ink.B(ArcTokens.Foreground), FontWeight.Medium, Align.Center);
        Ink.Label(context, "processed", _core.Center.X, _core.Bottom + 53, 11, Ink.B(ArcTokens.TextMuted), Align.Center);

        DrawRecords(context, width, coreLeft, coreRight, accent, idle);

        for (var i = 0; i < 3; i++)
        {
            DrawNode(context, _sources[i], _hover == i);
            DrawNode(context, _destinations[i], _hover == 4 + i);
        }

        if (_hover >= 0)
        {
            DrawDetail(context, width);
        }

        DrawLegend(context, width, height);
    }

    private void DrawNode(DrawingContext context, Node node, bool active)
    {
        Ink.Node(
            context,
            node.Rect,
            node.Icon,
            node.Label,
            node.Meta,
            Series(node.Series),
            active);
    }

    private void DrawDetail(DrawingContext context, double width)
    {
        string text;
        double x;
        double y;
        if (_hover < 3)
        {
            text = _sources[_hover].Detail;
            var rect = _sources[_hover].Rect;
            x = rect.X;
            y = rect.Y - 26;
        }
        else if (_hover == 3)
        {
            text = "12 records a second, six destinations";
            x = _core.Center.X - Ink.ChipWidth(text, 11) / 2;
            y = _core.Y - 30;
        }
        else
        {
            text = _destinations[_hover - 4].Detail;
            var rect = _destinations[_hover - 4].Rect;
            x = rect.Right - Ink.ChipWidth(text, 11);
            y = rect.Y - 26;
        }

        x = Math.Clamp(x, 20, width - 20 - Ink.ChipWidth(text, 11));
        Ink.Chip(context, x, y, text, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.TextSecondary), 21, 11, Ink.B(ArcTokens.Border));
    }

    private void DrawRecords(DrawingContext context, double width, Point coreLeft, Point coreRight, IBrush accent, IBrush idle)
    {
        var story = Story;
        var sourceAnchor = new Point(0, 0);
        for (var i = 0; i < RecordsPerCycle; i++)
        {
            var age = story - i * (CycleSeconds / RecordsPerCycle);
            if (age < 0)
            {
                age += CycleSeconds;
            }

            var sourceIndex = i % 3;
            var destinationIndex = i % 3;
            sourceAnchor = _sources[sourceIndex].Anchor;

            if (age <= 1.7)
            {
                var t = Ink.Smooth(age / 1.7);
                var point = Ink.FlowAt(sourceAnchor, coreLeft, t);
                context.DrawEllipse(Ink.B(ArcTokens.SurfaceRaised), new Pen(Ink.A(Series(_sources[sourceIndex].Series), 0.9), 1.6), point, 4.2, 4.2);
            }
            else if (age <= 2.15)
            {
                var t = Ink.Span(age, 1.7, 2.15);
                var point = new Point(
                    Ink.Lerp(coreLeft.X, _core.Center.X, t),
                    Ink.Lerp(coreLeft.Y, _core.Center.Y + 22, t));
                var side = Ink.Lerp(4.2, 5.4, Ink.EaseOut(t));
                context.DrawRectangle(Ink.A(accent, 0.25 + 0.75 * t), new Pen(accent, 1.2), new Rect(point.X - side / 2, point.Y - side / 2, side, side), 1.5, 1.5);
            }
            else if (age <= RecordLife)
            {
                var t = Ink.Smooth((age - 2.15) / (RecordLife - 2.15));
                var point = Ink.FlowAt(coreRight, _destinations[destinationIndex].Anchor, t);
                const double side = 7;
                context.DrawRectangle(accent, null, new Rect(point.X - side / 2, point.Y - side / 2, side, side), 2, 2);
                var back = Ink.FlowAt(coreRight, _destinations[destinationIndex].Anchor, Math.Max(0, t - 0.055));
                context.DrawLine(Ink.Stroke(Ink.A(accent, 0.30), 1.4), back, point);
            }
        }

        // The core flashes its ring whenever a record is being processed.
        foreach (var record in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var age = story - record * (CycleSeconds / RecordsPerCycle);
            if (age < 0)
            {
                age += CycleSeconds;
            }

            if (age > 1.55 && age < 2.45)
            {
                var t = Ink.Span(age, 1.55, 2.45);
                context.DrawRectangle(null, new Pen(Ink.A(accent, 0.55 * (1 - t)), 1.4), _core.Inflate(3 + t * 13), 20, 20);
            }
        }

        _ = idle;
    }

    private void DrawLegend(DrawingContext context, double width, double height)
    {
        var y = height - 26;
        var muted = Ink.B(ArcTokens.TextMuted);
        context.DrawEllipse(Ink.B(ArcTokens.SurfaceRaised), new Pen(Ink.A(Ink.B(ArcTokens.Accent), 0.9), 1.6), new Point(28, y), 4, 4);
        Ink.Label(context, "Raw record", 38, y, 11, muted);
        context.DrawRectangle(Ink.B(ArcTokens.Accent), null, new Rect(116, y - 3.5, 7, 7), 2, 2);
        Ink.Label(context, "Processed record", 129, y, 11, muted);
        Ink.Label(context, "Alerts takes about every third record", width - 24, y, 11, muted, Align.Right);
    }
}
