using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Motion.Controls;

/// <summary>
/// A floating command dock for the tools someone switches between all day. A label glides between items, a
/// calm accent tint springs to the chosen one, and a group grows a tray out of the dock surface, all inside
/// one hairline outline. Items can be reordered with Alt and the arrow keys. Reduced motion makes the label,
/// the tray and the selection jump.
/// </summary>
public class ArcDock : ArcMotionCanvas
{
    private sealed class DockItem
    {
        public DockItem(string id, string label, string icon, string shortcut, DockMember[]? members = null, int badge = 0)
        {
            Id = id;
            Label = label;
            Icon = icon;
            Shortcut = shortcut;
            Members = members;
            Badge = badge;
        }

        public string Id { get; }
        public string Label { get; }
        public string Icon { get; }
        public string Shortcut { get; }
        public DockMember[]? Members { get; }
        public int Badge { get; }
    }

    private sealed record DockMember(string Id, string Label, string Icon, string Shortcut);

    private static readonly DockItem[] Items =
    {
        new("select", "Select", "navigation", "V"),
        new("hand", "Hand", "move", "H"),
        new("shapes", "Shapes", "square", "S", new[]
        {
            new DockMember("rect", "Rectangle", "square", "R"),
            new DockMember("ellipse", "Ellipse", "circle", "O")
        }),
        new("pen", "Pen", "pencil", "P"),
        new("comment", "Comment", "message-circle", "C", null, 3)
    };

    private const int IdItem = 10;
    private const int IdMember = 30;
    private const int IdTray = 40;
    private const int IdLabel = 50;

    private const double Slot = 44;
    private const double Gap = 3;
    private const double Pad = 7;
    private const double BarHeight = 56;

    private readonly int[] _order = { 0, 1, 2, 3, 4 };
    private int _selected = 2;
    private int _member = 1;
    private bool _trayOpen = true;
    private double _trayProgress = 1;
    private int _focus;
    private int _trayFocus = 1;
    private int _trayItem = 2;
    private double _labelX = double.NaN;
    private double _labelTargetX = double.NaN;

    public ArcDock()
    {
        Sm.Name(this, "Tools", "Toolbar. Left and right arrows move along the dock, Enter chooses, Up opens a group tray.");
    }

    private double BarWidth => Pad * 2 + _order.Length * Slot + (_order.Length - 1) * Gap;

    private (double X, double Y, double W, double H) Bar(Size size) =>
        (size.Width / 2 - BarWidth / 2, size.Height - 92, BarWidth, BarHeight);

    private Rect ItemRect(Size size, int itemIndex)
    {
        var bar = Bar(size);
        for (var position = 0; position < _order.Length; position++)
        {
            if (_order[position] == itemIndex)
            {
                return new Rect(bar.X + Pad + position * (Slot + Gap), bar.Y + (BarHeight - Slot) / 2, Slot, Slot);
            }
        }

        return default;
    }

    private Rect TrayRect(Size size)
    {
        var group = ItemRect(size, _trayItem);
        var width = 168.0;
        var height = 8 + 2 * 42 + 6;
        var x = Math.Clamp(group.Center.X - width / 2, 16, size.Width - width - 16);
        var y = Bar(size).Y - height - 10;
        return new Rect(x, y, width, height);
    }

