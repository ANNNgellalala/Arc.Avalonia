using System;
using System.Collections.Generic;
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

/// <summary>Live state of one tool call.</summary>
public enum ArcToolCallStatus
{
    Running,
    Success,
    Error,
    Approval,
    Denied
}

/// <summary>One row of a tool's list output.</summary>
public sealed class ArcToolListItem
{
    public string? Lead { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Tag { get; set; }
}

/// <summary>A key and value pair the model passed to the tool.</summary>
public sealed class ArcToolArgument
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

/// <summary>One tool invocation in an agent transcript.</summary>
public sealed class ArcToolCallData
{
    public string Id { get; set; } = string.Empty;

    /// <summary>github, web, slack, postgres, gmail, linear or any other name.</summary>
    public string Tool { get; set; } = "web";

    public string Title { get; set; } = string.Empty;

    /// <summary>Present tense phrasing used while the call runs.</summary>
    public string? RunningTitle { get; set; }

    /// <summary>Imperative phrasing used while the call waits for approval.</summary>
    public string? ApprovalTitle { get; set; }

    public ArcToolCallStatus Status { get; set; }

    public IReadOnlyList<ArcToolArgument>? Input { get; set; }

    public IReadOnlyList<ArcToolListItem>? Output { get; set; }

    /// <summary>Quiet meta before the duration, such as "4 results".</summary>
    public string? Summary { get; set; }

    /// <summary>The error reason, or what allowing the call will do.</summary>
    public string? Message { get; set; }

    public double DurationSeconds { get; set; }
}

/// <summary>
/// Several tool calls from one assistant turn under a quiet "Used 4 tools" header. Each row opens in place
/// into its Input and Output, a call waiting for a decision shows Allow and Deny, and a failed call offers
/// Retry.
///
/// Platform approximations: the rows are composed Avalonia controls rather than a virtualised DOM, so the
/// height change on open is a layout step instead of an animated spring, and tool marks are neutral letter
/// or icon plates rather than brand glyphs.
/// </summary>
public class ArcToolCall : Panel
{
    public static readonly StyledProperty<IEnumerable<ArcToolCallData>?> CallsProperty =
        AvaloniaProperty.Register<ArcToolCall, IEnumerable<ArcToolCallData>?>(nameof(Calls));

    public static readonly StyledProperty<bool> ShowHeaderProperty =
        AvaloniaProperty.Register<ArcToolCall, bool>(nameof(ShowHeader), true);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcToolCall, bool>(nameof(IsOpen), true);

    public static readonly StyledProperty<string?> DefaultOpenIdProperty =
        AvaloniaProperty.Register<ArcToolCall, string?>(nameof(DefaultOpenId));

    private readonly HashSet<string> _open = new(StringComparer.Ordinal);
    private bool _pointerFocus;
    private bool _initialised;

    static ArcToolCall()
    {
        CallsProperty.Changed.AddClassHandler<ArcToolCall>((call, _) => call.Rebuild());
        ShowHeaderProperty.Changed.AddClassHandler<ArcToolCall>((call, _) => call.Rebuild());
        IsOpenProperty.Changed.AddClassHandler<ArcToolCall>((call, _) => call.Rebuild());
        DefaultOpenIdProperty.Changed.AddClassHandler<ArcToolCall>((call, _) => call.ResetOpen());
    }

