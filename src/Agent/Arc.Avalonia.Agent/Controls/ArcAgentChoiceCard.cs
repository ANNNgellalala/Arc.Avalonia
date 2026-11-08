using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Arc rendering of a <c>pura-agent</c> <c>choice</c> component: a question, single or multi select options
/// with descriptions and disabled states, and a submit action for multi select. Single select reports the
/// moment an option is chosen (docs/03 §4.3); multi select validates <c>min</c>/<c>max</c> and submits with
/// the button. The card reports through <see cref="Submitted"/> and its routed companion; it never talks to
/// an agent or a transport. Use <see cref="ApplyInteractionState"/> to restore a persisted selection.
/// </summary>
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_Title", typeof(TextBlock))]
[TemplatePart("PART_Description", typeof(TextBlock))]
[TemplatePart("PART_Options", typeof(StackPanel))]
[TemplatePart("PART_Error", typeof(TextBlock))]
[TemplatePart("PART_Footer", typeof(StackPanel))]
[TemplatePart("PART_Submit", typeof(ArcButton))]
[TemplatePart("PART_Hint", typeof(TextBlock))]
public class ArcAgentChoiceCard : TemplatedControl
{
    /// <summary>Parsed payload to render; null hides the card.</summary>
    public static readonly StyledProperty<ArcAgentChoiceModel?> ModelProperty =
        AvaloniaProperty.Register<ArcAgentChoiceCard, ArcAgentChoiceModel?>(nameof(Model));

    /// <summary>When false (host read-only mode or streaming) options and submit are disabled.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<ArcAgentChoiceCard, bool>(nameof(IsInteractive), true);

    /// <summary>Raised when a single choice is made or a multi selection is submitted (CLR companion).</summary>
    public static readonly RoutedEvent<ArcAgentChoiceSubmittedEventArgs> SubmittedEvent =
        RoutedEvent.Register<ArcAgentChoiceCard, ArcAgentChoiceSubmittedEventArgs>("Submitted", RoutingStrategies.Bubble);

    private readonly HashSet<string> _selected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArcButton> _optionButtons = new(StringComparer.Ordinal);

    private TextBlock? _title;
    private TextBlock? _description;
    private StackPanel? _optionsHost;
    private TextBlock? _error;
    private StackPanel? _footer;
    private ArcButton? _submit;
    private TextBlock? _hint;
    private bool _submitted;
    private string? _errorText;
    private string? _pendingFocus;
    private ArcMarkdownInteraction? _pendingState;

    static ArcAgentChoiceCard()
    {
        ModelProperty.Changed.AddClassHandler<ArcAgentChoiceCard>((card, _) => card.OnModelChanged());
        IsInteractiveProperty.Changed.AddClassHandler<ArcAgentChoiceCard>((card, _) => card.RebuildContent());
    }

    /// <summary>Creates the card; the ControlTheme is applied by the first attached instance.</summary>
    public ArcAgentChoiceCard() => ArcAgentCardTheme.EnsureApplied(this, typeof(ArcAgentChoiceCard));

    /// <summary>Parsed payload to render.</summary>
    public ArcAgentChoiceModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Whether the person may interact with the choice.</summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Component id used by <see cref="InteractionOccurred"/> persistence.</summary>
    public string ComponentId => Model?.Id ?? string.Empty;

    /// <summary>Selected option ids in declaration order.</summary>
    public IReadOnlyList<string> SelectedIds => Model is { } model
        ? model.Options.Where(option => _selected.Contains(option.Id)).Select(option => option.Id).ToArray()
        : Array.Empty<string>();

    /// <summary>True once the choice was submitted (a restored interaction also sets it).</summary>
    public bool IsSubmitted => _submitted;

    /// <summary>CLR companion of <see cref="SubmittedEvent"/>; both are raised for one interaction.</summary>
    public event EventHandler<ArcAgentChoiceSubmittedEventArgs>? Submitted;

    /// <summary>Bubbling routed companion of <see cref="Submitted"/>.</summary>
    public event EventHandler<ArcAgentChoiceSubmittedEventArgs>? SubmittedRouted
    {
        add => AddHandler(SubmittedEvent, value);
        remove => RemoveHandler(SubmittedEvent, value);
    }

