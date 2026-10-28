using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIExplainers.Controls;

/// <summary>
/// Payment flow illustration: the amount leaves checkout as a chip, slips under the processor, the card
/// network and the issuing bank while each hop writes its status line, then the approval travels back and
/// repaints the lane in success. Settlement runs on a second rail: the processor keeps its fee, the net
/// amount reaches the merchant balance, which counts up, and the step row fills as it lands.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public payment-flow documentation. Approximation: the currency is
/// formatted with a fixed en-US style rather than the full locale negotiation of the React version, and the
/// five node marks use the Arc icon set instead of brand logos. Timing follows the documented story: about
/// fifteen seconds per loop at speed 1, with authorization and settlement on separate rails.
/// </remarks>
public class ArcPaymentFlow : ArcExplainer
{
    private const double Cycle = 15.0;
    private const double Amount = 42.5;
    private const double Fee = 1.53;
    private const double StartBalance = 1284.1;

    private readonly string[] _labels = { "Checkout", "Stripe", "Mastercard", "Chase" };
    private readonly string[] _metas = { "$42.50 tap", "Processor", "Card network", "Issuing bank" };
    private readonly string[] _icons = { "credit-card", "server", "globe", "briefcase" };

    private readonly Rect[] _nodes = new Rect[4];
    private int _hover = -1;

    public ArcPaymentFlow()
    {
        Describe(
            "Payment flow illustration: a card tap for 42 dollars and 50 cents is authorized through the processor, the card network and the issuing bank, then settles to the merchant balance net of a 1 dollar 53 cent fee",
            "Each node carries a status line, so the state never relies on colour alone. Press Space to pause or resume the story.");
    }

    protected override double DefaultArtHeight => 390;

    protected override double CycleSeconds => Cycle;

