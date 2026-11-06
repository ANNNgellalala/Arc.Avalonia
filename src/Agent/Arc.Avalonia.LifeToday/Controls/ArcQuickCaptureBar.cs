using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.LifeToday.Controls;

/// <summary>What the host found inside the captured sentence.</summary>
public enum ArcQuickCaptureState
{
    /// <summary>Nothing typed yet, or the draft is being edited after a result.</summary>
    Idle,

    /// <summary>The host is parsing; the bar shows a spinner and holds confirmation.</summary>
    Parsing,

    /// <summary>A usable result came back and its chips are shown.</summary>
    Parsed,

    /// <summary>The host found more than one reading; the visitor resolves it before saving.</summary>
    Ambiguous,

    /// <summary>The host could not parse the sentence.</summary>
    Error
}

/// <summary>The type of detail a chip represents, which selects its icon and wording.</summary>
public enum ArcQuickCaptureChipKind
{
    Time,
    Person,
    Amount,
    List,
    Note
}

/// <summary>One parsed detail shown as an editable chip below the capture field.</summary>
public sealed class ArcQuickCaptureChip
{
    public ArcQuickCaptureChip()
    {
    }

    public ArcQuickCaptureChip(string id, ArcQuickCaptureChipKind kind, string label, string? icon = null)
    {
        Id = id;
        Kind = kind;
        Label = label;
        Icon = icon;
    }

    /// <summary>Stable id the host uses to map the chip back to its parsed field.</summary>
    public string Id { get; set; } = string.Empty;

    public ArcQuickCaptureChipKind Kind { get; set; }

    /// <summary>Short text on the chip, such as "Fri 7:00 pm" or "\u00a5280".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional Lucide icon name; the kind supplies the default.</summary>
    public string? Icon { get; set; }
}

/// <summary>
/// Arc's quick capture bar: the three second path from "I thought of something" to "it is in the system".
/// It is a pure UI surface. The host opens it (a global hotkey belongs to the host), feeds the draft text,
/// returns a parse state with chips, and persists on <see cref="SubmitRequested"/>; the control never parses,
/// never saves and never opens itself.
/// <para>
/// The field is the editing surface, the chips are the parsed result (each removes itself with
/// <see cref="ChipRemoveRequested"/>), and the status line explains idle, parsing, parsed, ambiguous and error.
/// Enter submits, Escape cancels. State moves between Idle, Parsing, Parsed, Ambiguous and Error.
/// </para>
/// </summary>
public class ArcQuickCaptureBar : Panel
{
    /// <summary>Draft text. Two-way in practice: typing raises the change, the host may replace it.</summary>
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ArcQuickCaptureBar, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcQuickCaptureBar, string>(
            nameof(Placeholder), "Capture anything: dinner with Priya on Friday at 7 pm, \u00a5280 for two");

    /// <summary>Parse state supplied by the host. The control only displays it.</summary>
    public static readonly StyledProperty<ArcQuickCaptureState> StateProperty =
        AvaloniaProperty.Register<ArcQuickCaptureBar, ArcQuickCaptureState>(nameof(State));

    /// <summary>Parsed details shown as chips. Empty for an idle or failed capture.</summary>
    public static readonly StyledProperty<IList<ArcQuickCaptureChip>?> ChipsProperty =
        AvaloniaProperty.Register<ArcQuickCaptureBar, IList<ArcQuickCaptureChip>?>(nameof(Chips));

    /// <summary>The keyboard reminder beside the footer actions.</summary>
    public static readonly StyledProperty<string> HintProperty =
        AvaloniaProperty.Register<ArcQuickCaptureBar, string>(nameof(Hint), "Enter to capture \u00b7 Esc to cancel");

    private readonly TextBox _input;
    private readonly Border _badgeHost;
    private readonly WrapPanel _chipsHost;
    private readonly StackPanel _statusRow;
    private readonly TextBlock _hintText;
    private readonly ArcButton _confirm;
    private readonly ArcButton _cancel;
    private bool _syncing;

    static ArcQuickCaptureBar()
    {
        TextProperty.Changed.AddClassHandler<ArcQuickCaptureBar>((bar, _) => bar.SyncInput());
        PlaceholderProperty.Changed.AddClassHandler<ArcQuickCaptureBar>((bar, _) => bar.SyncPlaceholder());
        StateProperty.Changed.AddClassHandler<ArcQuickCaptureBar>((bar, _) => bar.RefreshState());
        ChipsProperty.Changed.AddClassHandler<ArcQuickCaptureBar>((bar, _) => bar.RefreshChips());
        HintProperty.Changed.AddClassHandler<ArcQuickCaptureBar>((bar, _) => bar.RefreshHint());
    }

