using System;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AgentUI.Controls;

/// <summary>The state a run control bar can present. The control never changes it by itself.</summary>
public enum ArcRunBarStatus
{
    /// <summary>The agent is working through the plan.</summary>
    Running,

    /// <summary>The run is paused and can be resumed.</summary>
    Paused,

    /// <summary>The run ended; the transcript keeps the partial result.</summary>
    Stopped,

    /// <summary>The user took over; the agent is waiting to be handed back the run.</summary>
    TakenOver
}

/// <summary>
/// A control strip for a live agent run: the current step, elapsed time and a state badge on the left, and
/// the intents a person needs on the right (pause or resume, finish the current step, stop now, steer with a
/// new instruction, take over).
///
/// The control is pure UI: every action raises an intent event (<see cref="PauseRequested"/>,
/// <see cref="ResumeRequested"/>, <see cref="StopAfterStepRequested"/>, <see cref="StopRequested"/>,
/// <see cref="SteerRequested"/>, <see cref="TakeoverRequested"/>) and leaves <see cref="Status"/> untouched.
/// Stop now uses a two-step, inline confirmation because the port has no hold-to-confirm control in this
/// project; the host still owns the actual stopping and the approval audit.
/// </summary>
public class ArcRunControlBar : Panel
{
    /// <summary>The run's visible state, set by the host.</summary>
    public static readonly StyledProperty<ArcRunBarStatus> StatusProperty =
        AvaloniaProperty.Register<ArcRunControlBar, ArcRunBarStatus>(nameof(Status), ArcRunBarStatus.Running);

    /// <summary>One line describing the step that is running right now.</summary>
    public static readonly StyledProperty<string> CurrentStepProperty =
        AvaloniaProperty.Register<ArcRunControlBar, string>(
            nameof(CurrentStep), "Editing src/middleware/limits.ts");

    /// <summary>Small progress label beside the step, such as "Step 3 of 5".</summary>
    public static readonly StyledProperty<string?> StepLabelProperty =
        AvaloniaProperty.Register<ArcRunControlBar, string?>(nameof(StepLabel), "Step 3 of 5");

    /// <summary>Seconds the run has been going; rendered as a duration, the control does not tick it.</summary>
    public static readonly StyledProperty<double> ElapsedSecondsProperty =
        AvaloniaProperty.Register<ArcRunControlBar, double>(nameof(ElapsedSeconds), 72);

    private bool _steering;
    private bool _confirmStop;
    private TextBox? _steerBox;

    static ArcRunControlBar()
    {
        StatusProperty.Changed.AddClassHandler<ArcRunControlBar>((bar, _) =>
        {
            bar._steering = false;
            bar._confirmStop = false;
            bar.Rebuild();
        });
        CurrentStepProperty.Changed.AddClassHandler<ArcRunControlBar>((bar, _) => bar.Rebuild());
        StepLabelProperty.Changed.AddClassHandler<ArcRunControlBar>((bar, _) => bar.Rebuild());
        ElapsedSecondsProperty.Changed.AddClassHandler<ArcRunControlBar>((bar, _) => bar.Rebuild());
    }

