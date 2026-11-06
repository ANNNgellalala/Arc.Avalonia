using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcConfirmTone
{
    Danger,
    Neutral
}

public enum ArcConfirmMode
{
    /// <summary>One press on Confirm runs the action.</summary>
    Press,

    /// <summary>Confirm starts a short countdown that Cancel or Escape can still stop.</summary>
    Countdown
}

public enum ArcConfirmMorphState
{
    Idle,
    Confirming,
    Pending,
    Done,
    Error
}

/// <summary>
/// A destructive button that morphs in place into an inline question, then a spinner, then a result with
/// Undo. Only the pill's width springs; every face starts at the pill's leading edge, so nothing squashes.
/// </summary>
[TemplatePart("PART_Pill", typeof(Border))]
[TemplatePart("PART_Faces", typeof(Panel))]
public sealed class ArcConfirmMorph : Button
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(Label));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(Icon));

    public static readonly StyledProperty<string?> PromptProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(Prompt));

    public static readonly StyledProperty<string?> ConfirmLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(ConfirmLabel), "Delete");

    public static readonly StyledProperty<string?> CancelLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(CancelLabel), "Cancel");

    public static readonly StyledProperty<string?> PendingLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(PendingLabel), "Deleting");

    public static readonly StyledProperty<string?> DoneLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(DoneLabel), "Deleted");

    public static readonly StyledProperty<string?> ErrorLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(ErrorLabel), "Could not finish");

    public static readonly StyledProperty<string?> RetryLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(RetryLabel), "Retry");

    public static readonly StyledProperty<string?> UndoLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(UndoLabel), "Undo");

    public static readonly StyledProperty<string?> UndoingLabelProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, string?>(nameof(UndoingLabel), "Restoring");

    public static readonly StyledProperty<ArcConfirmTone> ToneProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, ArcConfirmTone>(nameof(Tone));

    public static readonly StyledProperty<ArcConfirmMode> ConfirmModeProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, ArcConfirmMode>(nameof(ConfirmMode));

    public static readonly StyledProperty<int> CountdownProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, int>(nameof(Countdown), 3);

    public static readonly StyledProperty<int> ConfirmTimeoutProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, int>(nameof(ConfirmTimeout), 6000);

    public static readonly StyledProperty<int> ResultTimeoutProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, int>(nameof(ResultTimeout), 5000);

    public static readonly StyledProperty<bool> CancelOnOutsidePressProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, bool>(nameof(CancelOnOutsidePress), true);

    public static readonly StyledProperty<ArcConfirmMorphState> StateProperty =
        AvaloniaProperty.Register<ArcConfirmMorph, ArcConfirmMorphState>(nameof(State));

    private readonly DispatcherTimer _confirmTimer;
    private readonly DispatcherTimer _resultTimer;
    private readonly DispatcherTimer _countdownTimer;
    private Border? _pill;
    private Panel? _faces;
    private IDisposable? _dismiss;
    private Control? _currentFace;
    private int _countdownLeft;
    private int _countdownBase;
    private DateTime _openedAt;
    private bool _focusOnOpen;

    static ArcConfirmMorph()
    {
        FocusableProperty.OverrideDefaultValue<ArcConfirmMorph>(true);
        StateProperty.Changed.AddClassHandler<ArcConfirmMorph>((morph, _) => morph.OnStateChanged());
        foreach (var property in new AvaloniaProperty[]
                 {
                     LabelProperty, IconProperty, PromptProperty, ConfirmLabelProperty, CancelLabelProperty,
                     PendingLabelProperty, DoneLabelProperty, ErrorLabelProperty, UndoLabelProperty, ToneProperty
                 })
        {
            property.Changed.AddClassHandler<ArcConfirmMorph>((morph, _) => morph.Rebuild());
        }
    }

    public ArcConfirmMorph()
    {
        Cursor = new Cursor(StandardCursorType.Hand);
        _confirmTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(6000) };
        _confirmTimer.Tick += (_, _) => Cancel();
        _resultTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(5000) };
        _resultTimer.Tick += (_, _) => ReturnToRest();
        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => TickCountdown();
    }

    public event EventHandler<ArcConfirmMorphState>? StateChanged;
    /// <summary>Bubbled copy of <see cref="StateChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcConfirmMorphStateChangedEventArgs> StateChangedEvent =
        RoutedEvent.Register<ArcConfirmMorph, ArcConfirmMorphStateChangedEventArgs>("StateChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="StateChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcConfirmMorphStateChangedEventArgs>? StateChangedRouted
    {
        add => AddHandler(StateChangedEvent, value);
        remove => RemoveHandler(StateChangedEvent, value);
    }

    /// <summary>Runs on confirm; return a task to show the pending face, throw to show the error face.</summary>
    public Func<Task>? Confirm { get; set; }

    /// <summary>Adds Undo to the result face.</summary>
    public Func<Task>? Undo { get; set; }

    public event EventHandler? Cancelled;

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The question while confirming; defaults to the label with a question mark.</summary>
    public string? Prompt
    {
        get => GetValue(PromptProperty);
        set => SetValue(PromptProperty, value);
    }

    public string? ConfirmLabel
    {
        get => GetValue(ConfirmLabelProperty);
        set => SetValue(ConfirmLabelProperty, value);
    }

    public string? CancelLabel
    {
        get => GetValue(CancelLabelProperty);
        set => SetValue(CancelLabelProperty, value);
    }

    public string? PendingLabel
    {
        get => GetValue(PendingLabelProperty);
        set => SetValue(PendingLabelProperty, value);
    }

    public string? DoneLabel
    {
        get => GetValue(DoneLabelProperty);
        set => SetValue(DoneLabelProperty, value);
    }

    public string? ErrorLabel
    {
        get => GetValue(ErrorLabelProperty);
        set => SetValue(ErrorLabelProperty, value);
    }

    public string? RetryLabel
    {
        get => GetValue(RetryLabelProperty);
        set => SetValue(RetryLabelProperty, value);
    }

    public string? UndoLabel
    {
        get => GetValue(UndoLabelProperty);
        set => SetValue(UndoLabelProperty, value);
    }

    public string? UndoingLabel
    {
        get => GetValue(UndoingLabelProperty);
        set => SetValue(UndoingLabelProperty, value);
    }

    /// <summary>danger colours the confirm answer; the resting button stays neutral either way.</summary>
    public ArcConfirmTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public ArcConfirmMode ConfirmMode
    {
        get => GetValue(ConfirmModeProperty);
        set => SetValue(ConfirmModeProperty, value);
    }

    public int Countdown
    {
        get => GetValue(CountdownProperty);
        set => SetValue(CountdownProperty, value);
    }

    /// <summary>Milliseconds before an unanswered question returns to rest; 0 turns it off.</summary>
    public int ConfirmTimeout
    {
        get => GetValue(ConfirmTimeoutProperty);
        set => SetValue(ConfirmTimeoutProperty, value);
    }

    /// <summary>Milliseconds a result stays before returning to rest; 0 turns it off.</summary>
    public int ResultTimeout
    {
        get => GetValue(ResultTimeoutProperty);
        set => SetValue(ResultTimeoutProperty, value);
    }

    public bool CancelOnOutsidePress
    {
        get => GetValue(CancelOnOutsidePressProperty);
        set => SetValue(CancelOnOutsidePressProperty, value);
    }

    /// <summary>The current face; settable so a page can present the question or the result.</summary>
    public ArcConfirmMorphState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _pill = e.NameScope.Find<Border>("PART_Pill");
        _faces = e.NameScope.Find<Panel>("PART_Faces");
        Rebuild(animate: false);
        OnStateChanged();
    }

    protected override void OnClick()
    {
        base.OnClick();
        if (State == ArcConfirmMorphState.Idle)
        {
            // A press within 280ms of opening is ignored so a double click never confirms by accident.
            SetCurrentValue(StateProperty, ArcConfirmMorphState.Confirming);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape && State != ArcConfirmMorphState.Idle)
        {
            Cancel();
            e.Handled = true;
        }
    }

    private void OnStateChanged()
    {
        var state = State;
        Classes.Set("confirming", state == ArcConfirmMorphState.Confirming);
        Classes.Set("pending", state == ArcConfirmMorphState.Pending);
        Classes.Set("done", state == ArcConfirmMorphState.Done);
        Classes.Set("error", state == ArcConfirmMorphState.Error);

        _confirmTimer.Stop();
        _resultTimer.Stop();
        _countdownTimer.Stop();
        _dismiss?.Dispose();
        _dismiss = null;

        switch (state)
        {
            case ArcConfirmMorphState.Idle:
                ShowFace(BuildRestFace(), animate: true);
                _dismiss?.Dispose();
                _dismiss = null;
                AutomationProperties.SetName(this, Label);
                break;
            case ArcConfirmMorphState.Confirming:
                _openedAt = DateTime.UtcNow;
                _focusOnOpen = IsFocused || IsPointerOver;
                ShowFace(BuildQuestionFace(), animate: true);
                if (CancelOnOutsidePress)
                {
                    _dismiss = ArcDismiss.Attach(this, Cancel);
                }

                if (ConfirmTimeout > 0)
                {
                    _confirmTimer.Interval = TimeSpan.FromMilliseconds(ConfirmTimeout);
                    _confirmTimer.Start();
                }

                break;
            case ArcConfirmMorphState.Pending:
                ShowFace(BuildPendingFace(), animate: true);
                break;
            case ArcConfirmMorphState.Done:
                ShowFace(BuildDoneFace(), animate: true);
                StartResultTimer();
                break;
            case ArcConfirmMorphState.Error:
                ShowFace(BuildErrorFace(), animate: true);
                StartResultTimer();
                break;
        }

                var stateChangedArgs = new ArcConfirmMorphStateChangedEventArgs(StateChangedEvent) { State = state };
        StateChanged?.Invoke(this, state);
        RaiseEvent(stateChangedArgs);
    }

    private void StartResultTimer()
    {
        if (ResultTimeout <= 0)
        {
            return;
        }

        _resultTimer.Interval = TimeSpan.FromMilliseconds(ResultTimeout);
        _resultTimer.Start();
    }

    private void Rebuild(bool animate = true)
    {
        if (_faces is null)
        {
            return;
        }

        ShowFace(State switch
        {
            ArcConfirmMorphState.Confirming => BuildQuestionFace(),
            ArcConfirmMorphState.Pending => BuildPendingFace(),
            ArcConfirmMorphState.Done => BuildDoneFace(),
            ArcConfirmMorphState.Error => BuildErrorFace(),
            _ => BuildRestFace()
        }, animate);
    }

    private void ShowFace(Control face, bool animate)
    {
        if (_faces is null || _pill is null)
        {
            return;
        }

        if (ReferenceEquals(face, _currentFace))
        {
            return;
        }

        _faces.Children.Clear();
        face.IsVisible = true;
        _faces.Children.Add(face);
        _currentFace = face;

        var pill = _pill;
        face.Measure(Size.Infinity);
        var width = face.DesiredSize.Width + 32;
        width = Math.Max(width, State == ArcConfirmMorphState.Idle ? 0 : 180);

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            pill.Transitions = null;
            pill.Width = width;
            return;
        }

        if (double.IsNaN(pill.Width) || pill.Width <= 0)
        {
            pill.Transitions = null;
            pill.Width = width;
        }
        else
        {
            pill.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = WidthProperty,
                    Duration = ArcMotion.SpringMorphDuration,
                    Easing = ArcMotion.SpringMorph
                }
            };
            pill.Width = width;
        }
    }

    private Control BuildRestFace()
    {
        var panel = FacePanel();
        if (!string.IsNullOrWhiteSpace(Icon))
        {
            var icon = ArcActionVisuals.InheritIcon(Icon, this, 16);
            icon.Margin = new Thickness(0, 0, 8, 0);
            panel.Children.Add(icon);
        }

        panel.Children.Add(ArcActionVisuals.InheritText(Label ?? "Delete", this, medium: true));
        AutomationProperties.SetName(this, Label);
        return panel;
    }

    private Control BuildQuestionFace()
    {
        var panel = FacePanel();
        var prompt = new TextBlock
        {
            Text = Prompt ?? $"{Label}?",
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };
        panel.Children.Add(prompt);

        var confirm = new ArcButton
        {
            Content = ConfirmLabel ?? "Delete",
            Variant = Tone == ArcConfirmTone.Danger ? ArcButtonVariant.Danger : ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            MinHeight = 32,
            Padding = new Thickness(14, 0),
            Margin = new Thickness(0, 0, 6, 0)
        };
        confirm.Classes.Add("confirm-answer");
        AutomationProperties.SetName(confirm, ConfirmLabel);
        confirm.Click += (_, _) => ConfirmNow();
        panel.Children.Add(confirm);

        var cancel = new ArcButton
        {
            Content = CancelLabel ?? "Cancel",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            MinHeight = 32,
            Padding = new Thickness(12, 0)
        };
        cancel.Classes.Add("confirm-cancel");
        AutomationProperties.SetName(cancel, CancelLabel);
        cancel.Click += (_, _) => Cancel();
        panel.Children.Add(cancel);

        Dispatcher.UIThread.Post(() =>
        {
            if (_focusOnOpen && State == ArcConfirmMorphState.Confirming)
            {
                confirm.Focus();
            }
        }, DispatcherPriority.Loaded);

        return panel;
    }

    private Control BuildPendingFace()
    {
        var panel = FacePanel();
        panel.Children.Add(new ArcSpinner
        {
            Width = 16,
            Height = 16,
            StrokeWidth = 2,
            Margin = new Thickness(0, 0, 8, 0),
            Stroke = Foreground
        });
        panel.Children.Add(new TextBlock
        {
            Text = PendingLabel ?? "Deleting",
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    private Control BuildDoneFace()
    {
        var panel = FacePanel();
        var icon = new ArcIcon { Icon = "circle-check", Size = 16, Margin = new Thickness(0, 0, 8, 0) };
        icon.Classes.Add("confirm-done-icon");
        panel.Children.Add(icon);
        panel.Children.Add(new TextBlock
        {
            Text = DoneLabel ?? "Deleted",
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        if (Undo is not null)
        {
            var undo = new ArcButton
            {
                Content = UndoLabel ?? "Undo",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                MinHeight = 30,
                Padding = new Thickness(10, 0)
            };
            undo.Classes.Add("confirm-undo");
            AutomationProperties.SetName(undo, UndoLabel);
            undo.Click += (_, _) => _ = UndoAsync();
            panel.Children.Add(undo);
        }

        return panel;
    }

    private Control BuildErrorFace()
    {
        var panel = FacePanel();
        var icon = new ArcIcon { Icon = "circle-alert", Size = 16, Margin = new Thickness(0, 0, 8, 0) };
        icon.Classes.Add("confirm-error-icon");
        panel.Children.Add(icon);
        panel.Children.Add(new TextBlock
        {
            Text = ErrorLabel ?? "Could not finish",
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var retry = new ArcButton
        {
            Content = RetryLabel ?? "Retry",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            MinHeight = 30,
            Padding = new Thickness(10, 0)
        };
        AutomationProperties.SetName(retry, RetryLabel);
        retry.Click += (_, _) => ConfirmNow();
        panel.Children.Add(retry);
        return panel;
    }

    private static StackPanel FacePanel() => new()
    {
        Orientation = Orientation.Horizontal,
        VerticalAlignment = VerticalAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Left
    };

    private void ConfirmNow()
    {
        if (State == ArcConfirmMorphState.Confirming && ConfirmMode == ArcConfirmMode.Countdown)
        {
            _countdownBase = Math.Max(1, Countdown);
            _countdownLeft = _countdownBase;
            ShowFace(BuildCountdownFace(), animate: true);
            _confirmTimer.Stop();
            _countdownTimer.Start();
            return;
        }

        _ = RunConfirmAsync();
    }

    private Control BuildCountdownFace()
    {
        var panel = FacePanel();
        panel.Children.Add(new TextBlock
        {
            Text = $"{PendingLabel} in {_countdownLeft}",
            Classes = { "arc-body-sm" },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        });

        var stop = new ArcButton
        {
            Content = CancelLabel ?? "Cancel",
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            MinHeight = 30,
            Padding = new Thickness(10, 0)
        };
        stop.Click += (_, _) => Cancel();
        panel.Children.Add(stop);
        return panel;
    }

    private void TickCountdown()
    {
        _countdownLeft--;
        if (_countdownLeft <= 0)
        {
            _countdownTimer.Stop();
            _ = RunConfirmAsync();
            return;
        }

        Rebuild(animate: false);
    }

    private async Task RunConfirmAsync()
    {
        SetCurrentValue(StateProperty, ArcConfirmMorphState.Pending);

        try
        {
            if (Confirm is not null)
            {
                await Confirm().ConfigureAwait(true);
            }

            SetCurrentValue(StateProperty, ArcConfirmMorphState.Done);
        }
        catch
        {
            SetCurrentValue(StateProperty, ArcConfirmMorphState.Error);
        }
    }

    private async Task UndoAsync()
    {
        try
        {
            if (Undo is not null)
            {
                await Undo().ConfigureAwait(true);
            }
        }
        catch
        {
            SetCurrentValue(StateProperty, ArcConfirmMorphState.Error);
            return;
        }

        ReturnToRest();
    }

    private void Cancel()
    {
        _confirmTimer.Stop();
        _countdownTimer.Stop();
        _dismiss?.Dispose();
        _dismiss = null;
        SetCurrentValue(StateProperty, ArcConfirmMorphState.Idle);
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    private void ReturnToRest()
    {
        _resultTimer.Stop();
        SetCurrentValue(StateProperty, ArcConfirmMorphState.Idle);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _dismiss?.Dispose();
        _dismiss = null;
        _confirmTimer.Stop();
        _resultTimer.Stop();
        _countdownTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}