    private int LabelTarget(Size size)
    {
        if (Hovering && HoverId >= IdItem && HoverId < IdItem + Items.Length)
        {
            return HoverId - IdItem;
        }

        if (IsFocused && !_trayOpen)
        {
            return _order[Math.Clamp(_focus, 0, _order.Length - 1)];
        }

        return _selected;
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (size.Width < 300 || size.Height < 180)
        {
            return;
        }

        DrawCanvas(context, size);
        var bar = Bar(size);

        // Dock surface.
        var barRect = new Rect(bar.X, bar.Y, bar.W, bar.H);
        Sm.Lift(context, barRect, 24, 1.1);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), barRect, 24, 24);

        for (var position = 0; position < _order.Length; position++)
        {
            var index = _order[position];
            var item = Items[index];
            var rect = ItemRect(size, index);
            var selected = index == _selected;
            var hovered = HoverId == IdItem + index;
            var focused = IsFocused && _focus == position;

            if (selected)
            {
                context.DrawRectangle(
                    Sm.Alpha(ArcTokens.Accent, 0.16),
                    new Pen(Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.Accent), 0.55)), 1),
                    rect,
                    16,
                    16);
            }
            else if (hovered || focused)
            {
                context.DrawRectangle(Sm.Alpha(ArcTokens.Foreground, 0.05), null, rect, 16, 16);
            }

            var icon = selected && item.Members is not null ? item.Members[_member].Icon : item.Icon;
            var color = selected
                ? Sm.Brush(ArcTokens.AccentStrong)
                : hovered || focused
                    ? Sm.Brush(ArcTokens.Foreground)
                    : Sm.Brush(ArcTokens.TextSecondary);
            Sm.Icon(context, icon, rect.Center, 21, color, selected ? 2 : 1.75);

            if (item.Members is not null)
            {
                var chevron = new Point(rect.Right - 9, rect.Y + 9);
                var pen = new Pen(Sm.Brush(selected ? ArcTokens.AccentStrong : ArcTokens.TextMuted), 1.4, null, PenLineCap.Round, PenLineJoin.Round);
                context.DrawLine(pen, new Point(chevron.X - 3, chevron.Y + 1), chevron);
                context.DrawLine(pen, chevron, new Point(chevron.X + 3, chevron.Y + 1));
            }

            if (item.Badge > 0)
            {
                var badge = new Point(rect.Right - 8, rect.Y + 8);
                context.DrawEllipse(Sm.Brush(ArcTokens.Accent), null, badge, 8, 8);
                var count = Sm.Text(item.Badge.ToString(), 9, Sm.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
                Sm.TextCentered(context, count, badge.X, badge.Y);
            }

            if (ShowFocusRing && focused && !_trayOpen)
            {
                Sm.FocusRing(context, rect, 16);
            }

            Hit(rect, IdItem + index);
        }

        if (_trayOpen || _trayProgress > 0.01)
        {
            DrawTray(context, size);
        }

        DrawLabel(context, size);
    }

    private void DrawCanvas(DrawingContext context, Size size)
    {
        var dot = Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.BorderStrong), 0.35));
        for (var x = 60.0; x < size.Width - 40; x += 26)
        {
            for (var y = 40.0; y < size.Height - 130; y += 26)
            {
                context.DrawEllipse(dot, null, new Point(x, y), 1, 1);
            }
        }

        var stroke = new Pen(Sm.Solid(ArcColor.WithAlpha(Sm.Color(ArcTokens.BorderStrong), 0.8)), 1.5);
        context.DrawRectangle(null, stroke, new Rect(size.Width / 2 - 190, 60, 200, 120), 18, 18);
        context.DrawEllipse(null, stroke, new Point(size.Width / 2 + 110, 128), 62, 62);
    }

    private void DrawTray(DrawingContext context, Size size)
    {
        var tray = TrayRect(size);
        var grow = Sm.Clamp01(_trayProgress);
        var opacity = grow;
        var height = tray.Height * (0.6 + 0.4 * grow);
        var rect = new Rect(tray.X, tray.Bottom - height, tray.Width, height);
        using (context.PushOpacity(opacity))
        {
            Sm.Lift(context, rect, 20, 0.9);
            context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), rect, 20, 20);

            var groupIndex = _trayItem;
            var item = Items[groupIndex];
            if (item.Members is null)
            {
                return;
            }

            for (var index = 0; index < item.Members.Length; index++)
            {
                var member = item.Members[index];
                var row = new Rect(rect.X + 6, rect.Y + 6 + index * 42, rect.Width - 12, 38);
                var selected = _selected == groupIndex && _member == index;
                var hovered = HoverId == IdMember + index;
                if (selected || hovered || (IsFocused && _trayOpen && _trayFocus == index))
                {
                    context.DrawRectangle(Sm.Alpha(ArcTokens.Foreground, selected ? 0.07 : 0.04), null, row, 12, 12);
                }

                if (IsFocused && _trayOpen && _trayFocus == index && ShowFocusRing)
                {
                    Sm.FocusRing(context, row, 12);
                }

                Sm.Icon(context, member.Icon, new Rect(row.X + 10, row.Center.Y - 9, 18, 18), selected ? Sm.Brush(ArcTokens.AccentStrong) : Sm.Brush(ArcTokens.TextSecondary));
                var label = Sm.Text(member.Label, 13, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
                context.DrawText(label, new Point(row.X + 38, row.Center.Y - label.Height / 2));
                var shortcut = Sm.Text(member.Shortcut, 11, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium, family: Sm.Mono);
                Sm.TextRight(context, shortcut, row.Right - 10, row.Center.Y);
                Hit(row, IdMember + index);
            }

            Hit(rect, IdTray);
        }
    }

    private void DrawLabel(DrawingContext context, Size size)
    {
        var target = LabelTarget(size);
        if (target < 0 || target >= Items.Length)
        {
            return;
        }

        var item = Items[target];
        var text = item.Members is not null && target == _selected
            ? $"{item.Label}: {item.Members[_member].Label}"
            : item.Label;
        var shortcut = item.Members is not null && target == _selected ? item.Members[_member].Shortcut : item.Shortcut;
        var label = Sm.Text(text, 12, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        var key = Sm.Text(shortcut, 11, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium, family: Sm.Mono);
        var width = label.Width + (string.IsNullOrEmpty(shortcut) ? 0 : key.Width + 8) + 22;
        var rect = ItemRect(size, target);
        var center = rect.Center.X;
        var trayOpen = _trayOpen || _trayProgress > 0.5;
        var y = trayOpen ? TrayRect(size).Y - 42 : Bar(size).Y - 36;

        if (double.IsNaN(_labelX))
        {
            _labelX = center;
            _labelTargetX = center;
        }

        if (Math.Abs(center - _labelTargetX) > 0.5)
        {
            var from = _labelX;
            var to = center;
            _labelTargetX = center;
            global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Animate(180, ArcMotion.EaseStandard, progress =>
                {
                    _labelX = Sm.Lerp(from, to, progress);
                    InvalidateVisual();
                });
            }, global::Avalonia.Threading.DispatcherPriority.Background);
        }

        var pill = new Rect(_labelX - width / 2, y, width, 28);
        Sm.Lift(context, pill, 14, 0.6);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), pill, 14, 14);
        context.DrawText(label, new Point(pill.X + 11, pill.Center.Y - label.Height / 2));
        if (!string.IsNullOrEmpty(shortcut))
        {
            Sm.TextRight(context, key, pill.Right - 11, pill.Center.Y);
        }

        Hit(pill, IdLabel);
    }

    // ---- Interaction -------------------------------------------------------------------------------

    protected override void OnPress(int id, Point point)
    {
        Focus();
        if (id >= IdItem && id < IdItem + Items.Length)
        {
            Choose(id - IdItem);
        }
        else if (id >= IdMember && id < IdMember + 2)
        {
            _member = id - IdMember;
            _selected = _order[Math.Clamp(_focus, 0, _order.Length - 1)];
            _trayOpen = false;
            AnimateTray();
        }
    }

    protected override void OnRelease(int id, Point point)
    {
        if (HitAt(point) != id)
        {
            return;
        }

        if (id >= IdItem && id < IdItem + Items.Length)
        {
            return;
        }

        InvalidateVisual();
    }

    private void Choose(int index)
    {
        var item = Items[index];
        _selected = index;
        _focus = Array.IndexOf(_order, index);
        if (item.Members is not null)
        {
            _trayItem = index;
            _trayOpen = !_trayOpen;
            AnimateTray();
        }
        else
        {
            _trayOpen = false;
            AnimateTray();
        }

        AutomationProperties.SetName(this, $"Tools, {item.Label} selected");
        InvalidateVisual();
    }

    private void AnimateTray()
    {
        var target = _trayOpen ? 1 : 0;
        var from = _trayProgress;
        Animate(240, ArcMotion.SpringSmooth, progress =>
        {
            _trayProgress = Sm.Lerp(from, target, progress);
            InvalidateVisual();
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        switch (e.Key)
        {
            case Key.Left when alt:
                Swap(-1);
                e.Handled = true;
                return;
            case Key.Right when alt:
                Swap(1);
                e.Handled = true;
                return;
            case Key.Left:
                _focus = (_focus - 1 + _order.Length) % _order.Length;
                e.Handled = true;
                break;
            case Key.Right:
                _focus = (_focus + 1) % _order.Length;
                e.Handled = true;
                break;
            case Key.Home:
                _focus = 0;
                e.Handled = true;
                break;
            case Key.End:
                _focus = _order.Length - 1;
                e.Handled = true;
                break;
            case Key.Up:
                var group = Items[_order[_focus]];
                if (group.Members is not null)
                {
                    _trayOpen = true;
                    _selected = _order[_focus];
                    AnimateTray();
                }

                e.Handled = true;
                break;
            case Key.Down:
                if (_trayOpen)
                {
                    _trayOpen = false;
                    AnimateTray();
                }

                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Choose(_order[_focus]);
                e.Handled = true;
                break;
            case Key.Escape:
                if (_trayOpen)
                {
                    _trayOpen = false;
                    AnimateTray();
                }

                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }

    private void Swap(int delta)
    {
        var target = _focus + delta;
        if (target < 0 || target >= _order.Length)
        {
            return;
        }

        (_order[_focus], _order[target]) = (_order[target], _order[_focus]);
        _focus = target;
        InvalidateVisual();
    }
}
