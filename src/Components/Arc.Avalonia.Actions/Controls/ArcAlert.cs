using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Layout;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcAlertTone
{
    Info,
    Success,
    Warning,
    Danger
}

/// <summary>
/// A persistent message inside the page flow: a tone icon, a short title, optional details and one
/// action. It stays until it is dismissed, unlike a toast.
/// </summary>
[TemplatePart("PART_Icon", typeof(ArcIcon))]
[TemplatePart("PART_Title", typeof(TextBlock))]
[TemplatePart("PART_Details", typeof(ContentPresenter))]
[TemplatePart("PART_Action", typeof(ArcButton))]
[TemplatePart("PART_Close", typeof(ArcButton))]
public sealed class ArcAlert : ContentControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcAlert, string?>(nameof(Title));

    public static readonly StyledProperty<ArcAlertTone> ToneProperty =
        AvaloniaProperty.Register<ArcAlert, ArcAlertTone>(nameof(Tone));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcAlert, bool>(nameof(IsOpen), true);

    public static readonly StyledProperty<bool> IsDismissibleProperty =
        AvaloniaProperty.Register<ArcAlert, bool>(nameof(IsDismissible), true);

    public static readonly StyledProperty<string?> ActionLabelProperty =
        AvaloniaProperty.Register<ArcAlert, string?>(nameof(ActionLabel));

    private ArcIcon? _icon;
    private TextBlock? _title;
    private ContentPresenter? _details;
    private ArcButton? _action;
    private ArcButton? _close;

    static ArcAlert()
    {
        TitleProperty.Changed.AddClassHandler<ArcAlert>((alert, _) => alert.UpdateCopy());
        ToneProperty.Changed.AddClassHandler<ArcAlert>((alert, _) => alert.UpdateTone());
        ActionLabelProperty.Changed.AddClassHandler<ArcAlert>((alert, _) => alert.UpdateCopy());
        IsOpenProperty.Changed.AddClassHandler<ArcAlert>((alert, _) => alert.IsVisible = alert.IsOpen);
        ContentProperty.Changed.AddClassHandler<ArcAlert>((alert, _) => alert.UpdateCopy());
        IsDismissibleProperty.Changed.AddClassHandler<ArcAlert>((alert, _) => alert.UpdateCopy());
    }

    /// <summary>Runs when the action button is pressed.</summary>
    public Action? Action { get; set; }

    public event EventHandler? Dismissed;
    /// <summary>Bubbled copy of <see cref="Dismissed"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcAlertDismissedEventArgs> DismissedEvent =
        RoutedEvent.Register<ArcAlert, ArcAlertDismissedEventArgs>("Dismissed", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="Dismissed"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcAlertDismissedEventArgs>? DismissedRouted
    {
        add => AddHandler(DismissedEvent, value);
        remove => RemoveHandler(DismissedEvent, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ArcAlertTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Shows the close button; turn it off for messages that must stay.</summary>
    public bool IsDismissible
    {
        get => GetValue(IsDismissibleProperty);
        set => SetValue(IsDismissibleProperty, value);
    }

    /// <summary>Label for the optional action; no label means no action button.</summary>
    public string? ActionLabel
    {
        get => GetValue(ActionLabelProperty);
        set => SetValue(ActionLabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<ArcIcon>("PART_Icon");
        _title = e.NameScope.Find<TextBlock>("PART_Title");
        _details = e.NameScope.Find<ContentPresenter>("PART_Details");
        _action = e.NameScope.Find<ArcButton>("PART_Action");
        _close = e.NameScope.Find<ArcButton>("PART_Close");

        if (_close is not null)
        {
            _close.Content = new ArcIcon { Icon = "x", Size = 15 };
            _close.Click += (_, _) => Dismiss();
        }

        if (_action is not null)
        {
            _action.Click += (_, _) => Action?.Invoke();
        }

        UpdateTone();
        UpdateCopy();
        IsVisible = IsOpen;
    }

    private void UpdateTone()
    {
        Classes.Set("info", Tone == ArcAlertTone.Info);
        Classes.Set("success", Tone == ArcAlertTone.Success);
        Classes.Set("warning", Tone == ArcAlertTone.Warning);
        Classes.Set("danger", Tone == ArcAlertTone.Danger);

        if (_icon is not null)
        {
            _icon.Icon = Tone switch
            {
                ArcAlertTone.Success => "circle-check",
                ArcAlertTone.Warning => "triangle-alert",
                ArcAlertTone.Danger => "circle-alert",
                _ => "info"
            };
            _icon.Classes.Set("success", Tone == ArcAlertTone.Success);
            _icon.Classes.Set("warning", Tone == ArcAlertTone.Warning);
            _icon.Classes.Set("danger", Tone == ArcAlertTone.Danger);
        }

        AutomationProperties.SetLiveSetting(this, Tone == ArcAlertTone.Danger ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }

    private void UpdateCopy()
    {
        if (_title is not null)
        {
            _title.Text = Title;
        }

        if (_details is not null)
        {
            _details.Content = Content;
        }

        if (_action is not null)
        {
            _action.Content = ActionLabel;
            _action.IsVisible = !string.IsNullOrWhiteSpace(ActionLabel);
        }

        if (_close is not null)
        {
            _close.IsVisible = IsDismissible;
            AutomationProperties.SetName(_close, $"Dismiss: {Title}");
        }

        AutomationProperties.SetName(this, Title ?? "Message");
    }

    private void Dismiss()
    {
        SetCurrentValue(IsOpenProperty, false);
                var dismissedArgs = new ArcAlertDismissedEventArgs(DismissedEvent);
        Dismissed?.Invoke(this, EventArgs.Empty);
        RaiseEvent(dismissedArgs);
    }
}