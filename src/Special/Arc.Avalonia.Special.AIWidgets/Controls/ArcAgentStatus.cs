using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>The run state an <see cref="ArcAgentStatus"/> reports.</summary>
public enum ArcAgentRunStatus
{
    Idle,
    Working,
    NeedsInput,
    Paused,
    Done,
    Failed,
    Stopped
}

/// <summary>State of one step in the run's history.</summary>
public enum ArcAgentStepState
{
    Pending,
    Active,
    Waiting,
    Done,
    Failed,
    Skipped
}

/// <summary>One step of an agent run, shown in the pill's detail panel.</summary>
public sealed class ArcAgentStep
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Lucide icon name; falls back to a small dot.</summary>
    public string? Icon { get; set; }

    public ArcAgentStepState State { get; set; }

    public double DurationSeconds { get; set; }
}

/// <summary>
/// A compact live status pill for a running AI agent: a live ring holds the agent mark, the current activity
/// sits beside it with a count and an elapsed timer, and a press opens the steps so far, a thin step progress
/// line and Pause, Stop and Open run.
///
/// Platform approximations: the detail panel expands in place inside the layout instead of floating over the
/// page on a shadow (the offscreen demo has no overlay host), the working label has no shimmer band (that lives
/// in the Effects project) and the agent mark is a generic four point star rather than a brand glyph.
/// </summary>
public class ArcAgentStatus : Panel
{
    public static readonly StyledProperty<ArcAgentRunStatus> StatusProperty =
        AvaloniaProperty.Register<ArcAgentStatus, ArcAgentRunStatus>(nameof(Status), ArcAgentRunStatus.Working);

    public static readonly StyledProperty<string> AgentNameProperty =
        AvaloniaProperty.Register<ArcAgentStatus, string>(nameof(AgentName), "Claude Sonnet 5.5");

    public static readonly StyledProperty<string> ActivityLabelProperty =
        AvaloniaProperty.Register<ArcAgentStatus, string>(nameof(ActivityLabel), "Running tests");

    public static readonly StyledProperty<string?> ActivityIconProperty =
        AvaloniaProperty.Register<ArcAgentStatus, string?>(nameof(ActivityIcon), "sparkles");

    /// <summary>A count that ticks in place without rolling the label, such as "8 of 12".</summary>
    public static readonly StyledProperty<string?> ActivityCountProperty =
        AvaloniaProperty.Register<ArcAgentStatus, string?>(nameof(ActivityCount), "8 of 12");

    public static readonly StyledProperty<double> ElapsedSecondsProperty =
        AvaloniaProperty.Register<ArcAgentStatus, double>(nameof(ElapsedSeconds), 72);

    public static readonly StyledProperty<IEnumerable<ArcAgentStep>?> StepsProperty =
        AvaloniaProperty.Register<ArcAgentStatus, IEnumerable<ArcAgentStep>?>(nameof(Steps));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ArcAgentStatus, bool>(nameof(IsExpanded));

    private readonly SvClock _clock = new();
    private double _displaySeconds;
    private TextBlock? _timerText;
    private ArcAgentLiveRing? _ring;
    private bool _pointerFocus;

