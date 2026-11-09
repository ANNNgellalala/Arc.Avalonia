using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>One revealed action: an icon, a short label and a tone fill that fills the row's height.</summary>
public sealed class ArcSwipeActionButton : Button
{
    public static readonly StyledProperty<ArcSwipeActionTone> ToneProperty =
        AvaloniaProperty.Register<ArcSwipeActionButton, ArcSwipeActionTone>(nameof(Tone));

    static ArcSwipeActionButton()
    {
        ToneProperty.Changed.AddClassHandler<ArcSwipeActionButton>((button, _) => button.UpdateClasses());
    }

    public ArcSwipeActionButton()
    {
        UpdateClasses();
    }

    public ArcSwipeActionTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    private void UpdateClasses()
    {
        Classes.Set("neutral", Tone == ArcSwipeActionTone.Neutral);
        Classes.Set("accent", Tone == ArcSwipeActionTone.Accent);
        Classes.Set("danger", Tone == ArcSwipeActionTone.Danger);
    }
}

/// <summary>
/// A triage row whose actions sit under its trailing and leading edges. A horizontal drag with
/// resistance reveals them and settles on a spring; a long drag or flick commits the outermost action.
/// Left and Right reveal the same actions from the keyboard, Tab walks into them and Escape puts the
/// row away, so the gesture is never the only path.
/// </summary>
[TemplatePart("PART_LeadingHost", typeof(StackPanel))]
[TemplatePart("PART_TrailingHost", typeof(StackPanel))]
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_Status", typeof(TextBlock))]
public class ArcSwipeActions : ContentControl
{
    public const double ActionWidth = 84;
    private const double DragLock = 6;
    private const double DragSlop = 24;

    public static readonly StyledProperty<IList<ArcSwipeAction>?> LeadingProperty =
        AvaloniaProperty.Register<ArcSwipeActions, IList<ArcSwipeAction>?>(nameof(Leading));

    public static readonly StyledProperty<IList<ArcSwipeAction>?> TrailingProperty =
        AvaloniaProperty.Register<ArcSwipeActions, IList<ArcSwipeAction>?>(nameof(Trailing));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcSwipeActions, string?>(nameof(Label));

    public static readonly StyledProperty<bool> FullSwipeProperty =
        AvaloniaProperty.Register<ArcSwipeActions, bool>(nameof(FullSwipe), true);

    private readonly List<ArcSwipeActionButton> _leadingButtons = new();
    private readonly List<ArcSwipeActionButton> _trailingButtons = new();
    private readonly ArcMediaTween _tween = new();
    private readonly TranslateTransform _translate = new();
    private StackPanel? _leadingHost;
    private StackPanel? _trailingHost;
    private Border? _surface;
    private TextBlock? _status;
    private double _offset;
    private Point _pressPoint;
    private double _startOffset;
    private bool _dragging;
    private bool _axisLocked;
    private bool _wasOpen;

    static ArcSwipeActions()
    {
        LeadingProperty.Changed.AddClassHandler<ArcSwipeActions>((row, _) => row.BuildActions());
        TrailingProperty.Changed.AddClassHandler<ArcSwipeActions>((row, _) => row.BuildActions());
        LabelProperty.Changed.AddClassHandler<ArcSwipeActions>((row, _) => row.UpdateLabel());
    }

    public ArcSwipeActions()
    {
        Focusable = true;
        IsTabStop = true;
    }

