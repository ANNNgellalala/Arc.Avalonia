using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.AgentUI.Controls;

/// <summary>The visual stage of a structured question; the host owns every transition.</summary>
public enum ArcAgentAskState
{
    /// <summary>Waiting for the user's answer.</summary>
    Question,

    /// <summary>An answer was submitted to the host.</summary>
    Answered,

    /// <summary>The user chose to skip the detail.</summary>
    Skipped,

    /// <summary>The user chose to answer later.</summary>
    Deferred,

    /// <summary>The question expired before it was answered.</summary>
    Expired
}

/// <summary>One selectable answer in an <see cref="ArcAgentAskCard"/>.</summary>
public sealed class ArcAgentAskOption
{
    /// <summary>Stable id returned in the submission.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The option label, sentence case and short.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional one line explaining the choice.</summary>
    public string? Description { get; set; }

    /// <summary>Optional Lucide icon name shown before the label.</summary>
    public string? Icon { get; set; }
}

/// <summary>What the user sent back from an <see cref="ArcAgentAskCard"/>.</summary>
public sealed class ArcAgentAskAnswer
{
    /// <summary>Ids of the chosen options, in the order the options were declared.</summary>
    public IReadOnlyList<string> SelectedIds { get; set; } = Array.Empty<string>();

    /// <summary>The free text answer, when the card allowed one and the user typed something.</summary>
    public string? FreeText { get; set; }
}

/// <summary>
/// A structured question from an agent: one clear question, a set of options, an optional free text field and
/// two ways to leave the question open (answer later). It is the MCP elicitation shape in UI form, used when a
/// chat bubble cannot carry the answer (pick a date, an account, a file or one of two paths).
///
/// The control only renders the question it is given. Submitting raises <see cref="Submitted"/> with the
/// selected ids and free text; answering later raises <see cref="DeferredRequested"/>. It never calls the
/// agent and never changes <see cref="State"/> itself.
/// </summary>
public class ArcAgentAskCard : Panel
{
    /// <summary>The question the agent needs answered.</summary>
    public static readonly StyledProperty<string> QuestionProperty =
        AvaloniaProperty.Register<ArcAgentAskCard, string>(
            nameof(Question), "Which day should I move the dentist appointment to?");

    /// <summary>The selectable options.</summary>
    public static readonly StyledProperty<IEnumerable<ArcAgentAskOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcAgentAskCard, IEnumerable<ArcAgentAskOption>?>(nameof(Options), SampleOptions());

    /// <summary>When true several options can be chosen; when false choosing one replaces the previous choice.</summary>
    public static readonly StyledProperty<bool> AllowMultipleProperty =
        AvaloniaProperty.Register<ArcAgentAskCard, bool>(nameof(AllowMultiple));

    /// <summary>When true the card shows a free text field beside the options.</summary>
    public static readonly StyledProperty<bool> AllowFreeTextProperty =
        AvaloniaProperty.Register<ArcAgentAskCard, bool>(nameof(AllowFreeText), true);

    /// <summary>The visual stage, set by the host.</summary>
    public static readonly StyledProperty<ArcAgentAskState> StateProperty =
        AvaloniaProperty.Register<ArcAgentAskCard, ArcAgentAskState>(nameof(State), ArcAgentAskState.Question);

    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcButton> _optionButtons = new(StringComparer.Ordinal);
    private TextBox? _freeBox;
    private ArcButton? _submit;
    private string _freeText = string.Empty;
    private string? _pendingFocus;
    private ArcAgentAskAnswer? _lastAnswer;

    static ArcAgentAskCard()
    {
        QuestionProperty.Changed.AddClassHandler<ArcAgentAskCard>((card, _) => card.Rebuild());
        OptionsProperty.Changed.AddClassHandler<ArcAgentAskCard>((card, _) =>
        {
            card._selected.Clear();
            card._freeText = string.Empty;
            card.Rebuild();
        });
        AllowMultipleProperty.Changed.AddClassHandler<ArcAgentAskCard>((card, _) =>
        {
            card._selected.Clear();
            card.Rebuild();
        });
        AllowFreeTextProperty.Changed.AddClassHandler<ArcAgentAskCard>((card, _) => card.Rebuild());
        StateProperty.Changed.AddClassHandler<ArcAgentAskCard>((card, _) => card.Rebuild());
    }