    public ArcToolCall()
    {
        Focusable = false;
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>The calls of one assistant turn, in order.</summary>
    public IEnumerable<ArcToolCallData>? Calls
    {
        get => GetValue(CallsProperty);
        set => SetValue(CallsProperty, value);
    }

    /// <summary>Shows the "Used 4 tools" header with the mark stack and the total duration.</summary>
    public bool ShowHeader
    {
        get => GetValue(ShowHeaderProperty);
        set => SetValue(ShowHeaderProperty, value);
    }

    /// <summary>Whether the list is open; a call that needs a decision opens it.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The row that starts expanded, so the transcript shows its arguments and result.</summary>
    public string? DefaultOpenId
    {
        get => GetValue(DefaultOpenIdProperty);
        set => SetValue(DefaultOpenIdProperty, value);
    }

    private void ResetOpen()
    {
        _open.Clear();
        if (!string.IsNullOrEmpty(DefaultOpenId))
        {
            _open.Add(DefaultOpenId!);
        }

        _initialised = true;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (!_initialised)
        {
            ResetOpen();
        }
    }

    private void Rebuild()
    {
        Children.Clear();
        var calls = (Calls ?? Array.Empty<ArcToolCallData>()).ToList();
        var root = new StackPanel { Spacing = 2 };

        if (!_initialised && !string.IsNullOrEmpty(DefaultOpenId))
        {
            _open.Add(DefaultOpenId!);
            _initialised = true;
        }

        if (ShowHeader)
        {
            root.Children.Add(BuildHeader(calls));
        }

        if (IsOpen || !ShowHeader)
        {
            foreach (var call in calls)
            {
                root.Children.Add(BuildRow(call));
                if (_open.Contains(call.Id))
                {
                    root.Children.Add(BuildPanel(call));
                }
            }
        }

        Children.Add(root);
        UpdateAutomation(calls);
    }

    private void UpdateAutomation(List<ArcToolCallData> calls)
    {
        var running = calls.Count(call => call.Status == ArcToolCallStatus.Running);
        AutomationProperties.SetName(this, ShowHeader ? $"Used {calls.Count} tools" : calls.FirstOrDefault()?.Title ?? "Tool call");
        AutomationProperties.SetHelpText(this, running > 0 ? $"{running} running" : "All calls finished");
    }

    private Control BuildHeader(List<ArcToolCallData> calls)
    {
        var total = calls.Sum(call => call.DurationSeconds);
        var active = calls.Any(call => call.Status == ArcToolCallStatus.Running);
        var text = active ? $"Using {calls.Count} tools" : $"Used {calls.Count} tools";

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(Sv.Icon(IsOpen ? "chevron-down" : "chevron-right", 14, ArcTokens.TextMuted));

        var marks = new StackPanel { Orientation = Orientation.Horizontal, Spacing = -6, VerticalAlignment = VerticalAlignment.Center };
        foreach (var tool in calls.Select(call => call.Tool).Distinct().Take(3))
        {
            var plate = ToolPlate(tool, 18);
            plate.BorderThickness = new Thickness(1.5);
            plate.Paint(Border.BorderBrushProperty, ArcTokens.Surface);
            marks.Children.Add(plate);
        }

        row.Children.Add(marks);

        var label = new TextBlock
        {
            Text = text,
            FontSize = Sv.Size(ArcTokens.TextXs),
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(label);

        if (total > 0)
        {
            row.Children.Add(Sv.Numeric($"· {Sv.Duration(total)}", "arc-caption"));
        }

        var header = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 5, 10, 5),
            Child = row,
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent
        };
        header.PointerEntered += (_, _) => header.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        header.PointerExited += (_, _) => header.Background = Brushes.Transparent;
        header.PointerPressed += (_, args) =>
        {
            _pointerFocus = true;
            SetCurrentValue(IsOpenProperty, !IsOpen);
            args.Handled = true;
        };
        AutomationProperties.SetName(header, $"{text}, {Sv.Duration(total)}, press to {(IsOpen ? "fold" : "open")}");
        return header;
    }

    private Control BuildRow(ArcToolCallData call)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*,Auto,Auto"), Height = 40 };

        var plate = ToolPlate(call.Tool, 22);
        plate.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(plate);

        var body = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
        var title = new TextBlock
        {
            Text = TitleFor(call),
            FontSize = Sv.Size(ArcTokens.TextSm),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (call.Status == ArcToolCallStatus.Running)
        {
            title.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        }

        if (call.Status == ArcToolCallStatus.Denied)
        {
            title.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        }

        body.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(call.Message) && call.Status is ArcToolCallStatus.Error or ArcToolCallStatus.Approval or ArcToolCallStatus.Denied)
        {
            var message = new TextBlock
            {
                Text = call.Message,
                FontSize = Sv.Size(ArcTokens.TextXs),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            message.Paint(TextBlock.ForegroundProperty, call.Status == ArcToolCallStatus.Error ? ArcTokens.Danger : ArcTokens.TextMuted);
            body.Children.Add(message);
        }

        Grid.SetColumn(body, 1);
        grid.Children.Add(body);

        if (call.Status is ArcToolCallStatus.Approval)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var allow = new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Allow" };
            allow.Click += (_, _) => Approve(call);
            var deny = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Deny" };
            deny.Click += (_, _) => Deny(call);
            AutomationProperties.SetName(allow, $"Allow: {call.Title}");
            AutomationProperties.SetName(deny, $"Deny: {call.Title}");
            actions.Children.Add(allow);
            actions.Children.Add(deny);
            Grid.SetColumn(actions, 2);
            grid.Children.Add(actions);
        }
        else if (call.Status == ArcToolCallStatus.Error)
        {
            var retry = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Retry" };
            retry.Click += (_, _) => Retry(call);
            AutomationProperties.SetName(retry, $"Retry: {call.Title}");
            retry.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetColumn(retry, 2);
            grid.Children.Add(retry);
        }
        else
        {
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            if (!string.IsNullOrWhiteSpace(call.Summary))
            {
                meta.Children.Add(Sv.Caption(call.Summary!));
            }

            if (call.DurationSeconds > 0)
            {
                meta.Children.Add(Sv.Numeric(Sv.Duration(call.DurationSeconds), "arc-caption"));
            }

            Grid.SetColumn(meta, 2);
            grid.Children.Add(meta);
        }

