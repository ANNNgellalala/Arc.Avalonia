using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

public enum ArcStepperOrientation
{
    Horizontal,
    Vertical
}

public enum ArcStepperDetails
{
    All,
    Current
}

/// <summary>One step in an <see cref="ArcStepper"/>.</summary>
public sealed class ArcStep
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>When set the marker becomes an alert and the description is replaced by this message.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Position in a multi-step flow: completed, current, upcoming, with an error state and optional
/// navigation back to completed steps. Arrow keys move the roving focus, Enter selects.
/// </summary>
public class ArcStepper : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcStep>?> StepsProperty =
        AvaloniaProperty.Register<ArcStepper, IEnumerable<ArcStep>?>(nameof(Steps));

    public static readonly StyledProperty<int> CurrentProperty =
        AvaloniaProperty.Register<ArcStepper, int>(nameof(Current));

    public static readonly StyledProperty<ArcStepperOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcStepper, ArcStepperOrientation>(nameof(Orientation));

    public static readonly StyledProperty<ArcStepperDetails> DetailsProperty =
        AvaloniaProperty.Register<ArcStepper, ArcStepperDetails>(nameof(Details));

    public static readonly StyledProperty<bool> CompactProperty =
        AvaloniaProperty.Register<ArcStepper, bool>(nameof(Compact));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcStepper, string>(nameof(Label), "Progress");

    public static readonly StyledProperty<string> CompleteLabelProperty =
        AvaloniaProperty.Register<ArcStepper, string>(nameof(CompleteLabel), "All steps complete");

    private readonly List<Border> _rings = new();
    private Panel? _host;
    private StackPanel? _root;
    private int _focusIndex;
    private bool _keyboardFocus;
    private int _count;

    static ArcStepper()
    {
        StepsProperty.Changed.AddClassHandler<ArcStepper>((stepper, _) => stepper.Rebuild());
        CurrentProperty.Changed.AddClassHandler<ArcStepper>((stepper, _) => stepper.Rebuild());
        OrientationProperty.Changed.AddClassHandler<ArcStepper>((stepper, _) => stepper.Rebuild());
        DetailsProperty.Changed.AddClassHandler<ArcStepper>((stepper, _) => stepper.Rebuild());
        CompactProperty.Changed.AddClassHandler<ArcStepper>((stepper, _) => stepper.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcStepper>((stepper, _) => stepper.UpdateAutomation());
    }

    public ArcStepper()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    /// <summary>Steps in order. An error step shows an alert marker and its message.</summary>
    public IEnumerable<ArcStep>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    /// <summary>Index of the step in progress; Steps.Count marks the flow complete.</summary>
    public int Current
    {
        get => GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public ArcStepperOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>"current" hides descriptions for steps that are not active; errors always show.</summary>
    public ArcStepperDetails Details
    {
        get => GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    /// <summary>Markers only, with a caption under the row.</summary>
    public bool Compact
    {
        get => GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    /// <summary>Accessible name of the stepper.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Shown once every step is complete.</summary>
    public string CompleteLabel
    {
        get => GetValue(CompleteLabelProperty);
        set => SetValue(CompleteLabelProperty, value);
    }

    /// <summary>Called with the index of a completed step when it is chosen.</summary>
    public event EventHandler<int>? StepSelected;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        Rebuild();
        UpdateAutomation();
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || _count == 0)
        {
            return;
        }

        var forward = Orientation == ArcStepperOrientation.Horizontal ? Key.Right : Key.Down;
        var backward = Orientation == ArcStepperOrientation.Horizontal ? Key.Left : Key.Up;

        switch (e.Key)
        {
            case Key.Home:
                MoveFocusTo(0, forward: true);
                e.Handled = true;
                break;
            case Key.End:
                MoveFocusTo(Math.Min(Current, _count - 1), forward: false);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                if (_focusIndex < Current && StepSelected is not null)
                {
                    StepSelected.Invoke(this, _focusIndex);
                    e.Handled = true;
                }

                break;
            default:
                if (e.Key == forward)
                {
                    MoveFocusTo(_focusIndex + 1, forward: true);
                    e.Handled = true;
                }
                else if (e.Key == backward)
                {
                    MoveFocusTo(_focusIndex - 1, forward: false);
                    e.Handled = true;
                }

                break;
        }
    }

    private void MoveFocusTo(int index, bool forward)
    {
        var current = Math.Clamp(index, 0, Math.Min(Current, _count - 1));
        if (current == _focusIndex)
        {
            return;
        }

        _focusIndex = current;
        _keyboardFocus = true;
        Focus();
        UpdateFocusVisuals();
    }

    private void UpdateFocusVisuals()
    {
        for (var i = 0; i < _rings.Count; i++)
        {
            if (IsFocused && _keyboardFocus && i == _focusIndex)
            {
                _rings[i].Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
            }
            else
            {
                _rings[i].BorderBrush = Brushes.Transparent;
            }
        }
    }

    private void UpdateAutomation()
    {
        AutomationProperties.SetName(this, Label);
        var steps = Steps?.ToList() ?? new List<ArcStep>();
        var help = steps.Count == 0
            ? Label
            : Current >= steps.Count
                ? CompleteLabel
                : $"Step {Math.Clamp(Current, 0, steps.Count - 1) + 1} of {steps.Count}: {steps[Math.Clamp(Current, 0, steps.Count - 1)].Label}";
        AutomationProperties.SetHelpText(this, help);
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _rings.Clear();
        var steps = Steps?.ToList() ?? new List<ArcStep>();
        _count = steps.Count;
        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, Math.Min(Current, _count - 1)));

        if (steps.Count == 0)
        {
            return;
        }

        _root = new StackPanel { Spacing = 12 };
        _root.Children.Add(Orientation == ArcStepperOrientation.Horizontal
            ? BuildHorizontal(steps)
            : BuildVertical(steps));

        if (IsComplete(steps.Count))
        {
            _root.Children.Add(BuildCompleteNote());
        }
        else if (Compact && Orientation == ArcStepperOrientation.Horizontal)
        {
            _root.Children.Add(BuildCaption(steps));
        }

        _host.Children.Add(_root);
        UpdateFocusVisuals();
        UpdateAutomation();
    }

    private bool IsComplete(int count) => Current >= count;

    private Control BuildCaption(IReadOnlyList<ArcStep> steps)
    {
        var index = Math.Clamp(Current, 0, steps.Count - 1);
        return FeedbackUi.Text($"Step {index + 1} of {steps.Count}: {steps[index].Label}", "arc-caption");
    }

    private Control BuildCompleteNote()
    {
        var row = FeedbackUi.Row(6, FeedbackUi.Icon("check", 14, ArcTokens.Success), FeedbackUi.Text(CompleteLabel, "arc-caption"));
        row.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        return row;
    }

    private Control BuildHorizontal(IReadOnlyList<ArcStep> steps)
    {
        var grid = new Grid();
        for (var i = 0; i < steps.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var ring = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(4, 2, 4, 2),
                Child = BuildHorizontalStep(steps[i], i)
            };
            _rings.Add(ring);
            Grid.SetColumn(ring, grid.ColumnDefinitions.Count - 1);
            grid.Children.Add(ring);

            if (i < steps.Count - 1)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                var connector = new Border
                {
                    Height = 2,
                    MinWidth = 24,
                    Margin = new Thickness(8, 19, 8, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                    CornerRadius = new CornerRadius(1)
                };
                connector.Paint(Border.BackgroundProperty, i < Current ? ArcTokens.Accent : ArcTokens.ControlTrack);
                Grid.SetColumn(connector, grid.ColumnDefinitions.Count - 1);
                grid.Children.Add(connector);
            }
        }

        return grid;
    }

    private Control BuildHorizontalStep(ArcStep step, int index)
    {
        var cell = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            MaxWidth = 220
        };

        cell.Children.Add(BuildMarker(step, index));

        if (!Compact)
        {
            var label = FeedbackUi.Text(step.Label, "arc-label");
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextAlignment = TextAlignment.Center;
            label.TextWrapping = TextWrapping.Wrap;
            cell.Children.Add(label);

            var detail = DetailText(step, index);
            if (detail is not null)
            {
                detail.HorizontalAlignment = HorizontalAlignment.Center;
                detail.TextAlignment = TextAlignment.Center;
                detail.TextWrapping = TextWrapping.Wrap;
                detail.MaxWidth = 200;
                cell.Children.Add(detail);
            }
        }

        var stepIndex = index;
        cell.PointerPressed += (_, args) =>
        {
            _keyboardFocus = false;
            _focusIndex = Math.Clamp(stepIndex, 0, Math.Max(0, Math.Min(Current, _count - 1)));
            Focus();
            UpdateFocusVisuals();
            if (stepIndex < Current && StepSelected is not null)
            {
                StepSelected.Invoke(this, stepIndex);
            }

            args.Handled = true;
        };

        AutomationProperties.SetName(cell, $"{step.Label}, {StateName(step, index)}");
        return cell;
    }

    private Control BuildVertical(IReadOnlyList<ArcStep> steps)
    {
        var stack = new StackPanel();
        for (var i = 0; i < steps.Count; i++)
        {
            var index = i;
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition(28, GridUnitType.Pixel));
            row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var gutter = new Panel { Width = 28 };

            if (index < steps.Count - 1)
            {
                var line = new Border
                {
                    Width = 2,
                    Margin = new Thickness(0, 28, 0, -4),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                line.Paint(Border.BackgroundProperty, index < Current ? ArcTokens.Accent : ArcTokens.ControlTrack);
                gutter.Children.Add(line);
            }

            var marker = BuildMarker(steps[index], index);
            marker.VerticalAlignment = VerticalAlignment.Top;
            marker.HorizontalAlignment = HorizontalAlignment.Center;
            gutter.Children.Add(marker);
            row.Children.Add(gutter);

            var content = new StackPanel { Spacing = 4, Margin = new Thickness(14, 2, 0, index < steps.Count - 1 ? 22 : 0) };
            var label = FeedbackUi.Text(steps[index].Label, "arc-label");
            label.TextWrapping = TextWrapping.Wrap;
            content.Children.Add(label);

            var detail = DetailText(steps[index], index);
            if (detail is not null)
            {
                detail.TextWrapping = TextWrapping.Wrap;
                detail.MaxWidth = 420;
                content.Children.Add(detail);
            }

            var ring = new Border
            {
                BorderThickness = new Thickness(2),
                BorderBrush = Brushes.Transparent,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(2),
                Child = content
            };
            _rings.Add(ring);
            Grid.SetColumn(ring, 1);
            row.Children.Add(ring);

            row.PointerPressed += (_, args) =>
            {
                _keyboardFocus = false;
                _focusIndex = Math.Clamp(index, 0, Math.Max(0, Math.Min(Current, _count - 1)));
                Focus();
                UpdateFocusVisuals();
                if (index < Current && StepSelected is not null)
                {
                    StepSelected.Invoke(this, index);
                }

                args.Handled = true;
            };

            stack.Children.Add(row);
        }

        return stack;
    }

    private TextBlock? DetailText(ArcStep step, int index)
    {
        if (!string.IsNullOrWhiteSpace(step.Error))
        {
            var error = FeedbackUi.Text(step.Error, "arc-caption");
            error.Paint(TextBlock.ForegroundProperty, ArcTokens.Warning);
            return error;
        }

        if (string.IsNullOrWhiteSpace(step.Description) || Compact)
        {
            return null;
        }

        if (Details == ArcStepperDetails.Current && index != Current)
        {
            return null;
        }

        return FeedbackUi.Text(step.Description, "arc-caption");
    }

    private Control BuildMarker(ArcStep step, int index)
    {
        var hasError = !string.IsNullOrWhiteSpace(step.Error);
        var done = index < Current;
        var current = index == Current;
        var size = 28;

        var marker = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            BorderThickness = new Thickness(current ? 2 : 1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = null
        };

        if (hasError)
        {
            marker.Paint(Border.BackgroundProperty, ArcTokens.WarningSubtle);
            marker.Paint(Border.BorderBrushProperty, ArcTokens.Warning);
            marker.Child = FeedbackUi.Icon("triangle-alert", 14, ArcTokens.Warning);
        }
        else if (done)
        {
            marker.Paint(Border.BackgroundProperty, ArcTokens.Accent);
            marker.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
            marker.Child = FeedbackUi.Icon("check", 14, ArcTokens.AccentForeground);
        }
        else if (current)
        {
            marker.Paint(Border.BackgroundProperty, ArcTokens.Surface);
            marker.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
            var number = FeedbackUi.Numeric($"{index + 1}", "arc-label");
            number.FontSize = 13;
            number.HorizontalAlignment = HorizontalAlignment.Center;
            number.VerticalAlignment = VerticalAlignment.Center;
            number.Paint(TextBlock.ForegroundProperty, ArcTokens.AccentStrong);
            marker.Child = number;
        }
        else
        {
            marker.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            marker.Paint(Border.BorderBrushProperty, ArcTokens.Border);
            var number = FeedbackUi.Numeric($"{index + 1}", "arc-label");
            number.FontSize = 13;
            number.HorizontalAlignment = HorizontalAlignment.Center;
            number.VerticalAlignment = VerticalAlignment.Center;
            number.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
            marker.Child = number;
        }

        return marker;
    }

    private string StateName(ArcStep step, int index)
    {
        if (!string.IsNullOrWhiteSpace(step.Error))
        {
            return "error";
        }

        if (index < Current)
        {
            return "completed";
        }

        return index == Current ? "current" : "not started";
    }
}

