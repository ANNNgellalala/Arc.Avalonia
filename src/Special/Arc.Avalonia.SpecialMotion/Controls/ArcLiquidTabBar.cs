using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Controls;

/// <summary>
/// Horizontal navigation for a handful of peer sections. The selection is a lens that slides and resizes to
/// the new tab on the morph spring, redrawing what passes under it inverted so outline icons read as filled.
/// Labels swap as the lens arrives, so two labels never show together. Reduced motion jumps the lens.
/// </summary>
public class ArcLiquidTabBar : ArcMotionCanvas
{
    private static readonly (string Label, string Icon, int Badge)[] Tabs =
    {
        ("Home", "layout-dashboard", 0),
        ("Radio", "headphones", 2),
        ("Library", "folder", 0),
        ("Profile", "user", 0)
    };

    private const double Slot = 56;
    private const double Gap = 6;
    private const double Pad = 8;
    private const double BarHeight = 64;
    private const int IdTab = 100;
    private const int IdLens = 200;

    private int _value;
    private int _from;
    private double _morph = 1;
    private double[] _fromX = new double[4];
    private double[] _fromW = new double[4];
    private double _fromBarWidth;
    private double _dragX = double.NaN;

    public ArcLiquidTabBar()
    {
        Sm.Name(this, "Sections", "Tab bar. Left and right arrows change section; Home and End jump.");
        _fromX = Layout(0).X;
        _fromW = Layout(0).W;
        _fromBarWidth = Layout(0).BarWidth;
    }

    /// <summary>The selected tab value, as an index into the built in tabs.</summary>
    public int SelectedIndex
    {
        get => _value;
        set => Select(value);
    }

    private void Select(int index)
    {
        index = ((index % Tabs.Length) + Tabs.Length) % Tabs.Length;
        if (index == _value)
        {
            return;
        }

        _fromX = CurrentLayout().X;
        _fromW = CurrentLayout().W;
        _fromBarWidth = CurrentLayout().BarWidth;
        _from = _value;
        _value = index;
        _morph = 0;
        Animate(420, ArcMotion.SpringMorph, progress =>
        {
            _morph = progress;
            InvalidateVisual();
        }, () => InvalidateVisual());
        EnsureFrame();
    }

    private (double[] X, double[] W, double BarWidth, double BarX) Layout(int selected)
    {
        var widths = new double[Tabs.Length];
        for (var index = 0; index < Tabs.Length; index++)
        {
            widths[index] = Slot + (index == selected ? Sm.MeasureWidth(Tabs[index].Label, 13, FontWeight.Medium) + 18 : 0);
        }

        var total = Pad * 2 + Gap * (Tabs.Length - 1);
        foreach (var width in widths)
        {
            total += width;
        }

        var barX = (Bounds.Width - total) / 2;
        if (barX <= 0)
        {
            barX = (DefaultWidth - total) / 2;
        }

        var xs = new double[Tabs.Length];
        var x = barX + Pad;
        for (var index = 0; index < Tabs.Length; index++)
        {
            xs[index] = x;
            x += widths[index] + Gap;
        }

        return (xs, widths, total, barX);
    }

    private (double[] X, double[] W, double BarWidth, double BarX) CurrentLayout() => Layout(_value);