    public ArcRunControlBar()
    {
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>The run's visible state, set by the host; actions raised by the control never mutate it.</summary>
    public ArcRunBarStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>One line describing the current step.</summary>
    public string CurrentStep
    {
        get => GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    /// <summary>A short progress label beside the step, for example "Step 3 of 5".</summary>
    public string? StepLabel
    {
        get => GetValue(StepLabelProperty);
        set => SetValue(StepLabelProperty, value);
    }

    /// <summary>Elapsed seconds supplied by the host.</summary>
    public double ElapsedSeconds
    {
        get => GetValue(ElapsedSecondsProperty);
        set => SetValue(ElapsedSecondsProperty, value);
    }

    /// <summary>Raised when the user confirms stopping the whole run immediately.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Raised when the user wants to stop after the current step completes.</summary>
    public event EventHandler? StopAfterStepRequested;

    /// <summary>Raised when the user pauses the run; the host changes <see cref="Status"/>.</summary>
    public event EventHandler? PauseRequested;

    /// <summary>Raised when the user resumes a paused or taken-over run; the host changes <see cref="Status"/>.</summary>
    public event EventHandler? ResumeRequested;

    /// <summary>Raised with the new instruction the user wants to insert into the running plan.</summary>
    public event EventHandler<string>? SteerRequested;

    /// <summary>Raised when the user takes over the run from the agent.</summary>
    public event EventHandler? TakeoverRequested;

    private void Rebuild()
    {
        Children.Clear();

        var shell = new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(Status == ArcRunBarStatus.TakenOver ? ArcTokens.Accent : ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(16, 12, 16, 12)
        };

        var layout = new StackPanel { Spacing = 10 };
        layout.Children.Add(BuildMainRow());

        if (_steering && Status is ArcRunBarStatus.Running or ArcRunBarStatus.Paused)
        {
            layout.Children.Add(ArcUi.Divider());
            layout.Children.Add(BuildSteerRow());
        }

        if (_confirmStop)
        {
            layout.Children.Add(ArcUi.Divider());
            layout.Children.Add(BuildStopConfirmRow());
        }

        shell.Child = layout;
        Children.Add(shell);

        ArcUi.Name(
            this,
            $"Run control: {StatusWord(Status)}. {CurrentStep}.",
            "Pause, finish the current step, stop the run, insert a new instruction or take over.");
        AutomationProperties.SetHelpText(this,
            Status == ArcRunBarStatus.Stopped
                ? "The run has ended."
                : "Actions raise intents for the host; this control does not stop anything itself.");

        if (_steering && _steerBox is not null)
        {
            var box = _steerBox;
            Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Loaded);
        }
    }

    private Control BuildMainRow()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(StatusMark());

        var copy = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var line = ArcUi.HStack(8);
        var step = ArcUi.Text(CurrentStep, "arc-body-sm arc-medium", 360);
        step.VerticalAlignment = VerticalAlignment.Center;
        step.TextTrimming = TextTrimming.CharacterEllipsis;
        line.Children.Add(step);
        line.Children.Add(StatusBadge(Status));
        copy.Children.Add(line);

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (!string.IsNullOrWhiteSpace(StepLabel))
        {
            meta.Children.Add(ArcUi.Caption(StepLabel!));
            meta.Children.Add(ArcUi.Caption("·"));
        }

        meta.Children.Add(ArcUi.Text(FormatElapsed(ElapsedSeconds), "arc-caption", wrap: TextWrapping.NoWrap));
        if (Status == ArcRunBarStatus.Running)
        {
            meta.Children.Add(ArcUi.Dot(ArcTokens.Accent, 6));
            meta.Children.Add(ArcUi.Caption("live"));
        }
        else if (Status == ArcRunBarStatus.TakenOver)
        {
            meta.Children.Add(ArcUi.Caption("· the agent is waiting for a hand back"));
        }
        else if (Status == ArcRunBarStatus.Stopped)
        {
            meta.Children.Add(ArcUi.Caption("· transcript keeps the partial result"));
        }

        copy.Children.Add(meta);
        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);