    public ArcAgentAskCard()
    {
        ClipToBounds = false;
        Rebuild();
    }

    /// <summary>The question the agent needs answered.</summary>
    public string Question
    {
        get => GetValue(QuestionProperty);
        set => SetValue(QuestionProperty, value);
    }

    /// <summary>The selectable options.</summary>
    public IEnumerable<ArcAgentAskOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Allows choosing more than one option.</summary>
    public bool AllowMultiple
    {
        get => GetValue(AllowMultipleProperty);
        set => SetValue(AllowMultipleProperty, value);
    }

    /// <summary>Shows a free text answer field.</summary>
    public bool AllowFreeText
    {
        get => GetValue(AllowFreeTextProperty);
        set => SetValue(AllowFreeTextProperty, value);
    }

    /// <summary>The visual stage of the card, set by the host.</summary>
    public ArcAgentAskState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Raised with the chosen option ids and free text when the user sends an answer.</summary>
    public event EventHandler<ArcAgentAskAnswer>? Submitted;

    /// <summary>Raised when the user chooses to answer later.</summary>
    public event EventHandler? DeferredRequested;

    private void Rebuild()
    {
        Children.Clear();
        _optionButtons.Clear();

        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(BuildHeader());

        if (State == ArcAgentAskState.Question)
        {
            var options = (Options ?? Array.Empty<ArcAgentAskOption>()).ToList();
            if (options.Count > 0)
            {
                var list = new StackPanel { Spacing = 6 };
                foreach (var option in options)
                {
                    list.Children.Add(BuildOption(option));
                }

                body.Children.Add(list);
            }

            if (AllowFreeText)
            {
                body.Children.Add(BuildFreeText());
            }

            body.Children.Add(BuildFooter(options.Count));
        }
        else
        {
            body.Children.Add(BuildResolvedNote());
        }

        var card = new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(20, 18, 20, 18),
            Child = body
        };

        Children.Add(card);

        ArcUi.Name(
            this,
            $"Agent question: {Question}. {StateWord(State)}.",
            "Choose an option, type an answer or answer later.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

        if (_pendingFocus is not null)
        {
            var id = _pendingFocus;
            _pendingFocus = null;
            Dispatcher.UIThread.Post(
                () =>
                {
                    if (_optionButtons.TryGetValue(id, out var button))
                    {
                        button.Focus();
                    }
                },
                DispatcherPriority.Loaded);
        }
    }

