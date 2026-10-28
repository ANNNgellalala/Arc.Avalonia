using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;
using LayoutOrientation = Avalonia.Layout.Orientation;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcFloatingButtonVariant
{
    /// <summary>Sits quietly on a page.</summary>
    Muted,

    /// <summary>Raises the tray for an overlay such as a canvas or selection toolbar.</summary>
    Floating
}

public enum ArcFloatingButtonSize
{
    Sm,
    Md
}

public enum ArcFloatingButtonOrientation
{
    Horizontal,
    Vertical
}

public enum ArcFloatingTooltipSide
{
    Auto,
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>One action in a floating toolbar, or a separator between logical clusters.</summary>
public sealed class ArcFloatingButtonItem
{
    public string? Id { get; set; }

    /// <summary>Visible text when the item shows a label; always the accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>Lucide icon name.</summary>
    public string? Icon { get; set; }

    /// <summary>Tooltip copy; falls back to the label.</summary>
    public string? Tooltip { get; set; }

    /// <summary>Shortcut hint shown in the tooltip. It is not bound to a key.</summary>
    public string? Shortcut { get; set; }

    /// <summary>Shows only the icon; the label moves into the tooltip and the accessible name.</summary>
    public bool IconOnly { get; set; }

    public bool IsDisabled { get; set; }

    /// <summary>Marks an action that switches a mode on, such as Present. Keeps a quiet tint while on.</summary>
    public bool IsPressed { get; set; }

    /// <summary>Draws a short rule instead of a button.</summary>
    public bool IsSeparator { get; set; }

    public ICommand? Command { get; set; }

    public object? CommandParameter { get; set; }
}

/// <summary>
/// Separate soft buttons in a quiet tray, with one shared highlight that morphs from button to button as
/// the pointer or keyboard focus moves, and a pressed state that settles in place. Buttons never scale and
/// the tray never resizes; only the highlight moves. The tray is a toolbar with a single roving tab stop.
/// </summary>
public sealed class ArcFloatingButtonGroup : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcFloatingButtonItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, IList<ArcFloatingButtonItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, string?>(nameof(Label));

    public static readonly StyledProperty<ArcFloatingButtonVariant> VariantProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, ArcFloatingButtonVariant>(nameof(Variant));

    public static readonly StyledProperty<ArcFloatingButtonSize> SizeProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, ArcFloatingButtonSize>(nameof(Size), ArcFloatingButtonSize.Md);

    public static readonly StyledProperty<ArcFloatingButtonOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, ArcFloatingButtonOrientation>(nameof(Orientation));

    public static readonly StyledProperty<bool> IconOnlyProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, bool>(nameof(IconOnly));

    public static readonly StyledProperty<ArcFloatingTooltipSide> TooltipSideProperty =
        AvaloniaProperty.Register<ArcFloatingButtonGroup, ArcFloatingTooltipSide>(nameof(TooltipSide));

    private readonly List<ArcButton> _buttons = new();
    private Border? _tray;
    private Panel? _highlightLayer;
    private ArcAdvancedGlide? _highlight;
    private StackPanel? _host;

    static ArcFloatingButtonGroup()
    {
        ItemsProperty.Changed.AddClassHandler<ArcFloatingButtonGroup>((group, _) => group.Rebuild());
        SizeProperty.Changed.AddClassHandler<ArcFloatingButtonGroup>((group, _) => group.Rebuild());
        OrientationProperty.Changed.AddClassHandler<ArcFloatingButtonGroup>((group, _) => group.Rebuild());
        IconOnlyProperty.Changed.AddClassHandler<ArcFloatingButtonGroup>((group, _) => group.Rebuild());
        VariantProperty.Changed.AddClassHandler<ArcFloatingButtonGroup>((group, _) => group.UpdateClasses());
        TooltipSideProperty.Changed.AddClassHandler<ArcFloatingButtonGroup>((group, _) => group.Rebuild());
    }

