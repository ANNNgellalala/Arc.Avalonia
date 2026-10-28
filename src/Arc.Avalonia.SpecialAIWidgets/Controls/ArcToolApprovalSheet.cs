using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialAIWidgets.Controls;

/// <summary>How much damage an approved tool call can do.</summary>
public enum ArcApprovalRisk
{
    /// <summary>Read-only or easily undone.</summary>
    Low,

    /// <summary>Changes data that a person would notice.</summary>
    Medium,

    /// <summary>Moves money, sends things or deletes data that cannot be recovered automatically.</summary>
    High
}

/// <summary>How long an approval lasts.</summary>
public enum ArcApprovalScope
{
    /// <summary>Approve this one call only.</summary>
    Once,

    /// <summary>Approve calls of this tool for the rest of the task.</summary>
    Task,

    /// <summary>Approve calls of this tool for one hour.</summary>
    Hour,

    /// <summary>Remember the decision until the host removes the rule.</summary>
    Always
}

/// <summary>The visual stage of the approval sheet; the host owns every transition.</summary>
public enum ArcApprovalSheetState
{
    /// <summary>Waiting for a decision.</summary>
    Checkout,

    /// <summary>The user is changing arguments.</summary>
    Editing,

    /// <summary>The decision was sent to the host and is being applied.</summary>
    Submitting,

    /// <summary>The host recorded a decision.</summary>
    Decided,

    /// <summary>The request expired before a decision.</summary>
    Expired,

    /// <summary>The request was cancelled.</summary>
    Cancelled
}

/// <summary>One editable argument in an approval request.</summary>
public sealed class ArcApprovalArgument
{
    /// <summary>The argument's display name, for example "start" or "attendees".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The value proposed by the agent.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>When true the user can rewrite the value before approving.</summary>
    public bool Editable { get; set; }
}

/// <summary>The approval the user sent back, including edited arguments and the chosen scope.</summary>
public sealed class ArcApprovalDecision
{
    /// <summary>Arguments as the user approved them.</summary>
    public IReadOnlyList<ArcApprovalArgument> ModifiedArguments { get; set; } = Array.Empty<ArcApprovalArgument>();

    /// <summary>How long the approval should last.</summary>
    public ArcApprovalScope Scope { get; set; }
}

/// <summary>
/// A pre-execution approval sheet for a risky tool call: the tool and its intent, editable arguments, a
/// preview of the side effects, a risk note, an expiry countdown, a scope choice and the three decisions
/// (approve, deny with a reason, delegate).
///
/// The control only renders the request it is given. Approving raises <see cref="ApproveRequested"/> with the
/// edited arguments and the chosen scope; denying raises <see cref="DenyRequested"/>; neither writes an audit
/// entry, runs the tool nor changes <see cref="State"/>. The countdown is presentation only and disables
/// Approve when it reaches zero; the host must still enforce the expiry.
/// </summary>
public class ArcToolApprovalSheet : Panel
{
    /// <summary>The tool being requested, shown as the sheet's subtitle.</summary>
    public static readonly StyledProperty<string> ToolNameProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, string>(nameof(ToolName), "calendar.create_event");

    /// <summary>The agent's intent in plain language, shown above the arguments.</summary>
    public static readonly StyledProperty<string> IntentProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, string>(
            nameof(Intent), "Book the dentist appointment for Thursday at 15:00 and invite Sam.");

    /// <summary>The risk level that drives the badge and the warning note.</summary>
    public static readonly StyledProperty<ArcApprovalRisk> RiskProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, ArcApprovalRisk>(nameof(Risk), ArcApprovalRisk.High);

    /// <summary>The arguments the agent wants to run with; <see cref="ArcApprovalArgument.Editable"/> ones become fields.</summary>
    public static readonly StyledProperty<IEnumerable<ArcApprovalArgument>?> ArgumentsProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, IEnumerable<ArcApprovalArgument>?>(nameof(Arguments),
            SampleArguments());

    /// <summary>Lines describing what will change if the call runs.</summary>
    public static readonly StyledProperty<IEnumerable<string>?> PreviewLinesProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, IEnumerable<string>?>(nameof(PreviewLines),
            new[] { "Creates Calendar event \"Dentist\" on Thu 15:00-15:45", "Sends an invite to sam@northwind.example" });

