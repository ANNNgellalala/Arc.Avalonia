using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;
namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// Prompt cache illustration: three requests share one long prefix. The first writes every segment and the
/// prefix lifts into a cache panel whose time to live ring starts draining. Later requests read the prefix
/// back with one quick sweep, process only their new message, and their input cost and time to first token
/// drop. A changed segment breaks the match, shows where it breaks and rewrites the rest.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public prompt-cache documentation. Approximations: prices are fixed
/// sample values shown in the caption rather than a model price prop, the simulated clock is compressed so
/// three requests fit one loop, and the cache panel is drawn beside the rows rather than above them at
/// narrow widths (the gallery is always wide).
/// </remarks>
public class ArcPromptCache : ArcExplainer
{
    private sealed record Segment(string Label, double Tokens, int Series);
    private sealed record Request(string Message, double Tokens, string Kind, double Cost, double FirstToken, double Processed, double Cached);

    private static readonly Segment[] Segments =
    {
        new("Tools", 3.8, 0),
        new("System prompt", 2.4, 1),
        new("Documents", 36.2, 2),
        new("Conversation", 6.0, 3)
    };

    private static readonly Request[] Requests =
    {
        new("Which refunds are still pending this week?", 0.18, "Cache write", 0.081, 0.84, 42.4, 0),
        new("Draft a reply to Northwind about their late refund", 0.24, "Cache hit", 0.004, 0.11, 0.24, 42.4),
        new("Summarize ticket 48213 and the attached invoice", 0.41, "Partial hit", 0.021, 0.39, 8.6, 37.8)
    };

    private Rect _editPill;
    private bool _editArmed = true;
    private int _hover = -1;

