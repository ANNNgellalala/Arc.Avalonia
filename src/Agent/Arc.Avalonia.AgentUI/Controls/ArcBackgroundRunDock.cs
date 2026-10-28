using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AgentUI.Controls;

/// <summary>The background state a run dock can present.</summary>
public enum ArcDockStatus
{
    /// <summary>No background run; the dock can stay hidden or show a quiet idle line.</summary>
    Idle,

    /// <summary>A run is working in the background.</summary>
    Running,

    /// <summary>The run is blocked on an approval or a question and needs the person.</summary>
    NeedsAttention,

    /// <summary>The run is paused and can be resumed.</summary>
    Paused
}

/// <summary>
/// A compact dock that says "the agent is still working in the background" without taking the window: a state
/// mark, the task label, a progress line and the intents a person needs (pause or resume, open the transcript,
/// stop). It is the in-window visual the host can reuse in a header, a sidebar footer or a floating corner;
/// the platform tray entry and the system notification stay with the host.
///
/// The control renders the state it is given and raises <see cref="PauseRequested"/> (a toggle intent: pause
/// while running, resume while paused), <see cref="StopRequested"/> and <see cref="ExpandRequested"/>.
/// </summary>
public class ArcBackgroundRunDock : Panel
{
    /// <summary>The background state, set by the host.</summary>
    public static readonly StyledProperty<ArcDockStatus> StatusProperty =
        AvaloniaProperty.Register<ArcBackgroundRunDock, ArcDockStatus>(nameof(Status), ArcDockStatus.Running);

    /// <summary>One line describing what the background run is doing.</summary>
    public static readonly StyledProperty<string> TaskLabelProperty =
        AvaloniaProperty.Register<ArcBackgroundRunDock, string>(nameof(TaskLabel), "Morning brief is running");

    /// <summary>Progress from 0 to 1, supplied by the host; the control never invents it.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<ArcBackgroundRunDock, double>(nameof(Progress), 0.42);

    static ArcBackgroundRunDock()
    {
        StatusProperty.Changed.AddClassHandler<ArcBackgroundRunDock>((dock, _) => dock.Rebuild());
        TaskLabelProperty.Changed.AddClassHandler<ArcBackgroundRunDock>((dock, _) => dock.Rebuild());
        ProgressProperty.Changed.AddClassHandler<ArcBackgroundRunDock>((dock, _) => dock.Rebuild());
    }

