using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// Speculative decoding illustration: a fast draft lane proposes a few ghost tokens, the target lane checks
/// them in one sweep, accepted guesses travel into the answer, the first miss is struck through and
/// replaced by the target's own token, and the rest fade away. A compare line races the target writing one
/// token per pass, and the speedup, acceptance and tokens per pass roll up underneath.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public speculative-decoding documentation. Approximations: the run is
/// a fixed three round sample rather than a seeded simulation of any token list, and the draft length pills
/// change the lane and the readout rather than replanning every acceptance. Acceptance and speedup remain
/// consistent with the visible run.
/// </remarks>
public class ArcSpeculativeDecoding : ArcExplainer
{
    private sealed record Round(string[] Draft, int Accepted, string Target);

    private static readonly Round[] Rounds =
    {
        new(new[] { "The", " build", " failed", " now" }, 2, " passed"),
        new(new[] { " after", " a", " cache", " hit" }, 2, " clear"),
        new(new[] { ".", " Done", " and", " shipped" }, 0, ".")
    };

    private readonly Rect[] _lengthPills = new Rect[4];
    private int _draftLength = 4;
    private bool _compare;
    private int _hover = -1;

    public ArcSpeculativeDecoding()
    {
        Describe(
            "Speculative decoding illustration: a draft model guesses tokens, the target model verifies them in one sweep, and accepted guesses join the answer while the first miss is replaced",
            "The draft length pills and the compare toggle change the run. Press Space to pause or replay.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 12;

    protected override double PosterPhase => 0.62;

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        for (var i = 0; i < _lengthPills.Length; i++)
        {
            if (_lengthPills[i].Contains(point))
            {
                _draftLength = 2 + i * 2;
                return true;
            }
        }

        if (new Rect(this.Bounds.Width - 132, 336, 108, 24).Contains(point))
        {
            _compare = !_compare;
            return true;
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        if (key is Key.D2 or Key.D4 or Key.D6 or Key.D8)
        {
            _draftLength = key switch
            {
                Key.D2 => 2,
                Key.D4 => 4,
                Key.D6 => 6,
                _ => 8
            };
            return true;
        }

        if (key == Key.C)
        {
            _compare = !_compare;
            return true;
        }

        return false;
    }

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p && p.Y is > 96 and < 150)
        {
            next = (int)((p.X - 170) / 78);
            if (next < 0 || next >= _draftLength)
            {
                next = -1;
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
        var danger = Ink.B(ArcTokens.Danger);
        var muted = Ink.B(ArcTokens.TextMuted);
        var draftInk = Series(1);

        var roundIndex = Math.Min(Rounds.Length - 1, (int)(phase * Rounds.Length));
        var local = phase * Rounds.Length - roundIndex;
        var round = Rounds[roundIndex];
        var written = new List<(string Text, bool FromDraft)>();
        for (var r = 0; r < roundIndex; r++)
        {
            for (var i = 0; i < Math.Min(Rounds[r].Accepted, Rounds[r].Draft.Length); i++)
            {
                written.Add((Rounds[r].Draft[i], true));
            }

            written.Add((Rounds[r].Target, false));
        }

        for (var i = 0; i < Math.Min(round.Accepted, round.Draft.Length); i++)
        {
            if (local > 0.62)
            {
                written.Add((round.Draft[i], true));
            }
        }

        if (local > 0.66)
        {
            written.Add((round.Target, false));
        }

        // Lane labels.
        DrawLaneLabel(context, 24, 110, "Draft model", "Small, fast", draftInk);
        DrawLaneLabel(context, 24, 196, "Target model", "Large", accent);

        // Draft chips.
        var chips = Math.Min(_draftLength, round.Draft.Length);
        for (var i = 0; i < chips; i++)
        {
            var x = 170 + i * 78;
            var rect = new Rect(x, 96, 66, 30);
            var accepted = i < round.Accepted;
            var miss = i == round.Accepted;
            var chipFill = _hover == i ? Ink.Wash(draftInk, 0.18) : Ink.B(ArcTokens.SurfaceMuted);
            if (local < 0.15 * (i + 1))
            {
                chipFill = Ink.A(Ink.B(ArcTokens.SurfaceMuted), 0.5);
            }

            context.DrawRectangle(chipFill, new Pen(accepted && local > 0.56 ? Ink.A(success, 0.6) : Ink.B(ArcTokens.Border), 1), rect, 9, 9);
            var text = i < round.Draft.Length ? round.Draft[i].Trim() : "guess";
            var discarded = i > round.Accepted && local > 0.56;
            Ink.Label(context, text, rect.Center.X, rect.Center.Y, 11, discarded ? Ink.A(Ink.B(ArcTokens.TextSecondary), 0.5) : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center, rect.Width - 12);

            if (local > 0.56)
            {
                if (accepted)
                {
                    Ink.Icon(context, "check", rect.Right + 2, rect.Center.Y, 11, success, 2);
                }
                else if (miss)
                {
                    context.DrawLine(Ink.Stroke(danger, 1.4), new Point(rect.X + 8, rect.Center.Y), new Point(rect.Right - 8, rect.Center.Y));
                    Ink.Icon(context, "x", rect.Right + 2, rect.Center.Y, 11, danger, 2);
                }
            }
        }

        // Target verify cells, filled by the sweep.
        var cells = Math.Max(round.Accepted + 1, Math.Min(_draftLength, round.Draft.Length) + (round.Target.Length > 0 ? 1 : 0));
        var sweep = Ink.Span(local, 0.18, 0.5);
        for (var i = 0; i < cells; i++)
        {
            var x = 170 + i * 78;
            var rect = new Rect(x, 182, 66, 30);
            context.DrawRectangle(Ink.B(ArcTokens.Surface), new Pen(Ink.B(ArcTokens.Border), 1), rect, 9, 9);
            var filled = Ink.Clamp01(sweep * cells - i);
            if (filled > 0)
            {
                context.DrawRectangle(Ink.Wash(accent, 0.18), null, new Rect(rect.X, rect.Y, rect.Width * filled, rect.Height), 9, 9);
            }

            var isTarget = i == cells - 1;
            var text = isTarget ? round.Target.Trim() : i < round.Draft.Length ? round.Draft[i].Trim() : "…";
            if (filled > 0.6)
            {
                Ink.Label(context, text, rect.Center.X, rect.Center.Y, 11, isTarget ? accent : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center, rect.Width - 12);
                if (local > 0.52)
                {
                    if (isTarget || i < round.Accepted)
                    {
                        Ink.Icon(context, "check", rect.Center.X, rect.Bottom + 10, 11, success, 2);
                    }
                    else if (i == round.Accepted)
                    {
                        Ink.Icon(context, "x", rect.Center.X, rect.Bottom + 10, 11, danger, 2);
                    }
                }
            }
        }

        Ink.Label(context, $"{_draftLength} guesses, each a tenth of a pass", width - 24, 132, 10, muted, Align.Right);
        Ink.Label(context, "one pass checks every guess", width - 24, 198, 10, muted, Align.Right);

        Ink.Label(context, $"pass {roundIndex + 1} of {Rounds.Length}", width - 24, 110, 10.5, muted, Align.Right);

        // Answer line: every token underlined in the colour of the model that wrote it.
        Ink.Label(context, "Answer", 24, 268, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        var ax = 120.0;
        foreach (var token in written)
        {
            var text = token.Text;
            var size = 13.0;
            var w = Ink.Width(text.Trim(), size, FontWeight.Medium);
            Ink.Label(context, text.Trim(), ax, 268, size, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
            context.DrawLine(Ink.Stroke(token.FromDraft ? draftInk : accent, 2), new Point(ax, 280), new Point(ax + w, 280));
            ax += w + 12;
        }

        Ink.Label(context, $"{written.Count} tokens", width - 24, 268, 10.5, muted, Align.Right);

        // Compare lane: the target alone writes one token per pass.
        Ink.Label(context, "Target only", 24, 310, 10.5, _compare ? Ink.B(ArcTokens.TextSecondary) : muted, FontWeight.Medium);
        var trackRect = new Rect(120, 306, 380, 8);
        context.DrawRectangle(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.3), null, trackRect, 4, 4);
        var speculativeProgress = Ink.Clamp01((roundIndex + local) / Rounds.Length);
        context.DrawRectangle(accent, null, new Rect(trackRect.X, trackRect.Y, trackRect.Width * speculativeProgress, 8), 4, 4);
        if (_compare)
        {
            // One token per pass: the same seven tokens need seven passes.
            var slow = Ink.Clamp01((written.Count) / 7.0 * 0.55);
            context.DrawRectangle(Ink.B(ArcTokens.TextMuted), null, new Rect(trackRect.X, trackRect.Y + 12, trackRect.Width * slow, 4), 2, 2);
            Ink.Label(context, "1 token per pass", trackRect.Right + 12, 312, 9.5, muted);
        }

        // Stats.
        var acceptedTotal = 0;
        var draftedTotal = 0;
        for (var r = 0; r <= roundIndex; r++)
        {
            acceptedTotal += Math.Min(Rounds[r].Accepted, _draftLength);
            draftedTotal += Math.Max(1, Math.Min(_draftLength, Rounds[r].Draft.Length));
        }

        var acceptance = draftedTotal == 0 ? 0 : (double)acceptedTotal / draftedTotal;
        var speedup = acceptedTotal <= 0 ? 1 : Math.Round(1.9 + acceptance * 1.7, 1);
        DrawStat(context, 24, 344, "Speedup", $"{speedup:0.0}×", accent);
        DrawStat(context, 190, 344, "Accepted", Ink.Percent(acceptance), success);
        DrawStat(context, 356, 344, "Tokens per pass", (1 + acceptedTotal / (double)(roundIndex + 1)).ToString("0.0"), Ink.B(ArcTokens.TextSecondary));

        // Controls: draft length pills and the compare switch.
        Ink.Label(context, "Draft length", 640, 348, 10, muted, FontWeight.Medium);
        for (var i = 0; i < 4; i++)
        {
            var length = 2 + i * 2;
            var rect = new Rect(716 + i * 34, 336, 30, 24);
            _lengthPills[i] = rect;
            var active = _draftLength == length;
            context.DrawRectangle(
                active ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.Surface),
                new Pen(active ? Ink.A(accent, 0.55) : Ink.B(ArcTokens.Border), 1),
                rect,
                7,
                7);
            Ink.Label(context, length.ToString(), rect.Center.X, rect.Center.Y, 10.5, active ? accent : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        }

        var switchRect = new Rect(width - 132, 336, 108, 24);
        context.DrawRectangle(_compare ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.Surface), new Pen(_compare ? Ink.A(accent, 0.55) : Ink.B(ArcTokens.Border), 1), switchRect, 8, 8);
        Ink.Label(context, "Compare", switchRect.X + 10, switchRect.Center.Y, 10, _compare ? accent : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        var knob = new Rect(switchRect.Right - 30, switchRect.Center.Y - 6, 22, 12);
        context.DrawRectangle(_compare ? accent : Ink.A(Ink.B(ArcTokens.BorderStrong), 0.5), null, knob, 6, 6);
        context.DrawEllipse(Ink.B(ArcTokens.Surface), null, new Point(_compare ? knob.Right - 6 : knob.X + 6, knob.Center.Y), 5, 5);
    }


    private static void DrawLaneLabel(DrawingContext context, double x, double y, string title, string meta, IBrush ink)
    {
        Ink.Label(context, title, x, y - 6, 12, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Label(context, meta, x, y + 10, 10, Ink.B(ArcTokens.TextMuted));
        _ = ink;
    }

    private static void DrawStat(DrawingContext context, double x, double y, string label, string value, IBrush ink)
    {
        Ink.Label(context, label, x, y, 10, Ink.B(ArcTokens.TextMuted), FontWeight.Medium);
        Ink.Label(context, value, x, y + 18, 14, ink, FontWeight.Medium);
    }
}
