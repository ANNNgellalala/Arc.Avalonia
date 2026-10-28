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

namespace Arc.Avalonia.SpecialAIWidgets.Controls;

/// <summary>Where a plan stands: an editable draft, a run in progress, paused, or finished.</summary>
public enum ArcAgentPlanStatus
{
    Draft,
    Running,
    Paused,
    Done
}

/// <summary>The state of one plan step.</summary>
public enum ArcAgentPlanStepState
{
    Pending,
    Active,
    Done,
    Failed,
    Skipped
}

/// <summary>One step of an agent plan.</summary>
public sealed class ArcAgentPlanStep
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public ArcAgentPlanStepState State { get; set; }

    /// <summary>Short activity lines under the active step; the first word picks the icon.</summary>
    public IReadOnlyList<string> SubSteps { get; set; } = Array.Empty<string>();

    /// <summary>For example "Added by agent", shown as a small accent note.</summary>
    public string? Note { get; set; }

    public string? Error { get; set; }

    public double DurationSeconds { get; set; }
}

/// <summary>
/// The plan an agent writes before it acts. While it is a draft every step can be renamed, moved with its grip
/// or with Alt and the arrow keys, removed, or added to, and Keep planning appends one more suggestion. Approve
/// plan locks it into a live checklist: the header counts finished steps over a thin progress line, the current
/// step shimmers beside a spinner with its activity lines, a failed step offers Retry and Skip, and the footer
/// ends with a one line summary.
///
/// Platform approximations: "Keep planning" appends a canned suggestion instead of streaming one from a model, and
/// the run advances on a fixed clock rather than your agent's events. Renaming uses a plain text field styled to
/// the row, because the Arc input controls live in another project.
/// </summary>
public class ArcAgentPlan : Panel
{
    public static readonly StyledProperty<IEnumerable<ArcAgentPlanStep>?> StepsProperty =
        AvaloniaProperty.Register<ArcAgentPlan, IEnumerable<ArcAgentPlanStep>?>(nameof(Steps));

    public static readonly StyledProperty<ArcAgentPlanStatus> StatusProperty =
        AvaloniaProperty.Register<ArcAgentPlan, ArcAgentPlanStatus>(nameof(Status), ArcAgentPlanStatus.Draft);

    public static readonly StyledProperty<string> GoalProperty =
        AvaloniaProperty.Register<ArcAgentPlan, string>(nameof(Goal), "Add rate limiting to the public API");

    public static readonly StyledProperty<string> ModelNameProperty =
        AvaloniaProperty.Register<ArcAgentPlan, string>(nameof(ModelName), "Claude Opus 5.5");

    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<ArcAgentPlan, string?>(nameof(Summary));

    private DispatcherTimer? _advance;
    private string? _editingId;
    private int _inserted;

    static ArcAgentPlan()
    {
        StepsProperty.Changed.AddClassHandler<ArcAgentPlan>((plan, _) => plan.Rebuild());
        StatusProperty.Changed.AddClassHandler<ArcAgentPlan>((plan, _) => plan.OnStatusChanged());
        GoalProperty.Changed.AddClassHandler<ArcAgentPlan>((plan, _) => plan.Rebuild());
        ModelNameProperty.Changed.AddClassHandler<ArcAgentPlan>((plan, _) => plan.Rebuild());
        SummaryProperty.Changed.AddClassHandler<ArcAgentPlan>((plan, _) => plan.Rebuild());
    }

    public ArcAgentPlan()
    {
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>The plan, in order.</summary>
    public IEnumerable<ArcAgentPlanStep>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public ArcAgentPlanStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Goal
    {
        get => GetValue(GoalProperty);
        set => SetValue(GoalProperty, value);
    }

    public string ModelName
    {
        get => GetValue(ModelNameProperty);
        set => SetValue(ModelNameProperty, value);
    }

    public string? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    private List<ArcAgentPlanStep> Plan => (Steps ?? Array.Empty<ArcAgentPlanStep>()).ToList();

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _advance?.Stop();
        _advance = null;
    }