        var status = BuildStatusGlyph(call.Status);
        status.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(status, 3);
        grid.Children.Add(status);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(8, 2, 10, 2),
            BorderThickness = new Thickness(1),
            Child = grid,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable(ArcTokens.Surface));
        frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable(call.Status == ArcToolCallStatus.Approval ? ArcTokens.Accent : ArcTokens.BorderSubtle));
        frame.Focusable = true;

        frame.PointerEntered += (_, _) =>
        {
            frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable(ArcTokens.SurfaceMuted));
        };
        frame.PointerExited += (_, _) =>
        {
            frame.Bind(Border.BackgroundProperty, frame.GetResourceObservable(ArcTokens.Surface));
            frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable(call.Status == ArcToolCallStatus.Approval ? ArcTokens.Accent : ArcTokens.BorderSubtle));
        };
        frame.PointerPressed += (_, args) =>
        {
            _pointerFocus = true;
            ToggleRow(call);
            args.Handled = true;
        };
        frame.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Enter or Key.Space)
            {
                ToggleRow(call);
                args.Handled = true;
            }
        };
        frame.GotFocus += (_, _) => ShowFocus(frame, true);
        frame.LostFocus += (_, _) => ShowFocus(frame, false);

        AutomationProperties.SetName(frame, Describe(call));
        AutomationProperties.SetHelpText(frame, _open.Contains(call.Id) ? "Open, press to fold" : "Press to open the input and output");
        return frame;
    }

    private void ShowFocus(Border frame, bool focused)
    {
        var ring = focused && !_pointerFocus;
        frame.BorderThickness = new Thickness(ring ? 2 : 1);
        frame.Padding = ring ? new Thickness(7, 1, 9, 1) : new Thickness(8, 2, 10, 2);
        frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable(ring ? ArcTokens.FocusOutline : ArcTokens.BorderSubtle));
    }

    private string Describe(ArcToolCallData call)
    {
        var status = call.Status switch
        {
            ArcToolCallStatus.Running => "Running",
            ArcToolCallStatus.Success => "Done",
            ArcToolCallStatus.Error => "Failed",
            ArcToolCallStatus.Approval => "Waiting for approval",
            _ => "Denied"
        };
        var summary = string.IsNullOrWhiteSpace(call.Summary) ? string.Empty : $", {call.Summary}";
        var duration = call.DurationSeconds > 0 ? $", {Sv.Duration(call.DurationSeconds)}" : string.Empty;
        return $"{ToolName(call.Tool)}: {call.Title} {status}{summary}{duration}";
    }

    private static string ToolName(string tool) => tool switch
    {
        "web" => "Web search",
        "postgres" => "PostgreSQL",
        "slack" => "Slack",
        "gmail" => "Gmail",
        "github" => "GitHub",
        "linear" => "Linear",
        _ => tool
    };

    private static string TitleFor(ArcToolCallData call) => call.Status switch
    {
        ArcToolCallStatus.Running => call.RunningTitle ?? call.Title,
        ArcToolCallStatus.Approval => call.ApprovalTitle ?? call.Title,
        _ => call.Title
    };

    private static Border ToolPlate(string tool, double size)
    {
        var icon = tool switch
        {
            "web" => "globe",
            "postgres" => "database",
            "slack" => "hash",
            "gmail" => "mail",
            "github" => "code",
            "linear" => "layers",
            _ => null
        };

        Control content;
        if (icon is not null)
        {
            content = Sv.Icon(icon, size * 0.58, ArcTokens.TextSecondary);
        }
        else
        {
            var text = new TextBlock
            {
                Text = tool.Length > 0 ? tool[..1].ToUpperInvariant() : "?",
                FontSize = size * 0.5,
                FontWeight = FontWeight.Medium
            };
            text.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
            content = text;
        }

        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        var plate = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size * 0.3),
            Child = new Panel { Children = { content } },
            BorderThickness = new Thickness(1)
        };
        plate.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        plate.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        return plate;
    }

    private static Control BuildStatusGlyph(ArcToolCallStatus status)
    {
        switch (status)
        {
            case ArcToolCallStatus.Running:
                return new ArcSpinner { Size = 14, StrokeWidth = 1.8, Width = 14, Height = 14 }
                    .Paint(ArcSpinner.StrokeProperty, ArcTokens.Accent);
            case ArcToolCallStatus.Success:
                return Sv.Icon("check", 16, ArcTokens.Success);
            case ArcToolCallStatus.Error:
                return Sv.Icon("circle-alert", 16, ArcTokens.Danger);
            case ArcToolCallStatus.Approval:
                return Sv.Icon("shield", 16, ArcTokens.Accent);
            default:
                return Sv.Icon("ban", 16, ArcTokens.TextMuted);
        }
    }

    private Control BuildPanel(ArcToolCallData call)
    {
        var panel = new StackPanel { Spacing = 8 };

        if (call.Input is { Count: > 0 })
        {
            panel.Children.Add(SectionLine("Input"));
            var list = new StackPanel { Spacing = 3 };
            foreach (var argument in call.Input)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*") };
                var key = Sv.MonoText(argument.Key);
                key.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
                row.Children.Add(key);
                var value = new TextBlock
                {
                    Text = argument.Value,
                    FontFamily = Sv.MonoFamily,
                    FontSize = Sv.Size(ArcTokens.TextXs),
                    TextWrapping = TextWrapping.Wrap
                };
                value.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
                Grid.SetColumn(value, 1);
                row.Children.Add(value);
                list.Children.Add(row);
            }

            panel.Children.Add(list);
        }

        if (call.Output is { Count: > 0 })
        {
            panel.Children.Add(SectionLine("Output", call.Summary));
            var list = new StackPanel { Spacing = 2 };
            foreach (var item in call.Output.Take(3))
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Height = 26 };
                if (!string.IsNullOrWhiteSpace(item.Lead))
                {
                    var lead = Sv.MonoText(item.Lead!, 12);
                    lead.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
                    lead.Margin = new Thickness(0, 0, 8, 0);
                    row.Children.Add(lead);
                }

                var title = new TextBlock
                {
                    Text = item.Title,
                    FontSize = Sv.Size(ArcTokens.TextXs),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(title, 1);
                row.Children.Add(title);

                if (!string.IsNullOrWhiteSpace(item.Tag))
                {
                    var tag = Sv.Pill(Sv.Text(item.Tag!, "arc-caption"), ArcTokens.SurfaceMuted, ArcTokens.TextMuted, 7, 1);
                    Grid.SetColumn(tag, 2);
                    row.Children.Add(tag);
                }

                list.Children.Add(row);
            }

            if (call.Output.Count > 3)
            {
                list.Children.Add(Sv.Caption($"Show {call.Output.Count - 3} more rows"));
            }

            panel.Children.Add(list);
        }

        if (call.Input is null && call.Output is null)
        {
            panel.Children.Add(Sv.Caption("No input or output was recorded for this call."));
        }

        var well = new Border
        {
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(36, 0, 4, 8),
            Child = panel
        };
        well.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        return well;
    }

    private Control SectionLine(string label, string? meta = null)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var text = Sv.Text(label, "arc-caption");
        text.FontWeight = FontWeight.Medium;
        text.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        row.Children.Add(text);
        if (!string.IsNullOrWhiteSpace(meta))
        {
            row.Children.Add(Sv.Caption(meta!));
        }

        var copy = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Content = Sv.Icon("copy", 13, ArcTokens.TextMuted),
            Margin = new Thickness(6, 0, 0, 0)
        };
        copy.Click += (_, _) => Flash(copy);
        AutomationProperties.SetName(copy, $"Copy {label.ToLowerInvariant()}");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(row);
        Grid.SetColumn(copy, 1);
        grid.Children.Add(copy);
        return grid;
    }

    private static void Flash(ArcButton button)
    {
        button.Content = Sv.Icon("check", 13, ArcTokens.Success);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            button.Content = Sv.Icon("copy", 13, ArcTokens.TextMuted);
        };
        timer.Start();
    }

    private void ToggleRow(ArcToolCallData call)
    {
        if (!_open.Add(call.Id))
        {
            _open.Remove(call.Id);
        }

        Rebuild();
    }



    private void Approve(ArcToolCallData call)
    {
        call.Status = ArcToolCallStatus.Running;
        call.Title = call.ApprovalTitle ?? call.Title;
        Rebuild();
        if (ArcThemeManager.ReducedMotion)
        {
            call.Status = ArcToolCallStatus.Success;
            Rebuild();
            return;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            call.Status = ArcToolCallStatus.Success;
            call.DurationSeconds = 1.2;
            call.Summary = call.Summary ?? "Sent";
            Rebuild();
        };
        timer.Start();
    }

    private void Deny(ArcToolCallData call)
    {
        call.Status = ArcToolCallStatus.Denied;
        call.Message = "The call never ran";
        Rebuild();
    }

    private void Retry(ArcToolCallData call)
    {
        call.Status = ArcToolCallStatus.Success;
        call.Message = null;
        call.Summary = call.Summary ?? "3 rows";
        call.DurationSeconds = 1.4;
        Rebuild();
    }
}