        var actions = BuildActions();
        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);
        return row;
    }

    private Control StatusMark()
    {
        var (icon, token) = Status switch
        {
            ArcRunBarStatus.Running => ("activity", ArcTokens.Accent),
            ArcRunBarStatus.Paused => ("pause", ArcTokens.TextSecondary),
            ArcRunBarStatus.TakenOver => ("user", ArcTokens.Accent),
            _ => ("square", ArcTokens.TextMuted)
        };

        return new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new ArcIcon
            {
                Icon = icon,
                Size = 17,
                Foreground = ArcUi.Brush(token),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }

    private Control BuildActions()
    {
        var actions = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (Status is ArcRunBarStatus.Running or ArcRunBarStatus.Paused)
        {
            var pause = Status == ArcRunBarStatus.Running
                ? ArcUi.Button("Pause", ArcButtonVariant.Secondary, ArcButtonSize.Sm, "pause")
                : ArcUi.Button("Resume", ArcButtonVariant.Primary, ArcButtonSize.Sm, "play");
            pause.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(pause, Status == ArcRunBarStatus.Running ? "Pause the run" : "Resume the run");
            pause.Click += (_, _) =>
            {
                if (Status == ArcRunBarStatus.Running)
                {
                    PauseRequested?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ResumeRequested?.Invoke(this, EventArgs.Empty);
                }
            };
            actions.Children.Add(pause);
        }
        else if (Status == ArcRunBarStatus.TakenOver)
        {
            var handBack = ArcUi.Button("Resume agent", ArcButtonVariant.Primary, ArcButtonSize.Sm, "play");
            handBack.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(handBack, "Hand the run back to the agent");
            handBack.Click += (_, _) => ResumeRequested?.Invoke(this, EventArgs.Empty);
            actions.Children.Add(handBack);
        }

        if (Status is ArcRunBarStatus.Running or ArcRunBarStatus.Paused or ArcRunBarStatus.TakenOver)
        {
            var steer = ArcUi.Button("Steer", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "corner-down-left");
            steer.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(steer, "Insert a new instruction into the run");
            steer.Click += (_, _) =>
            {
                _steering = !_steering;
                Rebuild();
            };
            actions.Children.Add(steer);
        }

        if (Status == ArcRunBarStatus.Running)
        {
            var finish = ArcUi.Button("Finish step", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "flag");
            finish.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(finish, "Stop after the current step");
            finish.Click += (_, _) => StopAfterStepRequested?.Invoke(this, EventArgs.Empty);
            actions.Children.Add(finish);
        }

        if (Status is ArcRunBarStatus.Running or ArcRunBarStatus.Paused or ArcRunBarStatus.TakenOver)
        {
            var takeover = ArcUi.Button("Take over", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "user");
            takeover.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(takeover, "Take over the run from the agent");
            takeover.IsEnabled = Status != ArcRunBarStatus.TakenOver;
            takeover.Click += (_, _) => TakeoverRequested?.Invoke(this, EventArgs.Empty);
            actions.Children.Add(takeover);

            var stop = ArcUi.Button(_confirmStop ? "Confirm stop" : "Stop", ArcButtonVariant.Danger, ArcButtonSize.Sm, "square");
            ArcUi.Name(stop, _confirmStop ? "Confirm stopping the run now" : "Stop the run now");
            stop.Click += (_, _) =>
            {
                if (_confirmStop)
                {
                    _confirmStop = false;
                    StopRequested?.Invoke(this, EventArgs.Empty);
                    Rebuild();
                }
                else
                {
                    _confirmStop = true;
                    _steering = false;
                    Rebuild();
                }
            };
            actions.Children.Add(stop);
        }

        return actions;
    }

    private Control BuildSteerRow()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };

        _steerBox = new TextBox
        {
            PlaceholderText = "Add an instruction without stopping the run",
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Background = ArcUi.Brush(ArcTokens.SurfaceRaised),
            Foreground = ArcUi.Brush(ArcTokens.Foreground),
            BorderBrush = ArcUi.Brush(ArcTokens.Border)
        };
        ArcUi.Name(_steerBox, "New instruction for the run");
        row.Children.Add(_steerBox);

        var send = ArcUi.Button("Send", ArcButtonVariant.Primary, ArcButtonSize.Sm, "send");
        send.VerticalAlignment = VerticalAlignment.Center;
        ArcUi.Name(send, "Send the new instruction");
        send.Click += (_, _) =>
        {
            var text = (_steerBox?.Text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return;
            }

            _steering = false;
            SteerRequested?.Invoke(this, text);
            Rebuild();
        };
        Grid.SetColumn(send, 1);
        row.Children.Add(send);

        var cancel = ArcUi.Button("Cancel", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
        cancel.VerticalAlignment = VerticalAlignment.Center;
        ArcUi.Name(cancel, "Cancel the new instruction");
        cancel.Click += (_, _) =>
        {
            _steering = false;
            Rebuild();
        };
        Grid.SetColumn(cancel, 2);
        row.Children.Add(cancel);

        _steerBox.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                _steering = false;
                args.Handled = true;
                Rebuild();
            }
        };

        return row;
    }

    private Control BuildStopConfirmRow()
    {
        var row = new Border
        {
            Background = ArcUi.Brush(ArcTokens.DangerSubtle),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(12, 8, 12, 8)
        };

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        content.Children.Add(ArcUi.Icon("triangle-alert", 16, ArcTokens.Danger));

        var copy = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(ArcUi.Text("Stop the run now?", "arc-body-sm arc-medium"));
        copy.Children.Add(ArcUi.Text(
            "The current capability call is abandoned; the host writes the audit entry.",
            "arc-caption"));
        Grid.SetColumn(copy, 1);
        content.Children.Add(copy);

        var buttons = ArcUi.HStack(8);
        var keep = ArcUi.Button("Keep running", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
        ArcUi.Name(keep, "Keep the run going");
        keep.Click += (_, _) =>
        {
            _confirmStop = false;
            Rebuild();
        };
        buttons.Children.Add(keep);

        var confirm = ArcUi.Button("Stop run", ArcButtonVariant.Danger, ArcButtonSize.Sm);
        ArcUi.Name(confirm, "Confirm and request the run be stopped");
        confirm.Click += (_, _) =>
        {
            _confirmStop = false;
            StopRequested?.Invoke(this, EventArgs.Empty);
            Rebuild();
        };
        buttons.Children.Add(confirm);

        Grid.SetColumn(buttons, 2);
        content.Children.Add(buttons);
        row.Child = content;
        return row;
    }

    private static ArcBadge StatusBadge(ArcRunBarStatus status) => status switch
    {
        ArcRunBarStatus.Running => ArcUi.Badge("Running", ArcBadgeTone.Accent),
        ArcRunBarStatus.Paused => ArcUi.Badge("Paused", ArcBadgeTone.Neutral),
        ArcRunBarStatus.TakenOver => ArcUi.Badge("You have control", ArcBadgeTone.Accent),
        _ => ArcUi.Badge("Stopped", ArcBadgeTone.Neutral)
    };

    private static string StatusWord(ArcRunBarStatus status) => status switch
    {
        ArcRunBarStatus.Running => "running",
        ArcRunBarStatus.Paused => "paused",
        ArcRunBarStatus.TakenOver => "taken over",
        _ => "stopped"
    };

    private static string FormatElapsed(double seconds)
    {
        if (seconds < 1)
        {
            return "0s";
        }

        var whole = (int)Math.Round(seconds);
        if (whole < 60)
        {
            return $"{whole}s";
        }

        var minutes = whole / 60;
        var rest = whole % 60;
        return rest == 0
            ? $"{minutes}m"
            : string.Format(CultureInfo.InvariantCulture, "{0}m {1}s", minutes, rest);
    }
}
