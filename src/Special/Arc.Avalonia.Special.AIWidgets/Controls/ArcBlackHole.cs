using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>One row that can be swallowed by the singularity.</summary>
public sealed class ArcBlackHoleItem
{
    public string Id { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string From { get; set; } = string.Empty;

    public string Time { get; set; } = string.Empty;
}

/// <summary>
/// A delete transition for a list: the chosen rows spiral into a tiny singularity while the rest of the list bends
/// around it, the hole collapses with a soft shockwave, and Undo spins the rows back out.
///
/// Platform approximations: nothing is painted to a texture and there is no gravitational lensing shader. The rows
/// are drawn by hand, so the "bend" is a small vertical and horizontal displacement near the hole, the spiral is a
/// logarithmic path on one clock, and the shockwave is a stroked ring. With reduced motion the rows fade out in
/// 200 ms and fade back in on undo.
/// </summary>
public class ArcBlackHole : Panel
{
    public static readonly StyledProperty<IEnumerable<ArcBlackHoleItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcBlackHole, IEnumerable<ArcBlackHoleItem>?>(nameof(Items));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcBlackHole, string>(nameof(Title), "Inbox");

    private readonly ArcBlackHoleList _list = new();
    private readonly List<string> _pendingSelection = new();
    private TextBlock? _statusBlock;
    private ArcButton? _deleteButton;
    private ArcButton? _undoButton;

    static ArcBlackHole()
    {
        ItemsProperty.Changed.AddClassHandler<ArcBlackHole>((hole, _) => hole.Load());
        TitleProperty.Changed.AddClassHandler<ArcBlackHole>((hole, _) => hole.Rebuild());
    }

    public ArcBlackHole()
    {
        ClipToBounds = false;
        Rebuild();
    }

    public IEnumerable<ArcBlackHoleItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Deletes the selected rows with the transition; the rows disappear when it finishes.</summary>
    public void Delete() => _list.Delete();

    /// <summary>Selects rows by id; the documentation demo preselects two so the delete action reads immediately.</summary>
    public void Select(params string[] ids)
    {
        _pendingSelection.Clear();
        _pendingSelection.AddRange(ids);
        if (_list.Count > 0)
        {
            _list.Select(ids);
            UpdateHeader();
        }
    }

    /// <summary>Restores the last deleted rows with the reverse transition.</summary>
    public void Undo() => _list.Undo();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    private void Load()
    {
        if (Items is not null)
        {
            _list.SetItems(Items);
        }

        if (_pendingSelection.Count > 0)
        {
            _list.Select(_pendingSelection);
        }

        UpdateHeader();
    }

    private void Rebuild()
    {
        Children.Clear();
        var root = new StackPanel { Spacing = 10 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock { Text = Title, Classes = { "arc-label" } };
        titles.Children.Add(title);
        _statusBlock = Sv.Caption("Select a row, then delete it. Undo brings it back.");
        titles.Children.Add(_statusBlock);
        header.Children.Add(titles);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _undoButton = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Undo", IsVisible = false };
        _undoButton.Click += (_, _) => _list.Undo();
        _deleteButton = new ArcButton { Variant = ArcButtonVariant.Danger, Size = ArcButtonSize.Sm, Content = "Delete" };
        _deleteButton.Click += (_, _) => _list.Delete();
        actions.Children.Add(_undoButton);
        actions.Children.Add(_deleteButton);
        Grid.SetColumn(actions, 1);
        header.Children.Add(actions);
        root.Children.Add(header);

        var frame = new Border
        {
            CornerRadius = Sv.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 10),
            ClipToBounds = true
        };
        frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        frame.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        Sv.Detach(_list);
        _list.SelectionChanged += UpdateHeader;
        _list.Deleted += UpdateHeader;
        frame.Child = _list;
        root.Children.Add(frame);

        Children.Add(root);
        UpdateHeader();
        AutomationProperties.SetName(this, $"{Title}, {_list.Count} conversations");
    }

