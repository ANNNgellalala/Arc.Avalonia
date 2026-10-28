using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// Multi-region failover on a layered 2.5D stage: users stream requests through a global load balancer to
/// two regional platforms of six instances each. The primary degrades, fails its health checks and drops
/// out of rotation, the balancer shifts its weight to the healthy region, and when the primary returns it
/// has to pass three checks and warm up before the split is even again.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public multi-region-failover documentation. Approximations: the stage
/// is drawn as two skewed platforms rather than a full isometric scene, request traffic is a fixed pool of
/// deterministic dots, and the outage is a six stage simulation with sample numbers, not a live status
/// feed. The stage pills jump between settled stages, as the React scrubber does; arrow keys move between
/// them too.
/// </remarks>
public class ArcMultiRegionFailover : ArcExplainer
{
    private sealed record Stage(
        string Title,
        string Narration,
        double PrimaryWeight,
        int PrimaryUp,
        int PrimaryChecks,
        int SecondaryUp,
        int SecondaryChecks,
        double ErrorRate,
        double PrimaryLatency,
        double SecondaryLatency,
        string PrimaryStatus,
        string SecondaryStatus,
        int PrimaryTone);

    private static readonly Stage[] Stages =
    {
        new("Steady", "An even split across two regions, twelve thousand requests a second.",
            0.5, 6, 3, 6, 3, 0.0, 38, 42, "Healthy", "Healthy", 1),
        new("Degrading", "Two instances in US East stop responding and the error rate climbs.",
            0.5, 4, 1, 6, 3, 8.4, 41, 42, "Degraded", "Healthy", 2),
        new("Shift", "Three failed checks pull US East out of rotation; traffic moves to EU West.",
            0.12, 0, 0, 6, 3, 27.3, 0, 46, "Out of rotation", "Healthy", 3),
        new("Recovering", "US East is back, but it has to pass three health checks before any traffic.",
            0.12, 3, 1, 6, 3, 4.1, 44, 46, "Warming up", "Healthy", 2),
        new("Warming", "Checks pass and the weight ramps back gradually, so nothing stampedes.",
            0.35, 5, 3, 6, 3, 0.4, 40, 44, "Warming up 62%", "Healthy", 2),
        new("Rebalanced", "The regions share the load evenly again and every check passes.",
            0.5, 6, 3, 6, 3, 0.0, 38, 42, "Healthy", "Healthy", 1)
    };

    private const double PlatformWidth = 372;
    private const double PlatformDepth = 78;
    private const double PlatformSkew = 44;
    private const double PlatformFront = 26;
    private const double PlatformX = 516;
    private const double PrimaryY = 84;
    private const double SecondaryY = 226;

    private int _stage = 2;
    private double _stageClock;
    private double _clock;
    private double _weightPrimary = 0.12;
    private double _hoverPlatform = -1;
    private readonly Rect[] _steps = new Rect[6];

    public ArcMultiRegionFailover()
    {
        Describe(
            "Multi-region failover illustration: requests reach a global load balancer that splits them between US East and EU West; US East fails three health checks, traffic shifts to EU West, then a gradual recovery returns to an even split",
            "Six stages step through the outage and recovery. Use the number pills or the left and right arrow keys to move between stages; Space pauses the simulation.");
    }

    protected override double DefaultArtHeight => 410;

    protected override double PosterPhase => 0.42;

    private Stage Current => Stages[_stage];

    protected override bool OnTick(double seconds)
    {
        var moved = false;
        if (!Reduced && IsPlaying)
        {
            _clock += seconds;
            _stageClock += seconds;
            if (_stageClock > 2.8)
            {
                _stageClock = 0;
                _stage = (_stage + 1) % Stages.Length;
            }

            moved = true;
        }

        var target = Current.PrimaryWeight;
        if (Math.Abs(_weightPrimary - target) > 0.002)
        {
            _weightPrimary += (target - _weightPrimary) * Math.Min(1, seconds * 4.5);
            moved = true;
        }

        return moved;
    }

