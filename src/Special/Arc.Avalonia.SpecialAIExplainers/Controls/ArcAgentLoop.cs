using System;
using Avalonia;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIExplainers.Controls;

/// <summary>
/// Agent loop illustration: a bright head with a soft trail circles a ring past three stations, Gather
/// context, Take action and Verify, with the model at the centre. Each pass adds a context block and rolls
/// the token count up, drops a tool call out of the ring on a short pin and resolves it with a result, and
/// ends on a check or a retry note. The third pass closes on the done state and the summary.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public agent-loop documentation. Approximations: the run is a fixed
/// three loop sample timeline rather than a data-driven iteration list, the tool marks are Arc icons rather
/// than brand logos, and the per-beat status region is represented by the visible caption and the
/// accessible description instead of a live region.
/// </remarks>
public class ArcAgentLoop : ArcExplainer
{
    private sealed record ContextBlock(string Label, string Detail, string Tokens);
    private sealed record ToolCall(string Tool, string Icon, string Action, string Result, bool Failed);

    private static readonly ContextBlock[] Blocks =
    {
        new("Build log", "Step 4 of 9 failed", "3.2K"),
        new("Workflow file", "deploy.yml, 84 lines", "1.9K"),
        new("Deploy log", "Cache miss, rerun", "2.4K")
    };

    private static readonly ToolCall[] Calls =
    {
        new("GitHub", "code", "Read workflow run", "Cache key mismatch", false),
        new("GitHub", "code", "Clear cache and rerun", "Deploy passed in 3m 12s", false),
        new("Slack", "message-circle", "Post update", "Posted in #releases", false)
    };

    private const double RingRadius = 100;
    private static readonly Point RingCenter = new(252, 182);
    private static readonly double[] StationAngles = { -90, 30, 150 };
    private static readonly string[] StationLabels = { "Gather context", "Take action", "Verify" };
    private static readonly string[] StationIcons = { "layers", "zap", "shield-check" };

    public ArcAgentLoop()
    {
        Describe(
            "Agent loop illustration: a bright head circles three stations, gathering context, taking a tool action and verifying the result, three times, then finishes with a summary",
            "The head eases from station to station and each pass adds a context block and a tool result. Press Space to pause or resume.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 12;

    protected override double PosterPhase => 0.62;

    private static Point Station(int index) => Ink.OnCircle(RingCenter, RingRadius, StationAngles[index]);

    private static int LoopIndex(double phase) => Math.Min(2, (int)(phase * 3));

    private static double LoopProgress(double phase) => phase * 3 - LoopIndex(phase);

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);
        var warning = Ink.B(ArcTokens.Warning);
        var muted = Ink.B(ArcTokens.TextMuted);
        var loop = LoopIndex(phase);
        var local = LoopProgress(phase);
        var done = phase >= 0.985;

        // Track and direction chevrons.
        context.DrawEllipse(null, new Pen(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.35), 2), RingCenter, RingRadius, RingRadius);
        for (var i = 0; i < 8; i++)
        {
            var point = Ink.OnCircle(RingCenter, RingRadius, i * 45 + 22);
            Ink.Icon(context, "chevron-right", point.X, point.Y, 9, Ink.A(muted, 0.5), 2);
        }

        // Trail sits under the stations so the nodes stay crisp, and fades toward its tail.
        var headAngle = done ? 270.0 : -90 + local * 360;
        for (var i = 0; i < 4; i++)
        {
            var from = headAngle - 64 + i * 16;
            var sweepAngle = 16.0;
            context.DrawGeometry(
                null,
                new Pen(Ink.A(accent, 0.18 + i * 0.20), 1.6 + i * 0.45, null, PenLineCap.Round),
                Ink.Arc(RingCenter, RingRadius, from, sweepAngle));
        }

        // Stations.
        for (var i = 0; i < 3; i++)
        {
            var point = Station(i);
            var passed = i < local * 3;
            var active = Math.Abs(local * 3 - i) < 0.55;
            var tone = active ? accent : passed ? success : Ink.B(ArcTokens.TextMuted);
            var fill = active ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.SurfaceRaised);
            if (active)
            {
                context.DrawEllipse(null, new Pen(Ink.A(accent, 0.26), 1.2), point, 27, 27);
            }

            context.DrawEllipse(fill, new Pen(tone, active ? 1.6 : 1.2), point, 20, 20);
            Ink.Icon(context, StationIcons[i], point.X, point.Y, 16, tone);

            var labelAt = Ink.OnCircle(RingCenter, RingRadius + 44, StationAngles[i]);
            Ink.Label(context, StationLabels[i], labelAt.X, labelAt.Y, 11.5, active ? Ink.B(ArcTokens.Foreground) : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
            Ink.Label(context, i == 0 ? "every pass" : i == 1 ? "one to three calls" : "pass or retry", labelAt.X, labelAt.Y + 15, 10, muted, Align.Center);
        }