    private void UpdateHeader()
    {
        var count = _list.SelectionCount;
        if (_deleteButton is not null)
        {
            _deleteButton.Content = count == 0 ? "Delete" : $"Delete {count}";
            _deleteButton.IsEnabled = count > 0;
        }

        if (_undoButton is not null)
        {
            _undoButton.IsVisible = _list.DeletedCount > 0;
            _undoButton.Content = _list.DeletedCount switch
            {
                0 => "Undo",
                1 => "Undo",
                _ => $"Undo {_list.DeletedCount}"
            };
        }

        if (_statusBlock is not null)
        {
            _statusBlock.Text = _list.HasMessage
                ? _list.Message
                : _list.Count == 0
                    ? "The list is empty. Undo brings the conversations back."
                    : "Select a row, then delete it. Undo brings it back.";
            _statusBlock.Paint(TextBlock.ForegroundProperty, _list.HasMessage ? ArcTokens.Accent : ArcTokens.TextMuted);
        }
    }


}

/// <summary>The drawn list: rows, checkboxes, the singularity, the spiral and the lensing of the rest.</summary>
internal sealed class ArcBlackHoleList : Control
{
    private const double RowHeight = 46;
    private const double RowGap = 6;

    private readonly List<ArcBlackHoleItem> _items = new();
    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly List<ArcBlackHoleItem> _deleted = new();
    private readonly List<int> _deletedAt = new();
    private readonly HashSet<string> _restoring = new(StringComparer.Ordinal);
    private readonly SvTween _tween = new();
    private double _phase;
    private int _hover = -1;
    private int _active;
    private bool _pointerFocus;
    private bool _releasing;
    private string _message = string.Empty;

    public ArcBlackHoleList()
    {
        Focusable = true;
        ClipToBounds = false;
        Height = 6 * (RowHeight + RowGap) + 8;
    }

    public event Action? SelectionChanged;

    public event Action? Deleted;

    public int Count => _items.Count;

    public int SelectionCount => _selected.Count;

    public int DeletedCount => _deleted.Count;

    public bool HasMessage => _message.Length > 0;

    public string Message => _message;

    /// <summary>Used by the document demo: selects two rows so the delete action reads immediately.</summary>
    public void Select(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            _selected.Add(id);
        }

        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    public void SetItems(IEnumerable<ArcBlackHoleItem> items)
    {
        _items.Clear();
        _items.AddRange(items);
        _selected.Clear();
        _deleted.Clear();
        _deletedAt.Clear();
        _phase = 0;
        Height = Math.Max(3, _items.Count) * (RowHeight + RowGap) + 8;
        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    private Rect RowRect(int index)
    {
        var top = 4 + index * (RowHeight + RowGap);
        return new Rect(2, top, Math.Max(60, Bounds.Width - 4), RowHeight);
    }

    private static Point HoleCenter(Rect bounds) => new(bounds.Width / 2, bounds.Height / 2);

    private static Rect Spiral(Rect from, Point hole, double phase, int seed)
    {
        var center = from.Center;
        var dx = center.X - hole.X;
        var dy = center.Y - hole.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var direction = seed % 2 == 0 ? 1 : -1;
        var angle = Math.Atan2(dy, dx) + direction * phase * 3.1;
        var radius = distance * (1 - phase);
        var scale = 1 - 0.72 * phase;
        var x = hole.X + Math.Cos(angle) * radius;
        var y = hole.Y + Math.Sin(angle) * radius;
        var width = from.Width * scale;
        var height = from.Height * scale;
        return new Rect(x - width / 2, y - height / 2, width, height);
    }

    private bool IsGhostItem(string id) => _releasing ? _restoring.Contains(id) : _selected.Contains(id);

    private bool IsGhost(int index) => _phase > 0.001 && index >= 0 && index < _items.Count && IsGhostItem(_items[index].Id);

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0)
        {
            return;
        }

        var hole = HoleCenter(Bounds);
        var surface = Sv.Brush(ArcTokens.Surface);
        var muted = Sv.Brush(ArcTokens.SurfaceMuted);
        var border = Sv.Brush(ArcTokens.Border);
        var accent = Sv.Brush(ArcTokens.Accent);
        var accentSubtle = Sv.Brush(ArcTokens.AccentSubtle);
        var foreground = Sv.Brush(ArcTokens.Foreground);
        var secondary = Sv.Brush(ArcTokens.TextSecondary);

        var ghostRows = _items.Count(item => IsGhostItem(item.Id));
        var removedHeight = ghostRows * (RowHeight + RowGap);

        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var rect = RowRect(index);
            var ghost = IsGhost(index);

            if (ghost)
            {
                var spiralled = Spiral(rect, hole, _phase, index);
                using (context.PushOpacity(Math.Clamp(1 - _phase * 0.9, 0, 1)))
                {
                    DrawRow(context, spiralled, item, selected: true, hover: false, surface, muted, border, accent, accentSubtle, foreground, secondary, ghost: true);
                }

                continue;
            }

