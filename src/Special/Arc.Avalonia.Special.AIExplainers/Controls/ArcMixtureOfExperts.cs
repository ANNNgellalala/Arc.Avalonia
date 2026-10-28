using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// Sparse mixture of experts routing: tokens slide in one at a time, the router scores every expert with a
/// small bar, and the token travels to its top one or two experts. The chosen experts light up, their load
/// counters roll, and their outputs merge back into the token's result. A capacity limit makes a full expert
/// skip a token, which the card shows as a warning.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public mixture-of-experts documentation. Approximations: the sequence
/// is eight sample tokens with hand tuned gate scores, the parameter readout is illustrative, and capacity
/// dropping is computed over the whole sequence rather than tracked live. Top 1 and Top 2 are real controls
/// and both change the picture, the readout and the load balance.
/// </remarks>
public class ArcMixtureOfExperts : ArcExplainer
{
    private sealed record Expert(string Name, string Handles, string Icon);

    private static readonly Expert[] Experts =
    {
        new("Code", "Identifiers and keywords", "code"),
        new("Math", "Numbers and operators", "hash"),
        new("Prose", "Everyday words", "align-left"),
        new("Names", "People and places", "user"),
        new("Grammar", "Sentence structure", "type"),
        new("Verbs", "Actions and tenses", "activity"),
        new("Dates", "Times and numbers", "calendar"),
        new("Symbols", "Punctuation marks", "dot")
    };

    private static readonly string[] Tokens = { "const", " total", " =", " paid", " +", " tax", " now", " ." };

    private static readonly double[][] Scores =
    {
        new[] { 0.70, 0.05, 0.05, 0.05, 0.05, 0.05, 0.02, 0.03 },
        new[] { 0.10, 0.10, 0.30, 0.10, 0.20, 0.10, 0.05, 0.05 },
        new[] { 0.05, 0.60, 0.05, 0.05, 0.05, 0.05, 0.05, 0.10 },
        new[] { 0.04, 0.02, 0.10, 0.02, 0.05, 0.68, 0.02, 0.07 },
        new[] { 0.05, 0.55, 0.05, 0.05, 0.05, 0.05, 0.05, 0.15 },
        new[] { 0.02, 0.10, 0.20, 0.02, 0.05, 0.25, 0.02, 0.34 },
        new[] { 0.02, 0.02, 0.35, 0.02, 0.40, 0.10, 0.05, 0.04 },
        new[] { 0.02, 0.02, 0.05, 0.02, 0.30, 0.03, 0.02, 0.54 }
    };

    private const double RowTop = 104;
    private const double RowHeight = 28;
    private readonly Rect[] _expertRows = new Rect[8];
    private readonly Rect[] _topKPills = new Rect[2];
    private readonly int[] _loads = new int[8];
    private int _topK = 2;
    private int _hover = -1;

