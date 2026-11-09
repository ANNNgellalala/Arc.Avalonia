using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Arc rendering of a <c>pura-agent</c> <c>confirm</c> component: one question, a confirm and a cancel action,
/// an optional danger styling for destructive confirmations. The first decision is reported through
/// <see cref="Decided"/> and its routed companion, then both buttons lock to prevent duplicates. The card
/// never calls an agent; use <see cref="ApplyInteractionState"/> to restore a persisted decision.
/// </summary>
[TemplatePart("PART_Surface", typeof(Border))]
[TemplatePart("PART_Title", typeof(TextBlock))]
[TemplatePart("PART_Description", typeof(TextBlock))]
[TemplatePart("PART_State", typeof(TextBlock))]
[TemplatePart("PART_Footer", typeof(StackPanel))]
[TemplatePart("PART_Confirm", typeof(ArcButton))]
[TemplatePart("PART_Cancel", typeof(ArcButton))]
public class ArcAgentConfirmCard : TemplatedControl
{
    /// <summary>Parsed payload to render; null hides the card.</summary>
    public static readonly StyledProperty<ArcAgentConfirmModel?> ModelProperty =
        AvaloniaProperty.Register<ArcAgentConfirmCard, ArcAgentConfirmModel?>(nameof(Model));

    /// <summary>When false (host read-only mode or streaming) both actions are disabled.</summary>
    public static readonly StyledProperty<bool> IsInteractiveProperty =
        AvaloniaProperty.Register<ArcAgentConfirmCard, bool>(nameof(IsInteractive), true);

    /// <summary>Raised when the person confirms or cancels (CLR companion).</summary>
    public static readonly RoutedEvent<ArcAgentConfirmDecidedEventArgs> DecidedEvent =
        RoutedEvent.Register<ArcAgentConfirmCard, ArcAgentConfirmDecidedEventArgs>("Decided", RoutingStrategies.Bubble);

    private TextBlock? _title;
    private TextBlock? _description;
    private TextBlock? _state;
    private StackPanel? _footer;
    private ArcButton? _confirm;
    private ArcButton? _cancel;
    private bool _decided;
    private bool _confirmed;
    private ArcMarkdownInteraction? _pendingState;

    static ArcAgentConfirmCard()
    {
        ModelProperty.Changed.AddClassHandler<ArcAgentConfirmCard>((card, _) => card.OnModelChanged());
        IsInteractiveProperty.Changed.AddClassHandler<ArcAgentConfirmCard>((card, _) => card.RebuildContent());
    }

    /// <summary>Creates the card; the ControlTheme is applied by the first attached instance.</summary>
    public ArcAgentConfirmCard() => ArcAgentCardTheme.EnsureApplied(this, typeof(ArcAgentConfirmCard));

    /// <summary>Parsed payload to render.</summary>
    public ArcAgentConfirmModel? Model
    {
        get => GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    /// <summary>Whether the person may interact with the confirmation.</summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Component id used by interaction persistence.</summary>
    public string ComponentId => Model?.Id ?? string.Empty;

    /// <summary>True once the person decided (or a persisted state was applied).</summary>
    public bool IsDecided => _decided;

    /// <summary>Whether the decision was the confirm action; only meaningful after <see cref="IsDecided"/>.</summary>
    public bool Confirmed => _decided && _confirmed;

    /// <summary>CLR companion of <see cref="DecidedEvent"/>; both are raised for one decision.</summary>
    public event EventHandler<ArcAgentConfirmDecidedEventArgs>? Decided;

    /// <summary>Bubbling routed companion of <see cref="Decided"/>.</summary>
    public event EventHandler<ArcAgentConfirmDecidedEventArgs>? DecidedRouted
    {
        add => AddHandler(DecidedEvent, value);
        remove => RemoveHandler(DecidedEvent, value);
    }

    /// <summary>Records a decision from code (also used by tests without a visual tree).</summary>
    /// <param name="confirmed">True for confirm, false for cancel.</param>
    /// <returns>False when the card is read-only or already decided.</returns>
    public bool TryDecide(bool confirmed)
    {
        if (!EffectiveInteractive)
        {
            return false;
        }

        _decided = true;
        _confirmed = confirmed;
        RebuildContent();
        RaiseDecided(confirmed);
        return true;
    }

    /// <summary>Restores a persisted confirm/cancel decision; the card locks and never re-raises.</summary>
    /// <param name="interaction">Recorded interaction for this component id.</param>
    public void ApplyInteractionState(ArcMarkdownInteraction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);
        if (!string.Equals(interaction.ComponentType, "confirm", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _pendingState = interaction;
        _decided = true;
        _confirmed = interaction.Confirmed;
        RebuildContent();
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _description = e.NameScope.Find<TextBlock>("PART_Description");
        _state = e.NameScope.Find<TextBlock>("PART_State");
        _footer = e.NameScope.Find<StackPanel>("PART_Footer");
        _confirm = e.NameScope.Find<ArcButton>("PART_Confirm");
        _cancel = e.NameScope.Find<ArcButton>("PART_Cancel");

        if (_confirm is not null)
        {
            _confirm.MinHeight = 44;
            var canConfirm = ArcAgentTapGuard.Watch(_confirm);
            _confirm.Click += (_, _) =>
            {
                if (canConfirm())
                {
                    TryDecide(true);
                }
            };
        }

        if (_cancel is not null)
        {
            _cancel.MinHeight = 44;
            var canCancel = ArcAgentTapGuard.Watch(_cancel);
            _cancel.Click += (_, _) =>
            {
                if (canCancel())
                {
                    TryDecide(false);
                }
            };
        }

        RebuildContent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ArcAgentCardTheme.EnsureApplied(this, typeof(ArcAgentConfirmCard));
    }

    private bool EffectiveInteractive => IsInteractive && !_decided;

    private void OnModelChanged()
    {
        _decided = false;
        _confirmed = false;
        if (_pendingState is { } pending)
        {
            ApplyInteractionState(pending);
            return;
        }

        RebuildContent();
    }

    private void RebuildContent()
    {
        if (_title is null || _description is null)
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

        if (_confirm is not null)
        {
            _confirm.Content = model.ConfirmLabel;
            _confirm.Variant = model.Danger ? ArcButtonVariant.Danger : ArcButtonVariant.Primary;
            _confirm.IsEnabled = EffectiveInteractive;
        }

        if (_cancel is not null)
        {
            _cancel.Content = model.CancelLabel;
            _cancel.IsEnabled = EffectiveInteractive;
        }

        if (_footer is not null)
        {
            _footer.IsVisible = !_decided;
        }

        if (_state is not null)
        {
            _state.IsVisible = _decided;
            _state.Text = _decided
                ? _confirmed
                    ? $"Confirmed: {model.ConfirmLabel}"
                    : $"Cancelled: {model.CancelLabel}"
                : string.Empty;
        }

        ArcUi.Name(
            this,
            $"{model.Question}. {(_decided ? (_confirmed ? "Confirmed." : "Cancelled.") : "Confirmation needed.")}",
            "Tab moves between the actions; Enter or Space presses the focused action.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    private void RaiseDecided(bool confirmed)
    {
        var args = new ArcAgentConfirmDecidedEventArgs
        {
            ComponentId = ComponentId,
            Confirmed = confirmed,
            Value = confirmed ? Model?.ConfirmLabel : Model?.CancelLabel
        };
        Decided?.Invoke(this, args);
        args.RoutedEvent = DecidedEvent;
        RaiseEvent(args);
    }
}