    public ArcBackgroundRunDock()
    {
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>The background state, set by the host.</summary>
    public ArcDockStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>One line describing the background task.</summary>
    public string TaskLabel
    {
        get => GetValue(TaskLabelProperty);
        set => SetValue(TaskLabelProperty, value);
    }

    /// <summary>Progress from 0 to 1.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Raised by the pause/resume button; the host decides which way the toggle goes.</summary>
    public event EventHandler? PauseRequested;

    /// <summary>Raised when the user wants the background run stopped.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Raised when the user wants the transcript or the run detail opened.</summary>
    public event EventHandler? ExpandRequested;

    private void Rebuild()
    {
        Children.Clear();

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        row.Children.Add(StatusMark());

        var copy = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        var label = ArcUi.Text(TaskLabel, "arc-body-sm arc-medium", wrap: TextWrapping.NoWrap);
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.VerticalAlignment = VerticalAlignment.Center;
        copy.Children.Add(label);

        if (Status is ArcDockStatus.Idle)
        {
            copy.Children.Add(ArcUi.Caption("Idle · nothing is running in the background."));
        }
        else
        {
            var meta = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
            var badge = StatusBadge(Status);
            badge.VerticalAlignment = VerticalAlignment.Center;
            meta.Children.Add(badge);

            var progress = new ArcDockProgressBar
            {
                Fraction = Math.Clamp(Progress, 0, 1),
                FillToken = Status == ArcDockStatus.NeedsAttention ? ArcTokens.Warning : ArcTokens.Accent,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(progress, 1);
            meta.Children.Add(progress);

            var percent = ArcUi.Text($"{Math.Round(Math.Clamp(Progress, 0, 1) * 100)}%", "arc-caption",
                wrap: TextWrapping.NoWrap);
            percent.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(percent, 2);
            meta.Children.Add(percent);
            copy.Children.Add(meta);
        }

        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);

        var actions = BuildActions();
        Grid.SetColumn(actions, 2);
        row.Children.Add(actions);

        Children.Add(new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(Status == ArcDockStatus.NeedsAttention ? ArcTokens.Warning : ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            Padding = new Thickness(14, 10, 14, 10),
            Child = row
        });

        ArcUi.Name(
            this,
            $"Background run: {TaskLabel}. {StatusWord(Status)}.",
            "Pause or resume the background run, open its detail or stop it.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private Control StatusMark()
    {
        var (icon, token, background) = Status switch
        {
            ArcDockStatus.Running => ("activity", ArcTokens.Accent, ArcTokens.AccentSubtle),
            ArcDockStatus.NeedsAttention => ("circle-alert", ArcTokens.Warning, ArcTokens.WarningSubtle),
            ArcDockStatus.Paused => ("pause", ArcTokens.TextSecondary, ArcTokens.SurfaceMuted),
            _ => ("sparkles", ArcTokens.TextMuted, ArcTokens.SurfaceMuted)
        };

        return new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            Background = ArcUi.Brush(background),
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

        if (Status is ArcDockStatus.Running or ArcDockStatus.Paused or ArcDockStatus.NeedsAttention)
        {
            var pause = Status == ArcDockStatus.Paused
                ? ArcUi.Button("Resume", ArcButtonVariant.Secondary, ArcButtonSize.Sm, "play")
                : ArcUi.Button("Pause", ArcButtonVariant.Secondary, ArcButtonSize.Sm, "pause");
            pause.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(pause, Status == ArcDockStatus.Paused ? "Resume the background run" : "Pause the background run");
            pause.Click += (_, _) => PauseRequested?.Invoke(this, EventArgs.Empty);
            actions.Children.Add(pause);

            var expand = ArcUi.Button(
                Status == ArcDockStatus.NeedsAttention ? "Review" : "Open",
                ArcButtonVariant.Ghost,
                ArcButtonSize.Sm,
                "maximize");
            expand.Margin = new Thickness(0, 0, 6, 0);
            ArcUi.Name(expand, "Open the background run transcript");
            expand.Click += (_, _) => ExpandRequested?.Invoke(this, EventArgs.Empty);
            actions.Children.Add(expand);

            var stop = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                Content = ArcUi.Icon("square", 14, ArcTokens.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            };
            ArcUi.Name(stop, "Stop the background run");
            ToolTip.SetTip(stop, "Stop the background run");
            stop.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);
            actions.Children.Add(stop);
        }

        return actions;
    }

    private static ArcBadge StatusBadge(ArcDockStatus status) => status switch
    {
        ArcDockStatus.Running => ArcUi.Badge("In background", ArcBadgeTone.Accent),
        ArcDockStatus.NeedsAttention => ArcUi.Badge("Needs you", ArcBadgeTone.Warning),
        ArcDockStatus.Paused => ArcUi.Badge("Paused", ArcBadgeTone.Neutral),
        _ => ArcUi.Badge("Idle", ArcBadgeTone.Neutral)
    };

    private static string StatusWord(ArcDockStatus status) => status switch
    {
        ArcDockStatus.Running => "running in the background",
        ArcDockStatus.NeedsAttention => "needs your attention",
        ArcDockStatus.Paused => "paused",
        _ => "idle"
    };
}

/// <summary>The thin progress line inside a dock; a plain block so it never animates or overshoots.</summary>
internal sealed class ArcDockProgressBar : Control
{
    public double Fraction { get; set; }

    public string FillToken { get; set; } = ArcTokens.Accent;

    protected override Size MeasureOverride(Size availableSize) => new(0, 4);

    public override void Render(DrawingContext context)
    {
        var height = Math.Min(4, Bounds.Height > 0 ? Bounds.Height : 4);
        if (height <= 0 || Bounds.Width <= 0)
        {
            return;
        }

        var top = (Bounds.Height - height) / 2;
        var radius = height / 2;
        context.DrawRectangle(ArcUi.Brush(ArcTokens.ControlTrack), null,
            new Rect(0, top, Bounds.Width, height), radius, radius);

        var width = Math.Clamp(Fraction, 0, 1) * Bounds.Width;
        if (width > 1)
        {
            context.DrawRectangle(ArcUi.Brush(FillToken), null,
                new Rect(0, top, Math.Max(height, width), height), radius, radius);
        }
    }
}