    /// <summary>Seconds until the request expires; 0 or less shows an expired request.</summary>
    public static readonly StyledProperty<double> ExpiresInSecondsProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, double>(nameof(ExpiresInSeconds), 144);

    /// <summary>The scope the user selected; the host decides what the selection allows.</summary>
    public static readonly StyledProperty<ArcApprovalScope> ScopeProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, ArcApprovalScope>(nameof(Scope), ArcApprovalScope.Once);

    /// <summary>The visual stage, set by the host.</summary>
    public static readonly StyledProperty<ArcApprovalSheetState> StateProperty =
        AvaloniaProperty.Register<ArcToolApprovalSheet, ArcApprovalSheetState>(nameof(State), ArcApprovalSheetState.Checkout);

    private readonly Dictionary<string, TextBox> _argumentBoxes = new(StringComparer.Ordinal);
    private TextBlock? _expiryText;
    private TextBox? _denyBox;
    private ArcButton? _approveButton;
    private DispatcherTimer? _timer;
    private double _remaining;
    private bool _denyOpen;
    private bool _isAttached;

    static ArcToolApprovalSheet()
    {
        ToolNameProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) => sheet.Rebuild());
        IntentProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) => sheet.Rebuild());
        RiskProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) => sheet.Rebuild());
        ArgumentsProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) => sheet.Rebuild());
        PreviewLinesProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) => sheet.Rebuild());
        ExpiresInSecondsProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) =>
        {
            sheet._remaining = sheet.ExpiresInSeconds;
            sheet.Rebuild();
            if (sheet._isAttached)
            {
                sheet.StartCountdown();
            }
        });
        ScopeProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) => sheet.Rebuild());
        StateProperty.Changed.AddClassHandler<ArcToolApprovalSheet>((sheet, _) =>
        {
            sheet._denyOpen = false;
            sheet.Rebuild();
            if (sheet._isAttached)
            {
                sheet.StartCountdown();
            }
        });
    }

    public ArcToolApprovalSheet()
    {
        ClipToBounds = false;
        _remaining = ExpiresInSeconds;
        Rebuild();
    }

    /// <summary>The tool the agent wants to call.</summary>
    public string ToolName
    {
        get => GetValue(ToolNameProperty);
        set => SetValue(ToolNameProperty, value);
    }

    /// <summary>The agent's intent in plain language.</summary>
    public string Intent
    {
        get => GetValue(IntentProperty);
        set => SetValue(IntentProperty, value);
    }

    /// <summary>Risk level used for the badge and the warning note.</summary>
    public ArcApprovalRisk Risk
    {
        get => GetValue(RiskProperty);
        set => SetValue(RiskProperty, value);
    }

    /// <summary>Arguments proposed by the agent.</summary>
    public IEnumerable<ArcApprovalArgument>? Arguments
    {
        get => GetValue(ArgumentsProperty);
        set => SetValue(ArgumentsProperty, value);
    }

    /// <summary>Lines describing the side effects of the call.</summary>
    public IEnumerable<string>? PreviewLines
    {
        get => GetValue(PreviewLinesProperty);
        set => SetValue(PreviewLinesProperty, value);
    }

    /// <summary>Seconds until the request expires.</summary>
    public double ExpiresInSeconds
    {
        get => GetValue(ExpiresInSecondsProperty);
        set => SetValue(ExpiresInSecondsProperty, value);
    }

    /// <summary>The scope selected in the sheet.</summary>
    public ArcApprovalScope Scope
    {
        get => GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }

    /// <summary>The visual stage of the sheet, set by the host.</summary>
    public ArcApprovalSheetState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    /// <summary>Raised with the arguments and scope the user approved.</summary>
    public event EventHandler<ArcApprovalDecision>? ApproveRequested;

    /// <summary>Raised with the reason the user typed when denying the request.</summary>
    public event EventHandler<string>? DenyRequested;

    /// <summary>Raised when the user hands the decision to somebody else.</summary>
    public event EventHandler? DelegateRequested;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        StartCountdown();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        StopCountdown();
    }

    private void Rebuild()
    {
        Children.Clear();
        _argumentBoxes.Clear();
        _expiryText = null;
        _approveButton = null;

        var body = new StackPanel { Spacing = 14 };
        body.Children.Add(BuildHeader());
        body.Children.Add(BuildIntent());

        var arguments = (Arguments ?? Array.Empty<ArcApprovalArgument>()).ToList();
        if (arguments.Count > 0)
        {
            body.Children.Add(ArcUi.Divider());
            body.Children.Add(BuildArguments(arguments));
        }

        var preview = (PreviewLines ?? Array.Empty<string>()).ToList();
        if (preview.Count > 0)
        {
            body.Children.Add(BuildPreview(preview));
        }

        body.Children.Add(BuildRiskNote());

        if (IsOpen)
        {
            body.Children.Add(BuildExpiryRow());
            body.Children.Add(BuildScopeRow());
            body.Children.Add(BuildFooter());

            if (_denyOpen)
            {
                body.Children.Add(BuildDenyRow());
            }
        }
        else
        {
            body.Children.Add(BuildResolvedNote());
        }

        var sheet = new Border
        {
            Background = ArcUi.Brush(ArcTokens.Surface),
            BorderBrush = ArcUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusSurface),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            Padding = new Thickness(22, 20, 22, 20),
            Child = body
        };

        Children.Add(sheet);

        ArcUi.Name(
            this,
            $"Tool approval for {ToolName}. {Intent}. Risk {Risk}. {StateWord(State)}.",
            "Approve, deny with a reason, edit the arguments, choose a scope or delegate the decision.");
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);

        if (_isAttached)
        {
            StartCountdown();
        }
    }

    private bool IsOpen => State is ArcApprovalSheetState.Checkout or ArcApprovalSheetState.Editing;

    private bool IsExpired => _remaining <= 0 && State is not (ArcApprovalSheetState.Decided or ArcApprovalSheetState.Cancelled);

    private Control BuildHeader()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };

        var disc = new Border
        {
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(13),
            Background = ArcUi.Brush(Risk switch
            {
                ArcApprovalRisk.High => ArcTokens.DangerSubtle,
                ArcApprovalRisk.Medium => ArcTokens.WarningSubtle,
                _ => ArcTokens.AccentSubtle
            }),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new ArcIcon
            {
                Icon = "shield-check",
                Size = 18,
                Foreground = ArcUi.Brush(RiskToken()),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        row.Children.Add(disc);

        var titles = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        titles.Children.Add(ArcUi.Strong("Approve a tool call"));
        titles.Children.Add(ArcUi.Mono(ToolName));
        Grid.SetColumn(titles, 1);
        row.Children.Add(titles);

        var badges = ArcUi.HStack(6);
        badges.Children.Add(ArcUi.Badge($"{Risk} risk", RiskTone()));
        badges.Children.Add(StateBadge(State));
        Grid.SetColumn(badges, 2);
        row.Children.Add(badges);
        return row;
    }

    private Control BuildIntent()
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(ArcUi.Caption("What the agent wants to do"));
        panel.Children.Add(ArcUi.Body(Intent, 680));
        return panel;
    }

    private Control BuildArguments(IReadOnlyList<ArcApprovalArgument> arguments)
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(ArcUi.Label("Arguments"));

        foreach (var argument in arguments)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), ColumnSpacing = 10 };
            var name = ArcUi.Mono(argument.Name);
            name.Margin = new Thickness(0, 6, 0, 0);
            row.Children.Add(name);

            Control value;
            if (argument.Editable && IsOpen && !IsExpired)
            {
                var box = new TextBox
                {
                    Text = argument.Value,
                    MinHeight = 34,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10, 4, 10, 4),
                    CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
                    BorderThickness = new Thickness(1),
                    FontSize = ArcResources.Double(ArcTokens.TextSm),
                    Background = ArcUi.Brush(ArcTokens.SurfaceRaised),
                    Foreground = ArcUi.Brush(ArcTokens.Foreground),
                    BorderBrush = ArcUi.Brush(ArcTokens.Border)
                };
                ArcUi.Name(box, $"{argument.Name} argument, editable");
                _argumentBoxes[argument.Name] = box;
                value = box;
            }
            else
            {
                var text = ArcUi.Text(argument.Value, "arc-body-sm");
                text.Margin = new Thickness(0, 6, 0, 0);
                AutomationProperties.SetName(text, $"{argument.Name}: {argument.Value}");
                value = text;
            }

            Grid.SetColumn(value, 1);
            row.Children.Add(value);
            panel.Children.Add(row);
        }

        if (IsOpen && !IsExpired)
        {
            panel.Children.Add(ArcUi.Caption("Editable values are approved exactly as written; the host applies them."));
        }

        return panel;
    }

    private Control BuildPreview(IReadOnlyList<string> preview)
    {
        var inner = new StackPanel { Spacing = 6 };
        inner.Children.Add(ArcUi.Label("What will change"));
        foreach (var line in preview)
        {
            var row = ArcUi.HStack(8);
            row.Children.Add(ArcUi.Icon("arrow-right", 13, ArcTokens.TextMuted));
            var text = ArcUi.Text(line, "arc-body-sm", 640);
            text.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(text);
            inner.Children.Add(row);
        }

        return new Border
        {
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(12, 10, 12, 10),
            Child = inner
        };
    }

    private Control BuildRiskNote()
    {
        var (background, token, copy) = Risk switch
        {
            ArcApprovalRisk.High => (
                ArcTokens.DangerSubtle,
                ArcTokens.Danger,
                "High risk: this action cannot be undone automatically. Check the recipient and the amount or date before approving."),
            ArcApprovalRisk.Medium => (
                ArcTokens.WarningSubtle,
                ArcTokens.Warning,
                "Medium risk: this changes data a person will notice. The run keeps a diff for review."),
            _ => (
                ArcTokens.SurfaceMuted,
                ArcTokens.TextSecondary,
                "Low risk: this request only reads data or makes a change that is easy to undo.")
        };

        var row = new Border
        {
            Background = ArcUi.Brush(background),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(12, 9, 12, 9)
        };

        var content = ArcUi.HStack(10);
        content.Children.Add(ArcUi.Icon(Risk == ArcApprovalRisk.Low ? "info" : "triangle-alert", 15, token));
        var text = ArcUi.Text(copy, "arc-body-sm", 640);
        text.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(text);
        row.Child = content;
        return row;
    }

    private Control BuildExpiryRow()
    {
        var row = ArcUi.HStack(6);
        row.Children.Add(ArcUi.Icon("clock", 13, IsExpired ? ArcTokens.Danger : ArcTokens.TextMuted));

        _expiryText = ArcUi.Text(ExpiryWord(), "arc-caption", wrap: TextWrapping.NoWrap);
        if (IsExpired)
        {
            _expiryText.Foreground = ArcUi.Brush(ArcTokens.Danger);
        }

        _expiryText.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(_expiryText);

        if (IsOpen && !IsExpired)
        {
            row.Children.Add(ArcUi.Caption("· the host enforces the deadline"));
        }

        return row;
    }

    private Control BuildScopeRow()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(ArcUi.Label("Allow for"));

        var options = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var scope in Enum.GetValues<ArcApprovalScope>())
        {
            var target = scope;
            var selected = Scope == scope;
            var button = new ArcButton
            {
                Content = ScopeLabel(scope),
                Variant = selected ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Margin = new Thickness(0, 0, 6, 6),
                IsEnabled = !IsExpired
            };
            ArcUi.Name(button, $"Allow for {ScopeLabel(scope)}");
            button.Click += (_, _) => SetCurrentValue(ScopeProperty, target);
            options.Children.Add(button);
        }

        panel.Children.Add(options);
        panel.Children.Add(ArcUi.Caption(ScopeHint()));
        return panel;
    }

    private Control BuildFooter()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 8 };

        var deny = ArcUi.Button("Deny", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "x");
        deny.IsEnabled = !IsExpired;
        ArcUi.Name(deny, "Deny the tool call with a reason");
        deny.Click += (_, _) =>
        {
            _denyOpen = true;
            Rebuild();
            if (_denyBox is not null)
            {
                var box = _denyBox;
                Dispatcher.UIThread.Post(() => box.Focus(), DispatcherPriority.Loaded);
            }
        };
        row.Children.Add(deny);

        var delegateButton = ArcUi.Button("Delegate", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "users");
        delegateButton.IsEnabled = !IsExpired;
        ArcUi.Name(delegateButton, "Ask somebody else to decide");
        delegateButton.Click += (_, _) => DelegateRequested?.Invoke(this, EventArgs.Empty);
        Grid.SetColumn(delegateButton, 2);
        row.Children.Add(delegateButton);

        _approveButton = ArcUi.Button(
            State == ArcApprovalSheetState.Editing ? "Approve edits" : "Approve",
            ArcButtonVariant.Primary,
            ArcButtonSize.Sm,
            "check");
        _approveButton.Loading = State == ArcApprovalSheetState.Submitting;
        _approveButton.IsEnabled = !IsExpired && State != ArcApprovalSheetState.Submitting;
        ArcUi.Name(_approveButton, "Approve the tool call with the shown arguments");
        _approveButton.Click += (_, _) => Approve();
        Grid.SetColumn(_approveButton, 3);
        row.Children.Add(_approveButton);

        return row;
    }

    private Control BuildDenyRow()
    {
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(ArcUi.Label("Reason for denying"));

        _denyBox = new TextBox
        {
            PlaceholderText = "Tell the agent why, so it can offer another way",
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(1),
            FontSize = ArcResources.Double(ArcTokens.TextSm),
            Background = ArcUi.Brush(ArcTokens.SurfaceRaised),
            Foreground = ArcUi.Brush(ArcTokens.Foreground),
            BorderBrush = ArcUi.Brush(ArcTokens.Border)
        };
        ArcUi.Name(_denyBox, "Reason for denying the tool call");
        panel.Children.Add(_denyBox);

        var actions = ArcUi.HStack(8);
        var confirm = ArcUi.Button("Deny request", ArcButtonVariant.Danger, ArcButtonSize.Sm);
        ArcUi.Name(confirm, "Send the denial with the typed reason");
        confirm.Click += (_, _) =>
        {
            var reason = (_denyBox?.Text ?? string.Empty).Trim();
            if (reason.Length == 0)
            {
                reason = "No reason given.";
            }

            DenyRequested?.Invoke(this, reason);
            _denyOpen = false;
            Rebuild();
        };
        actions.Children.Add(confirm);

        var cancel = ArcUi.Button("Cancel", ArcButtonVariant.Ghost, ArcButtonSize.Sm);
        ArcUi.Name(cancel, "Keep the approval open");
        cancel.Click += (_, _) =>
        {
            _denyOpen = false;
            Rebuild();
        };
        actions.Children.Add(cancel);
        panel.Children.Add(actions);
        return panel;
    }

    private Control BuildResolvedNote()
    {
        var (icon, token, copy) = State switch
        {
            ArcApprovalSheetState.Decided => ("check", ArcTokens.Success, "Decision recorded. The host applies the call and writes the audit entry."),
            ArcApprovalSheetState.Submitting => ("loader-circle", ArcTokens.Accent, "Sending the decision to the host."),
            ArcApprovalSheetState.Cancelled => ("ban", ArcTokens.TextMuted, "Request cancelled. Nothing was executed."),
            _ => ("clock", ArcTokens.Danger, "This request expired before a decision. The agent must ask again.")
        };

        var row = ArcUi.HStack(8);
        row.Children.Add(ArcUi.Icon(icon, 15, token));
        var text = ArcUi.Text(copy, "arc-body-sm", 640);
        text.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(text);
        return row;
    }

    private void Approve()
    {
        var arguments = (Arguments ?? Array.Empty<ArcApprovalArgument>())
            .Select(argument => new ArcApprovalArgument
            {
                Name = argument.Name,
                Value = _argumentBoxes.TryGetValue(argument.Name, out var box) ? box.Text ?? string.Empty : argument.Value,
                Editable = argument.Editable
            })
            .ToList();

        ApproveRequested?.Invoke(this, new ArcApprovalDecision
        {
            ModifiedArguments = arguments,
            Scope = Scope
        });
    }

    private void StartCountdown()
    {
        StopCountdown();
        if (_remaining <= 0 || State is ArcApprovalSheetState.Decided
                or ArcApprovalSheetState.Cancelled or ArcApprovalSheetState.Expired)
        {
            return;
        }

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _remaining = Math.Max(0, _remaining - 1);
            if (_expiryText is not null)
            {
                _expiryText.Text = ExpiryWord();
            }

            if (_remaining <= 0)
            {
                StopCountdown();
                if (_expiryText is not null)
                {
                    _expiryText.Foreground = ArcUi.Brush(ArcTokens.Danger);
                }

                if (_approveButton is not null)
                {
                    _approveButton.IsEnabled = false;
                }
            }
        };
        _timer.Start();
    }

    private void StopCountdown()
    {
        _timer?.Stop();
        _timer = null;
    }

    private string ExpiryWord()
    {
        if (IsExpired)
        {
            return "Expired: the agent must ask again";
        }

        var whole = (int)Math.Ceiling(_remaining);
        var minutes = whole / 60;
        var seconds = whole % 60;
        return string.Format(CultureInfo.InvariantCulture, "Expires in {0}:{1:00}", minutes, seconds);
    }

    private string RiskToken() => Risk switch
    {
        ArcApprovalRisk.High => ArcTokens.Danger,
        ArcApprovalRisk.Medium => ArcTokens.Warning,
        _ => ArcTokens.Accent
    };

    private ArcBadgeTone RiskTone() => Risk switch
    {
        ArcApprovalRisk.High => ArcBadgeTone.Danger,
        ArcApprovalRisk.Medium => ArcBadgeTone.Warning,
        _ => ArcBadgeTone.Success
    };

    private string ScopeHint() => Scope switch
    {
        ArcApprovalScope.Once => "Applies to this call only.",
        ArcApprovalScope.Task => "Applies to this tool for the rest of the task.",
        ArcApprovalScope.Hour => "Applies to this tool for one hour, then the agent asks again.",
        _ => "Remembered until the host removes the trust rule; the agent will not ask again for this tool."
    };

    private static string ScopeLabel(ArcApprovalScope scope) => scope switch
    {
        ArcApprovalScope.Once => "Once",
        ArcApprovalScope.Task => "This task",
        ArcApprovalScope.Hour => "1 hour",
        _ => "Always"
    };

    private static ArcBadge StateBadge(ArcApprovalSheetState state) => state switch
    {
        ArcApprovalSheetState.Checkout => ArcUi.Badge("Needs decision", ArcBadgeTone.Accent),
        ArcApprovalSheetState.Editing => ArcUi.Badge("Editing", ArcBadgeTone.Accent),
        ArcApprovalSheetState.Submitting => ArcUi.Badge("Submitting", ArcBadgeTone.Accent),
        ArcApprovalSheetState.Decided => ArcUi.Badge("Decided", ArcBadgeTone.Success),
        ArcApprovalSheetState.Expired => ArcUi.Badge("Expired", ArcBadgeTone.Danger),
        _ => ArcUi.Badge("Cancelled", ArcBadgeTone.Neutral)
    };

    private static string StateWord(ArcApprovalSheetState state) => state switch
    {
        ArcApprovalSheetState.Checkout => "waiting for a decision",
        ArcApprovalSheetState.Editing => "editing arguments",
        ArcApprovalSheetState.Submitting => "submitting the decision",
        ArcApprovalSheetState.Decided => "decided",
        ArcApprovalSheetState.Expired => "expired",
        _ => "cancelled"
    };

    private static IEnumerable<ArcApprovalArgument> SampleArguments() => new[]
    {
        new ArcApprovalArgument { Name = "title", Value = "Dentist", Editable = true },
        new ArcApprovalArgument { Name = "start", Value = "Thu 15:00 (Europe/Berlin)", Editable = true },
        new ArcApprovalArgument { Name = "duration", Value = "45 minutes", Editable = true },
        new ArcApprovalArgument { Name = "attendee", Value = "sam@northwind.example", Editable = false }
    };
}