    static ArcAgentStatus()
    {
        StatusProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
        AgentNameProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
        ActivityLabelProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
        ActivityIconProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
        ActivityCountProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
        ElapsedSecondsProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.ResetClock());
        StepsProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
        IsExpandedProperty.Changed.AddClassHandler<ArcAgentStatus>((status, _) => status.Rebuild());
    }

    public ArcAgentStatus()
    {
        Focusable = true;
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>The run's state; the pill words the state and changes its ring for each value.</summary>
    public ArcAgentRunStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string AgentName
    {
        get => GetValue(AgentNameProperty);
        set => SetValue(AgentNameProperty, value);
    }

    public string ActivityLabel
    {
        get => GetValue(ActivityLabelProperty);
        set => SetValue(ActivityLabelProperty, value);
    }

    public string? ActivityIcon
    {
        get => GetValue(ActivityIconProperty);
        set => SetValue(ActivityIconProperty, value);
    }

    public string? ActivityCount
    {
        get => GetValue(ActivityCountProperty);
        set => SetValue(ActivityCountProperty, value);
    }

    /// <summary>Seconds the run has been going; the pill keeps counting while the status is Working.</summary>
    public double ElapsedSeconds
    {
        get => GetValue(ElapsedSecondsProperty);
        set => SetValue(ElapsedSecondsProperty, value);
    }

    /// <summary>Every step, pending ones included, so the progress line knows the total.</summary>
    public IEnumerable<ArcAgentStep>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ResetClock();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }

    private void ResetClock()
    {
        _clock.Stop();
        _displaySeconds = ElapsedSeconds;
        UpdateTimerText();
        if (Status != ArcAgentRunStatus.Working || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        _clock.Start(seconds =>
        {
            _displaySeconds = ElapsedSeconds + seconds;
            UpdateTimerText();
        });
    }

    private void UpdateTimerText()
    {
        if (_timerText is not null)
        {
            _timerText.Text = Sv.Duration(_displaySeconds);
        }
    }

    private bool _expandedRendered;

    private void ToggleExpanded()
    {
        SetCurrentValue(IsExpandedProperty, !IsExpanded);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Enter or Key.Space)
        {
            ToggleExpanded();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && IsExpanded)
        {
            SetCurrentValue(IsExpandedProperty, false);
            e.Handled = true;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateFocusRing();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFocusRing();
    }

    private Border? _frame;

    private void UpdateFocusRing()
    {
        if (_frame is null)
        {
            return;
        }

        if (IsFocused && !_pointerFocus)
        {
            _frame.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
        }
        else
        {
            _frame.BorderBrush = Brushes.Transparent;
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        _expandedRendered = IsExpanded;

        var root = new StackPanel { Spacing = 10, HorizontalAlignment = HorizontalAlignment.Left };

        var pill = BuildPill();
        _frame = new Border
        {
            CornerRadius = new CornerRadius(9999),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Padding = new Thickness(1),
            Child = pill,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        _frame.PointerPressed += (_, args) =>
        {
            _pointerFocus = true;
            Focus();
            ToggleExpanded();
            args.Handled = true;
        };
        _frame.PointerEntered += (_, _) => pill.Opacity = 0.92;
        _frame.PointerExited += (_, _) => pill.Opacity = 1;
        AutomationProperties.SetName(_frame, Describe());
        AutomationProperties.SetHelpText(_frame, IsExpanded ? "Expanded, press to close the steps" : "Collapsed, press to open the steps");
        root.Children.Add(_frame);

        if (IsExpanded)
        {
            root.Children.Add(BuildDetail());
        }

        Children.Add(root);
        UpdateAutomation();
        UpdateFocusRing();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Describe());
        AutomationProperties.SetHelpText(this, $"Agent status: {Word(Status)}");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private string Describe()
    {
        var outcome = Status switch
        {
            ArcAgentRunStatus.Done => $"Done in {Sv.Duration(_displaySeconds)}",
            ArcAgentRunStatus.Failed => $"Failed after {Sv.Duration(_displaySeconds)}",
            ArcAgentRunStatus.Stopped => $"Stopped after {Sv.Duration(_displaySeconds)}",
            ArcAgentRunStatus.Paused => $"Paused at {Sv.Duration(_displaySeconds)}",
            ArcAgentRunStatus.NeedsInput => "Needs your approval",
            ArcAgentRunStatus.Idle => "Ready",
            _ => $"Working for {Sv.Duration(_displaySeconds)}"
        };

        var count = string.IsNullOrWhiteSpace(ActivityCount) ? string.Empty : $", {ActivityCount}";
        return $"{AgentName}: {ActivityLabel}{count}, {outcome}";
    }

    private static string Word(ArcAgentStepState state) => state switch
    {
        ArcAgentStepState.Done => "Done",
        ArcAgentStepState.Active => "In progress",
        ArcAgentStepState.Failed => "Failed",
        ArcAgentStepState.Skipped => "Skipped",
        ArcAgentStepState.Waiting => "Waiting",
        _ => "Pending"
    };

    private static string Word(ArcAgentRunStatus status) => status switch
    {
        ArcAgentRunStatus.Idle => "Ready",
        ArcAgentRunStatus.Working => "Working",
        ArcAgentRunStatus.NeedsInput => "Needs your approval",
        ArcAgentRunStatus.Paused => "Paused",
        ArcAgentRunStatus.Done => "Done",
        ArcAgentRunStatus.Failed => "Failed",
        _ => "Stopped"
    };

    private Control BuildPill()
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 2, 2, 2)
        };

        _ring = new ArcAgentLiveRing { Status = Status, Width = 30, Height = 30 };
        content.Children.Add(_ring);

        var activity = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 };
        var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        if (!string.IsNullOrWhiteSpace(ActivityIcon))
        {
            line.Children.Add(Sv.Icon(ActivityIcon!, 14, ArcTokens.TextSecondary));
        }

        var label = new TextBlock
        {
            Text = ActivityLabel,
            FontSize = Sv.Size(ArcTokens.TextSm),
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 260
        };
        line.Children.Add(label);
        activity.Children.Add(line);

        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        meta.Children.Add(Sv.Caption(AgentName));
        if (!string.IsNullOrWhiteSpace(ActivityCount))
        {
            meta.Children.Add(Sv.Pill(Sv.Numeric(ActivityCount!, "arc-caption"), ArcTokens.SurfaceMuted, ArcTokens.TextSecondary));
        }

        activity.Children.Add(meta);
        content.Children.Add(activity);

        if (Status is ArcAgentRunStatus.NeedsInput or ArcAgentRunStatus.Failed)
        {
            content.Children.Add(BuildInlineActions());
        }

        var tone = StateBadge(Status);
        if (tone is not null)
        {
            content.Children.Add(tone);
        }

        _timerText = new TextBlock
        {
            Text = Sv.Duration(_displaySeconds),
            FontSize = Sv.Size(ArcTokens.TextXs),
            FontFeatures = Sv.Tabular,
            Width = 54,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _timerText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        content.Children.Add(_timerText);

        var chevron = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Child = Sv.Icon(IsExpanded ? "chevron-up" : "chevron-down", 14, ArcTokens.TextMuted),
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(chevron);

        var pill = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(6, 4, 10, 4),
            BorderThickness = new Thickness(1),
            Child = content
        };
        pill.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        pill.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        var shadow = ArcResources.Shadows(ArcTokens.ShadowResting);
        pill.BoxShadow = shadow;
        return pill;
    }

    private Control? StateBadge(ArcAgentRunStatus status)
    {
        var (text, background, foreground) = status switch
        {
            ArcAgentRunStatus.Working => ("Working", ArcTokens.AccentSubtle, ArcTokens.Accent),
            ArcAgentRunStatus.NeedsInput => ("Needs input", ArcTokens.AccentSubtle, ArcTokens.Accent),
            ArcAgentRunStatus.Paused => ("Paused", ArcTokens.SurfaceMuted, ArcTokens.TextSecondary),
            ArcAgentRunStatus.Done => ("Done", ArcTokens.SuccessSubtle, ArcTokens.Success),
            ArcAgentRunStatus.Failed => ("Failed", ArcTokens.DangerSubtle, ArcTokens.Danger),
            ArcAgentRunStatus.Stopped => ("Stopped", ArcTokens.SurfaceMuted, ArcTokens.TextSecondary),
            _ => ("Ready", ArcTokens.SurfaceMuted, ArcTokens.TextSecondary)
        };

        var textBlock = Sv.Text(text, "arc-caption");
        textBlock.FontWeight = FontWeight.Medium;
        var pill = Sv.Pill(textBlock, background, foreground);
        pill.VerticalAlignment = VerticalAlignment.Center;
        return pill;
    }

    private Control BuildInlineActions()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        if (Status == ArcAgentRunStatus.NeedsInput)
        {
            var approve = new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Approve", Compact = false };
            approve.Click += (_, _) => SetCurrentValue(StatusProperty, ArcAgentRunStatus.Working);
            var deny = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Deny" };
            deny.Click += (_, _) => SetCurrentValue(StatusProperty, ArcAgentRunStatus.Stopped);
            row.Children.Add(approve);
            row.Children.Add(deny);
        }
        else
        {
            var retry = new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Retry" };
            retry.Click += (_, _) => SetCurrentValue(StatusProperty, ArcAgentRunStatus.Working);
            row.Children.Add(retry);
        }

        return row;
    }

    private Control BuildDetail()
    {
        var steps = (Steps ?? Array.Empty<ArcAgentStep>()).ToList();
        var done = steps.Count(step => step.State == ArcAgentStepState.Done);
        var total = Math.Max(1, steps.Count);

        var panel = new StackPanel { Spacing = 10, Width = 384 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var heading = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        heading.Children.Add(Sv.Label("Steps"));
        heading.Children.Add(Sv.Caption($"{done} of {steps.Count} finished"));
        header.Children.Add(heading);
        var openRun = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Open run" };
        Grid.SetColumn(openRun, 1);
        header.Children.Add(openRun);
        panel.Children.Add(header);

        var bar = new ArcMiniBar { Height = 4, Fraction = (double)done / total };
        panel.Children.Add(bar);

        foreach (var step in steps.TakeLast(5))
        {
            panel.Children.Add(BuildStepRow(step, Status == ArcAgentRunStatus.Working));
        }

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var pause = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    Sv.Icon(Status == ArcAgentRunStatus.Paused ? "play" : "pause", 14),
                    new TextBlock { Text = Status == ArcAgentRunStatus.Paused ? "Resume" : "Pause", FontSize = 13 }
                }
            }
        };
        pause.Click += (_, _) => SetCurrentValue(StatusProperty, Status == ArcAgentRunStatus.Paused ? ArcAgentRunStatus.Working : ArcAgentRunStatus.Paused);
        actions.Children.Add(pause);

        var stop = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    Sv.Icon("square", 13),
                    new TextBlock { Text = "Stop", FontSize = 13 }
                }
            }
        };
        stop.Click += (_, _) => SetCurrentValue(StatusProperty, ArcAgentRunStatus.Stopped);
        actions.Children.Add(stop);
        panel.Children.Add(actions);

        var card = Sv.Card(panel, 14, ArcTokens.Surface);
        card.CornerRadius = Sv.Radius(ArcTokens.RadiusPanel);
        card.HorizontalAlignment = HorizontalAlignment.Left;
        return card;
    }

    private Control BuildStepRow(ArcAgentStep step, bool shimmerActive)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*,Auto"), Height = 24 };
        AutomationProperties.SetName(grid, $"{Word(step.State)}, {step.Label}");

        Control glyph = step.State switch
        {
            ArcAgentStepState.Done => Sv.Icon("check", 14, ArcTokens.TextSecondary),
            ArcAgentStepState.Failed => Sv.Icon("x", 14, ArcTokens.Danger),
            ArcAgentStepState.Active or ArcAgentStepState.Waiting => new ArcSpinner { Size = 13, StrokeWidth = 1.8 }.Paint(ArcSpinner.StrokeProperty, ArcTokens.Accent),
            ArcAgentStepState.Skipped => Sv.Icon("minus", 14, ArcTokens.TextMuted),
            _ => Sv.Icon("circle", 12, ArcTokens.TextMuted)
        };
        grid.Children.Add(glyph);

        var label = new TextBlock
        {
            Text = step.Label,
            FontSize = Sv.Size(ArcTokens.TextSm),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 8, 0)
        };
        if (step.State is ArcAgentStepState.Done or ArcAgentStepState.Skipped)
        {
            label.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        }
        else if (step.State == ArcAgentStepState.Pending)
        {
            label.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        }
        else if (step.State == ArcAgentStepState.Active)
        {
            label.Paint(TextBlock.ForegroundProperty, ArcTokens.Accent);
        }

        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (step.DurationSeconds > 0)
        {
            var duration = Sv.Numeric(Sv.Duration(step.DurationSeconds), "arc-caption");
            duration.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
            Grid.SetColumn(duration, 2);
            grid.Children.Add(duration);
        }
        else if (step.State == ArcAgentStepState.Active && shimmerActive)
        {
            var live = Sv.Caption("now");
            live.Paint(TextBlock.ForegroundProperty, ArcTokens.Accent);
            Grid.SetColumn(live, 2);
            grid.Children.Add(live);
        }

        return grid;
    }
}

