using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

public enum ArcShortcutPlatform
{
    Other,
    Mac
}

/// <summary>A shortcut already in use, offered as a conflict when the recorder picks the same chord.</summary>
public sealed class ArcShortcutBinding
{
    public ArcShortcutBinding(string gesture, string label)
    {
        Gesture = gesture;
        Label = label;
    }

    public string Gesture { get; }

    public string Label { get; }
}

public sealed class ArcShortcutChangedEventArgs : RoutedEventArgs
{
    public ArcShortcutChangedEventArgs(string? gesture, ArcShortcutBinding? replaced)
    {
        Gesture = gesture;
        Replaced = replaced;
    }

    /// <summary>Routed payload factory used by <c>GestureChangedRouted</c>.</summary>
    public ArcShortcutChangedEventArgs(RoutedEvent routedEvent, string? gesture, ArcShortcutBinding? replaced)
        : base(routedEvent)
    {
        Gesture = gesture;
        Replaced = replaced;
    }

    public string? Gesture { get; }

    /// <summary>Set after "Use anyway", the binding the chord was taken from.</summary>
    public ArcShortcutBinding? Replaced { get; }
}

/// <summary>
/// Arc's shortcut recorder: a focusable field that records a key chord into key caps, warns about
/// conflicts and reserved combinations, and exposes the gesture as "Ctrl+Shift+K".
/// </summary>
[TemplatePart("PART_LabelText", typeof(TextBlock))]
[TemplatePart("PART_Shell", typeof(Border))]
[TemplatePart("PART_Caps", typeof(StackPanel))]
[TemplatePart("PART_PlaceholderText", typeof(TextBlock))]
[TemplatePart("PART_ClearButton", typeof(Button))]
[TemplatePart("PART_RecordingDot", typeof(Border))]
[TemplatePart("PART_Message", typeof(TextBlock))]
[TemplatePart("PART_Hint", typeof(TextBlock))]
[TemplatePart("PART_UseButton", typeof(Button))]
[TemplatePart("PART_CancelButton", typeof(Button))]
public class ArcShortcutRecorder : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, string?>(nameof(Label));

    public static readonly StyledProperty<bool> HideLabelProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, bool>(nameof(HideLabel));

    public static readonly StyledProperty<string?> GestureProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, string?>(nameof(Gesture));

    public static readonly StyledProperty<string?> ResetValueProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, string?>(nameof(ResetValue));

    public static readonly StyledProperty<IList<ArcShortcutBinding>?> BindingsProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, IList<ArcShortcutBinding>?>(nameof(Bindings));

    public static readonly StyledProperty<bool> WarnReservedProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, bool>(nameof(WarnReserved), true);

    public static readonly StyledProperty<bool> RequireModifierProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, bool>(nameof(RequireModifier), true);

    public static readonly StyledProperty<ArcShortcutPlatform> PlatformProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, ArcShortcutPlatform>(nameof(Platform));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, string?>(nameof(Placeholder), "Record shortcut");

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcShortcutRecorder, string?>(nameof(Description));

    private TextBlock? _labelText;
    private Border? _shell;
    private StackPanel? _caps;
    private TextBlock? _placeholderText;
    private Button? _clearButton;
    private Border? _recordingDot;
    private TextBlock? _message;
    private TextBlock? _hint;
    private Button? _useButton;
    private Button? _cancelButton;

    private bool _recording;
    private PendingGesture? _pending;
    private string? _conflictGesture;
    private ArcShortcutBinding? _conflictBinding;

    static ArcShortcutRecorder()
    {
        GestureProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateDisplay());
        LabelProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateDisplay());
        HideLabelProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateDisplay());
        PlaceholderProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateDisplay());
        DescriptionProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateMessage());
        BindingsProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateDisplay());
        PlatformProperty.Changed.AddClassHandler<ArcShortcutRecorder>((recorder, _) => recorder.UpdateDisplay());
    }

    public ArcShortcutRecorder()
    {
        Focusable = true;
    }

    /// <summary>Raised when the recorded gesture changes; null means cleared.</summary>
    public event EventHandler<ArcShortcutChangedEventArgs>? GestureChanged;

    public static readonly RoutedEvent<ArcShortcutChangedEventArgs> GestureChangedEvent =
        RoutedEvent.Register<ArcShortcutRecorder, ArcShortcutChangedEventArgs>("GestureChanged", RoutingStrategies.Bubble);

    /// <summary>Bubbling form of <see cref="GestureChanged"/>; the CLR event above keeps firing as before.</summary>
    public event EventHandler<ArcShortcutChangedEventArgs>? GestureChangedRouted
    {
        add => AddHandler(GestureChangedEvent, value);
        remove => RemoveHandler(GestureChangedEvent, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public bool HideLabel
    {
        get => GetValue(HideLabelProperty);
        set => SetValue(HideLabelProperty, value);
    }

    /// <summary>Normalized chord, such as "Ctrl+Shift+K"; null when nothing is recorded.</summary>
    public string? Gesture
    {
        get => GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    /// <summary>What the reset button restores; unused when null.</summary>
    public string? ResetValue
    {
        get => GetValue(ResetValueProperty);
        set => SetValue(ResetValueProperty, value);
    }

    /// <summary>Shortcuts already in use; recording one asks before taking it.</summary>
    public IList<ArcShortcutBinding>? Bindings
    {
        get => GetValue(BindingsProperty);
        set => SetValue(BindingsProperty, value);
    }

    /// <summary>Also warn about combinations the system keeps, such as Ctrl+W and Ctrl+C.</summary>
    public bool WarnReserved
    {
        get => GetValue(WarnReservedProperty);
        set => SetValue(WarnReservedProperty, value);
    }

    /// <summary>Require Ctrl, Alt or the Windows key; function keys are always allowed.</summary>
    public bool RequireModifier
    {
        get => GetValue(RequireModifierProperty);
        set => SetValue(RequireModifierProperty, value);
    }

    public ArcShortcutPlatform Platform
    {
        get => GetValue(PlatformProperty);
        set => SetValue(PlatformProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsRecording => _recording;

    /// <summary>Starts recording, as Enter, Space or a click do.</summary>
    public void BeginRecording()
    {
        if (_recording || !IsEnabled)
        {
            return;
        }

        _recording = true;
        _pending = null;
        _conflictGesture = null;
        _conflictBinding = null;
        UpdateDisplay();
        Focus();
    }

    /// <summary>Cancels recording without changing the gesture.</summary>
    public void CancelRecording()
    {
        if (!_recording)
        {
            return;
        }

        _recording = false;
        _pending = null;
        UpdateDisplay();
    }

    /// <summary>Clears the recorded gesture.</summary>
    public void Clear()
    {
        if (string.IsNullOrEmpty(Gesture))
        {
            return;
        }

        SetCurrentValue(GestureProperty, null);
        UpdateDisplay();
        RaiseGestureChanged(null, null);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_clearButton is not null)
        {
            _clearButton.Click -= OnClearClicked;
        }

        if (_useButton is not null)
        {
            _useButton.Click -= OnUseAnyway;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Click -= OnCancelConflict;
        }

        _labelText = e.NameScope.Find<TextBlock>("PART_LabelText");
        _shell = e.NameScope.Find<Border>("PART_Shell");
        _caps = e.NameScope.Find<StackPanel>("PART_Caps");
        _placeholderText = e.NameScope.Find<TextBlock>("PART_PlaceholderText");
        _clearButton = e.NameScope.Find<Button>("PART_ClearButton");
        _recordingDot = e.NameScope.Find<Border>("PART_RecordingDot");
        _message = e.NameScope.Find<TextBlock>("PART_Message");
        _hint = e.NameScope.Find<TextBlock>("PART_Hint");
        _useButton = e.NameScope.Find<Button>("PART_UseButton");
        _cancelButton = e.NameScope.Find<Button>("PART_CancelButton");

        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearClicked;
        }

        if (_useButton is not null)
        {
            _useButton.Click += OnUseAnyway;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Click += OnCancelConflict;
        }

        UpdateDisplay();
        UpdateMessage();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!IsEnabled)
        {
            return;
        }

        if (e.Source is Visual source && IsWithinButton(source))
        {
            return;
        }

        e.Handled = true;
        Focus();
        BeginRecording();
    }

    private static bool IsWithinButton(Visual source)
    {
        for (var current = source; current is not null; current = current.GetVisualParent())
        {
            if (current is Button)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled)
        {
            return;
        }

        if (_recording)
        {
            HandleRecordingKey(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
            case Key.Space:
                e.Handled = true;
                BeginRecording();
                break;
            case Key.Back:
            case Key.Delete:
                e.Handled = true;
                Clear();
                break;
        }
    }

    private void HandleRecordingKey(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelRecording();
            return;
        }

        if (e.Key == Key.Tab)
        {
            // Tab stops recording and moves focus on without recording itself.
            _recording = false;
            _pending = null;
            UpdateDisplay();
            return;
        }

        if (e.Key is Key.Back or Key.Delete)
        {
            e.Handled = true;
            _recording = false;
            _pending = null;
            Clear();
            return;
        }

        var key = e.Key;
        var modifiers = e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta);

        if (IsModifierKey(key))
        {
            _pending = new PendingGesture(modifiers, null);
            UpdateDisplay();
            e.Handled = true;
            return;
        }

        var gesture = Normalize(key, modifiers);
        if (gesture is null)
        {
            e.Handled = true;
            return;
        }

        e.Handled = true;

        if (RequireModifier && !HasModifier(modifiers) && !IsFunctionKey(key))
        {
            ShowHint("Add Ctrl, Alt or Shift to make this a shortcut.");
            return;
        }

        var conflict = FindConflict(gesture);
        if (conflict is not null)
        {
            _recording = false;
            _pending = null;
            _conflictGesture = gesture;
            _conflictBinding = conflict;
            UpdateDisplay();
            UpdateMessage();
            _useButton?.Focus();
            return;
        }

        CommitGesture(gesture);
    }

    private void CommitGesture(string gesture, ArcShortcutBinding? replaced = null)
    {
        _recording = false;
        _pending = null;
        _conflictGesture = null;
        _conflictBinding = null;
        SetCurrentValue(GestureProperty, gesture);
        UpdateDisplay();
        UpdateMessage();
        RaiseGestureChanged(gesture, replaced);
        Focus();
    }

    private void RaiseGestureChanged(string? gesture, ArcShortcutBinding? replaced)
    {
        GestureChanged?.Invoke(this, new ArcShortcutChangedEventArgs(gesture, replaced));
        RaiseEvent(new ArcShortcutChangedEventArgs(GestureChangedEvent, gesture, replaced));
    }

    private ArcShortcutBinding? FindConflict(string gesture)
    {
        if (Bindings is { } bindings)
        {
            foreach (var binding in bindings)
            {
                if (string.Equals(binding.Gesture, gesture, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(binding.Gesture, Gesture, StringComparison.OrdinalIgnoreCase))
                {
                    return binding;
                }
            }
        }

        if (WarnReserved && Reserved.Contains(gesture))
        {
            return new ArcShortcutBinding(gesture, "a browser or system shortcut");
        }

        return null;
    }

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "Ctrl+W",
        "Ctrl+T",
        "Ctrl+N",
        "Ctrl+R",
        "Ctrl+L",
        "Ctrl+C",
        "Ctrl+V",
        "Ctrl+X",
        "Ctrl+Z",
        "Ctrl+Shift+W",
        "Alt+F4"
    };

    private static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;

    private static bool HasModifier(KeyModifiers modifiers) =>
        modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Alt) ||
        modifiers.HasFlag(KeyModifiers.Shift) || modifiers.HasFlag(KeyModifiers.Meta);

    private static bool IsFunctionKey(Key key) => key >= Key.F1 && key <= Key.F24;

    /// <summary>Builds the normalized chord "Ctrl+Alt+Shift+Meta+Key" for a key press.</summary>
    public static string? Normalize(Key key, KeyModifiers modifiers)
    {
        if (key is Key.None or Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            return null;
        }

        var parts = new List<string>(5);
        if (modifiers.HasFlag(KeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(KeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(KeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(KeyModifiers.Meta))
        {
            parts.Add("Win");
        }

        parts.Add(ArcKeys.KeyName(key));
        return string.Join("+", parts);
    }

    /// <summary>True when a key event is this chord, comparing the normalized forms.</summary>
    public static bool Matches(string? gesture, Key key, KeyModifiers modifiers)
    {
        if (string.IsNullOrEmpty(gesture))
        {
            return false;
        }

        var normalized = Normalize(key, modifiers);
        return normalized is not null && string.Equals(normalized, gesture, StringComparison.OrdinalIgnoreCase);
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e) => Clear();

    private void OnUseAnyway(object? sender, RoutedEventArgs e)
    {
        if (_conflictGesture is null)
        {
            return;
        }

        var replaced = _conflictBinding;
        CommitGesture(_conflictGesture, replaced);
    }

    private void OnCancelConflict(object? sender, RoutedEventArgs e)
    {
        _conflictGesture = null;
        _conflictBinding = null;
        UpdateDisplay();
        UpdateMessage();
        Focus();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
        else if (change.Property == IsFocusedProperty)
        {
            Classes.Set("focus", IsFocused);
        }
    }

    private void UpdateDisplay()
    {
        if (_labelText is not null)
        {
            _labelText.Text = Label;
            _labelText.IsVisible = !HideLabel && !string.IsNullOrEmpty(Label);
        }

        if (_caps is not null)
        {
            _caps.Children.Clear();
            var parts = RecordingParts();
            var hasCaps = parts.Count > 0;
            _caps.IsVisible = hasCaps;

            for (var i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    _caps.Children.Add(new TextBlock
                    {
                        Text = "+",
                        Margin = new Thickness(1, 0),
                        FontSize = 12,
                        Foreground = ArcResources.Brush(ArcTokens.TextMuted),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }

                _caps.Children.Add(new ContentControl
                {
                    Theme = ThemeOf("ArcKbdCap"),
                    Content = parts[i],
                    VerticalAlignment = VerticalAlignment.Center
                });
            }
        }

        if (_placeholderText is not null)
        {
            _placeholderText.IsVisible = _caps is null || !_caps.IsVisible;
            _placeholderText.Text = _recording ? "Listening…" : Placeholder;
        }

        if (_clearButton is not null)
        {
            _clearButton.IsVisible = !_recording && !string.IsNullOrEmpty(Gesture) && _conflictGesture is null;
        }

        if (_recordingDot is not null)
        {
            _recordingDot.IsVisible = _recording;
        }

        Classes.Set("recording", _recording);
        Classes.Set("conflict", _conflictGesture is not null);
        UpdateMessage();
        UpdateAutomation();
    }

    private List<string> RecordingParts()
    {
        if (_conflictGesture is not null)
        {
            return _conflictGesture.Split('+').ToList();
        }

        if (_recording)
        {
            var parts = new List<string>();
            if (_pending is { } pending)
            {
                if (pending.Modifiers.HasFlag(KeyModifiers.Control))
                {
                    parts.Add("Ctrl");
                }

                if (pending.Modifiers.HasFlag(KeyModifiers.Alt))
                {
                    parts.Add("Alt");
                }

                if (pending.Modifiers.HasFlag(KeyModifiers.Shift))
                {
                    parts.Add("Shift");
                }

                if (pending.Modifiers.HasFlag(KeyModifiers.Meta))
                {
                    parts.Add("Win");
                }

                if (pending.Key is { } key)
                {
                    parts.Add(ArcKeys.KeyName(key));
                }
            }

            return parts;
        }

        return string.IsNullOrEmpty(Gesture) ? new List<string>() : Gesture!.Split('+').ToList();
    }

    private static ControlTheme? ThemeOf(string key) =>
        Application.Current is { } app && app.TryFindResource(key, out var value) ? value as ControlTheme : null;

    private void ShowHint(string text)
    {
        if (_hint is null)
        {
            return;
        }

        _hint.Text = text;
        _hint.IsVisible = true;
        ArcAnim.Delay(TimeSpan.FromMilliseconds(1600), () =>
        {
            if (_hint is not null)
            {
                _hint.IsVisible = false;
            }
        });
    }

    private void UpdateMessage()
    {
        if (_message is null)
        {
            return;
        }

        var conflict = _conflictGesture is not null;
        _message.IsVisible = conflict;
        _message.Text = conflict
            ? $"Already used by {_conflictBinding?.Label}."
            : null;

        if (_hint is not null)
        {
            _hint.IsVisible = _hint.IsVisible && !conflict;
        }

        if (_useButton is not null)
        {
            _useButton.IsVisible = conflict;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.IsVisible = conflict;
        }

        if (!conflict && _hint is not null && !_recording)
        {
            _hint.Text = Description;
            _hint.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        UpdateAutomation();
    }

    private void UpdateAutomation()
    {
        var label = Label ?? "Shortcut";
        var spoken = SpokenGesture(Gesture);
        AutomationProperties.SetName(this, string.IsNullOrEmpty(spoken) ? label : $"{label}: {spoken}");
        AutomationProperties.SetHelpText(
            this,
            _recording ? "Recording. Press a key combination, Escape to cancel." : "Press Enter to record a shortcut.");
    }

    private string SpokenGesture(string? gesture)
    {
        if (string.IsNullOrEmpty(gesture))
        {
            return string.Empty;
        }

        return string.Join(" ", gesture.Split('+').Select(part => Platform == ArcShortcutPlatform.Mac
            ? part switch
            {
                "Ctrl" => "Control",
                "Win" => "Command",
                "Alt" => "Option",
                _ => part
            }
            : part switch
            {
                "Ctrl" => "Control",
                "Win" => "Windows",
                _ => part
            }));
    }

    private readonly record struct PendingGesture(KeyModifiers Modifiers, Key? Key);
}