        // Head on top of the ring.
        var head = Ink.OnCircle(RingCenter, RingRadius, headAngle);
        context.DrawEllipse(Ink.A(accent, 0.18), null, head, 11, 11);
        context.DrawEllipse(accent, new Pen(Ink.B(ArcTokens.Surface), 2), head, 5.5, 5.5);

        // Model card and loop counter.
        var card = new Rect(RingCenter.X - 55, RingCenter.Y - 38, 110, 76);
        Ink.Card(context, card, 16, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Icon(context, "sparkles", card.Center.X, card.Y + 20, 15, accent);
        Ink.Label(context, "Opus 5.5", card.Center.X, card.Y + 39, 11.5, Ink.B(ArcTokens.Foreground), FontWeight.Medium, Align.Center);
        Ink.Label(context, $"loop {loop + 1} of 3", card.Center.X, card.Y + 57, 10, muted, Align.Center);

        // Context stack: a reserved slot per pass, filled as the head gathers.
        var stack = new Rect(560, 48, width - 584, 164);
        Ink.Card(context, stack, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Context", stack.X + 16, stack.Y + 20, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        var gathered = Math.Min(3, loop + 1);
        var tokens = 0.0;
        for (var i = 0; i < 3; i++)
        {
            var row = new Rect(stack.X + 12, stack.Y + 34 + i * 38, stack.Width - 24, 33);
            var live = i < gathered;
            context.DrawRectangle(
                live ? Ink.B(ArcTokens.SurfaceMuted) : Ink.A(Ink.B(ArcTokens.SurfaceMuted), 0.4),
                live ? null : new Pen(Ink.A(Ink.B(ArcTokens.Border), 0.8), 1, new DashStyle(new double[] { 3, 3 }, 0)),
                row,
                10,
                10);
            if (!live)
            {
                Ink.Label(context, "Waiting for the next pass", row.X + 10, row.Y + 17, 10, Ink.A(muted, 0.7));
                continue;
            }

            tokens += i == 0 ? 3.2 : i == 1 ? 1.9 : 2.4;
            Ink.Label(context, Blocks[i].Label, row.X + 10, row.Y + 12, 11.5, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
            Ink.Label(context, Blocks[i].Detail, row.X + 10, row.Y + 25, 10, muted, maxWidth: row.Width - 80);
            Ink.Label(context, Blocks[i].Tokens, row.Right - 10, row.Y + 17, 10.5, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Right);
        }

        Ink.Label(context, $"{tokens:0.0}K tokens", stack.Right - 16, stack.Bottom - 11, 10, muted, Align.Right);

        // Tool call lane: drops out of the ring on a short pin under Take action.
        var actAnchor = Station(1);
        var chip = new Rect(560, 226, width - 584, 60);
        var call = Calls[Math.Min(Calls.Length - 1, loop)];
        if (local > 0.38 || loop > 0)
        {
            context.DrawLine(
                Ink.Dashed(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.55), 1.2, 3, 3),
                new Point(actAnchor.X + 14, actAnchor.Y + 14),
                new Point(chip.X, chip.Center.Y - 8));

            Ink.Card(context, chip, 12, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
            Ink.Icon(context, call.Icon, chip.X + 18, chip.Y + 20, 14, Ink.B(ArcTokens.TextSecondary));
            Ink.Label(context, $"{call.Tool} · {call.Action}", chip.X + 32, chip.Y + 18, 11, Ink.B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: chip.Width - 44);

            var resolved = local > 0.78 || loop > 0;
            if (resolved)
            {
                Ink.Icon(context, call.Failed ? "circle-x" : "check", chip.X + 18, chip.Y + 40, 12, call.Failed ? Ink.B(ArcTokens.Danger) : success);
                Ink.Label(context, call.Result, chip.X + 32, chip.Y + 40, 10.5, Ink.B(ArcTokens.TextSecondary), maxWidth: chip.Width - 44);
            }
            else
            {
                Ink.Icon(context, "loader-circle", chip.X + 18, chip.Y + 40, 12, Ink.A(accent, 0.85));
                Ink.Label(context, "Running", chip.X + 32, chip.Y + 40, 10.5, muted);
            }
        }

        // Verify verdict beside the third station.
        if (local > 0.6 || loop > 0)
        {
            var passed = loop > 0;
            var text = passed ? "Check passed" : "Retry: fix not applied";
            var ink = passed ? success : warning;
            Ink.Chip(
                context,
                62,
                272,
                text,
                Ink.Wash(ink, 0.14),
                ink,
                21,
                10.5,
                dot: true);
        }

        // Caption and status.
        var caption = done || loop == 2
            ? "The build cache was stale. Cleared it, the deploy passed and #releases knows."
            : "Find out why EU cards are declined at checkout, then update the team.";
        Ink.Paragraph(context, caption, 24, 342, 11.5, Ink.B(ArcTokens.TextSecondary), 760);

        var status = done ? "Done" : local < 0.34 ? "Gathering" : local < 0.68 ? "Acting" : "Verifying";
        Ink.Label(context, status, width - 24, 26, 10.5, muted, FontWeight.Medium, Align.Right);
        _ = height;
    }
}
