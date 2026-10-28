using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

public sealed class ArcGlassTabBarItem
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Lucide icon name, sized at 22px in the bar.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional count shown as an accent badge on the icon.</summary>
    public string? Badge { get; set; }
}

/// <summary>
/// A floating Liquid Glass tab bar: the selected tab sits under a glass lens that glides, lifts, stretches
/// along its travel and magnifies the icon beneath it, and can be dragged across the tabs. An optional
/// round action bubble (search) sits beside the bar, and the whole bar compacts to icons on scroll down.
///
/// Platform approximations: Avalonia has no backdrop filter, so the bar reads the content behind it as
/// layered translucency rather than a displacement filter; magnification is a per item scale driven by the
/// lens position; drag is a mouse drag on desktop. The lens glide uses one spring for position and derives
/// stretch and lift from its velocity. No Pro source was used.
/// </summary>
public sealed class ArcGlassTabBar : Panel
{
    public static readonly StyledProperty<IList<ArcGlassTabBarItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcGlassTabBar, IList<ArcGlassTabBarItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcGlassTabBar, string?>(nameof(Value));

    public static readonly StyledProperty<ArcGlassTabBarItem?> ActionProperty =
        AvaloniaProperty.Register<ArcGlassTabBar, ArcGlassTabBarItem?>(nameof(Action));

    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<ArcGlassTabBar, bool>(nameof(Compact));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcGlassTabBar, string>(nameof(Label), "Tabs");

    private const double ExpandedSlot = 78;
    private const double CompactSlot = 52;
    private const double BarHeight = 64;
    private const double CompactHeight = 52;

    private sealed class Tab
    {
        public ArcGlassTabBarItem Item = new();
        public ArcButton Button = null!;
        public ArcIcon Icon = null!;
        public TextBlock LabelBlock = null!;
        public Border? Badge;
    }

    private readonly Border _bar;
    private readonly Canvas _lensHost;
    private readonly Border _lens;
    private readonly Grid _slots;
    private readonly Border _actionShell;
    private readonly ArcButton _actionButton;
    private readonly List<Tab> _tabs = new();
    private readonly GlassSpring _lensX = new(0, 280);
    private readonly ArcFrameLoop _loop;
    private bool _dragging;
    private bool _dragged;
    private double _dragStartX;
    private double _dragStartLens;
    private double _lastX;
    private ulong _lastTime;
    private double _velocity;
    private double _cell = ExpandedSlot;
    private double _activeLensWidth = 60;
    private bool _built;

    static ArcGlassTabBar()
    {
        ItemsProperty.Changed.AddClassHandler<ArcGlassTabBar>((bar, _) => bar.Rebuild());
        ActionProperty.Changed.AddClassHandler<ArcGlassTabBar>((bar, _) => bar.Rebuild());
        ValueProperty.Changed.AddClassHandler<ArcGlassTabBar>((bar, _) => bar.SyncValue(animate: true));
        CompactProperty.Changed.AddClassHandler<ArcGlassTabBar>((bar, _) => bar.ApplyCompact(animate: true));
        LabelProperty.Changed.AddClassHandler<ArcGlassTabBar>((bar, _) => bar.UpdateLabel());
    }

    public ArcGlassTabBar()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Left;

