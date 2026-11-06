using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;
using Avalonia.Controls.Metadata;

namespace Arc.Avalonia.Effects.Controls;

/// <summary>
/// A slim top-of-page bar that rotates two to four short messages, can show a live countdown and a call
/// to action, pauses while hovered or focused, and collapses its height when dismissed. Arc draws the
/// interval as a ring around the pause button; Avalonia has no ring token, so the same time is shown as
/// a thin line under the bar.
/// </summary>
[TemplatePart("PART_Root", typeof(Border))]
[TemplatePart("PART_ControlHost", typeof(StackPanel))]
[TemplatePart("PART_PrevButton", typeof(ArcButton))]
[TemplatePart("PART_PauseButton", typeof(ArcButton))]
[TemplatePart("PART_NextButton", typeof(ArcButton))]
[TemplatePart("PART_MessageHost", typeof(Panel))]
[TemplatePart("PART_DismissButton", typeof(ArcButton))]
[TemplatePart("PART_ProgressTrack", typeof(Border))]
[TemplatePart("PART_ProgressFill", typeof(Border))]
public class ArcAnnouncementBar : TemplatedControl
{
    public static readonly StyledProperty<IReadOnlyList<ArcAnnouncement>?> MessagesProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, IReadOnlyList<ArcAnnouncement>?>(nameof(Messages));

    /// <summary>Remembers dismissal for this campaign id, the analogue of Arc's localStorage key.</summary>
    public static readonly StyledProperty<string?> IdProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, string?>(nameof(Id));

    public static readonly StyledProperty<bool> OpenProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, bool>(nameof(Open), true);

    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, int>(nameof(Index));

    public static readonly StyledProperty<int> IntervalProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, int>(nameof(Interval), 6000);

    public static readonly StyledProperty<bool> AutoPlayProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, bool>(nameof(AutoPlay), true);

    public static readonly StyledProperty<bool> ControlsProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, bool>(nameof(Controls));

    public static readonly StyledProperty<bool> DismissibleProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, bool>(nameof(Dismissible), true);

    public static readonly StyledProperty<ArcAnnouncementTone> ToneProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, ArcAnnouncementTone>(nameof(Tone));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcAnnouncementBar, string>(nameof(Label), "Announcements");

    private static readonly HashSet<string> DismissedIds = new(StringComparer.Ordinal);

    private readonly List<ArcButton> _iconButtons = new();
    private Border? _root;
    private StackPanel? _controlHost;
    private ArcButton? _prev;
    private ArcButton? _pause;
    private ArcButton? _next;
    private Panel? _messageHost;
    private ArcButton? _dismiss;
    private Border? _progressTrack;
    private Border? _progressFill;
    private DispatcherTimer? _rotate;
    private DispatcherTimer? _countdown;
    private DispatcherTimer? _progress;
    private ArcTween? _tween;
    private Control? _view;
    private TextBlock? _countdownValue;
    private bool _hovering;
    private bool _paused;
    private bool _countdownEnded;
    private bool _lastOpen = true;
    private bool _closing;
    private DateTime _intervalStarted;

