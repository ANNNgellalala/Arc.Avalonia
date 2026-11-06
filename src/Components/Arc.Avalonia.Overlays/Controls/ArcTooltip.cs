using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Overlays.Controls;

public enum ArcTooltipSide
{
    Top,
    Bottom
}

/// <summary>
/// One line of supporting text for a control that is not self-explanatory: it appears after a short
/// delay on hover, immediately on keyboard focus, and hides on Escape. Never the only place for
/// information someone needs.
/// </summary>
[TemplatePart("PART_Overlay", typeof(Canvas))]
public sealed class ArcTooltip : ContentControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcTooltip, string?>(nameof(Text));

    public static readonly StyledProperty<ArcTooltipSide> SideProperty =
        AvaloniaProperty.Register<ArcTooltip, ArcTooltipSide>(nameof(Side));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcTooltip, bool>(nameof(IsOpen));

    public static readonly StyledProperty<int> DelayProperty =
        AvaloniaProperty.Register<ArcTooltip, int>(nameof(Delay), 250);

    private Canvas? _overlay;
    private Border? _bubble;
    private Rect _lastPlacement;
    private DispatcherTimer? _delay;
    private bool _hovering;

    static ArcTooltip()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcTooltip>((tooltip, _) => tooltip.Sync());
    }

    public ArcTooltip()
    {
        ClipToBounds = false;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        LayoutUpdated += (_, _) => PositionBubble();
    }

    /// <summary>The short text of the tip.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public ArcTooltipSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    /// <summary>Whether the bubble is showing; settable so a page can present the open state.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Milliseconds before the bubble appears on hover.</summary>
    public int Delay
    {
        get => GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _overlay = e.NameScope.Find<Canvas>("PART_Overlay");
        Sync();
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        _hovering = true;
        if (IsOpen)
        {
            return;
        }

        _delay?.Stop();
        _delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(0, Delay)) };
        _delay.Tick += (_, _) =>
        {
            _delay?.Stop();
            if (_hovering)
            {
                SetCurrentValue(IsOpenProperty, true);
            }
        };
        _delay.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _hovering = false;
        _delay?.Stop();
        if (IsOpen && !HasKeyboardFocusWithin())
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private bool HasKeyboardFocusWithin() =>
        TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is Visual focused && IsVisualAncestorOf(focused);

    private bool IsVisualAncestorOf(Visual visual)
    {
        for (Visual? current = visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, this))
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (!string.IsNullOrWhiteSpace(Text))
        {
            SetCurrentValue(IsOpenProperty, true);
        }
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!_hovering)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
            e.Handled = true;
        }
    }

    private void Sync()
    {
        if (IsOpen)
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void Show()
    {
        if (_overlay is null || string.IsNullOrWhiteSpace(Text) || _bubble is not null)
        {
            return;
        }

        var bubble = new Border
        {
            Classes = { "tooltip-surface" },
            Child = new TextBlock
            {
                Text = Text,
                Classes = { "arc-body-sm" },
                TextWrapping = TextWrapping.Wrap
            }
        };

        _bubble = bubble;
        ArcActionVisuals.SetOverlay(_overlay, bubble);
        AutomationProperties.SetName(bubble, Text);
        _lastPlacement = default;
        PositionBubble();
    }

    private void Hide()
    {
        if (_bubble is null)
        {
            return;
        }

        ArcActionVisuals.SetOverlay(_overlay, null);
        _bubble = null;
    }

    private void PositionBubble()
    {
        if (_bubble is null)
        {
            return;
        }

        var size = _bubble.Bounds.Width > 0 ? _bubble.Bounds.Size : _bubble.DesiredSize;
        var x = Math.Max(0, (Bounds.Width - size.Width) / 2);
        var y = Side == ArcTooltipSide.Top ? -size.Height - 8 : Bounds.Height + 8;

        var rect = new Rect(x, y, size.Width, size.Height);
        if (rect == _lastPlacement)
        {
            return;
        }

        _lastPlacement = rect;
        ArcActionVisuals.Place(_bubble, x, y);
    }
}