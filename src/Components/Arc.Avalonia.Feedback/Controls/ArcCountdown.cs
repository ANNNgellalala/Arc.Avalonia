using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcCountdownUnit
{
    Days,
    Hours,
    Minutes,
    Seconds
}

public enum ArcCountdownVariant
{
    Large,
    Compact
}

/// <summary>
/// A countdown to a fixed moment with tabular digits. At zero the digits give way to the live
/// state, and the remaining time is exposed as words for assistive tech.
/// </summary>

    /// <summary>Event data for the bubbling <see cref="CompletedRouted"/> event.</summary>
    public sealed class ArcCountdownCompletedEventArgs : RoutedEventArgs
    {
        /// <summary>Creates the event data for a routed event.</summary>
        public ArcCountdownCompletedEventArgs(RoutedEvent routedEvent) : base(routedEvent)
        {
        }
    }
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcCountdown : TemplatedControl
{
    public static readonly StyledProperty<DateTimeOffset?> TargetProperty =
        AvaloniaProperty.Register<ArcCountdown, DateTimeOffset?>(nameof(Target));

    public static readonly StyledProperty<IEnumerable<ArcCountdownUnit>?> UnitsProperty =
        AvaloniaProperty.Register<ArcCountdown, IEnumerable<ArcCountdownUnit>?>(nameof(Units));

    public static readonly StyledProperty<ArcCountdownVariant> VariantProperty =
        AvaloniaProperty.Register<ArcCountdown, ArcCountdownVariant>(nameof(Variant));

    public static readonly StyledProperty<string> CompleteLabelProperty =
        AvaloniaProperty.Register<ArcCountdown, string>(nameof(CompleteLabel), "Live now");

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcCountdown, string>(nameof(Label), "Time remaining");

    public static readonly StyledProperty<Func<long>?> ClockProperty =
        AvaloniaProperty.Register<ArcCountdown, Func<long>?>(nameof(Clock));

    private Panel? _host;
    private DispatcherTimer? _timer;
    private DispatcherTimer? _pulse;
    private long _lastShown = long.MinValue;
    private bool _completed;
    private Border? _liveDot;

    static ArcCountdown()
    {
        TargetProperty.Changed.AddClassHandler<ArcCountdown>((countdown, _) => countdown.Reset());
        UnitsProperty.Changed.AddClassHandler<ArcCountdown>((countdown, _) => countdown.Reset());
        VariantProperty.Changed.AddClassHandler<ArcCountdown>((countdown, _) => countdown.Reset());
        CompleteLabelProperty.Changed.AddClassHandler<ArcCountdown>((countdown, _) => countdown.Render());
        LabelProperty.Changed.AddClassHandler<ArcCountdown>((countdown, _) => countdown.Render());
    }

    /// <summary>The moment to count to.</summary>
    public DateTimeOffset? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Units to show, largest first; the largest unit absorbs the rest.</summary>
    public IEnumerable<ArcCountdownUnit>? Units
    {
        get => GetValue(UnitsProperty);
        set => SetValue(UnitsProperty, value);
    }

    public ArcCountdownVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Shown once the target passes, with a live dot.</summary>
    public string CompleteLabel
    {
        get => GetValue(CompleteLabelProperty);
        set => SetValue(CompleteLabelProperty, value);
    }

    /// <summary>Accessible name, such as "Keynote starts in".</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Clock source in epoch milliseconds; defaults to the system clock.</summary>
    public Func<long>? Clock
    {
        get => GetValue(ClockProperty);
        set => SetValue(ClockProperty, value);
    }

    /// <summary>Fires once when the countdown reaches zero.</summary>
    public event EventHandler? Completed;
    /// <summary>Bubbling routed counterpart of <see cref="Completed"/>.</summary>
    public static readonly RoutedEvent<ArcCountdownCompletedEventArgs> CompletedEvent =
        RoutedEvent.Register<ArcCountdown, ArcCountdownCompletedEventArgs>("Completed", RoutingStrategies.Bubble);

    /// <summary>Subscribes to the bubbling <see cref="ArcCountdownCompletedEventArgs"/> version of <see cref="Completed"/>.</summary>
    public event EventHandler<ArcCountdownCompletedEventArgs>? CompletedRouted
    {
        add => AddHandler(CompletedEvent, value);
        remove => RemoveHandler(CompletedEvent, value);
    }

    private void RaiseCompleted()
    {
        Completed?.Invoke(this, EventArgs.Empty);
        RaiseEvent(new ArcCountdownCompletedEventArgs(CompletedEvent));
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Reset();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        StartTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer?.Stop();
        _timer = null;
        _pulse?.Stop();
        _pulse = null;
    }

    private void Reset()
    {
        _completed = false;
        _lastShown = long.MinValue;
        Render();
        StartTimer();
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = null;
        if (_host is null)
        {
            return;
        }

        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Render());
        _timer.Start();
    }

    private long Now() => Clock?.Invoke() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void Render()
    {
        if (_host is null)
        {
            return;
        }

        var target = Target;
        var now = Now();
        var remainingMs = target is null ? 0 : Math.Max(0, target.Value.ToUnixTimeMilliseconds() - now);
        var complete = target is not null && remainingMs <= 0;

        AutomationProperties.SetName(this, Label);

        if (complete)
        {
            if (_completed && _host.Children.Count > 0)
            {
                return;
            }

            if (!_completed)
            {
                _completed = true;
                RaiseCompleted();
            }

            _host.Children.Clear();
            _host.Children.Add(BuildComplete());
            AutomationProperties.SetHelpText(this, CompleteLabel);
            StartPulse();
            return;
        }

        _pulse?.Stop();
        _pulse = null;
        if (remainingMs == _lastShown && _host.Children.Count > 0)
        {
            return;
        }

        _lastShown = remainingMs;
        var parts = Parts(remainingMs, out var words);
        _host.Children.Clear();
        _host.Children.Add(Variant == ArcCountdownVariant.Compact ? BuildCompact(parts) : BuildLarge(parts));
        AutomationProperties.SetHelpText(this, words + " remaining");
    }

    private Control BuildComplete()
    {
        var dot = new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center
        };
        dot.Paint(Border.BackgroundProperty, ArcTokens.Success);
        _liveDot = dot;

        var row = FeedbackUi.Row(8, dot, FeedbackUi.Text(CompleteLabel, "arc-label"));
        var pill = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(14, 7, 16, 7),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = row
        };
        pill.Paint(Border.BackgroundProperty, ArcTokens.SuccessSubtle);
        if (row.Children[1] is TextBlock text)
        {
            text.Paint(TextBlock.ForegroundProperty, ArcTokens.Success);
        }

        return pill;
    }

    private Control BuildLarge(IReadOnlyList<(string Value, string Unit)> parts)
    {
        var panel = new StackPanel { Spacing = 10 };
        if (!string.IsNullOrWhiteSpace(Label))
        {
            panel.Children.Add(FeedbackUi.Text(Label, "arc-caption"));
        }

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 24 };
        foreach (var (value, unit) in parts)
        {
            var block = new StackPanel { Spacing = 2 };
            var number = FeedbackUi.Numeric(value);
            number.FontSize = ArcResources.Double(ArcTokens.Text3xl);
            number.FontWeight = FontWeight.Medium;
            number.LineHeight = 40;
            number.LetterSpacing = -1;
            block.Children.Add(number);
            block.Children.Add(FeedbackUi.Text(unit, "arc-caption"));
            row.Children.Add(block);
        }

        panel.Children.Add(row);
        return panel;
    }

    private Control BuildCompact(IReadOnlyList<(string Value, string Unit)> parts)
    {
        var text = string.Join(" ", parts.Select(part => $"{CompactValue(part.Value)}{Short(part.Unit)}"));
        var number = FeedbackUi.Numeric(text, "arc-label");
        number.FontSize = 13;

        var pill = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(12, 5, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = number
        };
        pill.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        number.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        ToolTip.SetTip(pill, Label);
        return pill;
    }

    private static string Short(string unit) => unit switch
    {
        "days" => "d",
        "hours" => "h",
        "minutes" => "m",
        _ => "s"
    };

    private static string CompactValue(string value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number.ToString(CultureInfo.InvariantCulture)
            : value;

    private IReadOnlyList<(string Value, string Unit)> Parts(long remainingMs, out string words)
    {
        var units = (Units ?? new[]
        {
            ArcCountdownUnit.Days,
            ArcCountdownUnit.Hours,
            ArcCountdownUnit.Minutes,
            ArcCountdownUnit.Seconds
        }).ToList();

        units = units.Distinct().OrderBy(unit => (int)unit).ToList();
        var totalSeconds = (long)Math.Ceiling(remainingMs / 1000.0);
        var hasDays = units.Contains(ArcCountdownUnit.Days);

        long days = 0, hours = 0, minutes = 0, seconds = 0;
        if (hasDays)
        {
            days = totalSeconds / 86400;
            var rest = totalSeconds % 86400;
            hours = rest / 3600;
            minutes = rest % 3600 / 60;
            seconds = rest % 60;
        }
        else
        {
            hours = totalSeconds / 3600;
            minutes = totalSeconds % 3600 / 60;
            seconds = totalSeconds % 60;
        }

        var ordered = units.Select(unit => unit switch
        {
            ArcCountdownUnit.Days => (Value: days.ToString(CultureInfo.InvariantCulture), Unit: "days", Spoken: days.ToString(CultureInfo.InvariantCulture)),
            ArcCountdownUnit.Hours => (Value: hours.ToString(hours < 100 ? "00" : "0", CultureInfo.InvariantCulture), Unit: "hours", Spoken: hours.ToString(CultureInfo.InvariantCulture)),
            ArcCountdownUnit.Minutes => (Value: minutes.ToString("00", CultureInfo.InvariantCulture), Unit: "minutes", Spoken: minutes.ToString(CultureInfo.InvariantCulture)),
            _ => (Value: seconds.ToString("00", CultureInfo.InvariantCulture), Unit: "seconds", Spoken: seconds.ToString(CultureInfo.InvariantCulture))
        }).ToList();

        var compact = Variant == ArcCountdownVariant.Compact;
        if (compact)
        {
            var first = ordered.FindIndex(part => !int.TryParse(part.Spoken, out var value) || value != 0);
            if (first > 0 && first < ordered.Count)
            {
                ordered = ordered.Skip(first).ToList();
            }
        }

        words = string.Join(" ", ordered.Where(part => part.Spoken != "0")
            .Select(part => $"{part.Spoken} {part.Unit}"));
        if (string.IsNullOrWhiteSpace(words))
        {
            words = "Less than a second";
        }

        return ordered.Select(part => (part.Value, part.Unit)).ToList();
    }

    private void StartPulse()
    {
        _pulse?.Stop();
        _pulse = null;
        if (ArcThemeManager.ReducedMotion || _liveDot is null)
        {
            return;
        }

        var started = DateTime.UtcNow;
        _pulse = new DispatcherTimer(TimeSpan.FromMilliseconds(40), DispatcherPriority.Render, (_, _) =>
        {
            if (_liveDot is null)
            {
                return;
            }

            var phase = (DateTime.UtcNow - started).TotalSeconds / 1.8;
            _liveDot.Opacity = 0.55 + 0.45 * (0.5 + 0.5 * Math.Sin(phase * Math.PI * 2));
        });
        _pulse.Start();
    }
}
