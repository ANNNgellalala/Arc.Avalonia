using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>One pane and the divider that follows it, with its size constraints.</summary>
public sealed class ArcPaneSlot
{
    public Control? Pane { get; set; }

    public Control? Divider { get; set; }

    /// <summary>Share of the group; shares are relative and need not sum to 100.</summary>
    public double Share { get; set; } = 50;

    public double MinPx { get; set; } = 80;

    public double? MaxPx { get; set; }

    public bool Collapsed { get; set; }
}

/// <summary>
/// Lays the panes out along one axis with a fixed divider between each pair. Shares are percentages so a
/// container resize keeps the proportions; minimum and maximum sizes are enforced in pixels, and a
/// collapsed pane takes no space while its divider stays reachable.
/// </summary>
public sealed class ArcPanePanel : Panel
{
    private double[] _sizes = Array.Empty<double>();

    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    public double DividerThickness { get; set; } = 12;

    public List<ArcPaneSlot> Slots { get; } = new();

    public bool IsHorizontal => Orientation == global::Avalonia.Layout.Orientation.Horizontal;

    /// <summary>Length of the group along the split axis.</summary>
    public double Length => IsHorizontal ? Bounds.Width : Bounds.Height;

    /// <summary>Space available to panes, excluding the dividers.</summary>
    public double AvailableLength =>
        Math.Max(1, Length - DividerThickness * Math.Max(0, Slots.Count - 1));

    public double GetPaneLength(int index) =>
        index >= 0 && index < _sizes.Length ? _sizes[index] : 0;

    protected override Size MeasureOverride(Size availableSize)
    {
        var cross = IsHorizontal ? availableSize.Height : availableSize.Width;
        if (!double.IsFinite(cross) || cross <= 0)
        {
            cross = 220;
        }

        var axis = IsHorizontal ? availableSize.Width : availableSize.Height;
        var dividerConstraint = IsHorizontal
            ? new Size(DividerThickness, cross)
            : new Size(cross, DividerThickness);

        var wanted = DividerThickness * Math.Max(0, Slots.Count - 1);
        foreach (var slot in Slots)
        {
            wanted += Math.Max(slot.MinPx, slot.Collapsed ? 0 : 60);
            slot.Divider?.Measure(dividerConstraint);
        }

        if (!double.IsFinite(axis) || axis <= 0)
        {
            foreach (var slot in Slots)
            {
                slot.Pane?.Measure(IsHorizontal ? new Size(wanted / Math.Max(1, Slots.Count), cross) : new Size(cross, wanted / Math.Max(1, Slots.Count)));
            }

            return IsHorizontal ? new Size(wanted, cross) : new Size(cross, wanted);
        }

        // Measure each pane at the size it will actually get, so text wraps to its own column rather
        // than to the width of the whole group and then clipping.
        var available = Math.Max(1, axis - DividerThickness * Math.Max(0, Slots.Count - 1));
        _sizes = ComputeSizes(available);
        _measuredAvailable = available;
        for (var index = 0; index < Slots.Count; index++)
        {
            var slot = Slots[index];
            var size = _sizes[index];
            slot.Pane?.Measure(IsHorizontal ? new Size(size, cross) : new Size(cross, size));
        }

        return IsHorizontal ? new Size(axis, cross) : new Size(cross, axis);
    }

    private double _measuredAvailable = -1;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var available = IsHorizontal
            ? Math.Max(1, finalSize.Width - DividerThickness * Math.Max(0, Slots.Count - 1))
            : Math.Max(1, finalSize.Height - DividerThickness * Math.Max(0, Slots.Count - 1));

        if (Math.Abs(available - _measuredAvailable) > 0.5)
        {
            _sizes = ComputeSizes(available);
            _measuredAvailable = available;
            var cross = IsHorizontal ? finalSize.Height : finalSize.Width;
            for (var index = 0; index < Slots.Count; index++)
            {
                var size = _sizes[index];
                Slots[index].Pane?.Measure(IsHorizontal ? new Size(size, cross) : new Size(cross, size));
            }
        }

        var offset = 0.0;

        for (var index = 0; index < Slots.Count; index++)
        {
            var slot = Slots[index];
            var size = _sizes[index];
            var paneRect = IsHorizontal
                ? new Rect(offset, 0, size, finalSize.Height)
                : new Rect(0, offset, finalSize.Width, size);

            if (slot.Pane is not null)
            {
                slot.Pane.Arrange(paneRect);
                slot.Pane.IsVisible = size > 0.5;
                var fade = slot.MinPx > 0 ? Math.Clamp(size / slot.MinPx, 0.32, 1) : 1;
                slot.Pane.Opacity = slot.Collapsed ? 0 : fade;
            }

            offset += size;

            if (slot.Divider is null)
            {
                continue;
            }

            var dividerRect = IsHorizontal
                ? new Rect(offset, 0, DividerThickness, finalSize.Height)
                : new Rect(0, offset, finalSize.Width, DividerThickness);
            slot.Divider.Arrange(dividerRect);
            slot.Divider.IsVisible = true;
            offset += DividerThickness;
        }