    public ArcPromptCache()
    {
        Describe(
            "Prompt cache illustration: three requests share tools, system prompt, documents and conversation; the first writes the cache, later requests read it back, and the third breaks the match at a changed segment. Input cost and time to first token are shown per request",
            "Press Space to pause. Click or focus the figure and press E to change the system prompt and show a partial hit.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 20;

    protected override double PosterPhase => 0.78;

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        if (_editPill.Contains(point))
        {
            _editArmed = !_editArmed;
            return true;
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        if (key == Key.E)
        {
            _editArmed = !_editArmed;
            return true;
        }

        return false;
    }

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p && p.X < 620)
        {
            for (var i = 0; i < 3; i++)
            {
                if (p.Y > 84 + i * 78 && p.Y < 84 + i * 78 + 66)
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
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);
        var warning = Ink.B(ArcTokens.Warning);
        var muted = Ink.B(ArcTokens.TextMuted);
        var requestIndex = Math.Min(2, (int)(phase * 3.2));
        var local = phase * 3.2 - requestIndex;

        // Header.
        Ink.Label(context, "Three requests, one prefix", 24, 26, 11.5, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        Ink.Label(context, "Claude Sonnet · $2.00 input · $0.20 cache read per M tokens", width - 24, 26, 10, muted, Align.Right);

        // The requests.
        for (var i = 0; i < Requests.Length; i++)
        {
            var top = 84 + i * 78;
            var row = new Rect(24, top, 596, 66);
            var live = i <= requestIndex;
            var active = i == requestIndex;
            var fill = _hover == i ? Ink.Wash(accent, 0.05) : Ink.B(ArcTokens.Surface);
            context.DrawRectangle(fill, new Pen(active ? Ink.A(accent, 0.45) : Ink.B(ArcTokens.Border), 1), row, 14, 14);

            var request = Requests[i];
            var status = i == 0 ? "Cache write" : i == 1 ? "Cache hit" : _editArmed ? "Partial hit" : "Cache hit";
            var tone = i == 0 ? warning : i == 1 ? success : accent;
            Ink.Label(context, $"Request {i + 1}", row.X + 14, row.Y + 16, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
            Ink.Label(context, request.Message, row.X + 82, row.Y + 16, 10.5, muted, maxWidth: 250);
            Ink.Chip(
                context,
                row.Right - 106,
                row.Y + 6,
                status,
                Ink.Wash(tone, 0.13),
                tone,
                19,
                9.5,
                dot: true);

            // Segment bar: cached segments are one tone, processed segments another.
            var barX = row.X + 14;
            var barY = row.Y + 30;
            var barWidth = row.Width - 28;
            var total = 0.0;
            foreach (var segment in Segments)
            {
                total += Math.Sqrt(segment.Tokens);
            }

            total += Math.Sqrt(request.Tokens);
            var breakAt = _editArmed && i == 2 ? 1 : int.MaxValue;
            var offset = 0.0;
            for (var s = 0; s < Segments.Length; s++)
            {
                var segment = Segments[s];
                var w = barWidth * Math.Sqrt(segment.Tokens) / total;
                var cached = live && s < breakAt && i > 0;
                var moved = !live || (i == requestIndex && local < 0.25 * (s + 1));
                var brush = cached
                    ? Ink.A(Series(segment.Series), 0.35)
                    : moved
                        ? Ink.A(Series(segment.Series), 0.9)
                        : Ink.A(Series(segment.Series), 0.9);
                context.DrawRectangle(brush, cached ? new Pen(Ink.A(Series(segment.Series), 0.7), 1) : null, new Rect(barX + offset, barY, Math.Max(0, w - 2), 11), 3, 3);
                if (w > 78 && Ink.Width(segment.Label, 8.5, FontWeight.Medium) < w - 12)
                {
                    Ink.Label(context, segment.Label, barX + offset + 6, barY + 5.5, 8.5, Ink.B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: w - 10);
                }

                offset += w;
            }

            var messageWidth = Math.Max(6, barWidth * Math.Sqrt(request.Tokens) / total);
            context.DrawRectangle(Ink.Wash(accent, 0.9, ArcTokens.SurfaceRaised), null, new Rect(barX + offset, barY, messageWidth, 11), 3, 3);

            if (breakAt < Segments.Length)
            {
                var breakX = barX;
                for (var s = 0; s < breakAt; s++)
                {
                    breakX += barWidth * Math.Sqrt(Segments[s].Tokens) / total;
                }

                context.DrawLine(Ink.Stroke(warning, 2), new Point(breakX, barY - 4), new Point(breakX, barY + 15));
                context.DrawEllipse(warning, null, new Point(breakX, barY + 17), 2, 2);
            }

            // Metrics line.
            var processedText = live ? $"{request.Processed:0.#}K processed" : "not sent yet";
            Ink.Label(context, processedText, row.X + 14, row.Y + 55, 9.5, muted);
            Ink.Label(context, live ? $"${request.Cost:0.000} input" : "-", row.X + 150, row.Y + 55, 9.5, muted);
            Ink.Label(context, live ? $"{request.FirstToken:0.00} s to first token" : "-", row.X + 260, row.Y + 55, 9.5, muted);
            Ink.Label(context, i > 0 && live ? $"{request.Cached:0.#}K read from cache" : "cache empty", row.Right - 14, row.Y + 55, 9.5, i > 0 && live ? success : muted, Align.Right);
        }

        // Cache panel.
        var panel = new Rect(644, 60, width - 668, 244);
        Ink.Card(context, panel, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Cache", panel.X + 16, panel.Y + 20, 11.5, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        var ttl = phase < 0.94 ? 1 - phase * 0.85 : Math.Max(0, 1 - phase) * 6;
        Ink.Label(context, ttl > 0.05 ? $"expires in {ttl * 5:0}:{ttl * 60 % 60:00}" : "expired", panel.Right - 16, panel.Y + 20, 10, ttl > 0.05 ? muted : Ink.B(ArcTokens.Danger), Align.Right);

        // Time to live ring.
        var ringCenter = new Point(panel.X + 54, panel.Y + 74);
        context.DrawEllipse(null, new Pen(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.35), 6), ringCenter, 26, 26);
        var sweep = Math.Max(0.01, ttl) * 359;
        if (ttl > 0.02)
        {
            var ring = new StreamGeometry();
            using (var ctx = ring.Open())
            {
                ctx.BeginFigure(Ink.OnCircle(ringCenter, 26, -90), false);
                ctx.ArcTo(Ink.OnCircle(ringCenter, 26, -90 + sweep), new Size(26, 26), 0, sweep > 180, SweepDirection.Clockwise);
                ctx.EndFigure(false);
            }

            context.DrawGeometry(null, new Pen(accent, 6, null, PenLineCap.Round), ring);
        }

        Ink.Label(context, ttl > 0.05 ? "TTL" : "cold", ringCenter.X, ringCenter.Y - 2, 10, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        Ink.Label(context, ttl > 0.05 ? "5 min" : "0", ringCenter.X, ringCenter.Y + 11, 8.5, muted, Align.Center);

        // Cached segments.
        var rowY = panel.Y + 116;
        for (var s = 0; s < Segments.Length; s++)
        {
            var segment = Segments[s];
            var cached = phase > 0.15;
            context.DrawRectangle(cached ? Ink.A(Series(segment.Series), 0.7) : Ink.A(Ink.B(ArcTokens.BorderStrong), 0.3), null, new Rect(panel.X + 16, rowY - 4, 8, 8), 2, 2);
            Ink.Label(context, segment.Label, panel.X + 30, rowY, 10, Ink.B(ArcTokens.TextSecondary), maxWidth: panel.Width - 130);
            Ink.Label(context, $"{segment.Tokens:0.#}K", panel.Right - 16, rowY, 10, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Right);
            rowY += 22;
        }

        Ink.Label(context, "42.4K tokens cached", panel.X + 16, panel.Bottom - 14, 9.5, muted);
        Ink.Label(context, "reads cost 0.1x input, writes 1.25x", panel.X + 16, panel.Bottom - 30, 9, muted);

        // Totals and the edit toggle.
        Ink.Label(context, "Saved $0.09 on input", 24, 332, 12, success, FontWeight.Medium);
        Ink.Label(context, "7.6× faster to first token", 24, 352, 12, success, FontWeight.Medium);
        Ink.Label(context, "First request pays for the write; later hits pay it back.", 220, 352, 9.5, muted);

        _editPill = new Rect(width - 218, 330, 194, 26);
        context.DrawRectangle(_editArmed ? Ink.Wash(warning, 0.16) : Ink.B(ArcTokens.Surface), new Pen(_editArmed ? Ink.A(warning, 0.55) : Ink.B(ArcTokens.Border), 1), _editPill, 9, 9);
        Ink.Label(context, "Change the system prompt", _editPill.Center.X, _editPill.Center.Y, 10, _editArmed ? warning : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        _ = height;
    }
}