            // Rows below the selection close the gap as the hole eats, and every row bends a little near it.
            var shift = 0.0;
            if (_phase > 0.001)
            {
                var ghostsAbove = _items.Take(index).Count(entry => IsGhostItem(entry.Id));
                shift = -removedHeight * (_releasing ? 1 - _phase : _phase) * ghostsAbove;

                var dy = rect.Center.Y + shift - hole.Y;
                var dx = rect.Center.X - hole.X + 0.001;
                var distanceSq = dx * dx + dy * dy;
                var sigma = 90.0 * (1 - _phase * 0.4);
                var lens = Math.Exp(-distanceSq / (2 * sigma * sigma)) * _phase;
                shift += Math.Sign(dy) * 9 * lens;
                var inset = 12 * lens;
                rect = rect.Deflate(new Thickness(inset, 0, inset, 0)).Translate(new Vector(0, shift));
            }

            using (context.PushOpacity(Math.Clamp(1 - _phase * 0.55, 0.2, 1)))
            {
                DrawRow(context, rect, item, _selected.Contains(item.Id), index == _hover, surface, muted, border, accent, accentSubtle, foreground, secondary, ghost: false);
            }
        }

        // The singularity: opens with a slight overshoot, eats, then collapses and throws one soft ring.
        if (_phase > 0.001)
        {
            var open = Math.Sin(Math.Min(1, _phase * 1.35) * Math.PI * 0.5);
            var radius = 3 + 15 * open * (1 - Math.Pow(_phase, 6));
            using (context.PushOpacity(0.9))
            {
                context.DrawEllipse(Sv.AlphaBrush(ArcTokens.Accent, 0.16), null, hole, radius * 2.6, radius * 2.6);
                context.DrawEllipse(Sv.AlphaBrush(ArcTokens.Accent, 0.28), null, hole, radius * 1.7, radius * 1.7);
                context.DrawEllipse(foreground, null, hole, radius, radius);
            }

            if (_phase > 0.82)
            {
                var wave = (_phase - 0.82) / 0.18;
                context.DrawEllipse(null, new Pen(Sv.AlphaBrush(ArcTokens.Accent, 0.5 * (1 - wave)), 1.6), hole, radius + wave * 90, radius + wave * 90);
            }
        }

