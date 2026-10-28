using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIExplainers.Controls;

/// <summary>
/// Reasoning tree illustration: a question proposes candidate thoughts, each gets a score bar, the weakest
/// fade while their edges retract, the best few expand another depth, and the winning path draws from the
/// question to the answer. A beam width control from one to three changes how many thoughts survive each
/// depth, and a wider beam can find a better answer.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public reasoning-tree documentation. Approximations: the tree is a
/// fixed sample with hand picked scores rather than arbitrary thought data, and pruning is drawn as a
/// settled fade rather than retracting every edge. The beam width is a real control: one, two and three
/// survive different thoughts and can change the answer.
/// </remarks>
public class ArcReasoningTree : ArcExplainer
{
    private sealed class Thought
    {
        public string Label = string.Empty;
        public string Text = string.Empty;
        public double Score;
        public int Depth;
        public Thought? Parent;
        public readonly List<Thought> Children = new();
        public Rect Rect;
    }

    private readonly List<Thought> _roots = new();
    private readonly List<Thought> _all = new();
    private readonly List<Thought> _winners = new();
    private readonly List<Thought> _path = new();
    private readonly Rect[] _beamPills = new Rect[3];
    private int _beam = 2;
    private int _hover = -1;

    public ArcReasoningTree()
    {
        var cut = Add("Cut scope", "Ship the core flow and move the export to next week.", 0.72, 0, null, 64);
        var parallel = Add("Parallelize QA", "Run unit and end to end checks on a second CI lane.", 0.81, 0, null, 158);
        var delay = Add("Delay to Monday", "Keep the scope and take three more days.", 0.34, 0, null, 252);

        var drop = Add("Drop the export", "The export is the only large item left.", 0.66, 1, cut, 40);
        var keep = Add("Keep the export", "Ship late but complete.", 0.55, 1, cut, 100);
        var lane = Add("Second CI lane", "Double the runners for a day.", 0.84, 1, parallel, 150);
        var borrow = Add("Borrow a tester", "Move one QA from the platform team.", 0.71, 1, parallel, 208);
        var week = Add("Add a week", "Push the release by five days.", 0.45, 1, delay, 268);

        var core = Add("Ship core, export next", "Cut only the export and keep every fix.", 0.95, 2, drop, 44);
        var late = Add("Ship late, complete", "Everything lands Tuesday.", 0.4, 2, keep, 104);
        var tagged = Add("Ship core, tagged", "Tag the release and note the missing export.", 0.92, 2, lane, 156);
        var friday = Add("Ship Friday", "One tester is enough for the risk.", 0.66, 2, borrow, 212);
        _ = week;
        _ = core;
        _ = late;
        _ = tagged;
        _ = friday;

        Describe(
            "Reasoning tree illustration: a question proposes three candidate plans, the weakest are pruned, the best expand another depth, and the best path from the question to the answer is drawn",
            "The beam width pills or the 1, 2 and 3 keys change how many thoughts survive each depth. Press Space to pause the path animation.");
    }

    private Thought Add(string label, string text, double score, int depth, Thought? parent, double y)
    {
        var node = new Thought
        {
            Label = label,
            Text = text,
            Score = score,
            Depth = depth,
            Parent = parent
        };
        if (parent is null)
        {
            _roots.Add(node);
        }
        else
        {
            parent.Children.Add(node);
        }

        _all.Add(node);
        Positions[node] = y;
        return node;
    }

    private readonly Dictionary<Thought, double> Positions = new();

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 14;

    protected override double PosterPhase => 0.9;

    private int ColumnX(int depth) => depth switch
    {
        0 => 204,
        1 => 384,
        2 => 564,
        _ => 24
    };