        return finalSize;
    }

    /// <summary>Size for each pane from its share, clamped to its pixel limits with the slack redistributed.</summary>
    private double[] ComputeSizes(double available)
    {
        var count = Slots.Count;
        var sizes = new double[count];
        var active = new List<int>();
        var totalShare = 0.0;
        for (var index = 0; index < count; index++)
        {
            var slot = Slots[index];
            if (slot.Collapsed)
            {
                continue;
            }

            active.Add(index);
            totalShare += Math.Max(0, slot.Share);
        }

        if (active.Count == 0 || totalShare <= 0)
        {
            return sizes;
        }

        foreach (var index in active)
        {
            sizes[index] = Math.Max(0, Slots[index].Share) / totalShare * available;
        }

        for (var pass = 0; pass < 3; pass++)
        {
            var used = 0.0;
            var flexible = new List<int>();
            for (var index = 0; index < count; index++)
            {
                if (Slots[index].Collapsed)
                {
                    sizes[index] = 0;
                    continue;
                }

                var slot = Slots[index];
                var min = Math.Max(0, slot.MinPx);
                var max = Math.Max(min, slot.MaxPx ?? double.MaxValue);
                sizes[index] = Math.Clamp(sizes[index], min, max);
                used += sizes[index];
                if (sizes[index] < max - 0.5)
                {
                    flexible.Add(index);
                }
            }

            var leftover = available - used;
            if (Math.Abs(leftover) < 0.5 || flexible.Count == 0)
            {
                break;
            }

            var flexibleTotal = flexible.Sum(index => sizes[index]);
            foreach (var index in flexible)
            {
                sizes[index] += flexibleTotal > 0.5
                    ? leftover * sizes[index] / flexibleTotal
                    : leftover / flexible.Count;
            }
        }

        return sizes;
    }

    /// <summary>Invalidates the layout after shares, limits or collapse states change.</summary>
    public void Refresh() => InvalidateMeasure();
}