        if (IsFocused && !_pointerFocus && _items.Count > 0)
        {
            var active = RowRect(Math.Clamp(_active, 0, _items.Count - 1));
            SvDraw.Outline(context, active.Deflate(1), 14, Sv.Brush(ArcTokens.FocusOutline), 2);
        }
    }

    private static void DrawRow(
        DrawingContext context,
        Rect rect,
        ArcBlackHoleItem item,
        bool selected,
        bool hover,
        IBrush surface,
        IBrush muted,
        IBrush border,
        IBrush accent,
        IBrush accentSubtle,
        IBrush foreground,
        IBrush secondary,
        bool ghost)
    {
        if (rect.Width <= 2 || rect.Height <= 2)
        {
            return;
        }

        var radius = Math.Min(14, rect.Height / 2);
        var fill = selected ? accentSubtle : hover ? muted : surface;
        SvDraw.Fill(context, rect, radius, fill);
        SvDraw.Outline(context, rect, radius, selected ? accent : border, 1);

        if (rect.Width < 90)
        {
            return;
        }

        // Checkbox
        var box = new Rect(rect.X + 12, rect.Center.Y - 8, 16, 16);
        if (selected)
        {
            SvDraw.Fill(context, box, 5, accent);
            var pen = new Pen(Sv.Brush(ArcTokens.Surface), 1.8, null, PenLineCap.Round, PenLineJoin.Round);
            context.DrawLine(pen, new Point(box.X + 4, box.Center.Y), new Point(box.X + 7, box.Center.Y + 3.4));
            context.DrawLine(pen, new Point(box.X + 7, box.Center.Y + 3.4), new Point(box.X + 12, box.Center.Y - 3.4));
        }
        else
        {
            SvDraw.Outline(context, box, 5, border, 1.4);
        }

        if (ghost)
        {
            return;
        }

        var subject = SvDraw.Text(item.Subject, 13, selected ? foreground : secondary, FontWeight.Medium);
        SvDraw.DrawText(context, subject, rect.X + 38, rect.Center.Y - 15);
        var meta = SvDraw.Text(item.From, 11, secondary);
        SvDraw.DrawText(context, meta, rect.X + 38, rect.Center.Y + 3);
        var time = SvDraw.Text(item.Time, 11, secondary);
        SvDraw.DrawTextRight(context, time, rect.Right - 12, rect.Center.Y - 7);
    }

    private void Advance(double target)
    {
        _tween.Stop();
        var from = _phase;
        var duration = ArcThemeManager.ReducedMotion ? 220 : 1200;
        _tween.Run(from, target, duration, value =>
        {
            _phase = value;
            InvalidateVisual();
        }, () =>
        {
            _phase = target;
            if (target >= 1)
            {
                Finish();
            }
            else
            {
                FinishRelease();
            }
        });
    }

    public void Delete()
    {
        if (_selected.Count == 0 || _phase > 0.001)
        {
            return;
        }

        _releasing = false;
        if (ArcThemeManager.ReducedMotion)
        {
            _phase = 1;
            Finish();
            return;
        }

        Advance(1);
    }

    private void Finish()
    {
        var removed = _items.Where(item => _selected.Contains(item.Id)).ToList();
        _deleted.Clear();
        _deletedAt.Clear();
        for (var i = _items.Count - 1; i >= 0; i--)
        {
            if (_selected.Contains(_items[i].Id))
            {
                _deleted.Insert(0, _items[i]);
                _deletedAt.Insert(0, i);
                _items.RemoveAt(i);
            }
        }

        _selected.Clear();
        _phase = 0;
        _message = removed.Count == 1
            ? $"1 conversation deleted · Undo brings it back"
            : $"{removed.Count} conversations deleted · Undo brings them back";
        Height = Math.Max(3, _items.Count) * (RowHeight + RowGap) + 8;
        InvalidateVisual();
        Deleted?.Invoke();
        SelectionChanged?.Invoke();
    }

    public void Undo()
    {
        if (_deleted.Count == 0)
        {
            return;
        }

        _restoring.Clear();
        foreach (var item in _deleted)
        {
            _restoring.Add(item.Id);
        }

        var restore = _deleted.ToList();
        var positions = _deletedAt.ToList();
        for (var i = 0; i < restore.Count; i++)
        {
            var index = Math.Clamp(positions[i], 0, _items.Count);
            _items.Insert(index, restore[i]);
        }

        _releasing = true;
        _phase = 1;
        Height = Math.Max(3, _items.Count) * (RowHeight + RowGap) + 8;
        if (ArcThemeManager.ReducedMotion)
        {
            FinishRelease();
            return;
        }

        Advance(0);
    }

    private void FinishRelease()
    {
        _deleted.Clear();
        _deletedAt.Clear();
        _restoring.Clear();
        _phase = 0;
        _message = "Restored";
        InvalidateVisual();
        Deleted?.Invoke();
        SelectionChanged?.Invoke();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 600 : availableSize.Width;
        return new Size(width, Math.Max(3, _items.Count) * (RowHeight + RowGap) + 8);
    }

    private int IndexAt(Point point)
    {
        for (var i = 0; i < _items.Count; i++)
        {
            if (RowRect(i).Contains(point))
            {
                return i;
            }
        }

        return -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _pointerFocus = true;
        var index = IndexAt(e.GetPosition(this));
        if (index != _hover)
        {
            _hover = index;
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = -1;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _pointerFocus = true;
        Focus();
        var index = IndexAt(e.GetPosition(this));
        if (index >= 0 && _phase < 0.001)
        {
            _active = index;
            Toggle(index);
        }

        e.Handled = true;
    }

    private void Toggle(int index)
    {
        var id = _items[index].Id;
        if (!_selected.Add(id))
        {
            _selected.Remove(id);
        }

        InvalidateVisual();
        SelectionChanged?.Invoke();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_items.Count == 0)
        {
            return;
        }

        _pointerFocus = false;
        switch (e.Key)
        {
            case Key.Down:
                _active = Math.Min(_items.Count - 1, _active + 1);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Up:
                _active = Math.Max(0, _active - 1);
                InvalidateVisual();
                e.Handled = true;
                break;
            case Key.Space:
                Toggle(_active);
                e.Handled = true;
                break;
            case Key.Delete:
                Delete();
                e.Handled = true;
                break;
            case Key.Z when e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta):
                Undo();
                e.Handled = true;
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AutomationProperties.SetName(this, "Conversations");
        AutomationProperties.SetHelpText(this, "Up and down moves between rows, Space selects, Delete deletes, Control Z undoes.");
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
    }
}