    private void OnStatusChanged()
    {
        _advance?.Stop();
        _advance = null;

        if (Status == ArcAgentPlanStatus.Running)
        {
            if (!Plan.Any(step => step.State == ArcAgentPlanStepState.Active))
            {
                var first = Plan.FirstOrDefault(step => step.State == ArcAgentPlanStepState.Pending);
                if (first is not null)
                {
                    first.State = ArcAgentPlanStepState.Active;
                }
            }

            StartAdvancing();
        }
        else if (Status == ArcAgentPlanStatus.Draft)
        {
            foreach (var step in Plan)
            {
                step.State = ArcAgentPlanStepState.Pending;
            }
        }

        Rebuild();
    }

    private void StartAdvancing()
    {
        if (ArcThemeManager.ReducedMotion || Status != ArcAgentPlanStatus.Running)
        {
            return;
        }

        _advance = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1100) };
        _advance.Tick += (_, _) => Advance();
        _advance.Start();
    }

    private void Advance()
    {
        var steps = Plan;
        var active = steps.FirstOrDefault(step => step.State == ArcAgentPlanStepState.Active);
        if (active is null)
        {
            _advance?.Stop();
            return;
        }

        active.State = ArcAgentPlanStepState.Done;
        active.DurationSeconds = 1.3 + steps.IndexOf(active) * 0.4;

        var next = steps.FirstOrDefault(step => step.State == ArcAgentPlanStepState.Pending);
        if (next is null)
        {
            _advance?.Stop();
            SetCurrentValue(SummaryProperty, "Rate limiting is live and covered by six tests.");
            SetCurrentValue(StatusProperty, ArcAgentPlanStatus.Done);
            return;
        }

        if (_inserted == 0 && next.Id != "inserted")
        {
            _inserted++;
            var inserted = new ArcAgentPlanStep
            {
                Id = "inserted",
                Title = "Fall back to in-memory counters when Redis is down",
                State = ArcAgentPlanStepState.Pending,
                Note = "Added by agent"
            };
            var list = new List<ArcAgentPlanStep>(steps) { inserted };
            SetCurrentValue(StepsProperty, list);
            steps = list;
            next = inserted;
        }

        next.State = ArcAgentPlanStepState.Active;
        Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();
        var steps = Plan;
        var draft = Status == ArcAgentPlanStatus.Draft;
        var done = steps.Count(step => step.State == ArcAgentPlanStepState.Done);
        var total = Math.Max(1, steps.Count);

        var root = new StackPanel { Spacing = 12 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var mark = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(8),
            Child = new ArcAgentMark { Width = 16, Height = 16 },
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 9, 0)
        };
        mark.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        header.Children.Add(mark);

        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var first = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        first.Children.Add(Sv.Label($"Plan · {steps.Count} steps"));
        if (draft)
        {
            first.Children.Add(Sv.Caption(ModelName));
        }
        else if (Status is ArcAgentPlanStatus.Running or ArcAgentPlanStatus.Paused)
        {
            first.Children.Add(new ArcMiniBar { Width = 84, Fraction = (double)done / total, Height = 4, VerticalAlignment = VerticalAlignment.Center });
            first.Children.Add(Sv.Numeric($"{done} of {steps.Count}", "arc-caption"));
        }

        titles.Children.Add(first);
        if (Status == ArcAgentPlanStatus.Done)
        {
            titles.Children.Add(Sv.Caption("all steps finished"));
        }

        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);
        var badge = StatusBadge();
        Grid.SetColumn(badge, 2);
        header.Children.Add(badge);
        root.Children.Add(header);

        var goal = new TextBlock
        {
            Text = Goal,
            FontSize = Sv.Size(ArcTokens.TextSm),
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 2,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        goal.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        root.Children.Add(goal);

        var list = new StackPanel { Spacing = 2 };
        foreach (var step in steps)
        {
            list.Children.Add(BuildStep(step, draft, steps.IndexOf(step), steps.Count));
        }

        if (steps.Count == 0)
        {
            list.Children.Add(Sv.Caption("No steps yet. Add one to start the plan."));
        }

        root.Children.Add(list);
        root.Children.Add(BuildFooter(steps, done, total));

        var card = Sv.Card(root, 16, ArcTokens.Surface);
        card.MinWidth = 380;
        Children.Add(card);
        UpdateAutomation(steps, done);
    }

    private void UpdateAutomation(List<ArcAgentPlanStep> steps, int done)
    {
        AutomationProperties.SetName(this, $"Plan, {steps.Count} steps, {Goal}");
        AutomationProperties.SetHelpText(this, $"{Status}, {done} of {steps.Count} steps finished");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private Control StatusBadge()
    {
        var (text, background, foreground) = Status switch
        {
            ArcAgentPlanStatus.Draft => ("Draft", ArcTokens.SurfaceMuted, ArcTokens.TextSecondary),
            ArcAgentPlanStatus.Running => ("Running", ArcTokens.AccentSubtle, ArcTokens.Accent),
            ArcAgentPlanStatus.Paused => ("Paused", ArcTokens.WarningSubtle, ArcTokens.Warning),
            _ => ("Done", ArcTokens.SuccessSubtle, ArcTokens.Success)
        };

        var block = Sv.Text(text, "arc-caption");
        block.FontWeight = FontWeight.Medium;
        return Sv.Pill(block, background, foreground);
    }

    private Control BuildStep(ArcAgentPlanStep step, bool draft, int index, int count)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto"), MinHeight = 30 };

        if (draft)
        {
            row.Children.Add(BuildGrip(step, index, count));
        }
        else
        {
            row.Children.Add(new ArcStepMark { State = step.State, Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center });
        }

        var body = new StackPanel { Spacing = 2, Margin = new Thickness(4, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center };

        if (draft && _editingId == step.Id)
        {
            body.Children.Add(BuildEditor(step));
        }
        else
        {
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var title = new TextBlock
            {
                Text = step.Title,
                FontSize = Sv.Size(ArcTokens.TextSm),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            ApplyTitleTone(title, step.State);
            titleRow.Children.Add(title);
            if (!string.IsNullOrWhiteSpace(step.Note))
            {
                var note = Sv.Text(step.Note!, "arc-caption");
                note.Paint(TextBlock.ForegroundProperty, ArcTokens.Accent);
                titleRow.Children.Add(note);
            }

            body.Children.Add(titleRow);
        }

        if (!draft && step.State == ArcAgentPlanStepState.Active && step.SubSteps.Count > 0)
        {
            var sub = new StackPanel { Spacing = 1, Margin = new Thickness(0, 2, 0, 0) };
            foreach (var line in step.SubSteps)
            {
                var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                item.Children.Add(Sv.Icon(IconFor(line), 12, ArcTokens.TextMuted));
                item.Children.Add(Sv.Caption(line));
                sub.Children.Add(item);
            }

            body.Children.Add(sub);
        }

        if (!draft && step.State == ArcAgentPlanStepState.Failed && !string.IsNullOrWhiteSpace(step.Error))
        {
            var error = new TextBlock
            {
                Text = step.Error,
                FontSize = Sv.Size(ArcTokens.TextXs),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 2)
            };
            error.Paint(TextBlock.ForegroundProperty, ArcTokens.Danger);
            body.Children.Add(error);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 2, 0, 2) };
            var retry = new ArcButton { Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, Content = "Retry" };
            retry.Click += (_, _) =>
            {
                step.State = ArcAgentPlanStepState.Active;
                step.Error = null;
                Rebuild();
                StartAdvancing();
            };
            var skip = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Skip" };
            skip.Click += (_, _) =>
            {
                step.State = ArcAgentPlanStepState.Skipped;
                Rebuild();
            };
            AutomationProperties.SetName(retry, $"Retry: {step.Title}");
            AutomationProperties.SetName(skip, $"Skip: {step.Title}");
            actions.Children.Add(retry);
            actions.Children.Add(skip);
            body.Children.Add(actions);
        }

        Grid.SetColumn(body, 1);
        row.Children.Add(body);

        if (draft)
        {
            var remove = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                Content = Sv.Icon("x", 13),
                VerticalAlignment = VerticalAlignment.Center
            };
            remove.Click += (_, _) => Remove(step);
            AutomationProperties.SetName(remove, $"Remove step {index + 1}: {step.Title}");
            Grid.SetColumn(remove, 2);
            row.Children.Add(remove);
        }
        else if (step.DurationSeconds > 0)
        {
            var duration = Sv.Numeric(Sv.Duration(step.DurationSeconds), "arc-caption");
            duration.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(duration, 2);
            row.Children.Add(duration);
        }
        else if (step.State == ArcAgentPlanStepState.Active)
        {
            var label = Sv.Caption(Status == ArcAgentPlanStatus.Paused ? "paused" : "now");
            label.Paint(TextBlock.ForegroundProperty, Status == ArcAgentPlanStatus.Paused ? ArcTokens.TextMuted : ArcTokens.Accent);
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 2);
            row.Children.Add(label);
        }

        var frame = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(2, 0, 2, 0),
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent,
            Child = row
        };
        if (draft)
        {
            frame.Focusable = true;
            frame.KeyDown += (_, args) => OnStepKeyDown(args, step, index, count);
            frame.GotFocus += (_, _) => frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable(ArcTokens.FocusOutline));
            frame.LostFocus += (_, _) => frame.BorderBrush = Brushes.Transparent;
            frame.DoubleTapped += (_, args) =>
            {
                _editingId = step.Id;
                Rebuild();
                args.Handled = true;
            };
            AutomationProperties.SetName(frame, $"{index + 1} of {count}, {step.Title}, press Enter to rename, Alt plus arrow keys to move");
        }

        return frame;
    }

    private void ApplyTitleTone(TextBlock title, ArcAgentPlanStepState state)
    {
        switch (state)
        {
            case ArcAgentPlanStepState.Done:
                title.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
                break;
            case ArcAgentPlanStepState.Active:
                title.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
                break;
            case ArcAgentPlanStepState.Failed:
                title.Paint(TextBlock.ForegroundProperty, ArcTokens.Danger);
                break;
            case ArcAgentPlanStepState.Skipped:
                title.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
                break;
        }
    }

    private void OnStepKeyDown(KeyEventArgs args, ArcAgentPlanStep step, int index, int count)
    {
        var alt = args.KeyModifiers.HasFlag(KeyModifiers.Alt);
        switch (args.Key)
        {
            case Key.Enter when !alt:
                _editingId = step.Id;
                Rebuild();
                args.Handled = true;
                break;
            case Key.Up when alt:
                Move(index, -1);
                args.Handled = true;
                break;
            case Key.Down when alt:
                Move(index, 1);
                args.Handled = true;
                break;
            case Key.Back:
                Remove(step);
                args.Handled = true;
                break;
        }
    }

    private Control BuildGrip(ArcAgentPlanStep step, int index, int count)
    {
        var grip = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(6),
            Child = Sv.Icon("grip-vertical", 14, ArcTokens.TextMuted),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = true,
            Cursor = new Cursor(StandardCursorType.SizeNorthSouth),
            Margin = new Thickness(0, 0, 4, 0)
        };
        grip.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Up or Key.Down)
            {
                Move(index, args.Key == Key.Up ? -1 : 1);
                args.Handled = true;
            }
        };
        grip.GotFocus += (_, _) =>
        {
            grip.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            grip.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
        };
        grip.LostFocus += (_, _) =>
        {
            grip.Background = Brushes.Transparent;
            grip.BorderBrush = Brushes.Transparent;
        };
        grip.PointerPressed += (_, _) =>
        {
            grip.Focus();
        };
        AutomationProperties.SetName(grip, $"Move step {index + 1} of {count}: {step.Title}");
        return grip;
    }

    private Control BuildEditor(ArcAgentPlanStep step)
    {
        var editor = new TextBox
        {
            Text = step.Title,
            FontSize = Sv.Size(ArcTokens.TextSm),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0, 2, 0, 2),
            MinHeight = 22
        };
        editor.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);
        editor.AttachedToVisualTree += (_, _) =>
        {
            editor.Focus();
            editor.SelectAll();
        };
        void Commit()
        {
            var text = editor.Text?.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                step.Title = text!;
            }

            _editingId = null;
            Rebuild();
        }

        editor.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter)
            {
                Commit();
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                _editingId = null;
                Rebuild();
                args.Handled = true;
            }
        };
        editor.LostFocus += (_, _) =>
        {
            if (_editingId == step.Id)
            {
                Commit();
            }
        };
        AutomationProperties.SetName(editor, "Step title");
        return editor;
    }

    private void Move(int index, int delta)
    {
        var steps = Plan;
        var target = index + delta;
        if (target < 0 || target >= steps.Count)
        {
            return;
        }

        (steps[index], steps[target]) = (steps[target], steps[index]);
        SetCurrentValue(StepsProperty, steps);
    }

    private void Remove(ArcAgentPlanStep step)
    {
        var steps = Plan.Where(item => !ReferenceEquals(item, step)).ToList();
        SetCurrentValue(StepsProperty, steps);
    }

    private void AddStep()
    {
        var steps = Plan;
        var step = new ArcAgentPlanStep
        {
            Id = "step-" + Guid.NewGuid().ToString("N")[..6],
            Title = "New step",
            State = ArcAgentPlanStepState.Pending
        };
        steps.Add(step);
        SetCurrentValue(StepsProperty, steps);
        _editingId = step.Id;
        Rebuild();
    }

    private void KeepPlanning()
    {
        var steps = Plan;
        steps.Insert(Math.Max(0, steps.Count - 1), new ArcAgentPlanStep
        {
            Id = "suggested-" + Guid.NewGuid().ToString("N")[..6],
            Title = "Warm the limiter at boot so the first burst is smooth",
            State = ArcAgentPlanStepState.Pending,
            Note = "Suggested"
        });
        SetCurrentValue(StepsProperty, steps);
    }

    private Control BuildFooter(List<ArcAgentPlanStep> steps, int done, int total)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        if (Status == ArcAgentPlanStatus.Draft)
        {
            var add = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Add step" };
            add.Click += (_, _) => AddStep();
            var planning = new ArcButton { Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, Content = "Keep planning" };
            planning.Click += (_, _) => KeepPlanning();
            left.Children.Add(add);
            left.Children.Add(planning);
            row.Children.Add(left);

            var approve = new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Approve plan" };
            approve.Click += (_, _) =>
            {
                var clean = Plan.Where(step => !string.IsNullOrWhiteSpace(step.Title)).ToList();
                foreach (var step in clean)
                {
                    step.State = ArcAgentPlanStepState.Pending;
                }

                clean[0].State = ArcAgentPlanStepState.Active;
                SetCurrentValue(StepsProperty, clean);
                SetCurrentValue(StatusProperty, ArcAgentPlanStatus.Running);
            };
            Grid.SetColumn(approve, 1);
            row.Children.Add(approve);
            return row;
        }

        if (Status is ArcAgentPlanStatus.Running or ArcAgentPlanStatus.Paused)
        {
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
                        Sv.Icon(Status == ArcAgentPlanStatus.Paused ? "play" : "pause", 13),
                        new TextBlock { Text = Status == ArcAgentPlanStatus.Paused ? "Resume" : "Pause", FontSize = 13 }
                    }
                }
            };
            pause.Click += (_, _) =>
            {
                SetCurrentValue(StatusProperty, Status == ArcAgentPlanStatus.Paused ? ArcAgentPlanStatus.Running : ArcAgentPlanStatus.Paused);
            };
            left.Children.Add(Sv.Caption(Status == ArcAgentPlanStatus.Paused ? "Paused by you" : "Working through the plan"));
            row.Children.Add(left);
            Grid.SetColumn(pause, 1);
            row.Children.Add(pause);
            return row;
        }

        var summary = new TextBlock
        {
            Text = $"Done in {Sv.Duration(steps.Sum(step => step.DurationSeconds))}. {Summary}",
            FontSize = Sv.Size(ArcTokens.TextSm),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        summary.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        row.Children.Add(summary);

        var restart = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Start over" };
        restart.Click += (_, _) =>
        {
            foreach (var step in Plan)
            {
                step.State = ArcAgentPlanStepState.Pending;
                step.DurationSeconds = 0;
            }

            SetCurrentValue(StatusProperty, ArcAgentPlanStatus.Draft);
        };
        Grid.SetColumn(restart, 1);
        row.Children.Add(restart);
        return row;
    }

    private static string IconFor(string line)
    {
        var word = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant();
        return word switch
        {
            "read" => "file",
            "edited" or "edit" => "pencil",
            "created" or "added" => "plus",
            "searched" or "found" => "search",
            "ran" or "run" => "sparkles",
            "measured" => "chart-line",
            _ => "circle-small"
        };
    }
}

