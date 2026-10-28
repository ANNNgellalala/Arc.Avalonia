using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

/// <summary>One row in an <see cref="ArcReorderableList"/>.</summary>
public sealed class ArcReorderItem
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Detail { get; set; }

    /// <summary>A disabled row keeps its place but cannot be lifted or moved.</summary>
    public bool Disabled { get; set; }
}

/// <summary>
/// A short, user-ordered list. The 36 px grip starts a pointer drag; the same handle moves the row
/// with the arrow keys. Neighbouring rows travel with a FLIP transform on the morph spring, and
/// Space lift / Space drop / Escape cancel give keyboard users the same model without a drag.
/// </summary>
public class ArcReorderableList : TemplatedControl
{
    private const double RowHeight = 64;
    private const double RowGap = 8;
    private const double Step = RowHeight + RowGap;

    public static readonly StyledProperty<IList<ArcReorderItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcReorderableList, IList<ArcReorderItem>?>(nameof(Items));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcReorderableList, string>(nameof(Label), "Reorder items");

    private sealed class RowVisual
    {
        public required ArcReorderItem Item { get; init; }
        public required Border Frame { get; init; }
        public required ArcButton Handle { get; init; }
        public int Index { get; set; }
    }

    private readonly List<ArcReorderItem> _items = new();
    private readonly List<RowVisual> _rows = new();
    private Panel? _host;
    private StackPanel? _rowsHost;
    private TextBlock? _live;
    private bool _syncingItems;
    private bool _lifted;
    private string? _liftId;
    private string? _pendingFocusId;
    private int _liftOrigin;
    private RowVisual? _dragRow;
    private int _dragFrom;
    private int _dragTarget;
    private Point _dragStart;

    static ArcReorderableList()
    {
        ItemsProperty.Changed.AddClassHandler<ArcReorderableList>((list, _) => list.LoadItems());
        LabelProperty.Changed.AddClassHandler<ArcReorderableList>((list, _) =>
            AutomationProperties.SetName(list, list.Label));
    }

    public ArcReorderableList()
    {
        AutomationProperties.SetName(this, Label);
    }

    /// <summary>Items in their current order. The control updates this list as rows move.</summary>
    public IList<ArcReorderItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name for the list.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raised with the new order after a drag, an arrow move, or a keyboard drop.</summary>
    public event EventHandler<IReadOnlyList<ArcReorderItem>>? OrderChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        _rowsHost = e.NameScope.Find<StackPanel>("PART_Rows");
        _live = e.NameScope.Find<TextBlock>("PART_Live");
        if (_live is not null)
        {
            _live.IsHitTestVisible = false;
            _live.Opacity = 0;
            _live.Height = 1;
            AutomationProperties.SetLiveSetting(_live, AutomationLiveSetting.Polite);
        }