    public ArcQuickCaptureBar()
    {
        Width = 720;
        HorizontalAlignment = HorizontalAlignment.Left;
        _input = new TextBox
        {
            PlaceholderText = Placeholder,
            MinHeight = 36,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        ArcUi.Name(_input, "Capture text", "Describe the thing to capture, then press Enter.");
        _input.TextChanged += OnInputTextChanged;
        _input.KeyDown += OnInputKeyDown;

        _badgeHost = new Border { VerticalAlignment = VerticalAlignment.Center };

        _chipsHost = new WrapPanel { Orientation = Orientation.Horizontal };

        _statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        _hintText = ArcUi.Caption(Hint);

        _confirm = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = ArcUi.HStack(6, ArcUi.Icon("corner-down-left", 14), ArcUi.Text("Capture", "arc-body-sm"))
        };
        ArcUi.Name(_confirm, "Capture", "Save the parsed details. The host persists them.");
        _confirm.Click += (_, _) => RequestSubmit();

        _cancel = ArcUi.Button("Cancel", ArcButtonVariant.Ghost, ArcButtonSize.Sm, "x");
        ArcUi.Name(_cancel, "Cancel capture", "Close the bar and discard the draft.");
        _cancel.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);

        Build();
        ArcUi.Name(
            this,
            "Quick capture bar",
            "Type a sentence, review the parsed chips, then press Enter or Capture. Escape cancels.");

