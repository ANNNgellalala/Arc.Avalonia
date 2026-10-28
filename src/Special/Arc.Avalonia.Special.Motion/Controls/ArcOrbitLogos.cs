using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Controls;

/// <summary>
/// An integrations orbit: the product mark in the centre with sample brand marks on two flat rings that turn
/// slowly and hold still whenever the pointer or keyboard focus is inside. Category chips turn matching logos
/// toward the detail side and dim the rest; choosing one draws an accent connector and opens a detail card with
/// a description, status, Connect, Docs, and previous and next. Reduced motion stops the rotation.
/// </summary>
public class ArcOrbitLogos : ArcMotionCanvas
{
    private sealed record Logo(string Name, string Icon, string Category, string Description, bool Connected);

    private static readonly Logo[] Logos =
    {
        new("Stripe", "credit-card", "Payments", "Starts trials and bills usage", true),
        new("Vercel", "triangle-alert", "Dev", "Comments preview links on builds", false),
        new("Slack", "message-circle", "Comms", "Posts deploy alerts to your channels", true),
        new("Linear", "layers", "Dev", "Links issues to the release", false),
        new("Notion", "file", "Comms", "Writes the meeting notes to a page", false),
        new("Figma", "palette", "Design", "Embeds the latest frames", false),
        new("GitHub", "code", "Dev", "Opens pull requests from incidents", true),
        new("Sentry", "shield", "Dev", "Groups errors by release", false),
        new("Postgres", "database", "Data", "Syncs the warehouse nightly", false),
        new("OpenAI", "sparkles", "Data", "Summarises long threads", false),
        new("Zapier", "zap", "Comms", "Fires a zap on every deploy", false),
        new("Loom", "video", "Design", "Attaches a walkthrough to changes", false)
    };

    private static readonly string[] Categories = { "All", "Payments", "Dev", "Comms", "Data", "Design" };

    private const int IdChip = 10;
    private const int IdLogo = 30;
    private const int IdConnect = 60;
    private const int IdDocs = 61;
    private const int IdPrev = 62;
    private const int IdNext = 63;

    private int _category;
    private int _selected;
    private int _focus = 1;
    private double _speed;
    private double _angle;
    private double _time;
    private bool _connected;

    public ArcOrbitLogos()
    {
        Sm.Name(this, "Integrations", "Logos orbit the product mark. Arrow keys step between logos, Enter opens one, Escape closes the card.");
        _selected = 0;
        _connected = Logos[0].Connected;
    }

    protected override double DefaultWidth => 1000;

    protected override double DefaultHeight => 420;

    protected override bool NeedsFrame => base.NeedsFrame || (!Sm.Reduced && _speed > 0.02);

    private bool Matches(int index) => _category == 0 || Logos[index].Category == Categories[_category];

    private (Point Center, double Rx, double Ry) Orbit(Size size)
    {
        var area = Math.Min(size.Width - 370, 640);
        return (new Point(area / 2 + 40, size.Height / 2 + 10), area * 0.30, area * 0.115);
    }

    private Point LogoPoint(Size size, int index)
    {
        var orbit = Orbit(size);
        var inner = index < 6;
        var slot = index % 6;
        var rx = inner ? orbit.Rx : orbit.Rx * 1.42;
        var ry = inner ? orbit.Ry : orbit.Ry * 1.42;
        var baseAngle = inner ? 20 : 48;
        var angle = (baseAngle + slot * 60 + _angle * (inner ? 1 : 0.72)) * Math.PI / 180;
        return new Point(orbit.Center.X + Math.Cos(angle) * rx, orbit.Center.Y + Math.Sin(angle) * ry);
    }