        AutomationProperties.SetName(this, Label);
        LoadItems();
    }

    private void LoadItems()
    {
        if (_syncingItems || _rowsHost is null)
        {
            return;
        }

        _items.Clear();
        if (Items is not null)
        {
            _items.AddRange(Items);
        }

        Build(animate: false, null);
    }

    private void Build(bool animate, Dictionary<ArcReorderItem, int>? oldPositions)
    {
        if (_rowsHost is null)
        {
            return;
        }

        _rowsHost.Children.Clear();
        _rows.Clear();
        if (_items.Count == 0)
        {
            var empty = ArcContentUi.Body("No items to reorder.");
            empty.Margin = new Thickness(4, 12);
            _rowsHost.Children.Add(empty);
            return;
        }

        for (var i = 0; i < _items.Count; i++)
        {
            var item = _items[i];
            var index = i;
            var handle = new ArcButton
            {
                Content = ArcContentUi.Icon("grip-vertical", 17, ArcTokens.TextSecondary),
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                Width = 36,
                Height = 36,
                MinHeight = 36,
                Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = !item.Disabled,
                Cursor = item.Disabled ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand)
            };
            AutomationProperties.SetName(handle, BuildHandleName(item, i));
            ToolTip.SetTip(handle, item.Disabled
                ? $"{item.Title} is disabled"
                : $"Reorder {item.Title}. Position {i + 1} of {_items.Count}. Use arrow keys or drag.");

            var frame = BuildFrame(item, handle, i);
            var row = new RowVisual { Item = item, Frame = frame, Handle = handle, Index = i };
            _rows.Add(row);

            handle.KeyDown += (_, args) => OnHandleKeyDown(row, args);
            handle.PointerPressed += (_, args) => BeginDrag(row, args);
            handle.PointerMoved += (_, args) => ContinueDrag(row, args);
            handle.PointerReleased += (_, args) => EndDrag(args);
            handle.PointerCaptureLost += (_, _) => CancelDrag();

            if (!ArcThemeManager.ReducedMotion)
            {
                frame.Transitions = new Transitions
                {
                    new TransformOperationsTransition
                    {
                        Property = Visual.RenderTransformProperty,
                        Duration = ArcMotion.SpringMorphDuration,
                        Easing = ArcMotion.SpringMorph
                    },
                    new BrushTransition
                    {
                        Property = Border.BorderBrushProperty,
                        Duration = ArcMotion.Fast
                    }
                };
            }

            _rowsHost.Children.Add(frame);
        }

        if (animate && oldPositions is not null)
        {
            foreach (var row in _rows)
            {
                if (oldPositions.TryGetValue(row.Item, out var oldIndex) && oldIndex != row.Index)
                {
                    row.Frame.RenderTransform = Translate((oldIndex - row.Index) * Step);
                }
            }

            Dispatcher.UIThread.Post(() =>
            {
                foreach (var row in _rows)
                {
                    if (row.Frame.RenderTransform is not null)
                    {
                        row.Frame.RenderTransform = Translate(0);
                    }
                }
            }, DispatcherPriority.Loaded);
        }

        var focusId = _pendingFocusId ?? (_lifted ? _liftId : null);
        _pendingFocusId = null;
        if (focusId is not null)
        {
            Dispatcher.UIThread.Post(() => FocusHandle(focusId), DispatcherPriority.Loaded);
        }
    }

    private Border BuildFrame(ArcReorderItem item, ArcButton handle, int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("36,30,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        grid.Children.Add(handle);

        var position = ArcContentUi.Numeric((index + 1).ToString(CultureInfo.InvariantCulture), "arc-caption");
        position.FontSize = 12;
        position.HorizontalAlignment = HorizontalAlignment.Left;
        position.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        Grid.SetColumn(position, 1);
        grid.Children.Add(position);

        var content = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var title = ArcContentUi.Text(item.Title, "arc-label");
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.Paint(TextBlock.ForegroundProperty, item.Disabled ? ArcTokens.TextMuted : ArcTokens.Foreground);
        content.Children.Add(title);
        if (!string.IsNullOrWhiteSpace(item.Detail))
        {
            var detail = ArcContentUi.Caption(item.Detail!);
            detail.TextTrimming = TextTrimming.CharacterEllipsis;
            content.Children.Add(detail);
        }

        Grid.SetColumn(content, 2);
        grid.Children.Add(content);

        if (item.Disabled)
        {
            var badge = new ArcBadge { Content = "Disabled", Tone = ArcBadgeTone.Neutral };
            badge.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(badge, 3);
            grid.Children.Add(badge);
        }

        var frame = new Border
        {
            Height = RowHeight,
            Padding = new Thickness(10, 0, 14, 0),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            Background = ArcContentUi.Brush(ArcTokens.Surface),
            Child = grid,
            Cursor = item.Disabled ? new Cursor(StandardCursorType.Arrow) : new Cursor(StandardCursorType.Hand),
            RenderTransformOrigin = RelativePoint.Center
        };
        frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        if (item.Disabled)
        {
            frame.Opacity = 0.55;
        }

        AutomationProperties.SetName(frame,
            $"{item.Title}, position {index + 1} of {_items.Count}{(item.Disabled ? ", disabled" : string.Empty)}");

        frame.PointerEntered += (_, _) =>
        {
            if (!ReferenceEquals(_dragRow?.Frame, frame))
            {
                frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
                if (!item.Disabled)
                {
                    frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
                }
            }
        };
        frame.PointerExited += (_, _) =>
        {
            if (!ReferenceEquals(_dragRow?.Frame, frame))
            {
                frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
                frame.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            }
        };

        return frame;
    }

    private string BuildHandleName(ArcReorderItem item, int index) =>
        $"Reorder {item.Title}. Position {index + 1} of {_items.Count}. Use arrow keys.";

    private void BeginDrag(RowVisual row, PointerPressedEventArgs e)
    {
        if (row.Item.Disabled || _dragRow is not null || e.GetCurrentPoint(row.Handle).Properties.IsRightButtonPressed)
        {
            return;
        }

        _dragRow = row;
        _dragFrom = row.Index;
        _dragTarget = row.Index;
        _dragStart = e.GetPosition(_rowsHost);
        e.Pointer.Capture(row.Handle);
        row.Frame.ZIndex = 10;
        row.Frame.BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating);
        row.Frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceRaised);
        row.Frame.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
        row.Frame.Opacity = 1;

        foreach (var item in _rows)
        {
            item.Frame.Transitions = null;
        }

        e.Handled = true;
    }

    private void ContinueDrag(RowVisual row, PointerEventArgs e)
    {
        if (_dragRow is null || !ReferenceEquals(row, _dragRow) || !e.GetCurrentPoint(row.Handle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var point = e.GetPosition(_rowsHost);
        var delta = point.Y - _dragStart.Y;
        var raw = (int)Math.Round(_dragFrom + delta / Step, MidpointRounding.AwayFromZero);
        raw = Math.Clamp(raw, 0, _items.Count - 1);
        var target = NearestEnabled(raw, _dragFrom);
        _dragTarget = target;

        row.Frame.RenderTransform = Translate(delta);
        for (var i = 0; i < _rows.Count; i++)
        {
            if (i == _dragFrom)
            {
                continue;
            }

            var display = i;
            if (_dragFrom < target && i > _dragFrom && i <= target)
            {
                display = i - 1;
            }
            else if (_dragFrom > target && i >= target && i < _dragFrom)
            {
                display = i + 1;
            }

            _rows[i].Frame.RenderTransform = Translate((display - i) * Step);
        }

        e.Handled = true;
    }

    private int NearestEnabled(int target, int exclude)
    {
        if (!_items[target].Disabled)
        {
            return target;
        }

        for (var distance = 1; distance < _items.Count; distance++)
        {
            var above = target - distance;
            var below = target + distance;
            if (above >= 0 && !_items[above].Disabled)
            {
                return above;
            }

            if (below < _items.Count && !_items[below].Disabled)
            {
                return below;
            }
        }

        return exclude;
    }

    private void EndDrag(PointerReleasedEventArgs e)
    {
        if (_dragRow is null)
        {
            return;
        }

        var from = _dragFrom;
        var to = _dragTarget;
        var item = _dragRow.Item;
        // Releasing the pointer ends the capture; clearing it explicitly would raise CaptureLost and CancelDrag.
        ResetDragVisuals();

        if (from != to)
        {
            CommitMove(from, to, raise: true, focusId: item.Id);
        }
        else
        {
            Build(animate: false, null);
            FocusHandle(item.Id);
        }

        e.Handled = true;
    }

    private void CancelDrag()
    {
        if (_dragRow is null)
        {
            return;
        }

        var item = _dragRow.Item;
        ResetDragVisuals();
        Build(animate: false, null);
        FocusHandle(item.Id);
    }

    private void ResetDragVisuals()
    {
        if (_dragRow is not null)
        {
            _dragRow.Frame.ZIndex = 0;
            _dragRow.Frame.BoxShadow = default;
            _dragRow.Frame.Opacity = _dragRow.Item.Disabled ? 0.55 : 1;
            _dragRow.Frame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
            _dragRow.Frame.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        }

        _dragRow = null;
        foreach (var row in _rows)
        {
            row.Frame.RenderTransform = Translate(0);
        }
    }

    private void CommitMove(int from, int to, bool raise, string? focusId, bool rebuild = true)
    {
        if (from == to || from < 0 || to < 0 || from >= _items.Count || to >= _items.Count)
        {
            return;
        }

        var oldPositions = _items.Select((item, index) => (item, index)).ToDictionary(pair => pair.item, pair => pair.index);
        var moved = _items[from];
        _items.RemoveAt(from);
        _items.Insert(to, moved);

        _syncingItems = true;
        SetCurrentValue(ItemsProperty, _items.ToList());
        _syncingItems = false;

        if (rebuild)
        {
            _pendingFocusId = focusId;
            Build(animate: true, oldPositions);
        }

        Announce($"{moved.Title} moved to position {to + 1} of {_items.Count}");
        if (raise)
        {
            OrderChanged?.Invoke(this, _items.ToList());
        }
    }

    private void OnHandleKeyDown(RowVisual row, KeyEventArgs e)
    {
        if (row.Item.Disabled)
        {
            return;
        }

        var plainArrow = e.Key is Key.Up or Key.Down &&
                         !e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                         !e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        var altArrow = e.Key is Key.Up or Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt);
        if (plainArrow || altArrow)
        {
            var target = e.Key == Key.Up ? row.Index - 1 : row.Index + 1;
            target = NearestEnabled(Math.Clamp(target, 0, _items.Count - 1), row.Index);
            if (target != row.Index)
            {
                if (_lifted)
                {
                    CommitMove(row.Index, target, raise: false, focusId: row.Item.Id);
                }
                else
                {
                    CommitMove(row.Index, target, raise: true, focusId: row.Item.Id);
                }
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space)
        {
            if (!_lifted)
            {
                _lifted = true;
                _liftId = row.Item.Id;
                _liftOrigin = row.Index;
                row.Frame.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
                Announce($"{row.Item.Title} lifted. Use arrow keys to move, Space to drop, Escape to cancel.");
                AutomationProperties.SetHelpText(row.Handle,
                    "Lifted. Use arrow keys to move, Space to drop, Escape to cancel.");
            }
            else
            {
                var id = _liftId ?? row.Item.Id;
                var index = _items.FindIndex(item => item.Id == id);
                _lifted = false;
                _liftId = null;
                AutomationProperties.SetHelpText(row.Handle, null);
                _pendingFocusId = id;
                Build(animate: false, null);
                OrderChanged?.Invoke(this, _items.ToList());
                Announce($"{row.Item.Title} dropped at position {index + 1} of {_items.Count}");
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _lifted)
        {
            var id = _liftId ?? row.Item.Id;
            var current = _items.FindIndex(item => item.Id == id);
            if (current >= 0 && current != _liftOrigin)
            {
                _lifted = false;
                CommitMove(current, _liftOrigin, raise: false, focusId: id);
            }
            else
            {
                _pendingFocusId = id;
                Build(animate: false, null);
            }

            _lifted = false;
            _liftId = null;
            Announce("Move cancelled");
            e.Handled = true;
        }
    }

    private void FocusHandle(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        var row = _rows.FirstOrDefault(item => item.Item.Id == id);
        row?.Handle.Focus();
    }

    private void Announce(string message)
    {
        if (_live is not null)
        {
            _live.Text = message;
        }

        AutomationProperties.SetHelpText(this, message);
    }

    private static TransformOperations Translate(double y) =>
        TransformOperations.Parse(string.Create(CultureInfo.InvariantCulture, $"translateY({y}px)"));
}