        SyncInput();
        SyncPlaceholder();
        RefreshChips();
        RefreshHint();
        RefreshState();
    }

    /// <summary>Raised with the draft text when the visitor confirms; the host parses and persists.</summary>
    public static readonly RoutedEvent<ArcLifeTodayRoutedEventArgs> SubmitRequestedEvent =
        RoutedEvent.Register<ArcQuickCaptureBar, ArcLifeTodayRoutedEventArgs>("SubmitRequested", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="SubmitRequested"/>.</summary>
    public event EventHandler<ArcLifeTodayRoutedEventArgs>? SubmitRequestedRouted
    {
        add => AddHandler(SubmitRequestedEvent, value);
        remove => RemoveHandler(SubmitRequestedEvent, value);
    }

    public event Action<string>? SubmitRequested;

    /// <summary>Raised with the chip id when the visitor removes a parsed detail.</summary>
    public event Action<string>? ChipRemoveRequested;

    /// <summary>Raised when the bar should close. The draft stays with the host.</summary>
    public event EventHandler? CancelRequested;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public ArcQuickCaptureState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public IList<ArcQuickCaptureChip>? Chips
    {
        get => GetValue(ChipsProperty);
        set => SetValue(ChipsProperty, value);
    }

    public string Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }

    /// <summary>Puts keyboard focus in the capture field without opening the popup host.</summary>
    public void FocusInput() => _input.Focus();

    private void Build()
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10 };
        var icon = ArcUi.Icon("sparkles", 18, ArcTokens.AccentStrong);
        icon.Margin = new Thickness(2, 0, 2, 0);
        top.Children.Add(icon);
        Grid.SetColumn(_input, 1);
        top.Children.Add(_input);
        Grid.SetColumn(_badgeHost, 2);
        top.Children.Add(_badgeHost);

        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        footer.Children.Add(_hintText);
        Grid.SetColumn(_cancel, 1);
        _cancel.Margin = new Thickness(0, 0, 8, 0);
        footer.Children.Add(_cancel);
        Grid.SetColumn(_confirm, 2);
        footer.Children.Add(_confirm);

        var stack = ArcUi.Stack(12, top, _chipsHost, _statusRow, ArcUi.Divider(), footer);
        Children.Add(ArcUi.Surface(stack, padding: 16));
    }

    private void SyncInput()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            var text = Text ?? string.Empty;
            if (!string.Equals(_input.Text, text, StringComparison.Ordinal))
            {
                _input.Text = text;
            }
        }
        finally
        {
            _syncing = false;
        }

        UpdateConfirm();
    }

    private void SyncPlaceholder() => _input.PlaceholderText = Placeholder;

    private void RefreshHint() => _hintText.Text = Hint;

    private void OnInputTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        var incoming = _input.Text ?? string.Empty;
        if (string.Equals(incoming, Text ?? string.Empty, StringComparison.Ordinal))
        {
            // Host-driven text changes can surface here after SyncInput has released the guard; they are not
            // visitor edits and must not reset the host's parse state.
            return;
        }

        SetCurrentValue(TextProperty, incoming);
        if (State is ArcQuickCaptureState.Parsed or ArcQuickCaptureState.Ambiguous or ArcQuickCaptureState.Error)
        {
            SetCurrentValue(StateProperty, ArcQuickCaptureState.Idle);
        }

        UpdateConfirm();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                e.Handled = true;
                RequestSubmit();
                break;
            case Key.Escape:
                e.Handled = true;
                CancelRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private void RequestSubmit()
    {
        if (State == ArcQuickCaptureState.Parsing)
        {
            return;
        }

        var text = Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return;
        }

        SubmitRequested?.Invoke(text);
            RaiseEvent(new ArcLifeTodayRoutedEventArgs(SubmitRequestedEvent) { Text = text });
    }

    private void UpdateConfirm() =>
        _confirm.IsEnabled = State != ArcQuickCaptureState.Parsing && !string.IsNullOrWhiteSpace(Text);

    private void RefreshChips()
    {
        _chipsHost.Children.Clear();
        var chips = Chips;
        if (chips is not null)
        {
            foreach (var chip in chips)
            {
                _chipsHost.Children.Add(BuildChip(chip));
            }
        }

        _chipsHost.IsVisible = ShouldShowChips;
        UpdateConfirm();
    }

    private bool ShouldShowChips =>
        State is ArcQuickCaptureState.Parsed or ArcQuickCaptureState.Ambiguous &&
        _chipsHost.Children.Count > 0;

    private Control BuildChip(ArcQuickCaptureChip chip)
    {
        var content = ArcUi.HStack(
            6,
            ArcUi.Icon(chip.Icon ?? DefaultIcon(chip.Kind), 13, ArcTokens.AccentStrong),
            ArcUi.Text(chip.Label, "arc-body-sm", wrap: TextWrapping.NoWrap));

        var remove = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Content = ArcUi.Icon("x", 12, ArcTokens.TextMuted),
            Width = 24,
            Height = 24,
            MinHeight = 24,
            MinWidth = 24
        };
        ArcUi.Name(remove, $"Remove {chip.Label}", "Removes this parsed detail from the capture.");
        remove.Click += (_, _) => ChipRemoveRequested?.Invoke(chip.Id);

        return new Border
        {
            Background = ArcUi.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = ArcResources.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(10, 4, 4, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Child = ArcUi.HStack(4, content, remove)
        };
    }

    private void RefreshState()
    {
        _badgeHost.Child = ArcUi.Badge(StateWord(State), StateTone(State));
        _chipsHost.IsVisible = ShouldShowChips;
        _statusRow.Children.Clear();

        switch (State)
        {
            case ArcQuickCaptureState.Parsing:
                _statusRow.Children.Add(new ArcSpinner
                {
                    Size = 14,
                    StrokeWidth = 1.75,
                    Stroke = ArcUi.Brush(ArcTokens.TextSecondary),
                    VerticalAlignment = VerticalAlignment.Center
                });
                _statusRow.Children.Add(ArcUi.Caption("Parsing your capture\u2026"));
                break;
            case ArcQuickCaptureState.Parsed:
                _statusRow.Children.Add(ArcUi.Icon("circle-check", 14, ArcTokens.Success));
                _statusRow.Children.Add(ArcUi.Caption(
                    $"Parsed into {Chips?.Count ?? 0} details. Confirm, or remove a chip the host got wrong."));
                break;
            case ArcQuickCaptureState.Ambiguous:
                _statusRow.Children.Add(ArcUi.Icon("circle-help", 14, ArcTokens.Warning));
                _statusRow.Children.Add(ArcUi.Caption(
                    "Two readings match this sentence. Pick a chip to refine, or capture as typed."));
                break;
            case ArcQuickCaptureState.Error:
                var failure = ArcUi.Caption("The host could not parse this capture. Edit the text and try again.");
                failure.Foreground = ArcUi.Brush(ArcTokens.Danger);
                _statusRow.Children.Add(ArcUi.Icon("circle-alert", 14, ArcTokens.Danger));
                _statusRow.Children.Add(failure);
                break;
            default:
                _statusRow.Children.Add(ArcUi.Icon("info", 14, ArcTokens.TextMuted));
                _statusRow.Children.Add(ArcUi.Caption("Ready. The host parses after you submit."));
                break;
        }

        _statusRow.IsVisible = true;
        AutomationProperties.SetHelpText(this, State switch
        {
            ArcQuickCaptureState.Parsing => "Parsing the capture.",
            ArcQuickCaptureState.Parsed => "Capture parsed. Review the chips and confirm.",
            ArcQuickCaptureState.Ambiguous => "The capture is ambiguous. Resolve it before saving.",
            ArcQuickCaptureState.Error => "The capture could not be parsed.",
            _ => "Type a capture. Enter submits, Escape cancels."
        });
        UpdateConfirm();
    }

    private static string DefaultIcon(ArcQuickCaptureChipKind kind) => kind switch
    {
        ArcQuickCaptureChipKind.Time => "clock",
        ArcQuickCaptureChipKind.Person => "user",
        ArcQuickCaptureChipKind.Amount => "credit-card",
        ArcQuickCaptureChipKind.List => "list",
        _ => "pencil"
    };

    private static string StateWord(ArcQuickCaptureState state) => state switch
    {
        ArcQuickCaptureState.Parsing => "Parsing",
        ArcQuickCaptureState.Parsed => "Parsed",
        ArcQuickCaptureState.Ambiguous => "Ambiguous",
        ArcQuickCaptureState.Error => "Error",
        _ => "Idle"
    };

    private static ArcBadgeTone StateTone(ArcQuickCaptureState state) => state switch
    {
        ArcQuickCaptureState.Parsing => ArcBadgeTone.Accent,
        ArcQuickCaptureState.Parsed => ArcBadgeTone.Success,
        ArcQuickCaptureState.Ambiguous => ArcBadgeTone.Warning,
        ArcQuickCaptureState.Error => ArcBadgeTone.Danger,
        _ => ArcBadgeTone.Neutral
    };
}


