using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIExplainers.Controls;

/// <summary>
/// Vector search illustration: documents are embedded onto a tilted plane where similar topics land close
/// together, the index links each point to its nearest neighbours, and a typed query is embedded, lowered
/// onto the plane and resolved with a widening ring. The ring lights the three nearest documents with a
/// line, a halo and a similarity score, and the ranked list fills beside the plane.
/// </summary>
/// <remarks>
/// Recreated independently from Arc's public vector-search documentation. Approximations: the plane is a
/// skewed parallelogram rather than a full 3D projection, the search walk is shown as a widening ring with
/// a single signal dot instead of a hop-by-hop queue, and scores are derived from plane distance so the
/// numbers always agree with the picture. The four stage pills jump between settled frames.
/// </remarks>
public class ArcVectorSearch : ArcExplainer
{
    private sealed record Topic(string Name, double U, double V);

    private sealed class Document
    {
        public string Title = string.Empty;
        public string Source = string.Empty;
        public int Topic;
        public double U;
        public double V;
        public Point Point;
    }

    private static readonly Topic[] Topics =
    {
        new("Billing", -0.42, -0.34),
        new("Access", 0.46, -0.30),
        new("Deploys", -0.08, 0.52)
    };

    private static readonly string[][] Titles =
    {
        new[] { "Refund a charge", "Update a card on file", "Download invoices", "Dispute a charge", "Apply a coupon", "Tax and VAT" },
        new[] { "Rotate an API key", "Scope API tokens", "Revoke a session", "SSO for teams", "Invite a teammate", "Audit sign-ins" },
        new[] { "Roll back a deploy", "Preview environments", "Add a custom domain", "Build cache", "Environment variables", "Health checks" }
    };

    private static readonly string[] Sources = { "help.acme.dev", "docs.acme.dev", "runbook.acme.dev" };

    private const string QueryText = "How do I rotate a leaked API key?";
    private const int K = 3;

    private readonly List<Document> _documents = new();
    private readonly Rect[] _steps = new Rect[4];
    private readonly List<Document> _matches = new();
    private readonly (double U, double V) _query = (0.36, -0.06);
    private int _hover = -1;
    private int _manualStep = -1;

    public ArcVectorSearch()
    {
        for (var topic = 0; topic < 3; topic++)
        {
            for (var index = 0; index < 6; index++)
            {
                var angle = index * 2.399 + topic * 1.1;
                var radius = 0.09 + 0.032 * index;
                _documents.Add(new Document
                {
                    Title = Titles[topic][index],
                    Source = Sources[index % Sources.Length],
                    Topic = topic,
                    U = Topics[topic].U + Math.Cos(angle) * radius,
                    V = Topics[topic].V + Math.Sin(angle) * radius * 0.9
                });
            }
        }

        Describe(
            "Vector search illustration: eighteen help documents cluster into billing, access and deploys on a tilted embedding plane; a query about rotating a leaked API key lands on the plane and its three nearest documents are ranked with similarity scores",
            "Four stage pills move between embed, index, query and rank. Use the arrow keys to move between stages; Space pauses the illustration.");
    }

    protected override double DefaultArtHeight => 400;

    protected override double CycleSeconds => 18;

    protected override double PosterPhase => 0.86;

    private int Stage => _manualStep >= 0
        ? _manualStep
        : Phase switch
        {
            < 0.2 => 0,
            < 0.4 => 1,
            < 0.72 => 2,
            _ => 3
        };

    private Point Project(double u, double v) =>
        new(354 + u * 236 + v * 40, 206 + u * 18 + v * 104);