    private Control BuildHeader()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };

        row.Children.Add(new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(12),
            Background = ArcUi.Brush(ArcTokens.AccentSubtle),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ArcIcon
            {
                Icon = "circle-help",
                Size = 17,
                Foreground = ArcUi.Brush(ArcTokens.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });

        var copy = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
        copy.Children.Add(ArcUi.Caption("The agent needs one detail"));
        copy.Children.Add(ArcUi.Text(Question, "arc-body arc-medium", 520));
        Grid.SetColumn(copy, 1);
        row.Children.Add(copy);

        var badge = StateBadge(State);
        badge.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(badge, 2);
        row.Children.Add(badge);
        return row;
    }

    private Control BuildOption(ArcAgentAskOption option)
    {
        var selected = _selected.Contains(option.Id);

        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 10 };
        content.Children.Add(BuildSelectionMark(selected));

        if (!string.IsNullOrWhiteSpace(option.Icon))
        {
            var glyph = ArcUi.Icon(option.Icon!, 15, selected ? ArcTokens.Accent : ArcTokens.TextSecondary);
            glyph.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(glyph, 1);
            content.Children.Add(glyph);
        }

        var labels = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var label = ArcUi.Text(option.Label, selected ? "arc-body-sm arc-medium" : "arc-body-sm");
        labels.Children.Add(label);
        if (!string.IsNullOrWhiteSpace(option.Description))
        {
            labels.Children.Add(ArcUi.Caption(option.Description!));
        }

        Grid.SetColumn(labels, 2);
        content.Children.Add(labels);

        if (selected)
        {
            var check = ArcUi.Icon("check", 14, ArcTokens.Accent);
            check.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(check, 3);
            content.Children.Add(check);
        }

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8, 10, 8),
            Background = selected ? ArcUi.Brush(ArcTokens.AccentSubtle) : null,
            Foreground = selected ? ArcUi.Brush(ArcTokens.Foreground) : null,
            BorderThickness = new Thickness(1),
            BorderBrush = selected ? ArcUi.Brush(ArcTokens.Accent) : ArcUi.Brush(ArcTokens.BorderSubtle),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Content = content
        };
        AutomationProperties.SetName(button,
            $"{option.Label}. {(selected ? "Selected" : "Not selected")}. {(AllowMultiple ? "Toggle this option" : "Choose this option")}.");
        button.Click += (_, _) => ToggleOption(option.Id);

        _optionButtons[option.Id] = button;
        return button;
    }

    private static Control BuildSelectionMark(bool selected)
    {
        var size = 20.0;
        var mark = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            BorderThickness = new Thickness(selected ? 0 : 1),
            BorderBrush = ArcUi.Brush(selected ? ArcTokens.Accent : ArcTokens.BorderStrong),
            Background = selected ? ArcUi.Brush(ArcTokens.Accent) : null,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (selected)
        {
            mark.Child = new ArcIcon
            {
                Icon = "check",
                Size = 12,
                Foreground = ArcUi.Brush(ArcTokens.AccentForeground),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        return mark;
    }

    private Control BuildFreeText()
    {
        _freeBox = new TextBox
        {
            Text = _freeText,
            PlaceholderText = "Or type a short answer",
            MinHeight = 38,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Background = ArcUi.Brush(ArcTokens.SurfaceRaised),
            Foreground = ArcUi.Brush(ArcTokens.Foreground),
            BorderBrush = ArcUi.Brush(ArcTokens.Border)
        };
        ArcUi.Name(_freeBox, "Free text answer");
        _freeBox.TextChanged += (_, _) =>
        {
            _freeText = _freeBox?.Text ?? string.Empty;
            UpdateSubmitState();
        };
        return _freeBox;
    }

    private Control BuildFooter(int optionCount)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };

        var later = ArcUi.Button("Answer later", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "clock");
        ArcUi.Name(later, "Answer this question later");
        later.Click += (_, _) => DeferredRequested?.Invoke(this, EventArgs.Empty);
        row.Children.Add(later);

        var hint = ArcUi.Caption(optionCount == 0
            ? "Type an answer to send it."
            : AllowMultiple
                ? "Choose one or more options."
                : "Choose one option.");
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(hint, 1);
        row.Children.Add(hint);

        _submit = ArcUi.Button("Send answer", ArcButtonVariant.Primary, ArcButtonSize.Sm, "send");
        ArcUi.Name(_submit, "Send the answer to the agent");
        _submit.Click += (_, _) => Submit();
        Grid.SetColumn(_submit, 2);
        row.Children.Add(_submit);

        UpdateSubmitState();
        return row;
    }

    private void UpdateSubmitState()
    {
        if (_submit is null)
        {
            return;
        }

        _submit.IsEnabled = _selected.Count > 0 || (AllowFreeText && _freeText.Trim().Length > 0);
    }

    private Control BuildResolvedNote()
    {
        var (icon, token, copy) = State switch
        {
            ArcAgentAskState.Answered => ("check", ArcTokens.Success, "Answer sent to the agent."),
            ArcAgentAskState.Deferred => ("clock", ArcTokens.TextSecondary, "Deferred. The agent will ask again later."),
            ArcAgentAskState.Skipped => ("minus", ArcTokens.TextMuted, "Skipped. The agent continues without this detail."),
            _ => ("triangle-alert", ArcTokens.Danger, "This question expired. The agent must ask again.")
        };

        var panel = new StackPanel { Spacing = 8 };

        var row = ArcUi.HStack(8);
        row.Children.Add(ArcUi.Icon(icon, 15, token));
        var text = ArcUi.Text(copy, "arc-body-sm", 520);
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(text);
        panel.Children.Add(row);

        if (State == ArcAgentAskState.Answered && _lastAnswer is not null)
        {
            var summary = DescribeAnswer(_lastAnswer);
            if (summary.Length > 0)
            {
                var pill = ArcUi.Pill(summary, "check", accent: true);
                pill.HorizontalAlignment = HorizontalAlignment.Left;
                panel.Children.Add(pill);
            }
        }

        return panel;
    }

    private string DescribeAnswer(ArcAgentAskAnswer answer)
    {
        var options = (Options ?? Array.Empty<ArcAgentAskOption>()).ToList();
        var parts = new List<string>();
        foreach (var id in answer.SelectedIds)
        {
            var option = options.FirstOrDefault(candidate => candidate.Id == id);
            parts.Add(option?.Label ?? id);
        }

        if (!string.IsNullOrWhiteSpace(answer.FreeText))
        {
            parts.Add($"\"{answer.FreeText.Trim()}\"");
        }

        return string.Join(" · ", parts);
    }

    private void ToggleOption(string id)
    {
        if (!AllowMultiple)
        {
            var alreadyOnly = _selected.Count == 1 && _selected.Contains(id);
            _selected.Clear();
            if (!alreadyOnly)
            {
                _selected.Add(id);
            }
        }
        else if (!_selected.Remove(id))
        {
            _selected.Add(id);
        }

        _pendingFocus = id;
        Rebuild();
    }

    private void Submit()
    {
        var order = (Options ?? Array.Empty<ArcAgentAskOption>())
            .Where(option => _selected.Contains(option.Id))
            .Select(option => option.Id)
            .ToList();

        var answer = new ArcAgentAskAnswer
        {
            SelectedIds = order,
            FreeText = AllowFreeText && _freeText.Trim().Length > 0 ? _freeText.Trim() : null
        };

        _lastAnswer = answer;
        Submitted?.Invoke(this, answer);
    }

    private static ArcBadge StateBadge(ArcAgentAskState state) => state switch
    {
        ArcAgentAskState.Question => ArcUi.Badge("Needs answer", ArcBadgeTone.Accent),
        ArcAgentAskState.Answered => ArcUi.Badge("Answered", ArcBadgeTone.Success),
        ArcAgentAskState.Deferred => ArcUi.Badge("Deferred", ArcBadgeTone.Neutral),
        ArcAgentAskState.Expired => ArcUi.Badge("Expired", ArcBadgeTone.Danger),
        _ => ArcUi.Badge("Skipped", ArcBadgeTone.Neutral)
    };

    private static string StateWord(ArcAgentAskState state) => state switch
    {
        ArcAgentAskState.Question => "waiting for an answer",
        ArcAgentAskState.Answered => "answered",
        ArcAgentAskState.Deferred => "deferred",
        ArcAgentAskState.Expired => "expired",
        _ => "skipped"
    };

    private static IEnumerable<ArcAgentAskOption> SampleOptions() => new[]
    {
        new ArcAgentAskOption
        {
            Id = "thu", Label = "Thursday at 15:00", Description = "Keeps the morning free for the budget review.", Icon = "calendar"
        },
        new ArcAgentAskOption
        {
            Id = "fri", Label = "Friday at 9:30", Description = "Earliest slot after the dentist's next opening.", Icon = "calendar"
        },
        new ArcAgentAskOption
        {
            Id = "next-week", Label = "Next Monday at 11:00", Description = "Pushes the reminder out by four days.", Icon = "calendar"
        }
    };
}