/// <summary>The thin step progress line in the pill's panel.</summary>
public sealed class ArcMiniBar : Control
{
    public static readonly StyledProperty<double> FractionProperty =
        AvaloniaProperty.Register<ArcMiniBar, double>(nameof(Fraction));

    public static readonly StyledProperty<string?> FillTokenProperty =
        AvaloniaProperty.Register<ArcMiniBar, string?>(nameof(FillToken), ArcTokens.Accent);

    static ArcMiniBar()
    {
        AffectsRender<ArcMiniBar>(FractionProperty, FillTokenProperty);
    }

    public double Fraction
    {
        get => GetValue(FractionProperty);
        set => SetValue(FractionProperty, value);
    }

    public string? FillToken
    {
        get => GetValue(FillTokenProperty);
        set => SetValue(FillTokenProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(0, Math.Max(4, Height is > 0 ? Height : 4));

    public override void Render(DrawingContext context)
    {
        var height = Bounds.Height > 0 ? Math.Min(Bounds.Height, 8) : 4;
        var top = (Bounds.Height - height) / 2;
        var radius = height / 2;
        var track = new Rect(0, top, Bounds.Width, height);
        SvDraw.Fill(context, track, radius, Sv.Brush(ArcTokens.ControlTrack));
        var width = Sv.Clamp01(Fraction) * Bounds.Width;
        if (width > 1)
        {
            SvDraw.Fill(context, new Rect(0, top, Math.Max(height, width), height), radius, Sv.Brush(FillToken ?? ArcTokens.Accent));
        }
    }
}

/// <summary>The live ring around the agent mark: it spins while working, freezes when paused and closes for done or failed.</summary>
public sealed class ArcAgentLiveRing : Control
{
    public static readonly StyledProperty<ArcAgentRunStatus> StatusProperty =
        AvaloniaProperty.Register<ArcAgentLiveRing, ArcAgentRunStatus>(nameof(Status), ArcAgentRunStatus.Working);

    private readonly SvClock _clock = new();
    private double _angle;

    static ArcAgentLiveRing()
    {
        AffectsRender<ArcAgentLiveRing>(StatusProperty);
        StatusProperty.Changed.AddClassHandler<ArcAgentLiveRing>((ring, _) => ring.Sync());
    }



    public ArcAgentRunStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _clock.Stop();
    }

    private void Sync()
    {
        _clock.Stop();
        if (Status != ArcAgentRunStatus.Working)
        {
            _angle = 0;
            InvalidateVisual();
            return;
        }

        if (ArcThemeManager.ReducedMotion)
        {
            _angle = 38;
            InvalidateVisual();
            return;
        }

        _clock.Start(seconds =>
        {
            _angle = (seconds * 150) % 360;
            InvalidateVisual();
        });
    }

    protected override Size MeasureOverride(Size availableSize) => new(30, 30);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2 - 2;
        var track = Sv.Brush(ArcTokens.ControlTrack);
        var accent = Sv.Brush(ArcTokens.Accent);
        var muted = Sv.Brush(ArcTokens.TextSecondary);
        var success = Sv.Brush(ArcTokens.Success);
        var danger = Sv.Brush(ArcTokens.Danger);

        context.DrawEllipse(null, new Pen(track, 2), center, radius, radius);

        switch (Status)
        {
            case ArcAgentRunStatus.Working:
                SvDraw.Arc(context, center, radius, _angle - 90, 110, new Pen(accent, 2.2, null, PenLineCap.Round));
                SvDraw.Mark(context, center, radius * 0.62, muted, 1.6);
                break;
            case ArcAgentRunStatus.NeedsInput:
                context.DrawEllipse(Sv.AlphaBrush(ArcTokens.Accent, 0.18), null, center, radius * 0.92, radius * 0.92);
                context.DrawEllipse(accent, null, center, radius * 0.34, radius * 0.34);
                context.DrawEllipse(null, new Pen(Sv.AlphaBrush(ArcTokens.Accent, 0.5), 1.4), center, radius * 0.66, radius * 0.66);
                break;
            case ArcAgentRunStatus.Paused:
                SvDraw.Arc(context, center, radius, 0, 360, new Pen(track, 2.4));
                SvDraw.Fill(context, new Rect(center.X - 4.5, center.Y - 4, 2.6, 8), 1.2, muted);
                SvDraw.Fill(context, new Rect(center.X + 1.9, center.Y - 4, 2.6, 8), 1.2, muted);
                break;
            case ArcAgentRunStatus.Done:
                context.DrawEllipse(null, new Pen(success, 2), center, radius, radius);
                SvDraw.Check(context, center, radius * 1.05, success, 2);
                break;
            case ArcAgentRunStatus.Failed:
                context.DrawEllipse(null, new Pen(danger, 2), center, radius, radius);
                SvDraw.Cross(context, center, radius * 1.1, danger, 2);
                break;
            case ArcAgentRunStatus.Stopped:
                context.DrawEllipse(null, new Pen(track, 2), center, radius, radius);
                SvDraw.Fill(context, new Rect(center.X - 3.4, center.Y - 3.4, 6.8, 6.8), 1.6, muted);
                break;
            default:
                context.DrawEllipse(null, new Pen(track, 2), center, radius, radius);
                SvDraw.Mark(context, center, radius * 0.58, muted, 1.6);
                break;
        }
    }
}
