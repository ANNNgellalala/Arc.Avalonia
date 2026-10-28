using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AdvancedActions.Controls;

public enum ArcActionButtonState
{
    Idle,
    Pending,
    Success
}

/// <summary>
/// Arc's action button: one async commit such as Save, Publish or Submit whose result appears on the
/// button itself. It runs the action while a spinner holds the trailing slot, morphs to a check and the
/// success label, and returns to idle after <see cref="ResetAfterMs"/>. Pending keeps focus: the button is
/// never disabled, it only refuses a second press. Error resets to idle and reports through the callback.
/// The label crossfades and rises in place; the width follows layout rather than springing because
/// Avalonia has no auto-width animation, and the check scales in instead of drawing its stroke.
/// </summary>
public sealed class ArcActionButton : Button
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcActionButton, string>(nameof(Label), "Save");

    public static readonly StyledProperty<string> PendingLabelProperty =
        AvaloniaProperty.Register<ArcActionButton, string>(nameof(PendingLabel), "Saving");

    public static readonly StyledProperty<string> SuccessLabelProperty =
        AvaloniaProperty.Register<ArcActionButton, string>(nameof(SuccessLabel), "Saved");

    public static readonly StyledProperty<int> ResetAfterMsProperty =
        AvaloniaProperty.Register<ArcActionButton, int>(nameof(ResetAfterMs), 2400);

    public static readonly StyledProperty<Func<Task>?> OnActionProperty =
        AvaloniaProperty.Register<ArcActionButton, Func<Task>?>(nameof(OnAction));

    public static readonly StyledProperty<Action<Exception>?> OnActionErrorProperty =
        AvaloniaProperty.Register<ArcActionButton, Action<Exception>?>(nameof(OnActionError));

    public static readonly StyledProperty<ArcButtonVariant> VariantProperty =
        AvaloniaProperty.Register<ArcActionButton, ArcButtonVariant>(nameof(Variant), ArcButtonVariant.Primary);

    public static readonly StyledProperty<ArcButtonSize> SizeProperty =
        AvaloniaProperty.Register<ArcActionButton, ArcButtonSize>(nameof(Size), ArcButtonSize.Md);

    public static readonly StyledProperty<ArcActionButtonState> StateProperty =
        AvaloniaProperty.Register<ArcActionButton, ArcActionButtonState>(nameof(State));

    private readonly DispatcherTimer _reset;
    private TextBlock? _label;
    private ArcIcon? _arrow;
    private ArcIcon? _check;
    private ArcSpinner? _spinner;
    private bool _running;

    static ArcActionButton()
    {
        LabelProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.UpdateFace(animate: false));
        PendingLabelProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.UpdateFace(animate: false));
        SuccessLabelProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.UpdateFace(animate: false));
        VariantProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.UpdateClasses());
        SizeProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.UpdateClasses());
        StateProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.OnStateChanged());
        ResetAfterMsProperty.Changed.AddClassHandler<ArcActionButton>((button, _) => button.ArmReset());
    }

    public ArcActionButton()
    {
        _reset = new DispatcherTimer();
        _reset.Tick += (_, _) =>
        {
            _reset.Stop();
            SetCurrentValue(StateProperty, ArcActionButtonState.Idle);
        };
        UpdateClasses();
    }

    /// <summary>Idle label and accessible name.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Label while the action runs; defaults to "Saving".</summary>
    public string PendingLabel
    {
        get => GetValue(PendingLabelProperty);
        set => SetValue(PendingLabelProperty, value);
    }

    /// <summary>Label after the action resolves; defaults to "Saved".</summary>
    public string SuccessLabel
    {
        get => GetValue(SuccessLabelProperty);
        set => SetValue(SuccessLabelProperty, value);
    }

    /// <summary>Delay before returning to idle after success. 0 keeps the success state.</summary>
    public int ResetAfterMs
    {
        get => GetValue(ResetAfterMsProperty);
        set => SetValue(ResetAfterMsProperty, value);
    }

    /// <summary>The async action; the pending state lasts until the returned task settles.</summary>
    public Func<Task>? OnAction
    {
        get => GetValue(OnActionProperty);
        set => SetValue(OnActionProperty, value);
    }

    /// <summary>Called when <see cref="OnAction"/> throws; the button has already returned to idle.</summary>
    public Action<Exception>? OnActionError
    {
        get => GetValue(OnActionErrorProperty);
        set => SetValue(OnActionErrorProperty, value);
    }

    public ArcButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ArcButtonSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>The current face. Settable so a demo page can show the pending and success states.</summary>
    public ArcActionButtonState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _label = e.NameScope.Find<TextBlock>("Label");
        _arrow = e.NameScope.Find<ArcIcon>("Arrow");
        _check = e.NameScope.Find<ArcIcon>("Check");
        _spinner = e.NameScope.Find<ArcSpinner>("Spinner");

        SetUpMotion(_label);
        SetUpMotion(_arrow);
        SetUpMotion(_check);
        UpdateClasses();
        UpdateFace(animate: false);
    }

    protected override void OnClick()
    {
        if (State == ArcActionButtonState.Pending || _running)
        {
            return;
        }

        base.OnClick();
        _ = RunAsync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
    }

    /// <summary>Runs the action cycle; kept public so a page or another control can drive the same face.</summary>
    public async Task RunAsync()
    {
        if (_running)
        {
            return;
        }

        _reset.Stop();
        _running = true;
        SetCurrentValue(StateProperty, ArcActionButtonState.Pending);

        try
        {
            if (OnAction is not null)
            {
                await OnAction().ConfigureAwait(true);
            }

            SetCurrentValue(StateProperty, ArcActionButtonState.Success);
            ArmReset();
        }
        catch (Exception exception)
        {
            SetCurrentValue(StateProperty, ArcActionButtonState.Idle);
            OnActionError?.Invoke(exception);
        }
        finally
        {
            _running = false;
        }
    }

    private void OnStateChanged()
    {
        UpdateClasses();
        UpdateFace(animate: true);
        if (State != ArcActionButtonState.Success)
        {
            _reset.Stop();
        }
        else
        {
            ArmReset();
        }
    }

    private void ArmReset()
    {
        _reset.Stop();
        if (State != ArcActionButtonState.Success || ResetAfterMs <= 0)
        {
            return;
        }

        _reset.Interval = TimeSpan.FromMilliseconds(ResetAfterMs);
        _reset.Start();
    }

    private void UpdateClasses()
    {
        Classes.Set("primary", Variant == ArcButtonVariant.Primary);
        Classes.Set("secondary", Variant == ArcButtonVariant.Secondary);
        Classes.Set("ghost", Variant == ArcButtonVariant.Ghost);
        Classes.Set("danger", Variant == ArcButtonVariant.Danger);
        Classes.Set("sm", Size == ArcButtonSize.Sm);
        Classes.Set("md", Size == ArcButtonSize.Md);
        Classes.Set("lg", Size == ArcButtonSize.Lg);
        Classes.Set("pending", State == ArcActionButtonState.Pending);
        Classes.Set("success", State == ArcActionButtonState.Success);
    }

    private void UpdateFace(bool animate)
    {
        var face = State switch
        {
            ArcActionButtonState.Pending => PendingLabel,
            ArcActionButtonState.Success => SuccessLabel,
            _ => Label
        };

        if (_label is not null)
        {
            _label.Text = face;
            if (animate)
            {
                RiseIn(_label, 3);
            }
        }

        if (_arrow is not null)
        {
            _arrow.IsVisible = State == ArcActionButtonState.Idle;
            if (animate && State == ArcActionButtonState.Idle)
            {
                ScaleIn(_arrow, 0.6);
            }
        }

        if (_spinner is not null)
        {
            _spinner.IsVisible = State == ArcActionButtonState.Pending;
        }

        if (_check is not null)
        {
            _check.IsVisible = State == ArcActionButtonState.Success;
            if (animate && State == ArcActionButtonState.Success)
            {
                // Path drawing is approximated by a short scale-in of the finished check.
                ScaleIn(_check, 0.55);
            }
        }

        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(face) ? Label : face);
        AutomationProperties.SetItemStatus(this, State == ArcActionButtonState.Pending ? "Busy" : string.Empty);
        AutomationProperties.SetHelpText(this, State == ArcActionButtonState.Pending ? "Action in progress" : string.Empty);
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private void SetUpMotion(Control? control)
    {
        if (control is null)
        {
            return;
        }

        control.RenderTransformOrigin = RelativePoint.Center;
        if (ArcThemeManager.ReducedMotion)
        {
            return;
        }

        control.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = ArcMotion.Fast, Easing = ArcMotion.EaseStandard },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = ArcMotion.SpringSnappyDuration,
                Easing = ArcMotion.SpringSnappy
            }
        };
    }

    private static void RiseIn(Control control, double pixels)
    {
        if (ArcThemeManager.ReducedMotion)
        {
            control.Opacity = 1;
            control.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
            return;
        }

        control.Opacity = 0.2;
        control.RenderTransform = TransformOperations.Parse($"translate(0px, {pixels}px)");
        Dispatcher.UIThread.Post(() =>
        {
            control.Opacity = 1;
            control.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
        }, DispatcherPriority.Loaded);
    }

    private static void ScaleIn(Control control, double from)
    {
        if (ArcThemeManager.ReducedMotion)
        {
            control.Opacity = 1;
            control.RenderTransform = TransformOperations.Parse("scale(1)");
            return;
        }

        control.Opacity = 0.4;
        control.RenderTransform = TransformOperations.Parse($"scale({from})");
        Dispatcher.UIThread.Post(() =>
        {
            control.Opacity = 1;
            control.RenderTransform = TransformOperations.Parse("scale(1)");
        }, DispatcherPriority.Loaded);
    }
}