    protected override bool OnHover(Point? point)
    {
        var next = -1.0;
        if (point is { } p && p.X > PlatformX)
        {
            next = p.Y < 210 ? 0 : 1;
        }

        if (Math.Abs(next - _hoverPlatform) < 0.5)
        {
            return false;
        }

        _hoverPlatform = next;
        return true;
    }

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        for (var i = 0; i < _steps.Length; i++)
        {
            if (_steps[i].Contains(point))
            {
                _stage = i;
                _stageClock = 0;
                SetPaused(true);
                return true;
            }
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        switch (key)
        {
            case Key.Left:
            case Key.Down:
                _stage = (_stage + Stages.Length - 1) % Stages.Length;
                _stageClock = 0;
                SetPaused(true);
                return true;
            case Key.Right:
            case Key.Up:
                _stage = (_stage + 1) % Stages.Length;
                _stageClock = 0;
                SetPaused(true);
                return true;
            case Key.Home:
                _stage = 0;
                _stageClock = 0;
                SetPaused(true);
                return true;
            case Key.End:
                _stage = Stages.Length - 1;
                _stageClock = 0;
                SetPaused(true);
                return true;
            default:
                return false;
        }
    }

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        _ = phase;
        var stage = Current;
        var accent = Ink.B(ArcTokens.Accent);
        var success = Ink.B(ArcTokens.Success);
        var danger = Ink.B(ArcTokens.Danger);
        var warning = Ink.B(ArcTokens.Warning);
        var primaryBrush = Series(0);
        var secondaryBrush = Series(2);

