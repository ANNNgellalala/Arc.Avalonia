using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>
/// Arc's hold-to-confirm button. A press starts a linear fill that tracks the hold; releasing early
/// drains it on a spring and announces "keep holding" through the help text, and a completed hold
/// morphs the label, the icon and the tone to the done state. Space and Enter hold it too, and tabbing
/// away cancels the hold so it can never fire by accident.
/// </summary>
public class ArcHoldToConfirm : Button
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcHoldToConfirm, string>(nameof(Label), "Hold to delete project");

    public static readonly StyledProperty<string> ConfirmedLabelProperty =
        AvaloniaProperty.Register<ArcHoldToConfirm, string>(nameof(ConfirmedLabel), "Done");

    public static readonly StyledProperty<double> DurationProperty =
        AvaloniaProperty.Register<ArcHoldToConfirm, double>(nameof(Duration), 1200d);

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcHoldToConfirm, string?>(nameof(Icon), "trash-2");

    public static readonly StyledProperty<ArcHoldToConfirmTone> ToneProperty =
        AvaloniaProperty.Register<ArcHoldToConfirm, ArcHoldToConfirmTone>(nameof(Tone), ArcHoldToConfirmTone.Danger);

    public static readonly StyledProperty<bool?> IsConfirmedProperty =
        AvaloniaProperty.Register<ArcHoldToConfirm, bool?>(nameof(IsConfirmed));

    private readonly ArcMediaTween _tween = new();
    private Border? _root;
    private Border? _fill;
    private Border? _clip;
    private Grid? _fillRow;
    private StackPanel? _baseRow;
    private TextBlock? _labelBase;
    private TextBlock? _labelFill;
    private ArcIcon? _iconBase;
    private ArcIcon? _iconFill;
    private TextBlock? _status;
    private bool _holding;
    private bool _selfConfirmed;
    private double _fraction;
    private double _measuredWidth = -1;
    private Point _pressOrigin;
    private bool _pointerHold;

    static ArcHoldToConfirm()
    {
        LabelProperty.Changed.AddClassHandler<ArcHoldToConfirm>((button, _) => button.UpdateVisualState());
        ConfirmedLabelProperty.Changed.AddClassHandler<ArcHoldToConfirm>((button, _) => button.UpdateVisualState());
        IconProperty.Changed.AddClassHandler<ArcHoldToConfirm>((button, _) => button.UpdateVisualState());
        ToneProperty.Changed.AddClassHandler<ArcHoldToConfirm>((button, _) => button.UpdateVisualState());
        IsConfirmedProperty.Changed.AddClassHandler<ArcHoldToConfirm>((button, _) => button.SyncConfirmed());
        IsEnabledProperty.Changed.AddClassHandler<ArcHoldToConfirm>((button, _) =>
        {
            if (!button.IsEnabled)
            {
                button.CancelHold();
            }
        });
    }

    public ArcHoldToConfirm()
    {
        LayoutUpdated += OnLayoutUpdated;
    }

    /// <summary>Instruction and action, such as "Hold to delete project".</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Label after the hold completes.</summary>
    public string ConfirmedLabel
    {
        get => GetValue(ConfirmedLabelProperty);
        set => SetValue(ConfirmedLabelProperty, value);
    }

    /// <summary>Hold length in milliseconds.</summary>
    public double Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>Leading Lucide icon, replaced by a check when done.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public ArcHoldToConfirmTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    /// <summary>
    /// Controls the done state (Arc's <c>confirmed</c> prop). Null lets the button manage it; set true
    /// or false to drive it from a parent, for example to reset after an undo.
    /// </summary>
    public bool? IsConfirmed
    {
        get => GetValue(IsConfirmedProperty);
        set => SetValue(IsConfirmedProperty, value);
    }

    /// <summary>True while the button reads as done, whether self-managed or controlled.</summary>
    public bool Done => IsConfirmed ?? _selfConfirmed;

    /// <summary>Raised when a press or key hold begins.</summary>
    public event EventHandler? HoldStarted;

    /// <summary>Raised when a hold ends, whether it completed or was released early.</summary>
    public event EventHandler? HoldEnded;

    /// <summary>Raised once when the hold completes.</summary>
    public event EventHandler? Confirmed;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Border>("Root");
        _fill = e.NameScope.Find<Border>("Fill");
        _clip = e.NameScope.Find<Border>("Clip");
        _fillRow = e.NameScope.Find<Grid>("FillRow");
        _baseRow = e.NameScope.Find<StackPanel>("BaseRow");
        _labelBase = e.NameScope.Find<TextBlock>("LabelBase");
        _labelFill = e.NameScope.Find<TextBlock>("LabelFill");
        _iconBase = e.NameScope.Find<ArcIcon>("IconBase");
        _iconFill = e.NameScope.Find<ArcIcon>("IconFill");
        _status = e.NameScope.Find<TextBlock>("Status");
        _measuredWidth = -1;
        UpdateVisualState();
        ApplyFraction(_fraction);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        var width = _root?.Bounds.Width ?? 0;
        if (Math.Abs(width - _measuredWidth) < 0.5 || width <= 0)
        {
            return;
        }

        _measuredWidth = width;
        if (_fillRow is not null)
        {
            _fillRow.Width = width;
        }

        ApplyFraction(_fraction);
    }

    private void UpdateVisualState()
    {
        var done = Done;
        Classes.Set("danger", Tone == ArcHoldToConfirmTone.Danger);
        Classes.Set("neutral", Tone == ArcHoldToConfirmTone.Neutral);
        Classes.Set("confirmed", done);

        var text = done ? ConfirmedLabel : Label;
        if (_labelBase is not null)
        {
            _labelBase.Text = text;
        }

        if (_labelFill is not null)
        {
            _labelFill.Text = text;
        }

        var icon = done ? "check" : Icon;
        if (_iconBase is not null)
        {
            _iconBase.Icon = icon;
        }

        if (_iconFill is not null)
        {
            _iconFill.Icon = icon;
        }

        // In the done state the fill retracts and the face reports the result on its own colour, so the
        // contrasting fill copy must not sit over it.
        if (_fill is not null)
        {
            _fill.IsVisible = !done;
        }

        if (_clip is not null)
        {
            _clip.IsVisible = !done;
        }

        if (_baseRow is not null && _fillRow is not null)
        {
            // The two faces must measure identically so the fill edge cuts exactly through the label.
            var width = _measuredWidth > 0 ? _measuredWidth : 0;
            if (width > 0)
            {
                _fillRow.Width = width;
            }
        }

        var seconds = Math.Max(0.1, Duration / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        AutomationProperties.SetName(this, text);
        AutomationProperties.SetHelpText(this, done
            ? "Confirmed"
            : $"Press and hold for {seconds} seconds to confirm. With a keyboard, hold Space or Enter.");
    }

    private void SyncConfirmed()
    {
        var done = Done;
        if (!done)
        {
            _selfConfirmed = false;
            _tween.Stop();
            _fraction = 0;
            ApplyFraction(0);
        }
        else
        {
            _tween.Stop();
            _fraction = 1;
            ApplyFraction(1);
        }

        UpdateVisualState();
    }

    private void StartHold()
    {
        if (Done || _holding || !IsEffectivelyEnabled)
        {
            return;
        }

        _holding = true;
        if (!ArcThemeManager.ReducedMotion)
        {
            Classes.Set("holding", true);
        }
        HoldStarted?.Invoke(this, EventArgs.Empty);
        if (_status is not null)
        {
            _status.Text = "Hold started";
        }

        var remaining = Math.Max(60, Duration * (1 - Math.Clamp(_fraction, 0, 1)));
        _tween.Run(_fraction, 1, TimeSpan.FromMilliseconds(remaining), ApplyFraction, CompleteHold, t => t);
    }

    private void ReleaseHold()
    {
        if (!_holding)
        {
            return;
        }

        _holding = false;
        Classes.Set("holding", false);
        _tween.Stop();
        HoldEnded?.Invoke(this, EventArgs.Empty);
        if (Done)
        {
            return;
        }

        Rewind();
    }

    private void CancelHold()
    {
        if (!_holding)
        {
            return;
        }

        _holding = false;
        Classes.Set("holding", false);
        _tween.Stop();
        HoldEnded?.Invoke(this, EventArgs.Empty);
        if (!Done)
        {
            Rewind();
        }
    }

    private void Rewind()
    {
        if (_status is not null && _fraction > 0.05)
        {
            _status.Text = "Hold longer";
        }

        _tween.Run(_fraction, 0, ArcMotion.SpringSmoothDuration, ApplyFraction, null, ArcMotion.SpringSmooth.Ease);
    }

    private void CompleteHold()
    {
        _holding = false;
        Classes.Set("holding", false);
        _fraction = 1;
        ApplyFraction(1);
        if (IsConfirmed is null)
        {
            _selfConfirmed = true;
        }

        if (_status is not null)
        {
            _status.Text = ConfirmedLabel;
        }

        UpdateVisualState();
        HoldEnded?.Invoke(this, EventArgs.Empty);
        Confirmed?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyFraction(double fraction)
    {
        _fraction = Math.Clamp(fraction, 0, 1);
        var width = _root?.Bounds.Width ?? _measuredWidth;
        if (width <= 0)
        {
            return;
        }

        var filled = width * _fraction;
        if (_fill is not null)
        {
            _fill.Width = filled;
        }

        if (_clip is not null)
        {
            _clip.Width = filled;
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!IsEffectivelyEnabled || Done)
        {
            base.OnPointerPressed(e);
            return;
        }

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            base.OnPointerPressed(e);
            return;
        }

        Focus(NavigationMethod.Pointer);
        _pointerHold = true;
        _pressOrigin = e.GetPosition(this);
        e.Pointer.Capture(this);
        StartHold();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_pointerHold || e.Pointer.Captured != this)
        {
            base.OnPointerMoved(e);
            return;
        }

        var delta = e.GetPosition(this) - _pressOrigin;
        if (Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y) > 24)
        {
            // Sliding off the button is a cancel, matching Arc's 24px escape distance.
            _pointerHold = false;
            e.Pointer.Capture(null);
            CancelHold();
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_pointerHold)
        {
            base.OnPointerReleased(e);
            return;
        }

        _pointerHold = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }

        ReleaseHold();
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_pointerHold)
        {
            _pointerHold = false;
            CancelHold();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            if (!_holding && !Done)
            {
                StartHold();
            }

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _holding)
        {
            CancelHold();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (e.Key is Key.Space or Key.Enter)
        {
            ReleaseHold();
            e.Handled = true;
            return;
        }

        base.OnKeyUp(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_holding)
        {
            CancelHold();
        }
    }

    protected override void OnClick()
    {
        // A tap alone never confirms; the click is swallowed so only the hold path can fire.
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _tween.Stop();
        _holding = false;
        _pointerHold = false;
    }
}