    public ArcMixtureOfExperts()
    {
        for (var i = 0; i < 8; i++)
        {
            _expertRows[i] = default;
        }

        Recount();
        Describe(
            "Mixture of experts illustration: each token is scored by a router and sent to its top two experts, whose outputs merge into the token result, with per expert load counters and a capacity limit",
            "Use the Top 1 and Top 2 pills or the 1 and 2 keys to change how many experts run. Press Space to pause or resume routing.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 17.6;

    protected override double PosterPhase => 0.62;

    private void Recount()
    {
        Array.Clear(_loads);
        for (var i = 0; i < Tokens.Length; i++)
        {
            foreach (var expert in Top(Tokens[i], _topK))
            {
                _loads[expert]++;
            }
        }
    }

    private int[] Top(int token, int k)
    {
        var order = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
        order.Sort((a, b) => Scores[token][b].CompareTo(Scores[token][a]));
        return order.GetRange(0, k).ToArray();
    }

    private int[] Top(string token, int k) => Top(Array.IndexOf(Tokens, token), k);

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p)
        {
            for (var i = 0; i < 8; i++)
            {
                if (_expertRows[i].Contains(p))
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

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        for (var i = 0; i < 2; i++)
        {
            if (_topKPills[i].Contains(point))
            {
                SetTopK(i + 1);
                return true;
            }
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        if (key is Key.D1 or Key.NumPad1)
        {
            SetTopK(1);
            return true;
        }

        if (key is Key.D2 or Key.NumPad2)
        {
            SetTopK(2);
            return true;
        }

        return false;
    }

    private void SetTopK(int value)
    {
        if (_topK == value)
        {
            return;
        }

        _topK = value;
        Recount();
    }

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);
        var warning = Ink.B(ArcTokens.Warning);
        var muted = Ink.B(ArcTokens.TextMuted);

        var tokenIndex = Math.Min(Tokens.Length - 1, (int)(phase * Tokens.Length));
        var local = phase * Tokens.Length - tokenIndex;
        var chosen = Top(tokenIndex, _topK);

        Ink.Label(context, "Sparse routing", 24, 26, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        Ink.Label(context, $"token {tokenIndex + 1} of {Tokens.Length}", width - 24, 26, 10.5, muted, Align.Right);

        // Input token and the sequence it came from.
        var input = new Rect(24, 148, 116, 48);
        Ink.Card(context, input, 14, Ink.Wash(accent, 0.12), Ink.A(accent, 0.4));
        Ink.Icon(context, "hash", input.X + 18, input.Center.Y, 14, Ink.B(ArcTokens.TextSecondary));
        Ink.Label(context, Tokens[tokenIndex].Trim(), input.X + 34, input.Center.Y, 13, Ink.B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: 70);

        Ink.Label(context, "Input", input.X, input.Y - 12, 10, muted, FontWeight.Medium);
        Ink.Paragraph(context, "Only the chosen experts run, so each token costs a fraction of the model.", 24, 226, 10.5, muted, 134);

        // Router panel with a gate score per expert.
        var router = new Rect(170, 64, 196, RowTop - 64 + 8 * RowHeight + 16);
        Ink.Card(context, router, 18, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Icon(context, "filter", router.X + 18, router.Y + 18, 13, Ink.B(ArcTokens.TextSecondary));
        Ink.Label(context, "Router", router.X + 30, router.Y + 18, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        Ink.Label(context, "gate weights", router.Right - 14, router.Y + 18, 9.5, muted, Align.Right);

        // Experts panel with loads and capacity.
        var experts = new Rect(404, 64, 300, router.Height);
        Ink.Card(context, experts, 18, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Experts", experts.X + 16, experts.Y + 18, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        var capacity = (int)Math.Ceiling(Tokens.Length * (double)_topK / Experts.Length);
        Ink.Label(context, $"capacity {capacity} each", experts.Right - 14, experts.Y + 18, 9.5, muted, Align.Right);

        // Merge point and output.
        var merge = new Point(744, 186);
        var output = new Rect(792, 160, 250, 52);

        for (var i = 0; i < 8; i++)
        {
            var rowY = RowTop + i * RowHeight;
            _expertRows[i] = new Rect(experts.X + 8, rowY - 12, experts.Width - 16, RowHeight - 4);
            var isChosen = Array.IndexOf(chosen, i) >= 0;
            var rank = Array.IndexOf(chosen, i);
            var tone = rank == 0 ? accent : rank == 1 ? Series(2) : Ink.B(ArcTokens.TextMuted);

            // Gate bar.
            var barRect = new Rect(router.X + 14, rowY - 3, router.Width - 46, 6);
            context.DrawRectangle(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.3), null, barRect, 3, 3);
            var weight = Scores[tokenIndex][i];
            var barW = Math.Max(2, barRect.Width * weight);
            context.DrawRectangle(isChosen ? tone : Ink.A(Ink.B(ArcTokens.TextMuted), 0.45), null, new Rect(barRect.X, barRect.Y, barW, 6), 3, 3);
            Ink.Label(context, $"{weight:0.00}", router.Right - 14, rowY, 9.5, isChosen ? tone : muted, FontWeight.Medium, Align.Right);

            // Connector router to expert.
            if (isChosen)
            {
                context.DrawLine(Ink.Stroke(Ink.A(tone, 0.7), 1.4), new Point(router.Right, rowY), new Point(experts.X, rowY));
            }

            // Expert row.
            if (_hover == i || isChosen)
            {
                context.DrawRectangle(Ink.Wash(tone, 0.12), null, _expertRows[i], 9, 9);
            }

            var iconAt = new Point(experts.X + 24, rowY + 1);
            context.DrawEllipse(Ink.A(tone, isChosen ? 0.22 : 0.1), null, iconAt, 11, 11);
            Ink.Icon(context, Experts[i].Icon, iconAt.X, iconAt.Y, 12, isChosen ? tone : Ink.B(ArcTokens.TextMuted));

            Ink.Label(context, Experts[i].Name, experts.X + 42, rowY + 1, 11, isChosen ? Ink.B(ArcTokens.Foreground) : Ink.B(ArcTokens.TextSecondary), isChosen ? FontWeight.Medium : FontWeight.Normal);
            var load = _loads[i];
            var full = load > capacity;
            var loadInk = full ? warning : muted;
            Ink.Label(context, full ? $"{load} full" : $"{load}", experts.Right - 16, rowY + 1, 10, loadInk, FontWeight.Medium, Align.Right);
            Ink.Icon(context, full ? "circle-alert" : "circle-dot", experts.Right - 46, rowY + 1, 9, loadInk);

            // Connector to the merge point.
            if (isChosen)
            {
                var start = new Point(experts.Right, rowY + 1);
                context.DrawGeometry(null, Ink.Stroke(Ink.A(tone, 0.55), 1.4), Ink.Cubic(start, new Point(start.X + 44, start.Y), new Point(merge.X - 40, merge.Y), merge));
            }
        }

        // Flow packets: token into the router, then out along the chosen lanes.
        var packet = Ink.Smooth(local * 2.0);
        if (packet < 0.35)
        {
            var t = packet / 0.35;
            var point = new Point(Ink.Lerp(input.Right, router.X, t), Ink.Lerp(input.Center.Y, RowTop + 15, t));
            context.DrawEllipse(Ink.A(accent, 0.95), null, point, 4, 4);
        }
        else if (packet < 0.7)
        {
            var t = (packet - 0.35) / 0.35;
            foreach (var expert in chosen)
            {
                var rowY = RowTop + expert * RowHeight;
                var point = new Point(Ink.Lerp(router.X, experts.X, t), Ink.Lerp(RowTop + 15, rowY, t));
                context.DrawEllipse(Ink.A(accent, 0.95), null, point, 3.4, 3.4);
            }
        }
        else
        {
            var t = (packet - 0.7) / 0.3;
            foreach (var expert in chosen)
            {
                var rowY = RowTop + expert * RowHeight;
                var start = new Point(experts.Right, rowY + 1);
                var point = Ink.CubicAt(start, new Point(start.X + 44, start.Y), new Point(merge.X - 40, merge.Y), merge, t);
                context.DrawEllipse(Ink.A(accent, 0.95), null, point, 3.4, 3.4);
            }
        }

        // Merge and output.
        context.DrawEllipse(Ink.A(accent, 0.14), new Pen(Ink.A(accent, 0.5), 1.2), merge, 9, 9);
        context.DrawLine(Ink.Stroke(Ink.A(accent, 0.6), 1.4), merge, new Point(output.X, output.Center.Y));

        Ink.Card(context, output, 14, Ink.Wash(success, 0.1), Ink.A(success, 0.35));
        Ink.Icon(context, "sparkles", output.X + 20, output.Center.Y, 14, success);
        Ink.Label(context, Tokens[tokenIndex].Trim(), output.X + 38, output.Center.Y - 8, 12.5, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Label(context, _topK == 1 ? "1 expert ran" : "2 experts merged", output.X + 38, output.Center.Y + 9, 10, muted);

        // Top k control.
        var pillX = width - 24 - 2 * 48 - 6;
        for (var i = 0; i < 2; i++)
        {
            var rect = new Rect(pillX + i * 54, 58, 48, 24);
            _topKPills[i] = rect;
            var active = _topK == i + 1;
            context.DrawRectangle(
                active ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.Surface),
                new Pen(active ? Ink.A(accent, 0.55) : Ink.B(ArcTokens.Border), 1),
                rect,
                8,
                8);
            Ink.Label(context, $"Top {i + 1}", rect.Center.X, rect.Center.Y, 10.5, active ? accent : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        }

        // Footer: active parameters and load balance.
        var activeParams = 5 + _topK * 16;
        var widest = _loads.Max();
        Ink.Label(context, $"Active parameters {activeParams}B of 671B", 24, 366, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        Ink.Label(context, $"widest expert {widest} of {capacity} slots", 24, 384, 10, widest > capacity ? warning : muted);

        if (_hover >= 0)
        {
            var text = $"{Experts[_hover].Name} · {Experts[_hover].Handles} · load {_loads[_hover]} of {capacity}";
            Ink.Chip(
                context,
                Math.Clamp(_expertRows[_hover].X, 24, width - 24 - Ink.ChipWidth(text, 10.5)),
                _expertRows[_hover].Y - 22,
                text,
                Ink.B(ArcTokens.SurfaceRaised),
                Ink.B(ArcTokens.TextSecondary),
                21,
                10.5,
                Ink.B(ArcTokens.Border));
        }
    }

}