    protected override bool OnHover(Point? point)
    {
        var next = -1;
        if (point is { } p)
        {
            for (var i = 0; i < _documents.Count; i++)
            {
                var doc = _documents[i];
                if (Math.Abs(doc.Point.X - p.X) < 8 && Math.Abs(doc.Point.Y - p.Y) < 8)
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
        for (var i = 0; i < _steps.Length; i++)
        {
            if (_steps[i].Contains(point))
            {
                _manualStep = i;
                SetPaused(true);
                return true;
            }
        }

        return false;
    }

    protected override bool OnKey(Key key)
    {
        if (key is not (Key.Left or Key.Right or Key.Home or Key.End))
        {
            return false;
        }

        var stage = Stage;
        stage = key switch
        {
            Key.Left => (stage + 3) % 4,
            Key.Right => (stage + 1) % 4,
            Key.Home => 0,
            _ => 3
        };
        _manualStep = stage;
        SetPaused(true);
        return true;
    }

    protected override void DrawScene(DrawingContext context, double width, double height, double phase)
    {
        var stage = Stage;
        var accent = Ink.B(ArcTokens.Accent);
        var muted = Ink.B(ArcTokens.TextMuted);

        // Plane and its grid.
        var corners = new[] { Project(-1, -1), Project(1, -1), Project(1, 1), Project(-1, 1) };
        context.DrawGeometry(Ink.B(ArcTokens.SurfaceRaised), new Pen(Ink.B(ArcTokens.Border), 1), Ink.Poly(corners));
        for (var i = -1.0; i <= 1.001; i += 0.5)
        {
            context.DrawLine(Ink.Stroke(Ink.A(Ink.B(ArcTokens.BorderSubtle), 0.9), 1), Project(i, -1), Project(i, 1));
            context.DrawLine(Ink.Stroke(Ink.A(Ink.B(ArcTokens.BorderSubtle), 0.9), 1), Project(-1, i), Project(1, i));
        }

        // Counters.
        Ink.Label(context, "18 documents", 28, 26, 11, muted, FontWeight.Medium);
        Ink.Label(context, "29 links", 28, 44, 11, muted);
        Ink.Label(context, "4 hops", 28, 62, 11, muted);
        Ink.Label(context, "Tilted embedding plane", width - 24, 26, 11, muted, Align.Right);

        // Cluster regions and names.
        for (var topic = 0; topic < 3; topic++)
        {
            var centre = Topics[topic];
            var ring = new List<Point>();
            for (var a = 0; a < 40; a++)
            {
                var angle = a / 40.0 * Math.PI * 2;
                ring.Add(Project(centre.U + Math.Cos(angle) * 0.33, centre.V + Math.Sin(angle) * 0.30));
            }

            context.DrawGeometry(null, Ink.Dashed(Ink.A(Series(topic), 0.55), 1, 4, 4), Ink.Poly(ring.ToArray()));

            var labelAt = Project(centre.U, centre.V - 0.42);
            Ink.Chip(
                context,
                labelAt.X - Ink.ChipWidth(Topics[topic].Name, 10.5) / 2,
                labelAt.Y - 9,
                Topics[topic].Name,
                Ink.Wash(Series(topic), 0.14),
                Series(topic),
                18,
                10.5);
        }

        // Points, with an index link to each point's nearest neighbour inside its cluster.
        for (var i = 0; i < _documents.Count; i++)
        {
            _documents[i].Point = Project(_documents[i].U, _documents[i].V);
        }

        for (var i = 0; i < _documents.Count; i++)
        {
            var doc = _documents[i];
            var partner = -1;
            var best = 999.0;
            for (var j = 0; j < _documents.Count; j++)
            {
                if (j == i || _documents[j].Topic != doc.Topic)
                {
                    continue;
                }

                var distance = Distance(doc.Point, _documents[j].Point);
                if (distance < best)
                {
                    best = distance;
                    partner = j;
                }
            }

            if (partner > i)
            {
                context.DrawLine(Ink.Stroke(Ink.A(Series(doc.Topic), 0.35), 1), doc.Point, _documents[partner].Point);
            }
        }

        foreach (var doc in _documents)
        {
            var radius = _hover >= 0 && ReferenceEquals(_documents[_hover], doc) ? 5.5 : 4;
            context.DrawEllipse(Series(doc.Topic), null, doc.Point, radius, radius);
        }

        // Query: a small marker above its point on the plane, with a stem down to the point.
        var queryPoint = Project(_query.U, _query.V);
        if (stage >= 2)
        {
            context.DrawLine(Ink.Dashed(Ink.A(accent, 0.6), 1.2, 3, 3), new Point(queryPoint.X, queryPoint.Y - 26), queryPoint);
            var diamond = new[]
            {
                new Point(queryPoint.X, queryPoint.Y - 7),
                new Point(queryPoint.X + 7, queryPoint.Y),
                new Point(queryPoint.X, queryPoint.Y + 7),
                new Point(queryPoint.X - 7, queryPoint.Y)
            };
            context.DrawGeometry(accent, new Pen(Ink.B(ArcTokens.Surface), 1.5), Ink.Poly(diamond));
            var labelAt = new Point(600, 172);
            context.DrawLine(Ink.Dashed(Ink.A(accent, 0.5), 1, 3, 3), new Point(labelAt.X, labelAt.Y + 10), queryPoint);
            Ink.Chip(
                context,
                labelAt.X,
                labelAt.Y,
                "Query",
                Ink.Wash(accent, 0.16),
                accent,
                18,
                10,
                Ink.A(accent, 0.5));
        }

        // Matches are the three nearest documents; the ring finds them as it widens.
        _matches.Clear();
        _matches.AddRange(_documents.OrderBy(doc => Distance(doc.Point, queryPoint)).Take(K));
        var ringT = Ink.Span(phase, 0.72, 0.94);
        if (stage >= 3)
        {
            var far = _matches.Max(doc => Distance(doc.Point, queryPoint));
            var radius = Math.Max(10, far * (0.55 + 0.45 * Ink.EaseOut(ringT)));
            context.DrawEllipse(null, new Pen(Ink.A(accent, 0.5), 1.4), queryPoint, radius, radius * 0.82);
        }

        for (var index = 0; index < _matches.Count; index++)
        {
            var doc = _matches[index];
            var reached = stage >= 3 && Distance(doc.Point, queryPoint) < 200 * ringT + 40;
            if (!reached)
            {
                continue;
            }

            var score = ScoreFor(doc, queryPoint);
            context.DrawEllipse(Ink.A(accent, 0.16), null, doc.Point, 9, 9);
            context.DrawLine(Ink.Stroke(Ink.A(accent, 0.75), 1.2), queryPoint, doc.Point);
            var dirX = doc.Point.X - queryPoint.X;
            var dirY = doc.Point.Y - queryPoint.Y;
            var length = Math.Max(1, Math.Sqrt(dirX * dirX + dirY * dirY));
            var chipCenter = new Point(doc.Point.X + dirX / length * 30, doc.Point.Y + dirY / length * 30);
            var scoreText = score.ToString("0.00");
            Ink.Chip(
                context,
                chipCenter.X - Ink.ChipWidth(scoreText, 10) / 2,
                chipCenter.Y - 9,
                scoreText,
                Ink.B(ArcTokens.SurfaceRaised),
                accent,
                18,
                10,
                Ink.A(accent, 0.4),
                FontWeight.Medium);
        }

        if (_hover >= 0)
        {
            var doc = _documents[_hover];
            var text = $"{doc.Title} · {Topics[doc.Topic].Name} · {doc.Source}";
            Ink.Chip(
                context,
                Math.Clamp(doc.Point.X - Ink.ChipWidth(text, 10.5) / 2, 24, width - 24 - Ink.ChipWidth(text, 10.5)),
                doc.Point.Y - 34,
                text,
                Ink.B(ArcTokens.SurfaceRaised),
                Ink.B(ArcTokens.TextSecondary),
                21,
                10.5,
                Ink.B(ArcTokens.Border));
        }

        // Readout: the embedded query, then the ranked nearest documents.
        var panelX = width - 388;
        var queryPanel = new Rect(panelX, 78, 364, 94);
        Ink.Card(context, queryPanel, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Query · 1536 dimensions", queryPanel.X + 16, queryPanel.Y + 20, 10.5, muted, FontWeight.Medium);
        Ink.Paragraph(context, QueryText, queryPanel.X + 16, queryPanel.Y + 32, 12.5, Ink.B(ArcTokens.Foreground), 330, FontWeight.Medium);

        var panel = new Rect(panelX, 180, 364, 164);
        Ink.Card(context, panel, 20, Ink.B(ArcTokens.SurfaceRaised), Ink.B(ArcTokens.Border));
        Ink.Label(context, "Nearest documents", panel.X + 16, panel.Y + 18, 11, Ink.B(ArcTokens.TextSecondary), FontWeight.Medium);
        Ink.Label(context, $"{_matches.Count} of 18", panel.Right - 16, panel.Y + 18, 10.5, muted, Align.Right);

        for (var rank = 0; rank < K; rank++)
        {
            var top = panel.Y + 32 + rank * 42;
            var doc = _matches.Count > rank ? _matches[rank] : null;
            context.DrawRectangle(Ink.B(ArcTokens.SurfaceMuted), null, new Rect(panel.X + 10, top, panel.Width - 20, 38), 10, 10);
            if (doc is null)
            {
                continue;
            }

            var score = ScoreFor(doc, queryPoint);
            Ink.Label(context, doc.Title, panel.X + 22, top + 10, 12, Ink.B(ArcTokens.Foreground), FontWeight.Medium, maxWidth: panel.Width - 140);
            Ink.Label(context, doc.Source, panel.X + 22, top + 25, 10, muted, maxWidth: panel.Width - 140);

            var barRect = new Rect(panel.X + 150, top + 22, panel.Width - 210, 4);
            context.DrawRectangle(Ink.A(Ink.B(ArcTokens.BorderStrong), 0.35), null, barRect, 2, 2);
            context.DrawRectangle(Series(doc.Topic), null, new Rect(barRect.X, barRect.Y, Math.Max(4, barRect.Width * score), 4), 2, 2);
            Ink.Label(context, $"{score:0.00}", panel.Right - 22, top + 18, 11.5, Series(doc.Topic), FontWeight.Medium, Align.Right);
        }

        // Stage pills.
        var steps = new[] { "Embed", "Index", "Query", "Rank" };
        var px = 24.0;
        for (var i = 0; i < 4; i++)
        {
            var text = steps[i];
            var w = Ink.ChipWidth(text, 11) + 4;
            var rect = new Rect(px, 356, w, 24);
            _steps[i] = rect;
            var active = i == stage;
            context.DrawRectangle(
                active ? Ink.Wash(accent, 0.16) : Ink.B(ArcTokens.Surface),
                new Pen(active ? Ink.A(accent, 0.55) : Ink.B(ArcTokens.Border), 1),
                rect,
                8,
                8);
            Ink.Label(context, text, rect.Center.X, rect.Center.Y, 11, active ? accent : Ink.B(ArcTokens.TextSecondary), FontWeight.Medium, Align.Center);
            px += w + 8;
        }

        Ink.Label(context, "Nearest neighbours, ranked by similarity", width - 24, 368, 10.5, muted, Align.Right);
    }

    private static double ScoreFor(Document doc, Point query) =>
        Math.Clamp(0.97 - Distance(doc.Point, query) / 300.0, 0.28, 0.97);

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
}