        // Header: stage title and narration.
        Ink.Label(context, stage.Title, 24, 32, 16, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Paragraph(context, stage.Narration, 24, 46, 11.5, Ink.B(ArcTokens.TextSecondary), 460);

        // Users and balancer.
        var users = new Rect(24, 110, 148, 62);
        Ink.Node(context, users, "users", "Users", "12,000 req/s", primaryBrush);

        var balancer = new Rect(212, 100, 200, 88);
        Ink.Card(context, balancer, 18, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        var tile = new Rect(balancer.X + 12, balancer.Y + 13, 30, 30);
        context.DrawRectangle(Ink.Wash(accent, 0.14, ArcTokens.SurfaceRaised), null, tile, 9, 9);
        Ink.Icon(context, "globe", tile.Center.X, tile.Center.Y, 16, accent);
        Ink.Label(context, "Load balancer", balancer.X + 52, balancer.Y + 23, 13, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Label(context, "api.acme.dev", balancer.X + 52, balancer.Y + 41, 11, Ink.B(ArcTokens.TextMuted));

        var splitRect = new Rect(balancer.X + 14, balancer.Y + 55, balancer.Width - 28, 9);
        var primaryShare = Ink.Clamp01(_weightPrimary);
        context.DrawRectangle(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.45), null, splitRect, 4.5, 4.5);
        if (primaryShare > 0.001)
        {
            context.DrawRectangle(primaryBrush, null, new Rect(splitRect.X, splitRect.Y, Math.Max(3, splitRect.Width * primaryShare), splitRect.Height), 4.5, 4.5);
        }

        if (1 - primaryShare > 0.001)
        {
            var secondaryWidth = splitRect.Width * (1 - primaryShare);
            context.DrawRectangle(secondaryBrush, null, new Rect(splitRect.Right - secondaryWidth, splitRect.Y, secondaryWidth, splitRect.Height), 4.5, 4.5);
        }

        Ink.Label(context, $"US East {Ink.Percent(primaryShare)}", splitRect.X, splitRect.Bottom + 12, 10, Ink.B(ArcTokens.TextMuted));
        Ink.Label(context, $"EU West {Ink.Percent(1 - primaryShare)}", splitRect.Right, splitRect.Bottom + 12, 10, Ink.B(ArcTokens.TextMuted), Align.Right);

        // Failover policy panel fills the lower left and names the thresholds behind the story.
        var policy = new Rect(24, 206, 292, 112);
        Ink.Card(context, policy, 18, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Failover policy", policy.X + 16, policy.Y + 20, 11, Ink.B(ArcTokens.TextMuted), FontWeight.Medium);
        DrawPolicyRow(context, policy, 44, "Health check", "every 10 s");
        DrawPolicyRow(context, policy, 66, "Fail after", "3 failed checks");
        DrawPolicyRow(context, policy, 88, "Recover after", "3 passed checks");

        // Regional platforms.
        DrawPlatform(context, PrimaryY, stage.PrimaryUp, stage.PrimaryChecks, stage.PrimaryStatus, stage.PrimaryTone,
            "US East", "us-east-1", primaryBrush, danger, warning, success, 0);
        DrawPlatform(context, SecondaryY, stage.SecondaryUp, stage.SecondaryChecks, stage.SecondaryStatus, 1,
            "EU West", "eu-west-1", secondaryBrush, danger, warning, success, 1);

        DrawTraffic(
            context,
            new Point(users.Right, users.Center.Y),
            new Point(balancer.X, balancer.Center.Y),
            accent,
            danger,
            primaryBrush,
            secondaryBrush);

        // Stats.
        var errorBrush = stage.ErrorRate > 1 ? danger : Ink.B(ArcTokens.TextSecondary);
        DrawStat(context, 24, 346, "Requests", "12,000/s", Ink.B(ArcTokens.TextSecondary));
        DrawStat(context, 152, 346, "Latency", stage.PrimaryUp > 0 ? $"{stage.PrimaryLatency:0} · {stage.SecondaryLatency:0} ms" : $"{stage.SecondaryLatency:0} ms", Ink.B(ArcTokens.TextSecondary));
        DrawStat(context, 300, 346, "Errors", stage.ErrorRate <= 0.05 ? "0%" : $"{stage.ErrorRate:0.#}%", errorBrush);

        // Stage pills.
        Ink.Label(context, "Stage", 496, 358, 10, Ink.B(ArcTokens.TextMuted), FontWeight.Medium);
        var pillX = 534.0;
        for (var i = 0; i < Stages.Length; i++)
        {
            var rect = new Rect(pillX + i * 38, 346, 32, 24);
            _steps[i] = rect;
            var active = i == _stage;
            context.DrawRectangle(
                active ? accent : Ink.B(ArcTokens.SurfaceRaised),
                new Pen(active ? accent : Ink.B(ArcTokens.Border), 1),
                rect,
                8,
                8);
            Ink.Label(context, (i + 1).ToString(), rect.Center.X, rect.Center.Y, 11, active ? Ink.B(ArcTokens.AccentForeground) : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        }

        Ink.Label(
            context,
            "The outage, traffic and numbers are a simulation.",
            width - 24,
            388,
            10.5,
            Ink.B(ArcTokens.TextMuted),
            Align.Right);

        if (_hoverPlatform >= 0)
        {
            var top = _hoverPlatform < 0.5 ? PrimaryY : SecondaryY;
            Ink.Chip(
                context,
                PlatformX + 12,
                top + 22,
                "6 instances over 3 zones, health checked every 10 s",
                Ink.B(ArcTokens.SurfaceRaised),
                Ink.B(ArcTokens.TextSecondary),
                22,
                10.5,
                Ink.B(ArcTokens.Border));
        }
    }

    private static void DrawPolicyRow(DrawingContext context, Rect panel, double offsetY, string label, string value)
    {
        Ink.Label(context, label, panel.X + 16, panel.Y + offsetY, 11.5, Ink.B(ArcTokens.TextSecondary));
        Ink.Label(context, value, panel.Right - 16, panel.Y + offsetY, 11.5, Ink.B(ArcTokens.Foreground), FontWeight.Medium, Align.Right);
    }

    private void DrawPlatform(
        DrawingContext context,
        double y,
        int up,
        int checks,
        string status,
        int tone,
        string name,
        string code,
        IBrush tint,
        IBrush danger,
        IBrush warning,
        IBrush success,
        int index)
    {
        var x = PlatformX;
        var top = new[]
        {
            new Point(x, y),
            new Point(x + PlatformWidth, y),
            new Point(x + PlatformWidth + PlatformSkew, y + PlatformDepth),
            new Point(x + PlatformSkew, y + PlatformDepth)
        };
        var front = new[]
        {
            top[3],
            top[2],
            new Point(x + PlatformWidth + PlatformSkew, y + PlatformDepth + PlatformFront),
            new Point(x + PlatformSkew, y + PlatformDepth + PlatformFront)
        };

        var hot = Math.Abs(_hoverPlatform - index) < 0.5;
        context.DrawGeometry(Ink.B(ArcTokens.SurfaceMuted), null, Ink.Poly(front));
        context.DrawGeometry(Ink.B(ArcTokens.SurfaceRaised), new Pen(hot ? tint : Ink.B(ArcTokens.Border), hot ? 1.4 : 1), Ink.Poly(top));

        Ink.Label(context, name, x + PlatformSkew + 4, y + PlatformDepth + 13, 12, Ink.B(ArcTokens.Foreground), FontWeight.Medium);
        Ink.Label(context, code, x + PlatformSkew + 12 + Ink.Width(name, 12, FontWeight.Medium), y + PlatformDepth + 13, 11, Ink.B(ArcTokens.TextMuted));

        var statusInk = tone switch
        {
            3 => danger,
            2 => warning,
            _ => success
        };
        var chipFill = tone switch
        {
            3 => Ink.Wash(danger, 0.13, ArcTokens.SurfaceMuted),
            2 => Ink.Wash(warning, 0.13, ArcTokens.SurfaceMuted),
            _ => Ink.Wash(success, 0.13, ArcTokens.SurfaceMuted)
        };
        var chipWidth = Ink.ChipWidth(status, 10.5, dot: true);
        Ink.Chip(context, x + PlatformWidth + PlatformSkew - chipWidth - 10, y + PlatformDepth + 2, status, chipFill, statusInk, 20, 10.5, dot: true);

        // Six instances on the platform.
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var indexInPlatform = row * 3 + col;
                var u = 0.26 + col * 0.24 + (row == 1 ? (indexInPlatform - 3) * 0.018 : 0);
                var v = row == 0 ? 0.28 : 0.62;
                var centre = new Point(x + u * PlatformWidth + v * PlatformSkew, y + v * PlatformDepth);
                var alive = indexInPlatform < up;
                var warming = tone == 2 && alive;
                var fill = alive
                    ? Ink.Wash(tint, 0.85, ArcTokens.SurfaceRaised)
                    : Ink.Wash(Ink.B(ArcTokens.BorderStrong), 0.25, ArcTokens.SurfaceRaised);
                var ring = warming ? warning : alive ? tint : danger;
                context.DrawEllipse(fill, new Pen(ring, alive ? 1.2 : 1.5), centre, 14, 8);
                if (!alive)
                {
                    Ink.Icon(context, "x", centre.X, centre.Y, 9, danger, 1.8);
                }
                else if (warming)
                {
                    Ink.Icon(context, "refresh-cw", centre.X, centre.Y, 9, warning, 1.8);
                }
            }
        }

        // Health check pips above the platform edge.
        var failed = 3 - checks;
        for (var i = 0; i < 3; i++)
        {
            var passed = i < checks;
            context.DrawEllipse(
                passed ? success : Ink.B(ArcTokens.Surface),
                new Pen(passed ? success : danger, 1.2),
                new Point(x + 10 + i * 12, y - 12),
                3.6,
                3.6);
        }

        Ink.Label(
            context,
            failed > 0 ? $"Failed {failed}/3" : "Checks 3/3",
            x + 50,
            y - 12,
            10.5,
            failed > 0 ? danger : Ink.B(ArcTokens.TextMuted));
    }

    private void DrawTraffic(
        DrawingContext context,
        Point usersExit,
        Point balancerEntry,
        IBrush accent,
        IBrush danger,
        IBrush primaryBrush,
        IBrush secondaryBrush)
    {
        const int packets = 14;
        const double life = 3.0;
        var balancerExit = new Point(balancerEntry.X + 200, balancerEntry.Y);
        var primaryEntry = new Point(PlatformX + 26, PrimaryY + 26);
        var secondaryEntry = new Point(PlatformX + 26, SecondaryY + 26);

        // Faint wires so the two legs read even between packets.
        var wire = Ink.A(accent, 0.22);
        context.DrawGeometry(null, Ink.Dashed(wire, 1, 4, 5), Ink.Flow(usersExit, balancerEntry, 0, 0.5));
        context.DrawGeometry(null, Ink.Dashed(wire, 1, 4, 5), Ink.Flow(balancerExit, primaryEntry, 8, 0.5));
        context.DrawGeometry(null, Ink.Dashed(wire, 1, 4, 5), Ink.Flow(balancerExit, secondaryEntry, -8, 0.5));

        for (var i = 0; i < packets; i++)
        {
            var age = (_clock + i * life / packets) % life;
            var ratio = age / life;
            var jitter = ((i * 37) % 11) / 11.0 - 0.5;

            if (ratio < 0.30)
            {
                var t = Ink.Smooth(ratio / 0.30);
                var point = Ink.FlowAt(usersExit, balancerEntry, t, jitter * 12);
                context.DrawEllipse(Ink.A(accent, 0.9), null, point, 3, 3);
            }
            else
            {
                var primary = ((i * 0.6180339887) % 1.0) < _weightPrimary;
                var t = Ink.Smooth((ratio - 0.30) / 0.70);
                var target = primary ? primaryEntry : secondaryEntry;
                var bow = primary ? 8 : -8;
                var brush = primary ? primaryBrush : secondaryBrush;
                var failing = primary && Current.PrimaryUp < 6 && ((i * 0.4142135) % 1.0) < (6 - Current.PrimaryUp) / 6.0;
                if (failing)
                {
                    var stalled = Math.Min(t, 0.62);
                    var point = Ink.FlowAt(balancerExit, target, stalled, bow + jitter * 14);
                    var fade = t > 0.62 ? Ink.Clamp01(1 - (t - 0.62) / 0.3) : 1;
                    if (fade > 0.05)
                    {
                        context.DrawEllipse(Ink.A(danger, 0.18 * fade), null, point, 6, 6);
                        Ink.Icon(context, "x", point.X, point.Y, 9, Ink.A(danger, fade), 2);
                    }
                }
                else
                {
                    var point = Ink.FlowAt(balancerExit, target, t, bow + jitter * 14);
                    context.DrawEllipse(Ink.A(brush, 0.9), null, point, 3, 3);
                }
            }
        }
    }

    private static void DrawStat(DrawingContext context, double x, double y, string label, string value, IBrush ink)
    {
        Ink.Label(context, label, x, y, 10.5, Ink.B(ArcTokens.TextMuted), FontWeight.Medium);
        Ink.Label(context, value, x, y + 18, 13, ink, FontWeight.Medium);
    }
}