    protected override bool OnPress(Point point, PointerPressedEventArgs e)
    {
        for (var i = 0; i < 3; i++)
        {
            if (_beamPills[i].Contains(point))
            {
                _beam = i + 1;
                return true;
            }
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        if (key is Key.D1 or Key.NumPad1)
        {
            _beam = 1;
            return true;
        }

        if (key is Key.D2 or Key.NumPad2)
        {
            _beam = 2;
            return true;
        }

        if (key is Key.D3 or Key.NumPad3)
        {
            _beam = 3;
            return true;
        }

        return false;
    }

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p)
        {
            for (var i = 0; i < _all.Count; i++)
            {
                if (_all[i].Rect.Contains(p))
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
        var muted = Ink.B(ArcTokens.TextMuted);

        // Beam search over the sample tree.
        var beam1 = _roots.OrderByDescending(t => t.Score).Take(_beam).ToList();
        var beam2 = beam1.SelectMany(t => t.Children).OrderByDescending(t => t.Score).Take(_beam).ToList();
        var beam3 = beam2.SelectMany(t => t.Children).OrderByDescending(t => t.Score).Take(_beam).ToList();
        var answer = beam3.OrderByDescending(t => t.Score).FirstOrDefault();

        _winners.Clear();
        _winners.AddRange(beam1);
        _winners.AddRange(beam2);
        _winners.AddRange(beam3);
        _path.Clear();
        var cursor = answer;
        while (cursor is not null)
        {
            _path.Insert(0, cursor);
            cursor = cursor.Parent;
        }

        // Column captions.
        Ink.Label(context, "Question", 24, 28, 10.5, muted, FontWeight.Medium);
        for (var depth = 0; depth < 3; depth++)
        {
            var kept = depth == 0 ? beam1.Count : depth == 1 ? beam2.Count : beam3.Count;
            Ink.Label(context, $"Depth {depth + 1} · {kept} kept", ColumnX(depth), 28, 10.5, muted, FontWeight.Medium);
        }

        Ink.Label(context, "Answer", width - 24, 28, 10.5, muted, FontWeight.Medium, Align.Right);

        // Positions and edges.
        foreach (var node in _all)
        {
            var w = 150.0;
            var h = 46.0;
            node.Rect = new Rect(ColumnX(node.Depth), Positions[node], w, h);
        }

        var questionRect = new Rect(24, 150, 150, 96);
        foreach (var node in _all)
        {
            var parentRect = node.Parent is null ? questionRect : node.Parent.Rect;
            var start = new Point(parentRect.Right, parentRect.Center.Y);
            var end = new Point(node.Rect.X, node.Rect.Center.Y);
            var onPath = _path.Contains(node);
            var kept = _winners.Contains(node);
            var brush = onPath
                ? accent
                : kept
                    ? Ink.A(Ink.B(ArcTokens.BorderStrong), 0.8)
                    : Ink.A(Ink.B(ArcTokens.BorderStrong), 0.28);
            context.DrawGeometry(null, Ink.Stroke(brush, onPath ? 2 : 1.2), Ink.Cubic(start, new Point(start.X + 40, start.Y), new Point(end.X - 40, end.Y), end));
        }

        // Question node.
        Ink.Card(context, questionRect, 16, Ink.B(ArcTokens.SurfaceRaised), Ink.A(accent, 0.5));
        Ink.Icon(context, "circle-help", questionRect.X + 18, questionRect.Y + 22, 14, accent);
        Ink.Label(context, "Question", questionRect.X + 34, questionRect.Y + 22, 10.5, muted, FontWeight.Medium);
        Ink.Paragraph(context, "Which release plan ships by Friday?", questionRect.X + 16, questionRect.Y + 36, 11.5, Ink.B(ArcTokens.Foreground), questionRect.Width - 30, FontWeight.Medium);

        // Thoughts.
        foreach (var node in _all)
        {
            var kept = _winners.Contains(node);
            var onPath = _path.Contains(node);
            var hovered = _hover >= 0 && ReferenceEquals(_all[_hover], node);
            var alpha = kept ? 1.0 : 0.42;
            var border = onPath ? Ink.A(accent, 0.9) : hovered ? Ink.B(ArcTokens.BorderStrong) : Ink.A(Ink.B(ArcTokens.Border), kept ? 1 : 0.5);
            context.DrawRectangle(
                kept ? Ink.B(ArcTokens.SurfaceRaised) : Ink.A(Ink.B(ArcTokens.SurfaceRaised), 0.55),
                new Pen(border, onPath ? 1.5 : 1),
                node.Rect,
                12,
                12);

            Ink.Label(context, node.Label, node.Rect.X + 10, node.Rect.Y + 13, 11, Ink.A(kept ? Ink.B(ArcTokens.Foreground) : Ink.B(ArcTokens.TextMuted), alpha), FontWeight.Medium, maxWidth: node.Rect.Width - 50);
            Ink.Label(context, node.Score.ToString("0.00"), node.Rect.Right - 10, node.Rect.Y + 13, 10, Ink.A(onPath ? accent : Ink.B(ArcTokens.TextSecondary), alpha), FontWeight.Medium, Align.Right);

            var bar = new Rect(node.Rect.X + 10, node.Rect.Y + 28, node.Rect.Width - 20, 4);
            context.DrawRectangle(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.3), null, bar, 2, 2);
            context.DrawRectangle(Ink.A(onPath ? accent : Series(2), kept ? 0.9 : 0.35), null, new Rect(bar.X, bar.Y, Math.Max(2, bar.Width * node.Score), 4), 2, 2);
        }

        // The winning path draws in with a travelling sheen.
        if (_path.Count > 1)
        {
            var points = _path.Select(node => node.Rect).ToList();
            var total = points.Count;
            var travel = Ink.Clamp01(phase);
            var segment = Math.Min(total - 1, (int)(travel * total));
            var fraction = travel * total - segment;
            var fromRect = segment == 0 ? questionRect : points[segment - 1];
            var toRect = points[segment];
            var start = new Point(fromRect.Right, fromRect.Center.Y);
            var end = new Point(toRect.X, toRect.Center.Y);
            var sheen = Ink.CubicAt(start, new Point(start.X + 40, start.Y), new Point(end.X - 40, end.Y), end, fraction);
            context.DrawEllipse(Ink.A(accent, 0.25), null, sheen, 6, 6);
            context.DrawEllipse(accent, null, sheen, 3, 3);
        }

        // Answer card.
        if (answer is not null)
        {
            var card = new Rect(width - 288, 140, 264, 106);
            Ink.Card(context, card, 18, Ink.Wash(success, 0.08), Ink.A(success, 0.5));
            Ink.Icon(context, "check", card.X + 22, card.Y + 24, 15, success);
            Ink.Label(context, "Answer", card.X + 38, card.Y + 24, 10.5, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
            Ink.Paragraph(context, answer.Label, card.X + 16, card.Y + 36, 13, Ink.B(ArcTokens.Foreground), card.Width - 30, FontWeight.Medium);
            Ink.Label(context, $"score {answer.Score:0.00} · {_path.Count} thoughts deep", card.X + 16, card.Bottom - 22, 10, muted);
            Ink.Label(context, $"beam {_beam}", card.Right - 16, card.Y + 24, 10, muted, Align.Right);

            var last = _path[^1];
            context.DrawGeometry(
                null,
                Ink.Stroke(Ink.A(accent, 0.75), 1.6),
                Ink.Cubic(
                    new Point(last.Rect.Right, last.Rect.Center.Y),
                    new Point(last.Rect.Right + 26, last.Rect.Center.Y),
                    new Point(card.X - 26, card.Center.Y),
                    new Point(card.X, card.Center.Y)));
            Ink.ArrowHead(context, new Point(card.X - 2, card.Center.Y), new Point(card.X - 10, card.Center.Y), accent, 5);
        }

        // Beam width pills.
        var pillX = 24.0;
        for (var i = 0; i < 3; i++)
        {
            var rect = new Rect(pillX + i * 40, 348, 34, 24);
            _beamPills[i] = rect;
            var active = _beam == i + 1;
            context.DrawRectangle(
                active ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.Surface),
                new Pen(active ? Ink.A(accent, 0.55) : Ink.B(ArcTokens.Border), 1),
                rect,
                7,
                7);
            Ink.Label(context, (i + 1).ToString(), rect.Center.X, rect.Center.Y, 10.5, active ? accent : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
        }

        Ink.Label(context, "Beam width", 150, 360, 10, muted, FontWeight.Medium);
        Ink.Label(context, "Scores are illustrative, not a live model's.", width - 24, 360, 10, muted, Align.Right);

        if (_hover >= 0)
        {
            var node = _all[_hover];
            var text = $"{node.Label} · {node.Text}";
            Ink.Chip(
                context,
                Math.Clamp(node.Rect.X, 24, width - 24 - Ink.ChipWidth(text, 10)),
                node.Rect.Y - 26,
                text,
                Ink.B(ArcTokens.SurfaceRaised),
                Ink.B(ArcTokens.TextSecondary),
                21,
                10,
                Ink.B(ArcTokens.Border));
        }

        _ = height;
    }
}