        _lensHost = new Canvas { ClipToBounds = false, IsHitTestVisible = false };
        _lens = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Background = ArcGlass.Gradient(
                180,
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.34), 0),
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.18), 1)),
            BorderBrush = ArcGlass.Gradient(
                180,
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.AccentStrong, 0.55), 0),
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.AccentStrong, 0.24), 1)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowRaised),
            Height = BarHeight - 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        _lensHost.Children.Add(_lens);

        _slots = new Grid { ClipToBounds = false };
        _bar = ArcGlass.Panel(9999, new Thickness(4), floating: true, fillAlpha: 0.62);
        _bar.CornerRadius = new CornerRadius(32);
        _bar.Height = BarHeight;
        _bar.Child = new Panel { Children = { _lensHost, _slots } };

        _actionButton = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Width = 56,
            Height = 56,
            MinHeight = 56,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(28),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = ArcGlass.Icon("search", 22, ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)))
        };
        _actionButton.Classes.Add("sg-plain");
        _actionButton.Click += (_, _) => SelectAction();

        _actionShell = ArcGlass.Panel(9999, new Thickness(0), floating: true, fillAlpha: 0.62);
        _actionShell.Width = 56;
        _actionShell.Height = 56;
        _actionShell.Child = _actionButton;
        _actionShell.VerticalAlignment = VerticalAlignment.Bottom;
        _actionShell.Margin = new Thickness(10, 0, 0, 0);
        _actionShell.IsVisible = false;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { _bar, _actionShell }
        };
        Children.Add(row);

        _loop = new ArcFrameLoop(OnFrame);
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) =>
        {
            _dragging = false;
            _dragged = false;
        };
        UpdateLabel();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<bool>? CompactChanged;

    public IList<ArcGlassTabBarItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>A separate round glass tab beside the bar, such as search. It takes part in selection.</summary>
    public ArcGlassTabBarItem? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    /// <summary>Shrinks the bar to a pill of icons; the glass tab bar follows scroll down.</summary>
    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    /// <summary>Accessible name of the navigation.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, Label);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _loop.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private List<ArcGlassTabBarItem> ItemList() => Items?.ToList() ?? new List<ArcGlassTabBarItem>();

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        _tabs.Clear();
        _slots.Children.Clear();
        var items = ItemList();
        _slots.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", Math.Max(1, items.Count))));

        for (var i = 0; i < items.Count; i++)
        {
            BuildTab(items[i], i);
        }

        if (Action is { } action)
        {
            _actionShell.IsVisible = true;
            _actionButton.Content = ArcGlass.Icon(
                string.IsNullOrWhiteSpace(action.Icon) ? "search" : action.Icon,
                22,
                ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)));
            AutomationProperties.SetName(_actionButton, action.Label);
            AutomationProperties.SetHelpText(_actionButton, "Action tab. Use the arrow keys to reach it.");
        }
        else
        {
            _actionShell.IsVisible = false;
        }

        _built = true;
        _bar.Width = BarWidth();
        ApplyCompact(animate: false);
        SnapLens();
        SyncValue(animate: false);
    }

    private void BuildTab(ArcGlassTabBarItem item, int index)
    {
        var icon = ArcGlass.Icon(
            string.IsNullOrWhiteSpace(item.Icon) ? "circle-dot" : item.Icon,
            22,
            ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary)));
        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = 10,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };

        Border? badge = null;
        if (!string.IsNullOrWhiteSpace(item.Badge) && item.Badge != "0")
        {
            badge = new Border
            {
                CornerRadius = new CornerRadius(9999),
                MinWidth = 15,
                Height = 15,
                Padding = new Thickness(4, 0),
                Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Accent)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20, 0, 0, 0),
                Child = new TextBlock
                {
                    Text = item.Badge,
                    FontSize = 9,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentForeground))
                }
            };
        }

        var iconLayer = new Panel { Height = 26, Children = { icon } };
        if (badge is not null)
        {
            iconLayer.Children.Add(badge);
        }

        var content = new StackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { iconLayer, label }
        };

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Background = Brushes.Transparent,
            Padding = new Thickness(4, 0),
            Margin = new Thickness(3, 0),
            Height = BarHeight - 12,
            MinHeight = BarHeight - 12,
            CornerRadius = new CornerRadius(9999),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = content
        };
        button.Classes.Add("sg-plain");
        Grid.SetColumn(button, index);
        AutomationProperties.SetName(button, item.Label + (badge is not null ? $", {item.Badge} new" : string.Empty));
        AutomationProperties.SetHelpText(button, "Tab. Use the arrow keys to change tabs.");
        button.Click += (_, _) =>
        {
            if (_dragged)
            {
                _dragged = false;
                return;
            }

            Select(index);
        };
        button.GotFocus += (_, _) =>
        {
            if (!_dragging)
            {
                MoveLensTo(index);
            }
        };
        button.KeyDown += (_, e) => OnTabKeyDown(index, e);
        button.PointerPressed += (_, e) => OnTabPressed(index, e);

        _tabs.Add(new Tab { Item = item, Button = button, Icon = icon, LabelBlock = label, Badge = badge });
        _slots.Children.Add(button);
    }

    private void OnTabPressed(int index, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || index != SelectedIndex())
        {
            return;
        }

        _dragging = true;
        _dragged = false;
        _dragStartX = e.GetPosition(this).X;
        _dragStartLens = _lensX.Value;
        _lastX = _dragStartX;
        _lastTime = e.Timestamp;
        _velocity = 0;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var x = e.GetPosition(this).X;
        if (Math.Abs(x - _dragStartX) > 4)
        {
            _dragged = true;
        }

        if (!_dragged)
        {
            return;
        }

        var max = Math.Max(0, _bar.Bounds.Width - _activeLensWidth);
        _lensX.Jump(Math.Clamp(_dragStartLens + (x - _dragStartX), 0, max));
        ApplyLens();

        var delta = e.Timestamp - _lastTime;
        if (delta > 0)
        {
            _velocity = (x - _lastX) / delta;
        }

        _lastX = x;
        _lastTime = e.Timestamp;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        if (!_dragged)
        {
            return;
        }

        var centre = _lensX.Value + (_activeLensWidth / 2) + (_velocity * 140);
        var index = (int)Math.Round((centre - 6 - (_cell / 2)) / _cell);
        index = Math.Clamp(index, 0, Math.Max(0, _tabs.Count - 1));
        Select(index);
    }

    private void OnTabKeyDown(int index, KeyEventArgs e)
    {
        var count = _tabs.Count;
        if (count == 0)
        {
            return;
        }

        var next = index;
        switch (e.Key)
        {
            case Key.Left:
                next = (index - 1 + count) % count;
                break;
            case Key.Right:
                next = (index + 1) % count;
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = count - 1;
                break;
            default:
                return;
        }

        e.Handled = true;
        if (Compact)
        {
            SetCurrentValue(CompactProperty, false);
        }

        Select(next);
        if (next < _tabs.Count)
        {
            _tabs[next].Button.Focus();
        }
    }

    private int SelectedIndex()
    {
        var items = ItemList();
        for (var i = 0; i < items.Count; i++)
        {
            if (string.Equals(items[i].Value, Value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void Select(int index)
    {
        var items = ItemList();
        if (index < 0 || index >= items.Count)
        {
            return;
        }

        SetCurrentValue(ValueProperty, items[index].Value);
        MoveLensTo(index);
        ValueChanged?.Invoke(this, items[index].Value);
    }

    private void SelectAction()
    {
        if (Action is not { } action)
        {
            return;
        }

        SetCurrentValue(ValueProperty, action.Value);
        HideLens();
        ValueChanged?.Invoke(this, action.Value);
    }

    private void SyncValue(bool animate)
    {
        if (!_built)
        {
            return;
        }

        var index = SelectedIndex();
        if (index < 0)
        {
            HideLens();
            return;
        }

        _lens.IsVisible = true;
        UpdateFaces(index);
        UpdateRovingTabStop(index);
        if (animate)
        {
            MoveLensTo(index);
        }
        else
        {
            SnapLens();
        }
    }

    private void UpdateFaces(int selected)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            var active = i == selected;
            _tabs[i].LabelBlock.Foreground = ArcGlass.Solid(
                ArcGlass.Color(active ? ArcTokens.AccentForeground : ArcTokens.Foreground));
            _tabs[i].LabelBlock.Opacity = active ? 1 : 0.78;
            _tabs[i].Icon.Foreground = ArcGlass.Solid(active
                ? ArcGlass.Color(ArcTokens.AccentForeground)
                : ArcGlass.ArcAlpha(ArcTokens.Foreground, 0.78));
            AutomationProperties.SetItemStatus(_tabs[i].Button, active ? "Selected" : string.Empty);
        }
    }

    private void UpdateRovingTabStop(int selected)
    {
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Button.IsTabStop = i == selected;
        }
    }

    private void HideLens()
    {
        _lens.IsVisible = false;
        for (var i = 0; i < _tabs.Count; i++)
        {
            _tabs[i].Button.IsTabStop = i == 0;
        }
    }

    private double BarWidth()
    {
        var count = Math.Max(1, ItemList().Count);
        return (count * (Compact ? CompactSlot : ExpandedSlot)) + 12;
    }

    private void SnapLens()
    {
        var index = SelectedIndex();
        if (index < 0)
        {
            return;
        }

        _cell = Math.Max(1, ((_bar.Width > 0 ? _bar.Width : BarWidth()) - 12) / Math.Max(1, ItemList().Count));
        _activeLensWidth = Math.Max(28, _cell - 12);
        _lensX.Jump(6 + (index * _cell) + ((_cell - _activeLensWidth) / 2));
        ApplyLens();
    }

    private void MoveLensTo(int index)
    {
        var items = ItemList();
        if (items.Count == 0)
        {
            return;
        }

        _cell = Math.Max(1, ((_bar.Width > 0 ? _bar.Width : BarWidth()) - 12) / items.Count);
        var targetWidth = Math.Max(28, _cell - 12);
        var targetX = 6 + (index * _cell) + ((_cell - targetWidth) / 2);
        _activeLensWidth = targetWidth;
        _lens.IsVisible = true;

        if (ArcThemeManager.ReducedMotion)
        {
            _lensX.Jump(targetX);
            ApplyLens();
            return;
        }

        _lensX.Target = targetX;
        _loop.Start();
    }

    private void OnFrame(double dt)
    {
        _lensX.Update(dt);
        ApplyLens();
        UpdateMagnification();

        if (!_dragging && _lensX.Settled)
        {
            _loop.Stop();
        }
    }

    private void ApplyLens()
    {
        var velocity = Math.Abs(_lensX.Velocity);
        var stretch = Math.Clamp(velocity * 0.02, 0, 10);
        var lift = Math.Clamp(velocity * 0.008, 0, 4);
        var height = Math.Max(20, BarHeight - 14 - lift);
        _lens.Width = _activeLensWidth + stretch;
        _lens.Height = height;
        _lens.CornerRadius = new CornerRadius(9999);
        Canvas.SetLeft(_lens, _lensX.Value - (stretch / 2));
        Canvas.SetTop(_lens, (BarHeight - height) / 2);
        if (!ArcThemeManager.ReducedMotion)
        {
            _lens.Opacity = 0.85 + Math.Clamp(1 - (velocity * 0.004), 0, 0.15);
        }
    }

    /// <summary>Each tab's icon grows and its label brightens near the lens, so the accent glides with it.</summary>
    private void UpdateMagnification()
    {
        var lensCentre = _lensX.Value + (_activeLensWidth / 2);
        for (var i = 0; i < _tabs.Count; i++)
        {
            var centre = 6 + (i * _cell) + (_cell / 2);
            var distance = Math.Abs(centre - lensCentre);
            var closeness = Math.Clamp(1 - (distance / Math.Max(1, _cell * 1.4)), 0, 1);
            var scale = 1 + (0.30 * closeness);
            _tabs[i].Icon.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            _tabs[i].Icon.RenderTransform = TransformOperations.Parse($"scale({scale:0.###})");
            if (!Compact)
            {
                _tabs[i].LabelBlock.Opacity = 0.75 + (0.25 * closeness);
            }
        }
    }

    private void ApplyCompact(bool animate)
    {
        if (!_built)
        {
            return;
        }

        var targetWidth = BarWidth();
        var targetHeight = Compact ? CompactHeight : BarHeight;
        var labelTarget = Compact ? 0 : 1;

        void SetVisuals(double width, double height, double labelOpacity)
        {
            _bar.Width = width;
            _bar.Height = height;
            _actionShell.Width = height - 8;
            _actionShell.Height = height - 8;
            _actionButton.Width = height - 8;
            _actionButton.Height = height - 8;
            _actionButton.CornerRadius = new CornerRadius((height - 8) / 2);
            foreach (var tab in _tabs)
            {
                tab.Button.Height = height - 12;
                tab.Button.MinHeight = height - 12;
                tab.LabelBlock.Opacity = labelOpacity;
                tab.LabelBlock.IsVisible = labelOpacity > 0.05;
            }

            _cell = Math.Max(1, (width - 12) / Math.Max(1, _tabs.Count));
            _activeLensWidth = Math.Max(24, _cell - 12);
            if (!_dragging)
            {
                SnapLens();
            }

            UpdateMagnification();
        }

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            SetVisuals(targetWidth, targetHeight, labelTarget);
            CompactChanged?.Invoke(this, Compact);
            return;
        }

        var fromWidth = _bar.Width > 0 ? _bar.Width : targetWidth;
        var fromHeight = _bar.Height > 0 ? _bar.Height : targetHeight;
        var fromLabels = Compact ? 1 : 0;
        GlassTween.Run(GlassMotion.Morph, GlassMotion.MorphEase, progress =>
        {
            SetVisuals(
                fromWidth + ((targetWidth - fromWidth) * progress),
                fromHeight + ((targetHeight - fromHeight) * progress),
                fromLabels + ((labelTarget - fromLabels) * progress));
        }, () => CompactChanged?.Invoke(this, Compact));
    }
}