/// <summary>
/// Arc's resizable panels: two or more panes whose dividers can be dragged, nudged with the arrow keys,
/// collapsed with Enter and reset with a double click. Dividers are real focus stops with an accessible
/// name that reports the current share.
/// </summary>
public class ArcResizablePanels : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcResizablePane>?> PanesProperty =
        AvaloniaProperty.Register<ArcResizablePanels, IList<ArcResizablePane>?>(nameof(Panes));

    public static readonly StyledProperty<ArcPanelOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcResizablePanels, ArcPanelOrientation>(nameof(Orientation));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcResizablePanels, string?>(nameof(Label));

    public static readonly StyledProperty<bool> CollapseOnDoubleClickProperty =
        AvaloniaProperty.Register<ArcResizablePanels, bool>(nameof(CollapseOnDoubleClick));

    private ArcPanePanel? _stage;
    private TextBlock? _status;
    private readonly List<ArcPaneSlot> _slots = new();
    private int _dragIndex = -1;
    private double _dragStartPointer;
    private double[] _dragStartShares = Array.Empty<double>();
    private bool _dragging;

    static ArcResizablePanels()
    {
        PanesProperty.Changed.AddClassHandler<ArcResizablePanels>((panels, _) => panels.Build());
        OrientationProperty.Changed.AddClassHandler<ArcResizablePanels>((panels, _) => panels.Build());
        LabelProperty.Changed.AddClassHandler<ArcResizablePanels>((panels, _) => panels.UpdateAutomation());
    }

    public ArcResizablePanels()
    {
        LayoutUpdated += (_, _) => UpdateDividerState();
    }

    /// <summary>Panes in order, each with its size constraints.</summary>
    public IList<ArcResizablePane>? Panes
    {
        get => GetValue(PanesProperty);
        set => SetValue(PanesProperty, value);
    }

    public ArcPanelOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Names the group.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// When true a double click collapses or restores a collapsible neighbour; otherwise it resets the
    /// layout to the default shares, which is Arc's documented double-click behaviour.
    /// </summary>
    public bool CollapseOnDoubleClick
    {
        get => GetValue(CollapseOnDoubleClickProperty);
        set => SetValue(CollapseOnDoubleClickProperty, value);
    }

    /// <summary>Raised with each pane's share in percent after a resize settles.</summary>
    public event EventHandler<IReadOnlyList<double>>? LayoutChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _stage = e.NameScope.Find<ArcPanePanel>("PART_Stage");
        _status = e.NameScope.Find<TextBlock>("Status");
        Build();
        UpdateAutomation();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Resizable panels" : Label);
    }

    private void Build()
    {
        if (_stage is null)
        {
            return;
        }

        _stage.Children.Clear();
        _stage.Slots.Clear();
        _slots.Clear();
        _stage.Orientation = Horizontal ? global::Avalonia.Layout.Orientation.Horizontal : global::Avalonia.Layout.Orientation.Vertical;
        _stage.DividerThickness = 12;

        var panes = Panes?.ToList() ?? new List<ArcResizablePane>();
        for (var index = 0; index < panes.Count; index++)
        {
            var pane = panes[index];
            var host = new Border
            {
                Padding = new Thickness(0),
                ClipToBounds = true
            };
            host.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            host.Child = BuildPaneContent(pane);

            var slot = new ArcPaneSlot
            {
                Pane = host,
                Share = Math.Max(1, pane.DefaultSize),
                MinPx = Math.Max(0, pane.MinSize),
                MaxPx = pane.MaxSize,
                Collapsed = pane.Collapsible && pane.DefaultCollapsed
            };

            if (index < panes.Count - 1)
            {
                slot.Divider = BuildDivider(index);
            }

            _slots.Add(slot);
            _stage.Slots.Add(slot);
            _stage.Children.Add(host);
            if (slot.Divider is not null)
            {
                _stage.Children.Add(slot.Divider);
            }
        }

        _stage.Refresh();
        UpdateDividerState();
    }

    private Control BuildPaneContent(ArcResizablePane pane)
    {
        if (pane.Content is Control custom)
        {
            return custom;
        }

        if (pane.Content is string text)
        {
            return new Border
            {
                Padding = new Thickness(16),
                Child = new TextBlock
                {
                    Text = text,
                    Classes = { "arc-body-sm", "arc-secondary" },
                    TextWrapping = TextWrapping.Wrap
                }
            };
        }

        var stack = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        stack.Children.Add(new TextBlock { Text = pane.Label, Classes = { "arc-label" } });
        stack.Children.Add(new TextBlock
        {
            Text = "Pane content",
            Classes = { "arc-caption" }
        });
        return stack;
    }

    private Control BuildDivider(int index)
    {
        var horizontal = Orientation == ArcPanelOrientation.Horizontal;
        var ring = new Border
        {
            BorderThickness = new Thickness(2),
            BorderBrush = ArcMediaUi.Brush(ArcTokens.FocusOutline),
            CornerRadius = new CornerRadius(6),
            IsVisible = false,
            IsHitTestVisible = false
        };
        var hairline = new Border
        {
            Background = ArcMediaUi.Brush(ArcTokens.Border),
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var grip = new Border
        {
            Background = ArcMediaUi.Brush(ArcTokens.BorderStrong),
            CornerRadius = new CornerRadius(9999),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false
        };
        if (horizontal)
        {
            hairline.Width = 1;
            hairline.VerticalAlignment = VerticalAlignment.Stretch;
            grip.Width = 4;
            grip.Height = 36;
        }
        else
        {
            hairline.Height = 1;
            hairline.HorizontalAlignment = HorizontalAlignment.Stretch;
            grip.Width = 36;
            grip.Height = 4;
        }

        var chevron = ArcMediaUi.Icon(horizontal ? "chevrons-left-right" : "chevrons-up-down", 12, ArcTokens.TextMuted);
        chevron.IsVisible = false;
        chevron.IsHitTestVisible = true;
        chevron.Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(chevron, "Show pane");
        AutomationProperties.SetName(chevron, "Show pane");
        chevron.PointerPressed += (_, args) =>
        {
            if (!chevron.IsVisible)
            {
                return;
            }

            ToggleNeighbourCollapse(index);
            args.Handled = true;
        };

        var divider = new Border
        {
            Background = Brushes.Transparent,
            Focusable = true,
            IsTabStop = true,
            ClipToBounds = false,
            Cursor = new Cursor(horizontal ? StandardCursorType.SizeWestEast : StandardCursorType.SizeNorthSouth),
            Child = new Panel { Children = { hairline, grip, chevron, ring } }
        };

        divider.PointerEntered += (_, _) => grip.Background = ArcMediaUi.Brush(ArcTokens.TextMuted);
        divider.PointerExited += (_, _) => grip.Background = ArcMediaUi.Brush(ArcTokens.BorderStrong);
        divider.PointerPressed += (_, args) => OnDividerPressed(index, args);
        divider.DoubleTapped += (_, args) => OnDividerDoubleTapped(index, args);
        divider.KeyDown += (_, args) => OnDividerKeyDown(index, args);
        divider.GotFocus += (_, args) =>
        {
            if (args.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional)
            {
                ring.IsVisible = true;
            }
        };
        divider.LostFocus += (_, _) => ring.IsVisible = false;
        AutomationProperties.SetHelpText(divider,
            "Drag to resize. Arrow keys nudge, Shift moves further, Enter collapses a collapsible pane, Home and End reach the limits.");
        divider.Tag = chevron;
        return divider;
    }

    private bool Horizontal => Orientation == ArcPanelOrientation.Horizontal;

    private double PointerAxis(PointerEventArgs args) =>
        Horizontal ? args.GetPosition(this).X : args.GetPosition(this).Y;

    private void OnDividerPressed(int index, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _stage is null)
        {
            return;
        }

        _dragIndex = index;
        _dragging = false;
        _dragStartPointer = PointerAxis(args);
        _dragStartShares = _slots.Select(slot => slot.Share).ToArray();
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragIndex < 0 || _stage is null || e.Pointer.Captured != this)
        {
            return;
        }

        var delta = PointerAxis(e) - _dragStartPointer;
        if (!_dragging && Math.Abs(delta) < 3)
        {
            return;
        }

        _dragging = true;
        ApplyPairDelta(_dragIndex, delta, fromStart: true);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragIndex < 0)
        {
            return;
        }

        var index = _dragIndex;
        _dragIndex = -1;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        SettleAfterDrag(index);
        e.Handled = true;
    }

    private void ApplyPairDelta(int index, double delta, bool fromStart)
    {
        if (_stage is null || index + 1 >= _slots.Count)
        {
            return;
        }

        var available = _stage.AvailableLength;
        var deltaShare = delta / available * 100;
        var first = fromStart ? _dragStartShares[index] : _slots[index].Share;
        var second = fromStart ? _dragStartShares[index + 1] : _slots[index + 1].Share;
        var total = first + second;
        var leftPane = Panes is not null && index < Panes.Count ? Panes[index] : null;
        var rightPane = Panes is not null && index + 1 < Panes.Count ? Panes[index + 1] : null;

        var minLeft = MinShare(leftPane, available);
        var minRight = MinShare(rightPane, available);
        var maxLeft = MaxShare(leftPane, available, total - minRight);

        var desired = first + deltaShare;
        if (leftPane is { Collapsible: true })
        {
            // Dragging well past a collapsible pane's minimum is allowed; it collapses on release.
            minLeft = 0;
        }
        else if (rightPane is { Collapsible: true })
        {
            minRight = 0;
        }

        var clamped = Math.Clamp(desired, minLeft, Math.Min(maxLeft, total - minRight));
        _slots[index].Share = Math.Max(0, clamped);
        _slots[index + 1].Share = Math.Max(0, total - _slots[index].Share);
        _stage.Refresh();
        UpdateDividerState();
    }

    private static double MinShare(ArcResizablePane? pane, double available) =>
        pane is null ? 0 : Math.Clamp(pane.MinSize / available * 100, 0, 95);

    private static double MaxShare(ArcResizablePane? pane, double available, double fallback)
    {
        if (pane?.MaxSize is not { } max || available <= 0)
        {
            return fallback;
        }

        return Math.Clamp(max / available * 100, 0, 96);
    }

    private void SettleAfterDrag(int index)
    {
        if (_stage is null)
        {
            return;
        }

        var available = _stage.AvailableLength;
        for (var side = 0; side < 2; side++)
        {
            var paneIndex = index + side;
            var pane = Panes is not null && paneIndex < Panes.Count ? Panes[paneIndex] : null;
            if (pane is null || !pane.Collapsible)
            {
                continue;
            }

            var share = _slots[paneIndex].Share;
            if (share / 100 * available < pane.MinSize * 0.5)
            {
                _slots[paneIndex].Collapsed = true;
                _slots[paneIndex].Share = Math.Max(1, pane.DefaultSize);
                Announce($"{pane.Label} collapsed");
            }
        }

        _stage.Refresh();
        UpdateDividerState();
        RaiseLayoutChanged();
    }

    private void OnDividerKeyDown(int index, KeyEventArgs e)
    {
        var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 64.0 : 16.0;
        var towardStart = Horizontal ? Key.Left : Key.Up;
        var towardEnd = Horizontal ? Key.Right : Key.Down;

        if (e.Key == towardStart)
        {
            ApplyPairDelta(index, -step, fromStart: false);
            RaiseLayoutChanged();
            e.Handled = true;
        }
        else if (e.Key == towardEnd)
        {
            ApplyPairDelta(index, step, fromStart: false);
            RaiseLayoutChanged();
            e.Handled = true;
        }
        else if (e.Key == Key.Home || e.Key == Key.End)
        {
            var direction = e.Key == Key.Home ? -1000.0 : 1000.0;
            ApplyPairDelta(index, direction, fromStart: false);
            RaiseLayoutChanged();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ToggleNeighbourCollapse(index);
            e.Handled = true;
        }
    }

    private void OnDividerDoubleTapped(int index, TappedEventArgs e)
    {
        if (CollapseOnDoubleClick)
        {
            ToggleNeighbourCollapse(index);
        }
        else
        {
            Reset();
        }

        e.Handled = true;
    }

    private void ToggleNeighbourCollapse(int index)
    {
        if (Panes is null)
        {
            return;
        }

        // A collapsed neighbour is the one the affordance is pointing at, so restore it first; with both
        // panes open the divider collapses the pane before it.
        var candidates = new[] { index, index + 1 };
        var target = -1;
        foreach (var paneIndex in candidates)
        {
            if (paneIndex < Panes.Count && Panes[paneIndex].Collapsible && _slots[paneIndex].Collapsed)
            {
                target = paneIndex;
                break;
            }
        }

        if (target < 0)
        {
            foreach (var paneIndex in candidates)
            {
                if (paneIndex < Panes.Count && Panes[paneIndex].Collapsible)
                {
                    target = paneIndex;
                    break;
                }
            }
        }

        if (target < 0)
        {
            return;
        }

        _slots[target].Collapsed = !_slots[target].Collapsed;
        _stage?.Refresh();
        UpdateDividerState();
        Announce(_slots[target].Collapsed
            ? $"{Panes[target].Label} collapsed"
            : $"{Panes[target].Label} expanded");
        RaiseLayoutChanged();
    }

    /// <summary>Restores every pane to its default share and expands collapsed panes.</summary>
    public void Reset()
    {
        if (Panes is null)
        {
            return;
        }

        for (var index = 0; index < _slots.Count && index < Panes.Count; index++)
        {
            _slots[index].Share = Math.Max(1, Panes[index].DefaultSize);
            _slots[index].Collapsed = Panes[index].Collapsible && Panes[index].DefaultCollapsed;
        }

        _stage?.Refresh();
        UpdateDividerState();
        RaiseLayoutChanged();
        Announce("Layout reset");
    }

    private void UpdateDividerState()
    {
        if (_stage is null || Panes is null)
        {
            return;
        }

        var total = _slots.Sum(slot => slot.Collapsed ? 0 : slot.Share);
        for (var index = 0; index < _slots.Count - 1; index++)
        {
            var divider = _slots[index].Divider;
            if (divider is null)
            {
                continue;
            }

            var pane = index < Panes.Count ? Panes[index] : null;
            var next = index + 1 < Panes.Count ? Panes[index + 1] : null;
            var share = total > 0 ? _slots[index].Share / total * 100 : 0;
            var name = $"Resize {pane?.Label ?? "pane"} and {next?.Label ?? "pane"}, {Math.Round(share)} percent";
            if (_slots[index].Collapsed)
            {
                name = $"{pane?.Label ?? "Pane"} is collapsed, press Enter to show it";
            }

            AutomationProperties.SetName(divider, name);

            if (divider.Tag is ArcIcon chevron)
            {
                chevron.IsVisible = _slots[index].Collapsed || _slots[index + 1].Collapsed;
            }
        }
    }

    private void RaiseLayoutChanged()
    {
        if (_stage is null)
        {
            return;
        }

        var available = _stage.AvailableLength;
        if (available <= 1)
        {
            return;
        }

        var total = _slots.Where(slot => !slot.Collapsed).Sum(slot => slot.Share);
        var shares = _slots
            .Select(slot => slot.Collapsed ? 0 : total > 0 ? slot.Share / total * 100 : 0)
            .ToList();
        LayoutChanged?.Invoke(this, shares);
    }

    private void Announce(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && _dragIndex >= 0 && _stage is not null)
        {
            for (var index = 0; index < _slots.Count && index < _dragStartShares.Length; index++)
            {
                _slots[index].Share = _dragStartShares[index];
            }

            _dragIndex = -1;
            _dragging = false;
            _stage.Refresh();
            UpdateDividerState();
            e.Handled = true;
        }
    }
}