    public ArcFloatingButtonGroup()
    {
        ClipToBounds = false;
        UpdateClasses();
    }

    /// <summary>Raised with the item id after its own command runs.</summary>
    public event EventHandler<string?>? ActionInvoked;

    /// <summary>The buttons in order; put <see cref="ArcFloatingButtonItem.IsSeparator"/> between clusters.</summary>
    public IList<ArcFloatingButtonItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name of the toolbar, such as "Board actions".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set
        {
            SetValue(LabelProperty, value);
            ArcAdvancedVisuals.SetName(this, value);
            AutomationProperties.SetHelpText(this, "Toolbar");
        }
    }

    public ArcFloatingButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ArcFloatingButtonSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public ArcFloatingButtonOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Shows every item with an icon as an icon only. An item's own IconOnly wins.</summary>
    public bool IconOnly
    {
        get => GetValue(IconOnlyProperty);
        set => SetValue(IconOnlyProperty, value);
    }

    public ArcFloatingTooltipSide TooltipSide
    {
        get => GetValue(TooltipSideProperty);
        set => SetValue(TooltipSideProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _tray = e.NameScope.Find<Border>("Tray");
        _highlightLayer = e.NameScope.Find<Panel>("HighlightLayer");
        _highlight = e.NameScope.Find<ArcAdvancedGlide>("Highlight");
        _host = e.NameScope.Find<StackPanel>("ItemsHost");
        Rebuild();
    }

    private void UpdateClasses()
    {
        Classes.Set("muted", Variant == ArcFloatingButtonVariant.Muted);
        Classes.Set("floating", Variant == ArcFloatingButtonVariant.Floating);
        Classes.Set("vertical", Orientation == ArcFloatingButtonOrientation.Vertical);
        Classes.Set("sm", Size == ArcFloatingButtonSize.Sm);
        Classes.Set("md", Size == ArcFloatingButtonSize.Md);
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _highlight?.Hide();
        _host.Children.Clear();
        _buttons.Clear();

        var vertical = Orientation == ArcFloatingButtonOrientation.Vertical;
        var small = Size == ArcFloatingButtonSize.Sm;
        var height = small ? 30d : 36d;
        var padX = small ? 10d : 12d;
        var fontSize = small ? 12d : 14d;
        var outer = ArcAdvancedVisuals.Radius(ArcTokens.RadiusControl);
        var padding = small ? 3d : 4d;

        Padding = new Thickness(padding);
        if (_tray is not null)
        {
            _tray.CornerRadius = outer;
            _tray.Padding = new Thickness(padding);
        }

        if (_highlight is not null)
        {
            _highlight.CornerRadius = ArcAdvancedVisuals.Concentric(outer, padding + 1);
        }

        _host.Orientation = vertical ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        _host.Spacing = small ? 3 : 4;

        var items = Items?.ToList() ?? new List<ArcFloatingButtonItem>();
        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                _host.Children.Add(BuildSeparator(vertical, height));
                continue;
            }

            var button = BuildButton(item, vertical, small, height, padX, fontSize);
            _buttons.Add(button);
            _host.Children.Add(button);
        }

        var stop = _buttons.FirstOrDefault(button => button.IsEnabled && button.Classes.Contains("fbt-enabled"));
        foreach (var button in _buttons)
        {
            button.IsTabStop = ReferenceEquals(button, stop);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (FindFocused() is { } focused)
            {
                PlaceOn(focused, grow: false);
            }
            else
            {
                _highlight?.Hide();
            }
        }, DispatcherPriority.Loaded);
    }

    private Border BuildSeparator(bool vertical, double height)
    {
        var rule = new Border
        {
            Background = ArcAdvancedVisuals.Brush(ArcTokens.Foreground),
            Opacity = 0.12,
            Width = vertical ? height * 0.5 : 1,
            Height = vertical ? 1 : height * 0.5,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = vertical ? new Thickness(0, 2) : new Thickness(2, 0)
        };
        AutomationProperties.SetName(rule, "Separator");
        return rule;
    }

    private ArcButton BuildButton(ArcFloatingButtonItem item, bool vertical, bool small, double height, double padX, double fontSize)
    {
        var iconOnly = !string.IsNullOrWhiteSpace(item.Icon) && (item.IconOnly || IconOnly);
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = small ? ArcButtonSize.Sm : ArcButtonSize.Md,
            Height = height,
            MinHeight = height,
            MinWidth = height,
            Width = iconOnly ? height : double.NaN,
            Padding = iconOnly ? new Thickness(0) : new Thickness(padX, 0),
            CornerRadius = ArcAdvancedVisuals.Concentric(ArcAdvancedVisuals.Radius(ArcTokens.RadiusControl), (small ? 3 : 4) + 1),
            FontSize = fontSize,
            FontWeight = FontWeight.Medium,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            IsTabStop = false
        };

        button.Classes.Add("floating-item");
        button.Classes.Set("fbt-enabled", !item.IsDisabled);
        button.Classes.Set("is-pressed", item.IsPressed);
        if (item.IsDisabled)
        {
            button.Classes.Add("fbt-disabled");
        }

        var label = item.Label ?? item.Icon ?? "Action";
        ArcAdvancedVisuals.SetName(button, label);
        if (item.IsDisabled)
        {
            AutomationProperties.SetHelpText(button, "Unavailable");
        }
        else if (item.IsPressed)
        {
            AutomationProperties.SetItemStatus(button, "On");
        }

        button.Content = BuildContent(item, button, iconOnly, fontSize);

        if (iconOnly || !string.IsNullOrWhiteSpace(item.Shortcut))
        {
            var tip = string.IsNullOrWhiteSpace(item.Shortcut)
                ? (item.Tooltip ?? label)
                : $"{item.Tooltip ?? label}   {item.Shortcut}";
            ToolTip.SetTip(button, tip);
            ToolTip.SetShowDelay(button, 300);
            ToolTip.SetPlacement(button, PlacementFor());
        }

        button.Click += (_, _) => Activate(item);
        button.PointerEntered += (_, _) =>
        {
            if (!item.IsDisabled)
            {
                PlaceOn(button, grow: true);
            }
        };
        button.GotFocus += (_, _) =>
        {
            SetTabStop(button);
            PlaceOn(button, grow: true);
        };
        button.PointerPressed += (_, args) =>
        {
            if (item.IsDisabled || args.GetCurrentPoint(button).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            {
                return;
            }

            PlaceOn(button, grow: true);
            _highlight?.SetPressed(true);
        };
        button.PointerReleased += (_, _) => _highlight?.SetPressed(false);
        button.PointerCaptureLost += (_, _) => _highlight?.SetPressed(false);
        button.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Enter or Key.Space && !args.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                _highlight?.SetPressed(true);
            }
        };
        button.KeyUp += (_, _) => _highlight?.SetPressed(false);

        return button;
    }

    private Control BuildContent(ArcFloatingButtonItem item, ArcButton button, bool iconOnly, double fontSize)
    {
        var icon = string.IsNullOrWhiteSpace(item.Icon)
            ? null
            : ArcAdvancedVisuals.InheritIcon(item.Icon, button, 16);
        var text = string.IsNullOrWhiteSpace(item.Label)
            ? null
            : ArcAdvancedVisuals.InheritText(item.Label!, button, fontSize);

        if (iconOnly || text is null)
        {
            return icon ?? (Control)(text ?? new TextBlock { Text = item.Label ?? "Action" });
        }

        if (icon is null)
        {
            return text;
        }

        var panel = new StackPanel
        {
            Orientation = LayoutOrientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(icon);
        panel.Children.Add(text);
        return panel;
    }

    private PlacementMode PlacementFor()
    {
        var side = TooltipSide == ArcFloatingTooltipSide.Auto
            ? (Orientation == ArcFloatingButtonOrientation.Vertical ? ArcFloatingTooltipSide.Right : ArcFloatingTooltipSide.Top)
            : TooltipSide;

        return side switch
        {
            ArcFloatingTooltipSide.Bottom => PlacementMode.Bottom,
            ArcFloatingTooltipSide.Left => PlacementMode.Left,
            ArcFloatingTooltipSide.Right => PlacementMode.Right,
            _ => PlacementMode.Top
        };
    }

    private void Activate(ArcFloatingButtonItem item)
    {
        if (item.IsDisabled)
        {
            return;
        }

        if (item.Command is not null && item.Command.CanExecute(item.CommandParameter))
        {
            item.Command.Execute(item.CommandParameter);
        }

        ActionInvoked?.Invoke(this, item.Id);
    }

    private void SetTabStop(ArcButton button)
    {
        foreach (var other in _buttons)
        {
            other.IsTabStop = ReferenceEquals(other, button);
        }
    }

    private ArcButton? FindFocused()
    {
        return _buttons.FirstOrDefault(button => button.IsFocused);
    }

    private void PlaceOn(ArcButton button, bool grow)
    {
        if (_highlight is null || _highlightLayer is null || button.Bounds.Width <= 0)
        {
            return;
        }

        var origin = button.TranslatePoint(new Point(0, 0), _highlightLayer);
        if (origin is null)
        {
            return;
        }

        _highlight.ShowAt(new Rect(origin.Value, button.Bounds.Size), grow);
    }

    private void HideIfIdle()
    {
        if (IsKeyboardFocusWithin || _buttons.Any(button => button.IsPointerOver))
        {
            if (FindFocused() is { } focused)
            {
                PlaceOn(focused, grow: false);
            }

            return;
        }

        _highlight?.Hide();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HideIfIdle();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _buttons.Count == 0)
        {
            return;
        }

        var index = _buttons.FindIndex(button => button.IsFocused);
        if (index < 0)
        {
            return;
        }

        var vertical = Orientation == ArcFloatingButtonOrientation.Vertical;
        switch (e.Key)
        {
            case Key.Right when !vertical:
            case Key.Down when vertical:
                FocusAt(index, 1);
                e.Handled = true;
                break;
            case Key.Left when !vertical:
            case Key.Up when vertical:
                FocusAt(index, -1);
                e.Handled = true;
                break;
            case Key.Home:
                FocusEdge(first: true);
                e.Handled = true;
                break;
            case Key.End:
                FocusEdge(first: false);
                e.Handled = true;
                break;
        }
    }

    private void FocusEdge(bool first)
    {
        var ordered = first ? _buttons : Enumerable.Reverse(_buttons).ToList();
        ordered.FirstOrDefault(button => button.Classes.Contains("fbt-enabled"))?.Focus();
    }

    private void FocusAt(int index, int delta)
    {
        if (delta == 0)
        {
            if (_buttons[index].Classes.Contains("fbt-enabled"))
            {
                _buttons[index].Focus();
            }

            return;
        }

        var step = Math.Sign(delta);
        for (var offset = 1; offset <= _buttons.Count; offset++)
        {
            var candidate = _buttons[((index + step * offset) % _buttons.Count + _buttons.Count) % _buttons.Count];
            if (!candidate.Classes.Contains("fbt-enabled"))
            {
                continue;
            }

            candidate.Focus();
            return;
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        Dispatcher.UIThread.Post(() =>
        {
            if (IsKeyboardFocusWithin)
            {
                return;
            }

            if (_buttons.Any(button => button.IsPointerOver))
            {
                return;
            }

            _highlight?.Hide();
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _highlight?.Hide();
        base.OnDetachedFromVisualTree(e);
    }
}
