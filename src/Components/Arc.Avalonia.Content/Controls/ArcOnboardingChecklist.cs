using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

/// <summary>One setup step in an <see cref="ArcOnboardingChecklist"/>.</summary>
public sealed class ArcChecklistStep
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Primary action for this step, such as Connect or Invite.</summary>
    public string? ActionLabel { get; set; }
}

/// <summary>
/// A getting-started card with a progress ring, done/current/upcoming rows, expandable details,
/// one primary action per row, a body hide toggle and dismiss. Completing a step folds the ring
/// forward and announces the new count; reduced motion snaps both.
/// </summary>
public class ArcOnboardingChecklist : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, string>(nameof(Title), "Set up your workspace");

    public static readonly StyledProperty<string> DoneTitleProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, string>(nameof(DoneTitle), "You're all set");

    public static readonly StyledProperty<IEnumerable<ArcChecklistStep>?> StepsProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, IEnumerable<ArcChecklistStep>?>(nameof(Steps));

    public static readonly StyledProperty<IEnumerable<string>?> CompletedProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, IEnumerable<string>?>(nameof(Completed));

    public static readonly StyledProperty<IEnumerable<string>?> DefaultCompletedProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, IEnumerable<string>?>(nameof(DefaultCompleted));

    public static readonly StyledProperty<bool> DefaultHiddenProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, bool>(nameof(DefaultHidden));

    public static readonly StyledProperty<bool> DismissedProperty =
        AvaloniaProperty.Register<ArcOnboardingChecklist, bool>(nameof(Dismissed));

    private readonly List<ArcChecklistStep> _steps = new();
    private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly List<ArcButton> _titleButtons = new();
    private Panel? _host;
    private ArcChecklistRing? _ring;
    private TextBlock? _ringCount;
    private ArcIcon? _ringCheck;
    private TextBlock? _live;
    private bool _hidden;
    private bool _hiddenInitialized;
    private bool _syncingCompleted;
    private string? _pendingFocus;
    private string? _justExpanded;

    static ArcOnboardingChecklist()
    {
        TitleProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) => card.BuildShell());
        DoneTitleProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) => card.BuildShell());
        StepsProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) => card.LoadSteps());
        CompletedProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) => card.SyncCompleted());
        DefaultCompletedProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) =>
        {
            if (card.Completed is null)
            {
                card.LoadDefaultCompleted();
                card.BuildShell();
            }
        });
        DefaultHiddenProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) =>
        {
            card._hidden = card.DefaultHidden;
            card.BuildShell();
        });
        DismissedProperty.Changed.AddClassHandler<ArcOnboardingChecklist>((card, _) =>
        {
            if (card._host is not null)
            {
                card._host.IsVisible = !card.Dismissed;
            }
        });
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Title once every step is complete.</summary>
    public string DoneTitle
    {
        get => GetValue(DoneTitleProperty);
        set => SetValue(DoneTitleProperty, value);
    }

    public IEnumerable<ArcChecklistStep>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    /// <summary>Completed step ids in step order. Setting it updates the card.</summary>
    public IEnumerable<string>? Completed
    {
        get => GetValue(CompletedProperty);
        set => SetValue(CompletedProperty, value);
    }

    public IEnumerable<string>? DefaultCompleted
    {
        get => GetValue(DefaultCompletedProperty);
        set => SetValue(DefaultCompletedProperty, value);
    }

    /// <summary>Starts with the step body collapsed.</summary>
    public bool DefaultHidden
    {
        get => GetValue(DefaultHiddenProperty);
        set => SetValue(DefaultHiddenProperty, value);
    }

    /// <summary>Hides the card after Dismiss; set back to false to show it again.</summary>
    public bool Dismissed
    {
        get => GetValue(DismissedProperty);
        set => SetValue(DismissedProperty, value);
    }

    public event EventHandler<ArcChecklistStep>? StepInvoked;

    public event EventHandler<IReadOnlyList<string>>? CompletedChanged;

    public event EventHandler? DismissedChanged;

    public event EventHandler<bool>? HiddenChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        if (!_hiddenInitialized)
        {
            _hiddenInitialized = true;
            _hidden = DefaultHidden;
            if (Completed is null)
            {
                LoadDefaultCompleted();
            }
        }

        LoadSteps();
    }

    private void LoadDefaultCompleted()
    {
        _completed.Clear();
        foreach (var id in DefaultCompleted ?? Enumerable.Empty<string>())
        {
            _completed.Add(id);
        }
    }

    private void LoadSteps()
    {
        _steps.Clear();
        if (Steps is not null)
        {
            _steps.AddRange(Steps);
        }

        if (Completed is null)
        {
            LoadDefaultCompleted();
        }
        else
        {
            SyncCompletedIds();
        }

        BuildShell();
    }

    private void SyncCompleted()
    {
        if (_syncingCompleted)
        {
            return;
        }

        SyncCompletedIds();
        BuildShell();
    }

    private void SyncCompletedIds()
    {
        _completed.Clear();
        foreach (var id in Completed ?? Enumerable.Empty<string>())
        {
            _completed.Add(id);
        }
    }

    private void BuildShell()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _titleButtons.Clear();
        var previousRing = _ring?.Progress ?? 0;

        var total = _steps.Count;
        var done = OrderedCompleted().Count;
        var complete = total > 0 && done == total;
        var ringValue = total == 0 ? 0 : done / (double)total;

        var shell = new Border
        {
            Background = ArcContentUi.Brush(ArcTokens.Surface),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusSurface),
            Padding = new Thickness(22, 20, 22, 20)
        };
        shell.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), ClipToBounds = false };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var ringHost = new Panel { Width = 54, Height = 54, Margin = new Thickness(0, 0, 14, 0) };
        _ring = new ArcChecklistRing();
        _ring.SetProgress(previousRing, animate: false);
        _ring.SetProgress(ringValue, animate: true);
        ringHost.Children.Add(_ring);
        _ringCount = ArcContentUi.Numeric($"{done}/{total}", "arc-caption");
        _ringCount.FontSize = 11;
        _ringCount.HorizontalAlignment = HorizontalAlignment.Center;
        _ringCount.VerticalAlignment = VerticalAlignment.Center;
        _ringCount.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        _ringCount.IsVisible = !complete;
        ringHost.Children.Add(_ringCount);
        _ringCheck = ArcContentUi.Icon("check", 18, ArcTokens.Success);
        _ringCheck.HorizontalAlignment = HorizontalAlignment.Center;
        _ringCheck.VerticalAlignment = VerticalAlignment.Center;
        _ringCheck.IsVisible = complete;
        ringHost.Children.Add(_ringCheck);
        AutomationProperties.SetName(ringHost, $"{done} of {total} steps done");
        header.Children.Add(ringHost);

        var titles = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var heading = ArcContentUi.Text(complete ? DoneTitle : Title, "arc-subtitle");
        heading.FontSize = 20;
        heading.LineHeight = 26;
        titles.Children.Add(heading);
        var progressCopy = total == 0
            ? "Add a step to get started"
            : complete
                ? "Everything is ready. You can dismiss this checklist."
                : $"{done} of {total} steps done";
        var caption = ArcContentUi.Caption(progressCopy);
        titles.Children.Add(caption);
        Grid.SetColumn(titles, 1);
        header.Children.Add(titles);

        var hide = new ArcButton
        {
            Content = ArcContentUi.Row(6,
                ArcContentUi.Icon(_hidden ? "chevron-down" : "chevron-up", 13, ArcTokens.TextSecondary),
                ArcContentUi.Text(_hidden ? "Show" : "Hide", "arc-label")),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(hide, _hidden ? "Show checklist" : "Hide checklist");
        hide.Click += (_, _) => ToggleHidden();
        Grid.SetColumn(hide, 2);
        header.Children.Add(hide);

        var dismiss = new ArcButton
        {
            Content = ArcContentUi.Icon("x", 15, ArcTokens.TextSecondary),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 36,
            Height = 36,
            MinHeight = 36,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(dismiss, "Dismiss checklist");
        ToolTip.SetTip(dismiss, "Dismiss");
        dismiss.Click += (_, _) => DismissCard();
        Grid.SetColumn(dismiss, 3);
        header.Children.Add(dismiss);

        root.Children.Add(header);

        if (!_hidden)
        {
            var body = new StackPanel { Spacing = 2 };
            var divider = ArcContentUi.Divider(ArcTokens.BorderSubtle);
            divider.Margin = new Thickness(0, 16, 0, 10);
            body.Children.Add(divider);

            if (total == 0)
            {
                body.Children.Add(ArcContentUi.Body("No steps yet."));
            }
            else
            {
                var currentId = _steps.FirstOrDefault(step => !_completed.Contains(step.Id))?.Id;
                foreach (var step in _steps)
                {
                    body.Children.Add(BuildStep(step, step.Id == currentId, complete));
                }
            }

            Grid.SetRow(body, 1);
            root.Children.Add(body);
        }

        _live = new TextBlock
        {
            Opacity = 0,
            Height = 1,
            IsHitTestVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        AutomationProperties.SetLiveSetting(_live, AutomationLiveSetting.Polite);
        root.Children.Add(_live);

        shell.Child = root;
        _host.Children.Add(shell);
        _host.IsVisible = !Dismissed;
        AutomationProperties.SetName(this, complete ? DoneTitle : Title);

        if (_pendingFocus is not null)
        {
            var id = _pendingFocus;
            _pendingFocus = null;
            Dispatcher.UIThread.Post(() => FocusTitle(id), DispatcherPriority.Loaded);
        }

        if (_justExpanded is not null)
        {
            _justExpanded = null;
        }
    }

    private Control BuildStep(ArcChecklistStep step, bool current, bool allComplete)
    {
        var done = _completed.Contains(step.Id);
        var expanded = _expanded.Contains(step.Id);

        var wrapper = new StackPanel { Spacing = 0 };
        var row = new Border
        {
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(4, 6, 8, 6),
            Background = current ? ArcContentUi.Brush(ArcTokens.AccentSubtle) : Brushes.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = Brushes.Transparent
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("40,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var check = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 40,
            Height = 40,
            MinHeight = 40,
            Padding = new Thickness(0),
            Content = BuildCheckGlyph(done, current),
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(check, done ? $"Mark {step.Title} incomplete" : $"Mark {step.Title} complete");
        check.Click += (_, _) => ToggleStep(step);
        grid.Children.Add(check);

        var titleContent = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var titleText = ArcContentUi.Text(step.Title, "arc-label");
        titleText.FontSize = 14;
        titleText.TextWrapping = TextWrapping.Wrap;
        titleText.Paint(TextBlock.ForegroundProperty,
            done ? ArcTokens.TextMuted : ArcTokens.Foreground);
        if (done)
        {
            titleText.TextDecorations = TextDecorations.Strikethrough;
        }

        titleContent.Children.Add(titleText);

        if (!done && current)
        {
            var state = new Border
            {
                CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusPill),
                Padding = new Thickness(8, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = ArcContentUi.Text("Current", "arc-caption")
            };
            state.Paint(Border.BackgroundProperty, ArcTokens.AccentSubtle);
            if (state.Child is TextBlock stateText)
            {
                stateText.Paint(TextBlock.ForegroundProperty, ArcTokens.AccentStrong);
                stateText.FontWeight = FontWeight.Medium;
            }

            Grid.SetColumn(state, 1);
            titleContent.Children.Add(state);
        }

        var chevron = ArcContentUi.Icon("chevron-down", 14, ArcTokens.TextMuted);
        chevron.Margin = new Thickness(8, 0, 2, 0);
        chevron.RenderTransformOrigin = RelativePoint.Center;
        chevron.RenderTransform = expanded
            ? TransformOperations.Parse("rotate(0deg)")
            : TransformOperations.Parse("rotate(-90deg)");
        if (!ArcThemeManager.ReducedMotion)
        {
            chevron.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.SpringSnappyDuration,
                    Easing = ArcMotion.SpringSnappy
                }
            };
        }

        Grid.SetColumn(chevron, 2);
        titleContent.Children.Add(chevron);

        var titleButton = new ArcButton
        {
            Content = titleContent,
            Variant = ArcButtonVariant.Ghost,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(4, 2),
            MinHeight = 40
        };

        AutomationProperties.SetName(titleButton, $"{step.Title}. {(done ? "Completed" : current ? "Current step" : "Not started")}");
        AutomationProperties.SetHelpText(titleButton, expanded ? "Expanded. Press Enter to collapse." : "Press Enter to show details.");
        titleButton.Click += (_, _) => ToggleExpanded(step.Id);
        Grid.SetColumn(titleButton, 1);
        grid.Children.Add(titleButton);
        _titleButtons.Add(titleButton);

        if (!done && !string.IsNullOrWhiteSpace(step.ActionLabel))
        {
            var action = new ArcButton
            {
                Content = step.ActionLabel,
                Variant = current ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary,
                Size = ArcButtonSize.Sm,
                Padding = new Thickness(12, 0),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(action, $"{step.ActionLabel}: {step.Title}");
            action.Click += (_, _) =>
            {
                StepInvoked?.Invoke(this, step);
                MarkStep(step, true);
            };
            Grid.SetColumn(action, 2);
            grid.Children.Add(action);
        }

        row.Child = grid;
        row.PointerEntered += (_, _) =>
        {
            if (!current)
            {
                row.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            }
        };
        row.PointerExited += (_, _) =>
        {
            if (!current)
            {
                row.Background = Brushes.Transparent;
            }
        };
        wrapper.Children.Add(row);

        var details = new Border
        {
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(48, 0, 12, 12),
            ClipToBounds = true,
            IsVisible = expanded
        };
        var detailStack = new StackPanel { Spacing = 6 };
        var description = ArcContentUi.Body(
            string.IsNullOrWhiteSpace(step.Description) ? "No extra detail for this step." : step.Description!);
        description.MaxWidth = 560;
        detailStack.Children.Add(description);
        if (!done && string.IsNullOrWhiteSpace(step.ActionLabel))
        {
            var markDone = new ArcButton
            {
                Content = "Mark as done",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(8, 0)
            };
            AutomationProperties.SetName(markDone, $"Mark {step.Title} as done");
            markDone.Click += (_, _) => MarkStep(step, true);
            detailStack.Children.Add(markDone);
        }

        details.Child = detailStack;
        if (expanded && _justExpanded == step.Id && !ArcThemeManager.ReducedMotion)
        {
            details.Opacity = 0;
            details.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseEnter
                }
            };
            Dispatcher.UIThread.Post(() => details.Opacity = 1, DispatcherPriority.Loaded);
        }

        wrapper.Children.Add(details);
        return wrapper;
    }

    private Control BuildCheckGlyph(bool done, bool current)
    {
        var circle = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            BorderThickness = new Thickness(done ? 0 : 1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (done)
        {
            circle.Paint(Border.BackgroundProperty, ArcTokens.Accent);
            circle.BorderBrush = Brushes.Transparent;
            circle.Child = ArcContentUi.Icon("check", 13, ArcTokens.AccentForeground);
        }
        else if (current)
        {
            circle.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
            circle.BorderThickness = new Thickness(2);
            var dot = new Border
            {
                Width = 6,
                Height = 6,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dot.Paint(Border.BackgroundProperty, ArcTokens.Accent);
            circle.Child = dot;
        }
        else
        {
            circle.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
        }

        return circle;
    }

    internal void ExpandForDemo(string id)
    {
        _expanded.Add(id);
        _justExpanded = id;
        BuildShell();
    }

    private void ToggleExpanded(string id)
    {
        var opened = _expanded.Add(id);
        if (!opened)
        {
            _expanded.Remove(id);
        }
        else
        {
            _justExpanded = id;
        }

        _pendingFocus = id;
        BuildShell();
    }

    private void ToggleStep(ArcChecklistStep step)
    {
        MarkStep(step, !_completed.Contains(step.Id));
    }

    private void MarkStep(ArcChecklistStep step, bool done)
    {
        if (done)
        {
            _completed.Add(step.Id);
        }
        else
        {
            _completed.Remove(step.Id);
        }

        var order = OrderedCompleted();
        _syncingCompleted = true;
        SetCurrentValue(CompletedProperty, order);
        _syncingCompleted = false;

        var next = _steps.FirstOrDefault(candidate => !_completed.Contains(candidate.Id));
        _pendingFocus = next?.Id ?? step.Id;
        var total = _steps.Count;
        BuildShell();
        Announce(done
            ? $"{step.Title} complete. {order.Count} of {total} steps done."
            : $"{step.Title} not complete. {order.Count} of {total} steps done.");
        CompletedChanged?.Invoke(this, order);
    }

    private List<string> OrderedCompleted() =>
        _steps.Where(step => _completed.Contains(step.Id)).Select(step => step.Id).ToList();

    private void ToggleHidden()
    {
        _hidden = !_hidden;
        BuildShell();
        HiddenChanged?.Invoke(this, _hidden);
    }

    private void DismissCard()
    {
        SetCurrentValue(DismissedProperty, true);
        DismissedChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FocusTitle(string id)
    {
        var index = _steps.FindIndex(step => step.Id == id);
        if (index >= 0 && index < _titleButtons.Count)
        {
            _titleButtons[index].Focus();
        }
    }

    private void Announce(string message)
    {
        if (_live is not null)
        {
            _live.Text = message;
        }

        AutomationProperties.SetHelpText(this, message);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_titleButtons.Count == 0)
        {
            return;
        }

        var index = _titleButtons.FindIndex(button => button.IsFocused);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                _titleButtons[(index + 1) % _titleButtons.Count].Focus();
                e.Handled = true;
                break;
            case Key.Up:
                _titleButtons[(index - 1 + _titleButtons.Count) % _titleButtons.Count].Focus();
                e.Handled = true;
                break;
            case Key.Home:
                _titleButtons[0].Focus();
                e.Handled = true;
                break;
            case Key.End:
                _titleButtons[^1].Focus();
                e.Handled = true;
                break;
        }
    }
}

/// <summary>The animated progress ring: 270 degrees of travel is not needed here, a full circle reads better.</summary>
internal sealed class ArcChecklistRing : Control
{
    private readonly ArcContentTween _tween = new();
    private double _progress;

    public ArcChecklistRing()
    {
        Width = 54;
        Height = 54;
    }

    public double Progress => _progress;

    public void SetProgress(double value, bool animate)
    {
        value = Math.Clamp(value, 0, 1);
        if (!animate)
        {
            _tween.Stop();
            _progress = value;
            InvalidateVisual();
            return;
        }

        _tween.Run(_progress, value, ArcMotion.SpringSmoothDuration, next =>
        {
            _progress = next;
            InvalidateVisual();
        });
    }

    protected override Size MeasureOverride(Size availableSize) => new(54, 54);

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 2)
        {
            return;
        }

        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var thickness = 5;
        var radius = Math.Max(1, size / 2 - thickness / 2 - 1);
        var track = new Pen(ArcResources.Brush(ArcTokens.Border), thickness,
            null, PenLineCap.Round, PenLineJoin.Round);
        context.DrawEllipse(null, track, center, radius, radius);

        if (_progress <= 0.0001)
        {
            return;
        }

        var color = ArcResources.Brush(_progress >= 0.999 ? ArcTokens.Success : ArcTokens.Accent);
        var pen = new Pen(color, thickness, null, PenLineCap.Round, PenLineJoin.Round);
        if (_progress >= 0.999)
        {
            context.DrawEllipse(null, pen, center, radius, radius);
            return;
        }

        var startAngle = -90 * Math.PI / 180;
        var sweep = 360 * _progress * Math.PI / 180;
        var start = new Point(center.X + radius * Math.Cos(startAngle), center.Y + radius * Math.Sin(startAngle));
        var end = new Point(center.X + radius * Math.Cos(startAngle + sweep),
            center.Y + radius * Math.Sin(startAngle + sweep));
        var geometry = new StreamGeometry();
        using (var figure = geometry.Open())
        {
            figure.BeginFigure(start, false);
            figure.ArcTo(end, new Size(radius, radius), 0, sweep > Math.PI, SweepDirection.Clockwise);
            figure.EndFigure(false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}