/// <summary>The generic model mark: a four point star used wherever a model or agent is named.</summary>
public sealed class ArcAgentMark : Control
{
    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        SvDraw.Mark(context, new Point(Bounds.Width / 2, Bounds.Height / 2), size / 2, Sv.Brush(ArcTokens.TextSecondary), 1.6);
    }
}

/// <summary>The step state mark: a dashed ring, a spinning arc, a disc with a check or a cross.</summary>
public sealed class ArcStepMark : Control
{
    public static readonly StyledProperty<ArcAgentPlanStepState> StateProperty =
        AvaloniaProperty.Register<ArcStepMark, ArcAgentPlanStepState>(nameof(State));

    private readonly SvClock _clock = new();
    private double _angle;

    static ArcStepMark()
    {
        AffectsRender<ArcStepMark>(StateProperty);
        StateProperty.Changed.AddClassHandler<ArcStepMark>((mark, _) => mark.Sync());
    }

    public ArcAgentPlanStepState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(20, 20);

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
        if (State != ArcAgentPlanStepState.Active)
        {
            _angle = 40;
            InvalidateVisual();
            return;
        }

        _clock.Start(seconds =>
        {
            _angle = (seconds * 170) % 360;
            InvalidateVisual();
        });
    }

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var radius = size / 2 - 2;
        var muted = Sv.Brush(ArcTokens.TextMuted);
        var track = Sv.Brush(ArcTokens.ControlTrack);
        var accent = Sv.Brush(ArcTokens.Accent);
        var success = Sv.Brush(ArcTokens.Success);
        var danger = Sv.Brush(ArcTokens.Danger);

        switch (State)
        {
            case ArcAgentPlanStepState.Done:
                context.DrawEllipse(Sv.AlphaBrush(ArcTokens.Success, 0.14), null, center, radius, radius);
                SvDraw.Check(context, center, radius * 1.2, success, 1.8);
                break;
            case ArcAgentPlanStepState.Active:
                context.DrawEllipse(null, new Pen(track, 1.8), center, radius, radius);
                SvDraw.Arc(context, center, radius, _angle - 90, 120, new Pen(accent, 2, null, PenLineCap.Round));
                break;
            case ArcAgentPlanStepState.Failed:
                context.DrawEllipse(Sv.AlphaBrush(ArcTokens.Danger, 0.14), null, center, radius, radius);
                SvDraw.Cross(context, center, radius * 1.1, danger, 1.8);
                break;
            case ArcAgentPlanStepState.Skipped:
                SvDraw.Ticks(context, center, radius - 1.6, radius, 10, muted, 1.4);
                break;
            default:
                SvDraw.Ticks(context, center, radius - 1.6, radius, 10, track, 1.4);
                break;
        }
    }
}