    /// <summary>
    /// Submits the current multi selection, validating <c>min</c>/<c>max</c>. Also usable from code and
    /// tests without a visual tree. Returns false when the card is not interactive or the selection fails
    /// validation (the inline error is shown instead).
    /// </summary>
    public bool TrySubmit()
    {
        if (Model is not { Multi: true } model || !EffectiveInteractive)
        {
            return false;
        }

        var selected = SelectedIds;
        var minimum = Math.Max(1, model.Min);
        if (selected.Count < minimum)
        {
            ShowError($"Select at least {minimum} option{(minimum == 1 ? string.Empty : "s")}.");
            return false;
        }

        if (model.Max is { } maximum && selected.Count > maximum)
        {
            ShowError($"Select at most {maximum} option{(maximum == 1 ? string.Empty : "s")}.");
            return false;
        }

        _errorText = null;
        _submitted = true;
        RebuildContent();
        RaiseSubmitted();
        return true;
    }

    /// <summary>
    /// Toggles one option exactly like a click on its row. Single select immediately submits; multi select
    /// only updates the local selection. Returns false when the option cannot be selected.
    /// </summary>
    /// <param name="optionId">Option id from the payload.</param>
    public bool ToggleOption(string optionId)
    {
        if (Model is not { } model || !EffectiveInteractive)
        {
            return false;
        }

        var option = model.Options.FirstOrDefault(candidate => string.Equals(candidate.Id, optionId, StringComparison.Ordinal));
        if (option is null || option.Disabled)
        {
            return false;
        }

        _pendingFocus = option.Id;
        if (!model.Multi)
        {
            _selected.Clear();
            _selected.Add(option.Id);
            _errorText = null;
            _submitted = true;
            RebuildContent();
            RaiseSubmitted();
            return true;
        }

        if (!_selected.Remove(option.Id))
        {
            if (model.Max is { } maximum && _selected.Count >= maximum)
            {
                ShowError($"Select at most {maximum} option{(maximum == 1 ? string.Empty : "s")}.");
                return false;
            }

            _selected.Add(option.Id);
        }

        _errorText = null;
        RebuildContent();
        return true;
    }

    /// <summary>
    /// Restores a persisted interaction (also used after virtualization/re-render). The selection is filtered
    /// against the payload options; a confirmed state locks the card and disables the controls.
    /// </summary>
    /// <param name="interaction">Recorded interaction for this component id.</param>
    public void ApplyInteractionState(ArcMarkdownInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        if (!string.Equals(interaction.ComponentType, "choice", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Model is null)
        {
            _pendingState = interaction;
            return;
        }

        _selected.Clear();
        foreach (var id in interaction.SelectedIds)
        {
            if (Model.Options.Any(option => string.Equals(option.Id, id, StringComparison.Ordinal)))
            {
                _selected.Add(id);
            }
        }

        _submitted = interaction.Confirmed;
        _errorText = null;
        _pendingState = null;
        RebuildContent();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _optionsHost = e.NameScope.Find<StackPanel>("PART_Options");
        _error = e.NameScope.Find<TextBlock>("PART_Error");
        _footer = e.NameScope.Find<StackPanel>("PART_Footer");
        _submit = e.NameScope.Find<ArcButton>("PART_Submit");
        _hint = e.NameScope.Find<TextBlock>("PART_Hint");

        if (_submit is not null)
        {
            _submit.Click += (_, _) => TrySubmit();
        }

        if (_error is not null)
        {
            _error.IsVisible = false;
        }

        RebuildContent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ArcAgentCardTheme.EnsureApplied(this, typeof(ArcAgentChoiceCard));
    }

    private bool EffectiveInteractive => IsInteractive && Model is { Disabled: false } && !_submitted;

    private void OnModelChanged()
    {
        _selected.Clear();
        _submitted = false;
        _errorText = null;
        _pendingFocus = null;
        if (Model is { } model)
        {
            foreach (var id in model.DefaultSelected)
            {
                if (model.Options.Any(option => string.Equals(option.Id, id, StringComparison.Ordinal)))
                {
                    _selected.Add(id);
                }
            }
        }

        if (_pendingState is { } pending)
        {
            ApplyInteractionState(pending);
            return;
        }

        RebuildContent();
    }

    private void RebuildContent()
    {
        if (_title is null || _description is null || _optionsHost is null)
        {
            return;
        }

        var model = Model;
        if (model is null)
        {
            IsVisible = false;
            return;
        }

        IsVisible = true;
        _title.Text = model.Question;
        _description.Text = model.Description ?? string.Empty;
        _description.IsVisible = !string.IsNullOrWhiteSpace(model.Description);

        _optionsHost.Children.Clear();
        _optionButtons.Clear();
        foreach (var option in model.Options)
        {
            var button = BuildOption(option, model);
            _optionButtons[option.Id] = button;
            _optionsHost.Children.Add(button);
        }

        if (_error is not null)
        {
            _error.Text = _errorText ?? string.Empty;
            _error.IsVisible = !string.IsNullOrWhiteSpace(_errorText);
        }

        UpdateFooter(model);
        UpdateAutomation(model);
        RestoreFocus();
    }

    private ArcButton BuildOption(ArcAgentChoiceOption option, ArcAgentChoiceModel model)
    {
        var selected = _selected.Contains(option.Id);
        var interactive = EffectiveInteractive;

        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.Children.Add(BuildSelectionMark(selected, model.Multi));

        var labels = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var label = ArcUi.Text(option.Label, selected ? "arc-body-sm arc-medium" : "arc-body-sm");
        if (option.Disabled)
        {
            label.Foreground = ArcUi.Brush(ArcTokens.TextMuted);
        }

        labels.Children.Add(label);
        if (!string.IsNullOrWhiteSpace(option.Description))
        {
            labels.Children.Add(ArcUi.Caption(option.Description!));
        }

        Grid.SetColumn(labels, 1);
        content.Children.Add(labels);

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8, 10, 8),
            BorderThickness = new Thickness(1),
            BorderBrush = ArcUi.Brush(selected ? ArcTokens.Accent : ArcTokens.BorderSubtle),
            Background = selected ? ArcUi.Brush(ArcTokens.AccentSubtle) : null,
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            IsEnabled = interactive && !option.Disabled,
            Content = content
        };