    /// <summary>Actions under the left edge, revealed by swiping right. The first commits on a full swipe.</summary>
    public IList<ArcSwipeAction>? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    /// <summary>Actions under the right edge, revealed by swiping left. The last commits on a full swipe.</summary>
    public IList<ArcSwipeAction>? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }

    /// <summary>Names the row for the keyboard and assistive technology.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Lets a long drag or flick commit the outermost action.</summary>
    public bool FullSwipe
    {
        get => GetValue(FullSwipeProperty);
        set => SetValue(FullSwipeProperty, value);
    }

    /// <summary>Raised when an action is tapped or committed by a full swipe.</summary>
    public static readonly RoutedEvent<ArcSwipeActionInvokedRoutedEventArgs> ActionInvokedEvent =
        RoutedEvent.Register<ArcSwipeActions, ArcSwipeActionInvokedRoutedEventArgs>("ActionInvoked", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="ActionInvoked"/>.</summary>
    public event EventHandler<ArcSwipeActionInvokedRoutedEventArgs>? ActionInvokedRouted
    {
        add => AddHandler(ActionInvokedEvent, value);
        remove => RemoveHandler(ActionInvokedEvent, value);
    }

    public event EventHandler<ArcSwipeAction>? ActionInvoked;

    /// <summary>True when either side is showing.</summary>
    public bool IsOpen => Math.Abs(_offset) > 1;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _leadingHost = e.NameScope.Find<StackPanel>("PART_LeadingHost");
        _trailingHost = e.NameScope.Find<StackPanel>("PART_TrailingHost");
        _surface = e.NameScope.Find<Border>("PART_Surface");
        _status = e.NameScope.Find<TextBlock>("PART_Status");
        if (_surface is not null)
        {
            _surface.RenderTransform = _translate;
        }

        BuildActions();
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Swipe actions row" : Label);
        AutomationProperties.SetHelpText(this,
            "Press Left or Right arrow to reveal actions. Tab moves into them and Escape puts the row away.");
    }

    private void BuildActions()
    {
        if (_leadingHost is null || _trailingHost is null)
        {
            return;
        }

        _leadingHost.Children.Clear();
        _trailingHost.Children.Clear();
        _leadingButtons.Clear();
        _trailingButtons.Clear();

        if (Leading is { } leading)
        {
            foreach (var action in leading)
            {
                var button = CreateAction(action, _leadingButtons.Count);
                _leadingButtons.Add(button);
                _leadingHost.Children.Add(button);
            }
        }

        if (Trailing is { } trailing)
        {
            foreach (var action in trailing)
            {
                var button = CreateAction(action, _trailingButtons.Count);
                _trailingButtons.Add(button);
                _trailingHost.Children.Add(button);
            }
        }

        ApplyOffset(_offset);
    }

    private ArcSwipeActionButton CreateAction(ArcSwipeAction action, int index)
    {
        var button = new ArcSwipeActionButton
        {
            Tone = action.Tone,
            Width = ActionWidth,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsTabStop = false
        };

        var icon = new ArcIcon
        {
            Icon = action.Icon,
            Size = 18,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        icon.Bind(ArcIcon.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = button });

        var label = new TextBlock
        {
            Text = action.Label,
            FontSize = 11,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = ActionWidth - 12
        };
        label.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = button });

        button.Content = new StackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, label }
        };

        AutomationProperties.SetName(button, action.Label);
        ToolTip.SetTip(button, action.Label);
        button.Click += (_, _) => Invoke(action);
        return button;
    }

    private void Invoke(ArcSwipeAction action)
    {
        action.Action?.Invoke();
        ActionInvoked?.Invoke(this, action);
        RaiseEvent(new ArcSwipeActionInvokedRoutedEventArgs(ActionInvokedEvent, action));
        Close();
    }

    /// <summary>Reveals one side: -1 trailing, +1 leading.</summary>
    public void Open(int side)
    {
        var width = side < 0 ? _trailingButtons.Count * ActionWidth : _leadingButtons.Count * ActionWidth;
        if (width <= 0)
        {
            return;
        }

        AnimateTo(side < 0 ? -width : width);
    }

    /// <summary>Puts the row away and returns focus to it.</summary>
    public void Close()
    {
        AnimateTo(0);
        if (IsKeyboardFocusWithin || IsFocused)
        {
            Focus();
        }
    }

    private void AnimateTo(double target)
    {
        _tween.Run(_offset, target, ArcMediaUi.Duration(ArcTokens.DurationStandard), ApplyOffset, () => AfterSettle(target));
    }

    private void AfterSettle(double target)
    {
        ApplyOffset(target);
        if (Math.Abs(target) < 1)
        {
            foreach (var button in _leadingButtons)
            {
                button.IsTabStop = false;
            }

            foreach (var button in _trailingButtons)
            {
                button.IsTabStop = false;
            }
        }
    }

    private void ApplyOffset(double value)
    {
        _offset = value;
        _translate.X = value;
        var open = Math.Abs(value) > 1;

        // Only the side being revealed may be painted; the gap the surface leaves on the opposite edge
        // must stay empty rather than exposing the other action stack.
        if (_leadingHost is not null)
        {
            _leadingHost.IsVisible = value > 0.5;
        }

        if (_trailingHost is not null)
        {
            _trailingHost.IsVisible = value < -0.5;
        }

        foreach (var button in _leadingButtons)
        {
            button.IsTabStop = open && value > 0;
        }

        foreach (var button in _trailingButtons)
        {
            button.IsTabStop = open && value < 0;
        }

        Classes.Set("open", open);
        if (open != _wasOpen)
        {
            _wasOpen = open;
            Announce(open ? RevealText(value < 0) : "Actions hidden");
        }
    }

    private string RevealText(bool trailing)
    {
        var actions = trailing ? Trailing : Leading;
        if (actions is null || actions.Count == 0)
        {
            return "Actions hidden";
        }

        var names = new List<string>();
        foreach (var action in actions)
        {
            names.Add(action.Label);
        }

        return "Actions: " + string.Join(", ", names);
    }

    private void Announce(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed is false)
        {
            return;
        }

        if (_leadingButtons.Count == 0 && _trailingButtons.Count == 0)
        {
            return;
        }

        // A press on a revealed action is a tap, not the start of a drag.
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is ArcSwipeActionButton)
            {
                return;
            }
        }

        Focus(NavigationMethod.Pointer);
        _tween.Stop();
        _dragging = true;
        _axisLocked = false;
        _pressPoint = e.GetPosition(this);
        _startOffset = _offset;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging || e.Pointer.Captured != this)
        {
            return;
        }

        var point = e.GetPosition(this);
        var dx = point.X - _pressPoint.X;
        var dy = point.Y - _pressPoint.Y;

        if (!_axisLocked)
        {
            if (Math.Abs(dx) < DragLock && Math.Abs(dy) < DragLock)
            {
                return;
            }

            if (Math.Abs(dy) > Math.Abs(dx) + 2)
            {
                _dragging = false;
                e.Pointer.Capture(null);
                return;
            }

            _axisLocked = true;
            Classes.Set("dragging", true);
        }

        ApplyOffset(Resist(_startOffset + dx));
        e.Handled = true;
    }

    /// <summary>Rubber-bands past a stop so the gesture reads as elastic, never as broken.</summary>
    private double Resist(double value)
    {
        var right = _leadingButtons.Count * ActionWidth;
        var left = -_trailingButtons.Count * ActionWidth;
        if (value > right)
        {
            return right + (value - right) * 0.24;
        }

        if (value < left)
        {
            return left + (value - left) * 0.24;
        }

        return value;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        Classes.Set("dragging", false);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        var trailingWidth = _trailingButtons.Count * ActionWidth;
        var leadingWidth = _leadingButtons.Count * ActionWidth;
        var rowWidth = Math.Max(1, Bounds.Width);

        if (_offset < 0 && trailingWidth > 0)
        {
            var fullThreshold = Math.Max(trailingWidth + 48, rowWidth * 0.56);
            if (FullSwipe && -_offset > fullThreshold)
            {
                CommitTrailing();
                e.Handled = true;
                return;
            }

            if (-_offset > trailingWidth * 0.5)
            {
                Open(-1);
            }
            else
            {
                Close();
            }
        }
        else if (_offset > 0 && leadingWidth > 0)
        {
            var fullThreshold = Math.Max(leadingWidth + 48, rowWidth * 0.56);
            if (FullSwipe && _offset > fullThreshold)
            {
                CommitLeading();
                e.Handled = true;
                return;
            }

            if (_offset > leadingWidth * 0.5)
            {
                Open(1);
            }
            else
            {
                Close();
            }
        }
        else
        {
            Close();
        }

        e.Handled = true;
    }

    private void CommitTrailing()
    {
        if (Trailing is not { Count: > 0 } trailing)
        {
            Close();
            return;
        }

        Invoke(trailing[^1]);
    }

    private void CommitLeading()
    {
        if (Leading is not { Count: > 0 } leading)
        {
            Close();
            return;
        }

        Invoke(leading[0]);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            Classes.Set("dragging", false);
            Close();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
        _dragging = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Left && _trailingButtons.Count > 0)
        {
            if (!IsOpen || _offset > 0)
            {
                Open(-1);
            }
            else
            {
                _trailingButtons[0].Focus();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Right && _leadingButtons.Count > 0)
        {
            if (!IsOpen || _offset < 0)
            {
                Open(1);
            }
            else
            {
                _leadingButtons[0].Focus();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && IsOpen)
        {
            Close();
            e.Handled = true;
        }
    }
}