    protected override void OnFrame(double dt)
    {
        _time += dt;
        _angle += dt * 5.6 * _speed;
        if (_angle > 360)
        {
            _angle -= 360;
        }
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (size.Width < 640 || size.Height < 300)
        {
            return;
        }

        DrawChips(context, size);
        var orbit = Orbit(size);

        var ring = new Pen(Sm.Brush(ArcTokens.BorderSubtle), 1);
        context.DrawEllipse(null, ring, orbit.Center, orbit.Rx, orbit.Ry);
        context.DrawEllipse(null, ring, orbit.Center, orbit.Rx * 1.42, orbit.Ry * 1.42);

        var selectedPoint = default(Point);
        for (var index = 0; index < Logos.Length; index++)
        {
            var point = LogoPoint(size, index);
            var selected = index == _selected;
            if (selected)
            {
                selectedPoint = point;
            }
        }

        if (_selected >= 0)
        {
            var line = new Pen(Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.Accent), 0.55)), 1.5, new DashStyle(new double[] { 3, 4 }, 0));
            context.DrawLine(line, orbit.Center, selectedPoint);
        }

        DrawCenter(context, orbit.Center);

        for (var index = 0; index < Logos.Length; index++)
        {
            var point = LogoPoint(size, index);
            var logo = Logos[index];
            var selected = index == _selected;
            var hovered = HoverId == IdLogo + index || (IsFocused && _focus == index);
            var matches = Matches(index);
            var opacity = matches ? 1.0 : 0.22;
            var radius = selected ? 26 : 22;

            using (context.PushOpacity(opacity))
            {
                Sm.Lift(context, new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2), 14, 0.5);
                var fill = selected ? Sm.Brush(ArcTokens.Accent) : Sm.Brush(ArcTokens.SurfaceRaised);
                var ink = selected ? Sm.Brush(ArcTokens.AccentForeground) : Sm.Brush(ArcTokens.TextSecondary);
                context.DrawEllipse(fill, new Pen(Sm.Brush(ArcTokens.Border), 1), point, radius, radius);
                Sm.Icon(context, logo.Icon, point, selected ? 22 : 19, ink);

                if (hovered && !selected)
                {
                    var tag = Sm.Text(logo.Name, 11, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
                    var pill = new Rect(point.X - tag.Width / 2 - 8, point.Y - radius - 28, tag.Width + 16, 22);
                    context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), pill, 11, 11);
                    Sm.TextCentered(context, tag, pill.Center.X, pill.Center.Y);
                }

                if (selected && IsFocused)
                {
                    Sm.FocusRing(context, new Rect(point.X - radius, point.Y - radius, radius * 2, radius * 2), radius);
                }
            }

            HitCircle(point, radius + 4, IdLogo + index);
        }

        DrawCard(context, size);
    }

    private void DrawCenter(DrawingContext context, Point center)
    {
        var rect = new Rect(center.X - 38, center.Y - 38, 76, 76);
        Sm.Lift(context, rect, 38, 0.8);
        var gradient = Sm.Gradient(ArcTokens.GradientFrom, ArcTokens.GradientTo, vertical: false);
        context.DrawEllipse(gradient, new Pen(Sm.Brush(ArcTokens.Border), 1), center, 38, 38);
        var mark = Sm.Text("arc", 17, Sm.Brush(ArcTokens.GradientForeground), FontWeight.Medium);
        Sm.TextCentered(context, mark, center.X, center.Y);

        var dot = new Pen(Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.GradientForeground), 0.4)), 1);
        context.DrawEllipse(null, dot, center, 47, 47);
    }

    private void DrawChips(DrawingContext context, Size size)
    {
        var x = 30.0;
        for (var index = 0; index < Categories.Length; index++)
        {
            var text = Sm.Text(Categories[index], 12, Sm.Brush(index == _category ? ArcTokens.AccentForeground : ArcTokens.TextSecondary), FontWeight.Medium);
            var pill = new Rect(x, 22, text.Width + 22, 28);
            if (index == _category)
            {
                context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, pill, 14, 14);
            }
            else
            {
                context.DrawRectangle(Sm.Brush(ArcTokens.Surface), new Pen(Sm.Brush(ArcTokens.Border), 1), pill, 14, 14);
            }

            Sm.TextCentered(context, text, pill.Center.X, pill.Center.Y);
            Hit(pill, IdChip + index);
            x = pill.Right + 6;
        }

        var label = Sm.Text("Sample marks on two rings", 11, Sm.Brush(ArcTokens.TextMuted));
        Sm.TextRight(context, label, size.Width - 370, 36);
    }

    private void DrawCard(DrawingContext context, Size size)
    {
        var card = new Rect(size.Width - 350, 66, 320, 300);
        Sm.Lift(context, card, 26, 1.1);
        context.DrawRectangle(Sm.Brush(ArcTokens.Surface), new Pen(Sm.Brush(ArcTokens.Border), 1), card, 26, 26);

        if (_selected < 0)
        {
            var title = Sm.Text("Integrations", 18, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
            context.DrawText(title, new Point(card.X + 22, card.Y + 22));
            var body = Sm.Text("Pick a logo to see what it does, or filter by category.", 13, Sm.Brush(ArcTokens.TextSecondary), maxWidth: card.Width - 44, maxLines: 2);
            context.DrawText(body, new Point(card.X + 22, card.Y + 52));
            return;
        }

        var logo = Logos[_selected];
        var tile = new Rect(card.X + 22, card.Y + 22, 44, 44);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), new Pen(Sm.Brush(ArcTokens.Border), 1), tile, 14, 14);
        Sm.Icon(context, logo.Icon, tile.Center, 22, Sm.Brush(ArcTokens.TextSecondary));

        var name = Sm.Text(logo.Name, 17, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(name, new Point(tile.Right + 12, tile.Y + 4));
        var category = Sm.Text(logo.Category, 12, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(category, new Point(tile.Right + 12, tile.Y + 26));

        var description = Sm.Text(logo.Description, 13, Sm.Brush(ArcTokens.TextSecondary), maxWidth: card.Width - 44, maxLines: 3);
        context.DrawText(description, new Point(card.X + 22, card.Y + 84));

        var connected = _connected;
        var statusText = connected ? "Connected" : "Not connected";
        var statusInk = connected ? Sm.Brush(ArcTokens.Success) : Sm.Brush(ArcTokens.TextMuted);
        var status = Sm.Text(statusText, 12, statusInk, FontWeight.Medium);
        var statusPill = new Rect(card.X + 22, card.Y + 132, status.Width + (connected ? 40 : 24), 26);
        context.DrawRectangle(
            connected ? Sm.Brush(ArcTokens.SuccessSubtle) : Sm.Brush(ArcTokens.SurfaceMuted),
            null,
            statusPill,
            13,
            13);
        if (connected)
        {
            Sm.Icon(context, "check", new Rect(statusPill.X + 8, statusPill.Y + 7, 12, 12), statusInk);
        }

        context.DrawText(status, new Point(statusPill.X + (connected ? 26 : 12), statusPill.Y + 4));
        var guide = Sm.Text("Two way sync · read the setup guide in Docs", 11, Sm.Brush(ArcTokens.TextMuted));
        context.DrawText(guide, new Point(card.X + 22, statusPill.Bottom + 14));

        var connectText = Sm.Text(connected ? "Connected" : "Connect", 13, Sm.Brush(connected ? ArcTokens.Foreground : ArcTokens.AccentForeground), FontWeight.Medium);
        var connect = new Rect(card.X + 22, card.Bottom - 52, 118, 36);
        context.DrawRectangle(
            connected ? Sm.Brush(ArcTokens.SurfaceMuted) : Sm.Brush(ArcTokens.Accent),
            connected ? new Pen(Sm.Brush(ArcTokens.Border), 1) : null,
            connect,
            18,
            18);
        Sm.TextCentered(context, connectText, connect.Center.X, connect.Center.Y);

        var docsText = Sm.Text("Docs", 13, Sm.Brush(ArcTokens.TextSecondary), FontWeight.Medium);
        var docs = new Rect(connect.Right + 8, connect.Y, 74, 36);
        context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), docs, 18, 18);
        Sm.TextCentered(context, docsText, docs.Center.X, docs.Center.Y);

        var prev = new Rect(card.Right - 78, card.Bottom - 52, 34, 36);
        var next = new Rect(card.Right - 40, card.Bottom - 52, 34, 36);
        context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), prev, 12, 12);
        context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), next, 12, 12);
        Sm.Icon(context, "chevron-left", prev.Center, 15, Sm.Brush(ArcTokens.TextSecondary));
        Sm.Icon(context, "chevron-right", next.Center, 15, Sm.Brush(ArcTokens.TextSecondary));

        Hit(connect, IdConnect);
        Hit(docs, IdDocs);
        Hit(prev, IdPrev);
        Hit(next, IdNext);
    }

    // ---- Interaction -------------------------------------------------------------------------------

    protected override void OnPress(int id, Point point)
    {
        Focus();
        if (id >= IdChip && id < IdChip + Categories.Length)
        {
            _category = id - IdChip;
            _selected = -1;
            SelectFirstMatch();
            return;
        }

        if (id >= IdLogo && id < IdLogo + Logos.Length)
        {
            var index = id - IdLogo;
            if (!Matches(index))
            {
                return;
            }

            if (_selected == index)
            {
                _selected = -1;
            }
            else
            {
                _selected = index;
                _focus = index;
                _connected = Logos[index].Connected;
            }

            InvalidateVisual();
            return;
        }

        switch (id)
        {
            case IdConnect:
                _connected = !_connected;
                InvalidateVisual();
                break;
            case IdDocs:
                InvalidateVisual();
                break;
            case IdPrev:
                Step(-1);
                break;
            case IdNext:
                Step(1);
                break;
        }
    }

    private void SelectFirstMatch()
    {
        for (var index = 0; index < Logos.Length; index++)
        {
            if (Matches(index))
            {
                _selected = index;
                _focus = index;
                _connected = Logos[index].Connected;
                return;
            }
        }
    }

    private void Step(int delta)
    {
        if (_selected < 0)
        {
            SelectFirstMatch();
            return;
        }

        var index = _selected;
        for (var attempt = 0; attempt < Logos.Length; attempt++)
        {
            index = (index + delta + Logos.Length) % Logos.Length;
            if (Matches(index))
            {
                _selected = index;
                _focus = index;
                _connected = Logos[index].Connected;
                return;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                Step(-1);
                e.Handled = true;
                break;
            case Key.Right:
                Step(1);
                e.Handled = true;
                break;
            case Key.Home:
                _focus = 0;
                StepTo(0);
                e.Handled = true;
                break;
            case Key.End:
                _focus = Logos.Length - 1;
                StepTo(Logos.Length - 1);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (_selected < 0)
                {
                    SelectFirstMatch();
                }
                else
                {
                    _selected = -1;
                }

                e.Handled = true;
                break;
            case Key.Escape:
                _selected = -1;
                UpdateSpeed();
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    private void StepTo(int index)
    {
        if (Matches(index))
        {
            _selected = index;
            _connected = Logos[index].Connected;
        }
    }

    protected override void OnHoverChanged(int id)
    {
        if (id >= IdLogo && id < IdLogo + Logos.Length)
        {
            _focus = id - IdLogo;
        }

        UpdateSpeed();
    }

    private void UpdateSpeed()
    {
        // The rings rest while the pointer is anywhere inside the orbit, or while a card is open.
        var target = Hovering || _selected >= 0 ? 0 : 1;
        if (Math.Abs(target - _speed) < 0.01)
        {
            return;
        }

        var from = _speed;
        Animate(320, ArcMotion.EaseStandard, progress =>
        {
            _speed = Sm.Lerp(from, target, progress);
            InvalidateVisual();
        });
    }
}