    static ArcAnnouncementBar()
    {
        MessagesProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.Sync());
        IdProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.Sync());
        OpenProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.Sync());
        IndexProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.RenderMessage(animate: true));
        IntervalProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.RestartRotation());
        AutoPlayProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.RestartRotation());
        ControlsProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.SyncChrome());
        DismissibleProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.SyncChrome());
        ToneProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) => bar.ApplyTone());
        LabelProperty.Changed.AddClassHandler<ArcAnnouncementBar>((bar, _) =>
            AutomationProperties.SetName(bar, bar.Label));
    }

    public ArcAnnouncementBar()
    {
        Focusable = false;
        PointerEntered += (_, _) => SetHover(true);
        PointerExited += (_, _) => SetHover(false);
        GotFocus += (_, _) => SetHover(true);
        LostFocus += (_, _) => SetHover(false);
    }

    public event EventHandler? Dismissed;

    public event EventHandler<bool>? OpenChanged;

    /// <summary>Bubbled copy of <see cref="OpenChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcAnnouncementBarOpenChangedEventArgs> OpenChangedEvent =
        RoutedEvent.Register<ArcAnnouncementBar, ArcAnnouncementBarOpenChangedEventArgs>("OpenChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="OpenChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcAnnouncementBarOpenChangedEventArgs>? OpenChangedRouted
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    private void RaiseOpenChanged(bool isOpen)
    {
        OpenChanged?.Invoke(this, isOpen);
        RaiseEvent(new ArcAnnouncementBarOpenChangedEventArgs(OpenChangedEvent) { IsOpen = isOpen });
    }

    public event EventHandler<int>? IndexChanged;

    /// <summary>Bubbled copy of <see cref="IndexChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcAnnouncementBarIndexChangedEventArgs> IndexChangedEvent =
        RoutedEvent.Register<ArcAnnouncementBar, ArcAnnouncementBarIndexChangedEventArgs>("IndexChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="IndexChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcAnnouncementBarIndexChangedEventArgs>? IndexChangedRouted
    {
        add => AddHandler(IndexChangedEvent, value);
        remove => RemoveHandler(IndexChangedEvent, value);
    }

    private void RaiseIndexChanged(int index)
    {
        IndexChanged?.Invoke(this, index);
        RaiseEvent(new ArcAnnouncementBarIndexChangedEventArgs(IndexChangedEvent) { Index = index });
    }

    public event EventHandler<ArcAnnouncement>? ActionInvoked;

    public event EventHandler<ArcAnnouncement>? CountdownEnded;

    public IReadOnlyList<ArcAnnouncement>? Messages
    {
        get => GetValue(MessagesProperty);
        set => SetValue(MessagesProperty, value);
    }

    public string? Id
    {
        get => GetValue(IdProperty);
        set => SetValue(IdProperty, value);
    }

    /// <summary>Whether the bar is showing; settable so a page can present either state.</summary>
    public bool Open
    {
        get => GetValue(OpenProperty);
        set => SetValue(OpenProperty, value);
    }

    public int Index
    {
        get => GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    /// <summary>Milliseconds each message stays before the next one.</summary>
    public int Interval
    {
        get => GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    /// <summary>Rotates automatically; off when reduced motion is on, as Arc does.</summary>
    public bool AutoPlay
    {
        get => GetValue(AutoPlayProperty);
        set => SetValue(AutoPlayProperty, value);
    }

    /// <summary>Shows previous and next beside the message.</summary>
    public bool Controls
    {
        get => GetValue(ControlsProperty);
        set => SetValue(ControlsProperty, value);
    }

    public bool Dismissible
    {
        get => GetValue(DismissibleProperty);
        set => SetValue(DismissibleProperty, value);
    }

    public ArcAnnouncementTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Forgets a remembered dismissal so the bar with this id shows again.</summary>
    public static void ClearDismissal(string id) => DismissedIds.Remove(id);

    /// <summary>Whether this campaign id was already dismissed in this session.</summary>
    public static bool IsDismissed(string id) => DismissedIds.Contains(id);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _root = e.NameScope.Find<Border>("PART_Root");
        _controlHost = e.NameScope.Find<StackPanel>("PART_ControlHost");
        _prev = e.NameScope.Find<ArcButton>("PART_PrevButton");
        _pause = e.NameScope.Find<ArcButton>("PART_PauseButton");
        _next = e.NameScope.Find<ArcButton>("PART_NextButton");
        _messageHost = e.NameScope.Find<Panel>("PART_MessageHost");
        _dismiss = e.NameScope.Find<ArcButton>("PART_DismissButton");
        _progressTrack = e.NameScope.Find<Border>("PART_ProgressTrack");
        _progressFill = e.NameScope.Find<Border>("PART_ProgressFill");

        _iconButtons.Clear();
        foreach (var button in new[] { _prev, _pause, _next, _dismiss })
        {
            if (button is null)
            {
                continue;
            }

            button.Click += OnChromeClick;
            _iconButtons.Add(button);
        }

        if (_dismiss is not null)
        {
            _dismiss.Content = ArcEffectVisuals.Icon("x", 16);
        }

        if (_prev is not null)
        {
            _prev.Content = ArcEffectVisuals.Icon("chevron-left", 16);
        }

        if (_next is not null)
        {
            _next.Content = ArcEffectVisuals.Icon("chevron-right", 16);
        }

        ApplyTone();
        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopRotation();
        StopCountdown();
        _tween?.Stop();
        _tween = null;
    }

    /// <summary>Collapses the bar and raises <see cref="Dismissed"/>.</summary>
    public void Dismiss()
    {
        if (!Open)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Id))
        {
            DismissedIds.Add(Id!);
        }

        SetCurrentValue(OpenProperty, false);
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void OnChromeClick(object? sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, _dismiss))
        {
            Dismiss();
        }
        else if (ReferenceEquals(sender, _prev))
        {
            Step(-1);
        }
        else if (ReferenceEquals(sender, _next))
        {
            Step(1);
        }
        else if (ReferenceEquals(sender, _pause))
        {
            _paused = !_paused;
            UpdatePauseIcon();
            RestartRotation();
        }
    }

    private void Step(int delta)
    {
        var count = Messages?.Count ?? 0;
        if (count == 0)
        {
            return;
        }

        var index = ((Index + delta) % count + count) % count;
        SetCurrentValue(IndexProperty, index);
        RaiseIndexChanged(index);
        RestartRotation();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape && Open && Dismissible)
        {
            Dismiss();
            e.Handled = true;
        }
    }

    private void SetHover(bool hovering)
    {
        if (_hovering == hovering)
        {
            return;
        }

        _hovering = hovering;
        RestartRotation();
    }

    private ArcAnnouncement? Current =>
        Messages is { Count: > 0 } messages ? messages[Math.Clamp(Index, 0, messages.Count - 1)] : null;

    private void Sync()
    {
        if (_root is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(Id) && DismissedIds.Contains(Id!))
        {
            SetCurrentValue(OpenProperty, false);
            return;
        }

        if (Open)
        {
            Show();
        }
        else
        {
            Collapse();
        }

        if (_lastOpen != Open)
        {
            _lastOpen = Open;
            RaiseOpenChanged(Open);
        }
    }

    private void Show()
    {
        if (_root is null)
        {
            return;
        }

        _closing = false;
        _root.IsVisible = true;
        _root.Height = double.NaN;
        _root.Opacity = 1;
        RenderMessage(animate: false);
        SyncChrome();
        RestartRotation();
        AutomationProperties.SetName(this, Label);
    }

    private void Collapse()
    {
        if (_root is null || _closing || !_root.IsVisible)
        {
            StopRotation();
            StopCountdown();
            return;
        }

        _closing = true;
        StopRotation();
        StopCountdown();

        var height = _root.Bounds.Height;
        if (ArcThemeManager.ReducedMotion || height <= 0)
        {
            _root.IsVisible = false;
            _root.Height = double.NaN;
            _root.Opacity = 1;
            _closing = false;
            return;
        }

        _root.Height = height;
        _tween?.Stop();
        _tween = ArcTween.Run(ArcMotion.SpringSmoothDuration, ArcMotion.SpringSmooth, progress =>
        {
            _root.Height = Math.Max(0, height * (1 - progress));
            _root.Opacity = 1 - progress;
        }, () =>
        {
            _root.IsVisible = false;
            _root.Height = double.NaN;
            _root.Opacity = 1;
            _closing = false;
        });
    }

    private void SyncChrome()
    {
        var count = Messages?.Count ?? 0;
        if (_controlHost is not null)
        {
            _controlHost.IsVisible = Controls && count > 1;
        }

        if (_progressTrack is not null)
        {
            _progressTrack.IsVisible = Controls && count > 1;
        }

        if (_dismiss is not null)
        {
            _dismiss.IsVisible = Dismissible;
            AutomationProperties.SetName(_dismiss, "Dismiss announcements");
        }

        if (_prev is not null)
        {
            AutomationProperties.SetName(_prev, "Previous announcement");
        }

        if (_next is not null)
        {
            AutomationProperties.SetName(_next, "Next announcement");
        }

        UpdatePauseIcon();
    }

    private void UpdatePauseIcon()
    {
        if (_pause is null)
        {
            return;
        }

        _pause.Content = ArcEffectVisuals.Icon(_paused ? "play" : "pause", 16);
        AutomationProperties.SetName(_pause, _paused ? "Resume announcements" : "Pause announcements");
        AutomationProperties.SetHelpText(_pause, _paused ? "Auto-rotation paused" : "Auto-rotation running");
        ApplyIconTone(_pause);
    }

    private void ApplyTone()
    {
        var inverted = Tone == ArcAnnouncementTone.Inverted;
        _root?.Classes.Set("inverted", inverted);
        foreach (var button in _iconButtons)
        {
            button.Classes.Set("on-inverted", inverted);
            ApplyIconTone(button);
        }
    }

    private void ApplyIconTone(ArcButton button)
    {
        if (button.Content is ArcIcon icon)
        {
            icon.Foreground = ArcEffectVisuals.Brush(Tone == ArcAnnouncementTone.Inverted
                ? ArcTokens.Background
                : ArcTokens.TextSecondary);
        }
    }

    private void RenderMessage(bool animate)
    {
        if (_messageHost is null)
        {
            return;
        }

        var announcement = Current;

        if (announcement is null)
        {
            _messageHost.Children.Clear();
            _view = null;
            StopCountdown();
            return;
        }

        var inverted = Tone == ArcAnnouncementTone.Inverted;
        var row = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var message = new TextBlock
        {
            Text = announcement.Message,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 420
        };
        message.Classes.Add("arc-body-sm");
        message.Classes.Add("arc-medium");
        message.Margin = new Thickness(0, 4, 12, 4);
        if (inverted)
        {
            message.Classes.Add("on-inverted");
        }

        row.Children.Add(message);

        _countdownValue = null;
        if (announcement.Countdown is { } countdown)
        {
            row.Children.Add(BuildCountdown(countdown, inverted));
        }

        if (BuildAction(announcement) is { } actionButton)
        {
            row.Children.Add(actionButton);
        }

        var old = _view;
        _view = row;
        _messageHost.Children.Add(row);
        StartCountdown(announcement);

        if (!animate || old is null || ArcThemeManager.ReducedMotion)
        {
            if (old is not null)
            {
                _messageHost.Children.Remove(old);
            }

            return;
        }

        var oldMove = new TranslateTransform(0, 0);
        old.RenderTransform = oldMove;
        var newMove = new TranslateTransform(0, 12);
        row.RenderTransform = newMove;
        row.Opacity = 0;

        _tween?.Stop();
        _tween = ArcTween.Run(ArcMotion.Standard, ArcMotion.EaseEnter, progress =>
        {
            old.Opacity = 1 - progress;
            oldMove.Y = -12 * progress;
            row.Opacity = progress;
            newMove.Y = 12 * (1 - progress);
        }, () =>
        {
            _messageHost.Children.Remove(old);
            row.Opacity = 1;
            newMove.Y = 0;
        });
    }

    private Control BuildCountdown(ArcAnnouncementCountdown countdown, bool inverted)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        var label = new TextBlock
        {
            Text = countdown.Label ?? "Ends in",
            VerticalAlignment = VerticalAlignment.Center
        };
        label.Classes.Add("arc-caption");
        if (inverted)
        {
            label.Classes.Add("on-inverted");
        }

        var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        value.Classes.Add("arc-label");
        if (inverted)
        {
            value.Classes.Add("on-inverted");
        }

        value.Text = FormatCountdown(countdown.To);
        AutomationProperties.SetName(value, $"Ends in {SpokenCountdown(countdown.To)}");
        AutomationProperties.SetLiveSetting(value, AutomationLiveSetting.Off);
        _countdownValue = value;

        panel.Children.Add(label);
        panel.Children.Add(value);
        return panel;
    }

    private ArcButton? BuildAction(ArcAnnouncement? announcement)
    {
        if (announcement?.Action is not { } action || string.IsNullOrWhiteSpace(action.Label))
        {
            return null;
        }

        var button = new ArcButton
        {
            Content = action.Label,
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Margin = new Thickness(0, 2, 0, 2)
        };
        button.Classes.Add("bar-action");
        if (Tone == ArcAnnouncementTone.Inverted)
        {
            button.Classes.Add("on-inverted");
            button.Foreground = ArcEffectVisuals.Brush(ArcTokens.Background);
        }

        AutomationProperties.SetName(button, action.Label);
        button.Click += (_, _) =>
        {
            action.OnClick?.Invoke();
            ActionInvoked?.Invoke(this, announcement);
        };

        return button;
    }

    private void StartCountdown(ArcAnnouncement announcement)
    {
        StopCountdown();
        _countdownEnded = false;
        if (announcement.Countdown is not { } countdown)
        {
            return;
        }

        _countdown = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) =>
        {
            if (_countdownValue is null)
            {
                return;
            }

            var remaining = countdown.To - DateTimeOffset.UtcNow;
            _countdownValue.Text = FormatCountdown(countdown.To);
            if (_countdownEnded || remaining > TimeSpan.Zero)
            {
                return;
            }

            _countdownEnded = true;
            CountdownEnded?.Invoke(this, announcement);
        });
        _countdown.Start();
    }

    private void StopCountdown()
    {
        _countdown?.Stop();
        _countdown = null;
        _countdownValue = null;
    }

    private static string FormatCountdown(DateTimeOffset to)
    {
        var remaining = to - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return "Ended";
        }

        return remaining.TotalDays >= 1
            ? $"{(int)remaining.TotalDays}d {remaining.Hours:00}h"
            : remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}h {remaining.Minutes:00}m"
                : $"{remaining.Minutes}m {remaining.Seconds:00}s";
    }

    private static string SpokenCountdown(DateTimeOffset to)
    {
        var remaining = to - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return "zero";
        }

        return $"{(int)remaining.TotalHours} hours and {remaining.Minutes} minutes";
    }

    private void RestartRotation()
    {
        StopRotation();
        StopProgress();
        var count = Messages?.Count ?? 0;
        var canRotate = Open && AutoPlay && !_paused && !_hovering && count > 1 && !ArcThemeManager.ReducedMotion;
        AutomationProperties.SetLiveSetting(this, canRotate ? AutomationLiveSetting.Off : AutomationLiveSetting.Polite);

        if (!canRotate)
        {
            if (_progressFill is not null)
            {
                _progressFill.Width = 0;
            }

            return;
        }

        _intervalStarted = DateTime.UtcNow;
        _rotate = new DispatcherTimer(
            TimeSpan.FromMilliseconds(Math.Max(1200, Interval)),
            DispatcherPriority.Background,
            (_, _) => Advance());
        _rotate.Start();

        if (Controls)
        {
            _progress = new DispatcherTimer(TimeSpan.FromMilliseconds(50), DispatcherPriority.Background, (_, _) => UpdateProgress());
            _progress.Start();
        }
    }

    private void UpdateProgress()
    {
        if (_progressFill is null || _progressTrack is null)
        {
            return;
        }

        var width = _progressTrack.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        var elapsed = (DateTime.UtcNow - _intervalStarted).TotalMilliseconds / Math.Max(1200, Interval);
        _progressFill.Width = width * Math.Clamp(elapsed, 0, 1);
    }

    private void StopProgress()
    {
        _progress?.Stop();
        _progress = null;
    }

    private void StopRotation()
    {
        _rotate?.Stop();
        _rotate = null;
        StopProgress();
    }

    private void Advance()
    {
        var count = Messages?.Count ?? 0;
        if (count <= 1)
        {
            return;
        }

        var next = (Index + 1) % count;
        SetCurrentValue(IndexProperty, next);
        RaiseIndexChanged(next);
        RestartRotation();
    }
}
