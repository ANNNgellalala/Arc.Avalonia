using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcTimelineTone
{
    Neutral,
    Success,
    Danger
}

/// <summary>One update in an <see cref="ArcTimeline"/>.</summary>
public sealed class ArcTimelineEvent
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    public string? Actor { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Meta { get; set; }

    /// <summary>Optional expandable detail shown in place.</summary>
    public object? Detail { get; set; }

    public string? Avatar { get; set; }

    /// <summary>Lucide icon name for the node; a dot is drawn when neither icon nor avatar is set.</summary>
    public string? Icon { get; set; }

    public ArcTimelineTone Tone { get; set; }
}

/// <summary>
/// A vertical activity feed grouped by day, newest first. Rows with detail expand in place;
/// arrow keys move between rows and Enter toggles, and the connector line reads as one path.
/// </summary>
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcTimeline : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcTimelineEvent>?> EventsProperty =
        AvaloniaProperty.Register<ArcTimeline, IEnumerable<ArcTimelineEvent>?>(nameof(Events));

    public static readonly StyledProperty<DateTimeOffset> NowProperty =
        AvaloniaProperty.Register<ArcTimeline, DateTimeOffset>(nameof(Now), DateTimeOffset.Now);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcTimeline, string>(nameof(Label), "Activity");

    public static readonly StyledProperty<IEnumerable<string>?> DefaultExpandedProperty =
        AvaloniaProperty.Register<ArcTimeline, IEnumerable<string>?>(nameof(DefaultExpanded));

    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly List<Border> _rowFrames = new();
    private readonly List<bool> _rowExpandable = new();
    private Panel? _host;
    private int _focusIndex;
    private bool _keyboardFocus;

    static ArcTimeline()
    {
        EventsProperty.Changed.AddClassHandler<ArcTimeline>((timeline, _) => timeline.Rebuild());
        NowProperty.Changed.AddClassHandler<ArcTimeline>((timeline, _) => timeline.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcTimeline>((timeline, _) => timeline.UpdateAutomation());
        DefaultExpandedProperty.Changed.AddClassHandler<ArcTimeline>((timeline, _) => timeline.ResetExpanded());
        MaxHeightProperty.Changed.AddClassHandler<ArcTimeline>((timeline, _) => timeline.Rebuild());
    }

    public ArcTimeline()
    {
        Focusable = true;
    }

    public IEnumerable<ArcTimelineEvent>? Events
    {
        get => GetValue(EventsProperty);
        set => SetValue(EventsProperty, value);
    }

    /// <summary>Reference time for relative labels and day groups.</summary>
    public DateTimeOffset Now
    {
        get => GetValue(NowProperty);
        set => SetValue(NowProperty, value);
    }

    /// <summary>Accessible name for the feed.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Event ids expanded on mount.</summary>
    public IEnumerable<string>? DefaultExpanded
    {
        get => GetValue(DefaultExpandedProperty);
        set => SetValue(DefaultExpandedProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        ResetExpanded();
    }

    private void ResetExpanded()
    {
        _expanded.Clear();
        foreach (var id in DefaultExpanded ?? Array.Empty<string>())
        {
            _expanded.Add(id);
        }

        Rebuild();
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label);
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _rowFrames.Clear();
        _rowExpandable.Clear();
        _focusIndex = 0;

        var events = (Events ?? Array.Empty<ArcTimelineEvent>())
            .OrderByDescending(item => item.At)
            .ToList();

        var content = new StackPanel { Spacing = 0 };
        var groups = events.GroupBy(item => DateOnly.FromDateTime(item.At.LocalDateTime));

        foreach (var group in groups)
        {
            var header = new TextBlock
            {
                Text = DayLabel(group.Key),
                FontSize = ArcResources.Double(ArcTokens.TextXs),
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(0, 14, 0, 8)
            };
            header.Classes.Add("arc-label");
            header.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            content.Children.Add(header);

            var rows = group.ToList();
            for (var i = 0; i < rows.Count; i++)
            {
                content.Children.Add(BuildRow(rows[i], last: i == rows.Count - 1));
            }
        }

        if (events.Count == 0)
        {
            content.Children.Add(FeedbackUi.Text("No activity yet.", "arc-caption"));
        }

        if (double.IsNaN(MaxHeight) || MaxHeight <= 0)
        {
            _host.Children.Add(content);
        }
        else
        {
            _host.Children.Add(new ScrollViewer
            {
                MaxHeight = MaxHeight,
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
        }

        UpdateFocusVisuals();
        UpdateAutomation();
    }

    private string DayLabel(DateOnly day)
    {
        var today = DateOnly.FromDateTime(Now.LocalDateTime);
        if (day == today)
        {
            return "Today";
        }

        if (day == today.AddDays(-1))
        {
            return "Yesterday";
        }

        return day.ToString("MMM d", CultureInfo.InvariantCulture);
    }

    private Control BuildRow(ArcTimelineEvent item, bool last)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition(56, GridUnitType.Pixel));
        row.ColumnDefinitions.Add(new ColumnDefinition(28, GridUnitType.Pixel));
        row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var time = FeedbackUi.Numeric(item.At.LocalDateTime.ToString("HH:mm", CultureInfo.InvariantCulture), "arc-caption");
        time.TextAlignment = TextAlignment.Right;
        time.Margin = new Thickness(0, 2, 10, 0);
        time.VerticalAlignment = VerticalAlignment.Top;
        time.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        row.Children.Add(time);

        var gutter = new Panel();
        if (!last)
        {
            var line = new Border
            {
                Width = 1,
                Margin = new Thickness(0, 26, 0, -2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            line.Paint(Border.BackgroundProperty, ArcTokens.ControlTrack);
            gutter.Children.Add(line);
        }

        var node = BuildNode(item);
        node.VerticalAlignment = VerticalAlignment.Top;
        node.HorizontalAlignment = HorizontalAlignment.Center;
        gutter.Children.Add(node);
        Grid.SetColumn(gutter, 1);
        row.Children.Add(gutter);

        var body = new StackPanel { Spacing = 3, Margin = new Thickness(10, 0, 12, 18) };
        var title = new TextBlock { TextWrapping = TextWrapping.Wrap };
        title.Classes.Add("arc-body-sm");
        if (!string.IsNullOrWhiteSpace(item.Actor))
        {
            FeedbackUi.AddRun(title, item.Actor!, null, FontWeight.Medium);
            FeedbackUi.AddRun(title, " ");
        }

        FeedbackUi.AddRun(title, item.Title);
        body.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(item.Meta))
        {
            body.Children.Add(FeedbackUi.Text(item.Meta, "arc-caption"));
        }

        if (item.Detail is not null)
        {
            Control detailContent = item.Detail is string text
                ? new TextBlock
                {
                    Text = text,
                    FontFamily = FeedbackUi.MonoFamily,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap
                }.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary)
                : new ContentControl { Content = item.Detail };

            var detail = new Border
            {
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 8, 0, 4),
                IsVisible = _expanded.Contains(item.Id),
                Child = detailContent,
                Tag = "detail"
            };
            detail.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            body.Children.Add(detail);
        }

        Grid.SetColumn(body, 2);
        row.Children.Add(body);

        var chevron = new ArcIconButton
        {
            Icon = "chevron-down",
            IconSize = 14,
            BoxSize = 24,
            Muted = true,
            Label = $"Expand {item.Title}",
            IsVisible = item.Detail is not null,
            VerticalAlignment = VerticalAlignment.Top
        };
        if (_expanded.Contains(item.Id))
        {
            chevron.RenderTransform = new RotateTransform(180);
        }

        Grid.SetColumn(chevron, 3);
        row.Children.Add(chevron);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(4, 4, 2, 0),
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Child = row
        };
        _rowFrames.Add(frame);
        _rowExpandable.Add(item.Detail is not null);

        var index = _rowFrames.Count - 1;
        frame.Cursor = new Cursor(item.Detail is not null ? StandardCursorType.Hand : StandardCursorType.Arrow);
        frame.PointerPressed += (_, args) =>
        {
            _keyboardFocus = false;
            _focusIndex = index;
            Focus();
            Toggle(index);
            UpdateFocusVisuals();
            args.Handled = true;
        };

        AutomationProperties.SetName(frame, BuildAutomationName(item));
        return frame;
    }

    private string BuildAutomationName(ArcTimelineEvent item)
    {
        var parts = new List<string> { item.At.LocalDateTime.ToString("HH:mm", CultureInfo.InvariantCulture) };
        parts.Add(string.IsNullOrWhiteSpace(item.Actor) ? item.Title : $"{item.Actor} {item.Title}");
        if (!string.IsNullOrWhiteSpace(item.Meta))
        {
            parts.Add(item.Meta!);
        }

        if (item.Tone == ArcTimelineTone.Success)
        {
            parts.Add("success");
        }
        else if (item.Tone == ArcTimelineTone.Danger)
        {
            parts.Add("error");
        }

        return string.Join(", ", parts);
    }

    private Control BuildNode(ArcTimelineEvent item)
    {
        if (!string.IsNullOrWhiteSpace(item.Avatar))
        {
            return new ArcAvatar
            {
                DisplayName = item.Actor ?? string.Empty,
                Src = item.Avatar,
                Size = ArcAvatarSize.Xs
            };
        }

        var (background, foreground, icon) = item.Tone switch
        {
            ArcTimelineTone.Success => (ArcTokens.SuccessSubtle, ArcTokens.Success, "check"),
            ArcTimelineTone.Danger => (ArcTokens.DangerSubtle, ArcTokens.Danger, "triangle-alert"),
            _ => (ArcTokens.SurfaceMuted, ArcTokens.TextSecondary, item.Icon)
        };

        var node = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(12),
            Child = icon is null
                ? new Border
                {
                    Width = 7,
                    Height = 7,
                    CornerRadius = new CornerRadius(4),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
                : FeedbackUi.Icon(icon, 13, foreground)
        };
        node.Paint(Border.BackgroundProperty, background);

        if (node.Child is Border dot)
        {
            dot.Paint(Border.BackgroundProperty, foreground);
        }

        return node;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rowFrames.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                MoveFocus(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveFocus(-1);
                e.Handled = true;
                break;
            case Key.Home:
                _focusIndex = 0;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                e.Handled = true;
                break;
            case Key.End:
                _focusIndex = _rowFrames.Count - 1;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Toggle(_focusIndex);
                e.Handled = true;
                break;
        }
    }

    private void MoveFocus(int delta)
    {
        var index = _focusIndex;
        for (var step = 0; step < _rowFrames.Count; step++)
        {
            index = Math.Clamp(index + delta, 0, _rowFrames.Count - 1);
            if (_rowExpandable[index] || !_keyboardFocus)
            {
                break;
            }
        }

        _focusIndex = index;
        _keyboardFocus = true;
        UpdateFocusVisuals();
    }

    private void Toggle(int index)
    {
        if (index < 0 || index >= _rowFrames.Count || !_rowExpandable[index])
        {
            return;
        }

        var frame = _rowFrames[index];
        if (frame.Child is Grid row && row.Children[2] is StackPanel body)
        {
            var detail = body.Children.OfType<Border>().FirstOrDefault(child => Equals(child.Tag, "detail"));
            if (detail is not null)
            {
                detail.IsVisible = !detail.IsVisible;
                if (row.Children[3] is ArcIconButton chevron)
                {
                    chevron.RenderTransform = detail.IsVisible ? new RotateTransform(180) : null;
                }
            }
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateFocusVisuals();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFocusVisuals();
    }

    private void UpdateFocusVisuals()
    {
        for (var i = 0; i < _rowFrames.Count; i++)
        {
            if (IsFocused && _keyboardFocus && i == _focusIndex)
            {
                _rowFrames[i].Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
            }
            else
            {
                _rowFrames[i].BorderBrush = Brushes.Transparent;
            }
        }
    }
}