        button.Click += (_, _) => ToggleOption(option.Id);
        AutomationProperties.SetName(
            button,
            $"{option.Label}. {(selected ? "Selected" : "Not selected")}. " +
            (option.Disabled ? "Disabled." : model.Multi ? "Toggle this option." : "Choose this option."));
        if (!string.IsNullOrWhiteSpace(option.Description))
        {
            AutomationProperties.SetHelpText(button, option.Description);
        }

        return button;
    }

    private static Control BuildSelectionMark(bool selected, bool multi)
    {
        const double size = 20;
        var mark = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(multi ? 6 : size / 2),
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

    private void UpdateFooter(ArcAgentChoiceModel model)
    {
        if (_footer is null || _submit is null || _hint is null)
        {
            return;
        }

        var submitted = _submitted;
        _submit.IsVisible = model.Multi && !submitted;
        _submit.Content = model.SubmitLabel;
        _submit.IsEnabled = EffectiveInteractive;

        _hint.Text = submitted
            ? $"Selection sent: {string.Join(", ", SelectedIds)}"
            : model.Multi
                ? SelectionHint(model)
                : string.Empty;
        _hint.IsVisible = submitted || model.Multi;

        _footer.IsVisible = model.Multi || submitted;
    }

    private static string SelectionHint(ArcAgentChoiceModel model)
    {
        var minimum = Math.Max(1, model.Min);
        if (model.Max is { } maximum)
        {
            return minimum == maximum
                ? $"Choose {minimum} option{(minimum == 1 ? string.Empty : "s")}."
                : $"Choose {minimum} to {maximum} options.";
        }

        return $"Choose at least {minimum} option{(minimum == 1 ? string.Empty : "s")}.";
    }

    private void UpdateAutomation(ArcAgentChoiceModel model)
    {
        ArcUi.Name(
            this,
            $"{model.Question}. {(_submitted ? "Selection submitted." : model.Multi ? "Multiple choice." : "Single choice.")}",
            "Choose an option with the keyboard: Tab moves between options, Enter or Space selects.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private void ShowError(string message)
    {
        _errorText = message;
        if (_error is not null)
        {
            _error.Text = message;
            _error.IsVisible = true;
        }

        if (_optionButtons.Count > 0)
        {
            _pendingFocus = _optionButtons.Keys.First();
            RestoreFocus();
        }
    }

    private void RestoreFocus()
    {
        if (_pendingFocus is not { } id)
        {
            return;
        }

        _pendingFocus = null;
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_optionButtons.TryGetValue(id, out var button) && button.IsEffectivelyEnabled)
                {
                    button.Focus();
                }
            },
            DispatcherPriority.Loaded);
    }

    private void RaiseSubmitted()
    {
        var args = new ArcAgentChoiceSubmittedEventArgs
        {
            ComponentId = ComponentId,
            SelectedIds = SelectedIds
        };
        Submitted?.Invoke(this, args);
        args.RoutedEvent = SubmittedEvent;
        RaiseEvent(args);
    }
}