    protected override double PosterPhase => 0.78;

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p)
        {
            for (var i = 0; i < 4; i++)
            {
                if (_nodes[i].Contains(p))
                {
                    next = i;
                    break;
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
        var t = phase * Cycle;
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);

        const double nodeWidth = 176;
        const double nodeHeight = 58;
        const double rowTop = 40;
        var chainY = rowTop + nodeHeight / 2;
        var xs = new[] { 24.0, 272.0, 520.0, 768.0 };
        for (var i = 0; i < 4; i++)
        {
            _nodes[i] = new Rect(xs[i], rowTop, nodeWidth, nodeHeight);
        }

        var centers = new[] { 112.0, 360.0, 608.0, 856.0 };
        var laneY = 144.0;
        var railY = 216.0;
        var balanceRect = new Rect(width - 256, 170, 232, 84);

        // Baseline wires.
        var baseline = Ink.A(Ink.B(ArcTokens.BorderStrong), 0.45);
        context.DrawLine(Ink.Stroke(baseline, 1.4), new Point(centers[0], chainY), new Point(centers[3], chainY));
        for (var i = 0; i < 3; i++)
        {
            Ink.ArrowHead(context, new Point(centers[i + 1] - nodeWidth / 2 - 8, chainY), new Point(centers[i] + 8, chainY), baseline, 5);
        }

        // Authorization return lane and settlement rail.
        var lane = new List<Point>
        {
            new(centers[3], rowTop + nodeHeight),
            new(centers[3], laneY),
            new(centers[0], laneY),
            new(centers[0], rowTop + nodeHeight)
        };
        context.DrawGeometry(null, Ink.Dashed(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.5), 1.2, 5, 4), Polyline(lane));

        var rail = new List<Point>
        {
            new(centers[1], rowTop + nodeHeight),
            new(centers[1], railY),
            new(balanceRect.X, railY)
        };
        context.DrawGeometry(null, Ink.Dashed(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.5), 1.2, 5, 4), Polyline(rail));

        // Amount chip on the authorization chain.
        var chipX = chipXAt(t, centers);
        var approved = t >= 8.6;
        var travelled = new Point(Math.Min(chipX, centers[3]), chainY);
        context.DrawLine(
            Ink.Stroke(approved ? Ink.A(success, 0.55) : Ink.A(accent, 0.9), 2),
            new Point(centers[0], chainY),
            travelled);
        Ink.ArrowHead(context, new Point(travelled.X - 2, chainY), new Point(travelled.X - 10, chainY), approved ? Ink.A(success, 0.7) : accent, 4.5);

        // Approval travels back along the lower lane and repaints it.
        var approvalT = Ink.Span(t, 8.6, 12.5);
        if (t >= 8.6)
        {
            context.DrawGeometry(null, Ink.Stroke(success, 2), PartialPath(lane, approvalT));
            if (t <= 12.8)
            {
                var point = PointOnPath(lane, approvalT);
                Ink.Chip(
                    context,
                    point.X - 36,
                    point.Y - 11,
                    "Approved",
                    Ink.A(success, 0.16),
                    success,
                    22,
                    11,
                    Ink.A(success, 0.55),
                    dot: true);
            }
        }

        // Settlement: gross crosses the processor and continues as the net amount.
        var settleT = Ink.Span(t, 10.2, 13.5);
        if (t >= 10.2)
        {
            context.DrawGeometry(null, Ink.Stroke(Ink.A(accent, 0.8), 2), PartialPath(rail, settleT));
            if (t <= 13.7)
            {
                var point = PointOnPath(rail, settleT);
                var label = t < 10.55 ? Ink.Money(Amount) : Ink.Money(Amount - Fee);
                Ink.Chip(context, point.X - 30, point.Y - 21, label, accent, Ink.B(ArcTokens.AccentForeground), 22, 11, weight: FontWeight.Medium);
            }

            Ink.Label(context, $"-{Ink.Money(Fee)} fee", centers[1] + 14, railY + 24, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        }

        // The travelling amount on the chain, drawn under the node cards so it can slip behind them.
        if (t < 8.6)
        {
            var text = Ink.Money(Amount);
            var chipW = Ink.ChipWidth(text) + 6;
            Ink.Chip(
                context,
                chipX - chipW / 2,
                chainY - 13,
                text,
                accent,
                Ink.B(ArcTokens.AccentForeground),
                26,
                12,
                weight: FontWeight.Medium);
        }

        // Nodes and their status lines.
        for (var i = 0; i < 4; i++)
        {
            DrawNode(context, i, t, chainY);
            var status = StatusFor(i, t);
            var tone = status.Tone switch
            {
                2 => success,
                1 => accent,
                3 => Ink.B(ArcTokens.Danger),
                _ => Ink.B(ArcTokens.TextMuted)
            };
            var fill = status.Tone switch
            {
                2 => Ink.Wash(success, 0.14),
                1 => Ink.Wash(accent, 0.14),
                3 => Ink.Wash(Ink.B(ArcTokens.Danger), 0.12),
                _ => Ink.B(ArcTokens.SurfaceMuted)
            };
            Ink.Chip(
                context,
                _nodes[i].Center.X - Ink.ChipWidth(status.Text, 11, dot: true) / 2,
                rowTop + nodeHeight + 10,
                status.Text,
                fill,
                tone,
                21,
                11,
                status.Tone == 0 ? Ink.B(ArcTokens.Border) : null,
                dot: true);
        }

        DrawBalance(context, balanceRect, t, success, accent);
        DrawSteps(context, width, height, t, accent, success);
    }

    private static double chipXAt(double t, double[] centers)
    {
        if (t < 0.4)
        {
            return centers[0];
        }

        if (t < 2.2)
        {
            return Ink.Lerp(centers[0], centers[1], Ink.Smooth((t - 0.4) / 1.8));
        }

        if (t < 3.4)
        {
            return centers[1];
        }

        if (t < 5.2)
        {
            return Ink.Lerp(centers[1], centers[2], Ink.Smooth((t - 3.4) / 1.8));
        }

        if (t < 6.2)
        {
            return centers[2];
        }

        if (t < 8.0)
        {
            return Ink.Lerp(centers[2], centers[3], Ink.Smooth((t - 6.2) / 1.8));
        }

        return centers[3];
    }

    private void DrawNode(DrawingContext context, int index, double t, double chainY)
    {
        var active = IsProcessing(index, t);
        var hover = _hover == index;
        var fill = Ink.B(ArcTokens.SurfaceRaised);
        var border = active || hover ? Ink.B(ArcTokens.Accent) : Ink.B(ArcTokens.Border);
        context.DrawRectangle(fill, Ink.Stroke(border, active || hover ? 1.4 : 1), _nodes[index], 14, 14);

        var tile = new Rect(_nodes[index].X + 10, chainY - 13, 26, 26);
        var tint = index switch
        {
            0 => Series(0),
            1 => Series(1),
            2 => Series(2),
            _ => Series(3)
        };
        context.DrawRectangle(Ink.A(tint, active ? 0.20 : 0.10), null, tile, 8, 8);
        Ink.Icon(context, _icons[index], tile.Center.X, tile.Center.Y, 15, active ? Ink.B(ArcTokens.Accent) : tint);

        Ink.Label(context, _labels[index], tile.Right + 10, chainY - 8, 13, Ink.B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: _nodes[index].Right - tile.Right - 16);
        Ink.Label(context, _metas[index], tile.Right + 10, chainY + 9, 11, Ink.B(ArcTokens.TextMuted), maxWidth: _nodes[index].Right - tile.Right - 16);
    }

    private static bool IsProcessing(int index, double t) => index switch
    {
        0 => t < 0.4,
        1 => t >= 2.2 && t < 3.4,
        2 => t >= 5.2 && t < 6.2,
        _ => t >= 8.0 && t < 8.6
    };

    private (string Text, int Tone) StatusFor(int index, double t)
    {
        switch (index)
        {
            case 0:
                if (t < 0.4) return ("Tap", 1);
                if (t < 2.2) return ("Checking risk", 1);
                if (t < 8.6) return ("Authorizing", 0);
                return ("Charged", 2);
            case 1:
                if (t < 2.2) return ("Idle", 0);
                if (t < 3.4) return ("Risk check", 1);
                if (t < 10.2) return ("Forwarding", 0);
                return ($"Fee {Ink.Money(Fee)}", 1);
            case 2:
                if (t < 5.2) return ("Idle", 0);
                if (t < 6.2) return ("Routing", 1);
                if (t < 13.5) return ("Clearing", 0);
                return ("Cleared", 2);
            default:
                if (t < 8.0) return ("Idle", 0);
                if (t < 8.6) return ("Reviewing", 1);
                if (t < 13.5) return ("Approved", 2);
                return ("Settled", 2);
        }
    }

    private void DrawBalance(DrawingContext context, Rect rect, double t, IBrush success, IBrush accent)
    {
        Ink.Card(context, rect, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Icon(context, "receipt", rect.X + 26, rect.Y + 28, 15, Ink.B(ArcTokens.TextSecondary));
        Ink.Label(context, "Your balance", rect.X + 42, rect.Y + 28, 11, Ink.B(ArcTokens.TextMuted), FontWeight.Medium);

        var credited = Ink.Span(t, 12.6, 13.6);
        var balance = StartBalance + (Amount - Fee) * Ink.Smooth(credited);
        Ink.Label(context, Ink.Money(balance), rect.X + 22, rect.Y + 56, 22, Ink.B(ArcTokens.Foreground), FontWeight.Medium);

        if (credited > 0)
        {
            Ink.Label(context, $"+{Ink.Money(Amount - Fee)}", rect.Right - 20, rect.Y + 56, 11, success, FontWeight.Medium, Align.Right);
        }
        else
        {
            Ink.Label(context, "net of fee", rect.Right - 20, rect.Y + 56, 11, Ink.B(ArcTokens.TextMuted), Align.Right);
        }

        _ = accent;
    }

    private void DrawSteps(DrawingContext context, double width, double height, double t, IBrush accent, IBrush success)
    {
        var muted = Ink.B(ArcTokens.TextMuted);
        var border = Ink.B(ArcTokens.Border);

        var auth = Ink.Span(t, 0.4, 8.4);
        var settle = Ink.Span(t, 10.2, 13.5);

        DrawStepRow(context, 24, 306, "Authorization", auth, (1.34 * Ink.Smooth(auth)).ToString("0.00") + " s", accent, success, auth >= 1);
        DrawStepRow(context, 24, 352, "Settlement", settle, "Next business day", accent, success, settle >= 1);

        Ink.Label(
            context,
            "Approved in about a second, paid out the next business day.",
            width - 24,
            378,
            11,
            muted,
            Align.Right);
    }

    private static void DrawStepRow(
        DrawingContext context,
        double x,
        double y,
        string label,
        double progress,
        string value,
        IBrush accent,
        IBrush success,
        bool done)
    {
        var track = Ink.A(Ink.B(ArcTokens.BorderStrong), 0.45);
        const double barX = 120;
        const double barWidth = 380;
        Ink.Label(context, label, x, y + 4, 12, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        var rect = new Rect(barX, y, barWidth, 8);
        context.DrawRectangle(track, null, rect, 4, 4);
        var filled = Math.Max(0, Math.Min(barWidth, barWidth * progress));
        if (filled > 0.5)
        {
            context.DrawRectangle(done ? success : accent, null, new Rect(barX, y, filled, 8), 4, 4);
        }

        Ink.Label(context, value, barX + barWidth + 16, y + 4, 11, done ? success : Ink.B(ArcTokens.TextMuted), FontWeight.Medium);
    }

    private static StreamGeometry Polyline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], false);
        for (var i = 1; i < points.Count; i++)
        {
            ctx.LineTo(points[i]);
        }

        ctx.EndFigure(false);
        return geometry;
    }

    private static StreamGeometry PartialPath(IReadOnlyList<Point> points, double t)
    {
        var geometry = new StreamGeometry();
        var total = PathLength(points);
        if (total <= 0)
        {
            return geometry;
        }

        var remaining = total * Ink.Clamp01(t);
        using var ctx = geometry.Open();
        ctx.BeginFigure(points[0], false);
        for (var i = 1; i < points.Count; i++)
        {
            var length = Distance(points[i - 1], points[i]);
            if (remaining >= length)
            {
                ctx.LineTo(points[i]);
                remaining -= length;
                continue;
            }

            var ratio = length <= 0 ? 0 : remaining / length;
            ctx.LineTo(new Point(
                Ink.Lerp(points[i - 1].X, points[i].X, ratio),
                Ink.Lerp(points[i - 1].Y, points[i].Y, ratio)));
            break;
        }

        ctx.EndFigure(false);
        return geometry;
    }

    private static Point PointOnPath(IReadOnlyList<Point> points, double t)
    {
        var total = PathLength(points);
        var remaining = total * Ink.Clamp01(t);
        for (var i = 1; i < points.Count; i++)
        {
            var length = Distance(points[i - 1], points[i]);
            if (remaining <= length)
            {
                var ratio = length <= 0 ? 0 : remaining / length;
                return new Point(
                    Ink.Lerp(points[i - 1].X, points[i].X, ratio),
                    Ink.Lerp(points[i - 1].Y, points[i].Y, ratio));
            }

            remaining -= length;
        }

        return points[^1];
    }

    private static double PathLength(IReadOnlyList<Point> points)
    {
        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            total += Distance(points[i - 1], points[i]);
        }

        return total;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