    private (double[] X, double[] W, double BarWidth, double BarX, double X0, double X1) Blended()
    {
        var from = Layout(_from);
        var to = CurrentLayout();
        var xs = new double[Tabs.Length];
        var ws = new double[Tabs.Length];
        for (var index = 0; index < Tabs.Length; index++)
        {
            xs[index] = Sm.Lerp(_fromX.Length == Tabs.Length ? _fromX[index] : from.X[index], to.X[index], _morph);
            ws[index] = Sm.Lerp(_fromW.Length == Tabs.Length ? _fromW[index] : from.W[index], to.W[index], _morph);
        }

        var barWidth = Sm.Lerp(_fromBarWidth > 0 ? _fromBarWidth : from.BarWidth, to.BarWidth, _morph);
        var barX = (Bounds.Width - barWidth) / 2;
        return (xs, ws, barWidth, barX, _morph, 1 - _morph);
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (size.Width < 200 || size.Height < 120)
        {
            return;
        }

        var layout = Blended();
        var barY = size.Height - 96;
        var bar = new Rect(layout.BarX, barY, layout.BarWidth, BarHeight);
        var radius = BarHeight / 2;

        DrawContent(context, size, barY);

        Sm.Lift(context, bar, radius, 1.1);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), bar, radius, radius);

        var lensX = Sm.Lerp(layout.X[_from], layout.X[_value], _morph);
        var lensW = Sm.Lerp(layout.W[_from], layout.W[_value], _morph);
        var hoverLean = Hovering && HoverId >= IdTab && HoverId < IdTab + Tabs.Length
            ? Math.Sign(layout.X[HoverId - IdTab] + layout.W[HoverId - IdTab] / 2 - (lensX + lensW / 2)) * 3
            : 0;
        var lens = new Rect(lensX + hoverLean, barY + 6, lensW, BarHeight - 12);

        context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, lens, (BarHeight - 12) / 2, (BarHeight - 12) / 2);

        for (var index = 0; index < Tabs.Length; index++)
        {
            var tabX = layout.X[index];
            var tabW = layout.W[index];
            var center = new Point(tabX + (index == _value || index == _from ? 26 : tabW / 2), barY + BarHeight / 2);
            var selected = index == _value;
            var color = selected || index == _from && _morph < 0.5
                ? Sm.Brush(ArcTokens.AccentForeground)
                : HoverId == IdTab + index
                    ? Sm.Brush(ArcTokens.Foreground)
                    : Sm.Brush(ArcTokens.TextSecondary);

            Sm.Icon(context, Tabs[index].Icon, center, 20, color, selected ? 2.2 : 1.75);

            if (Tabs[index].Badge > 0)
            {
                var badgeCenter = new Point(center.X + 11, center.Y - 11);
                var badgeFill = selected ? Sm.Brush(ArcTokens.AccentForeground) : Sm.Brush(ArcTokens.Accent);
                context.DrawEllipse(badgeFill, null, badgeCenter, 7.5, 7.5);
                var count = Sm.Text(Tabs[index].Badge.ToString(), 9, Sm.Brush(selected ? ArcTokens.Accent : ArcTokens.AccentForeground), FontWeight.Medium);
                Sm.TextCentered(context, count, badgeCenter.X, badgeCenter.Y);
                HitCircle(badgeCenter, 10, IdTab + index);
            }

            if (selected)
            {
                var label = Sm.Text(Tabs[index].Label, 13, color, FontWeight.Medium);
                using (context.PushOpacity(Sm.Clamp01((_morph - 0.35) / 0.65)))
                {
                    context.DrawText(label, new Point(center.X + 18, center.Y - label.Height / 2));
                }
            }

            Hit(new Rect(layout.X[index], barY + 6, layout.W[index], BarHeight - 12), IdTab + index);
        }

        Hit(lens, IdLens);

        if (ShowFocusRing)
        {
            Sm.FocusRing(context, new Rect(bar.X, bar.Y, bar.Width, bar.Height), radius);
        }
    }

    private void DrawContent(DrawingContext context, Size size, double barY)
    {
        var card = new Rect(size.Width / 2 - 280, Math.Max(20, barY - 190), 560, 150);
        Sm.Lift(context, card, 24, 0.7);
        context.DrawRectangle(Sm.Brush(ArcTokens.Surface), new Pen(Sm.Brush(ArcTokens.Border), 1), card, 24, 24);

        var heading = Sm.Text(Tabs[_value].Label, 18, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(heading, new Point(card.X + 24, card.Y + 22));
        var caption = Sm.Text("The lens redraws what it passes over, so outline icons fill in.", 12, Sm.Brush(ArcTokens.TextMuted), maxWidth: card.Width - 48, maxLines: 2);
        context.DrawText(caption, new Point(card.X + 24, card.Y + 50));

        for (var row = 0; row < 2; row++)
        {
            var y = card.Y + 92 + row * 26;
            var w = row == 0 ? card.Width - 48 : card.Width - 140;
            context.DrawRectangle(Sm.Alpha(ArcTokens.Foreground, 0.05), null, new Rect(card.X + 24, y, w, 10), 5, 5);
        }
    }

    // ---- Interaction -------------------------------------------------------------------------------

    protected override void OnPress(int id, Point point)
    {
        Focus();
        if (id == IdLens || (id >= IdTab && id < IdTab + Tabs.Length))
        {
            _dragX = point.X;
        }
    }

    protected override void OnDrag(int id, Point point)
    {
        if (double.IsNaN(_dragX))
        {
            return;
        }

        _dragX = point.X;
        InvalidateVisual();
    }

    protected override void OnRelease(int id, Point point)
    {
        if (double.IsNaN(_dragX))
        {
            return;
        }

        _dragX = double.NaN;
        var layout = CurrentLayout();
        var best = _value;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < Tabs.Length; index++)
        {
            var center = layout.X[index] + layout.W[index] / 2;
            var distance = Math.Abs(center - point.X);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = index;
            }
        }

        // A press on a tab always lands on it; a drag lands on the nearest tab.
        if (id >= IdTab && id < IdTab + Tabs.Length && Math.Abs(point.X - (layout.X[id - IdTab] + layout.W[id - IdTab] / 2)) < layout.W[id - IdTab] / 2)
        {
            best = id - IdTab;
        }

        Select(best);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Left:
                Select(_value - 1);
                e.Handled = true;
                break;
            case Key.Right:
                Select(_value + 1);
                e.Handled = true;
                break;
            case Key.Home:
                Select(0);
                e.Handled = true;
                break;
            case Key.End:
                Select(Tabs.Length - 1);
                e.Handled = true;
                break;
        }
    }
}
