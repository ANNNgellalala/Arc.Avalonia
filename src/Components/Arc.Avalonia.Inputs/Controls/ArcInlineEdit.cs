using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

public enum ArcInlineEditVariant
{
    /// <summary>Names and headings: display face at heading size.</summary>
    Title,

    /// <summary>Descriptions and notes: body face at 14px.</summary>
    Body
}

/// <summary>Carries one save attempt so a handler can refuse it or keep it pending.</summary>
public sealed class ArcInlineEditSaveEventArgs : RoutedEventArgs
{
    public ArcInlineEditSaveEventArgs(string value) => Value = value;

    /// <summary>Routed payload factory used by <c>SaveRouted</c>.</summary>
    public ArcInlineEditSaveEventArgs(RoutedEvent routedEvent, string value) : base(routedEvent) => Value = value;

    /// <summary>The draft that should be persisted.</summary>
    public string Value { get; }

    /// <summary>Set to a message to keep the field open with that error.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Set to the save task to show the busy state. When it faults the field shows the message and keeps
    /// the draft so the save can be retried.
    /// </summary>
    public Task? Pending { get; set; }
}

/// <summary>
/// Arc's inline edit: text that becomes a field in place on click or Enter, saves on Enter or blur,
/// rolls back on Escape, and reports saving and validation state in a message slot beneath.
/// </summary>
[TemplatePart("PART_Resting", typeof(Border))]
[TemplatePart("PART_RestingText", typeof(TextBlock))]
[TemplatePart("PART_EditorShell", typeof(Border))]
[TemplatePart("PART_Editor", typeof(TextBox))]
[TemplatePart("PART_Busy", typeof(ArcSpinner))]
[TemplatePart("PART_SaveButton", typeof(Button))]
[TemplatePart("PART_CancelButton", typeof(Button))]
[TemplatePart("PART_Message", typeof(TextBlock))]
[TemplatePart("PART_SavedMark", typeof(ArcIcon))]
public class ArcInlineEdit : TemplatedControl
{
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcInlineEdit, string?>(nameof(Value), string.Empty);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcInlineEdit, string?>(nameof(Label));

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<ArcInlineEdit, string?>(nameof(Placeholder));

    public static readonly StyledProperty<bool> MultilineProperty =
        AvaloniaProperty.Register<ArcInlineEdit, bool>(nameof(Multiline));

    public static readonly StyledProperty<ArcInlineEditVariant> VariantProperty =
        AvaloniaProperty.Register<ArcInlineEdit, ArcInlineEditVariant>(nameof(Variant));

    private Border? _resting;
    private TextBlock? _restingText;
    private Border? _editor;
    private TextBox? _box;
    private ArcSpinner? _busy;
    private Button? _saveButton;
    private Button? _cancelButton;
    private TextBlock? _message;
    private ArcIcon? _check;

    private bool _editing;
    private bool _saving;
    private string _savedValue = string.Empty;
    private string? _error;
    private bool _pendingBegin;

    static ArcInlineEdit()
    {
        ValueProperty.Changed.AddClassHandler<ArcInlineEdit>((edit, _) => edit.OnValueChanged());
        VariantProperty.Changed.AddClassHandler<ArcInlineEdit>((edit, _) => edit.ApplyVariant());
        MultilineProperty.Changed.AddClassHandler<ArcInlineEdit>((edit, _) => edit.ApplyVariant());
        LabelProperty.Changed.AddClassHandler<ArcInlineEdit>((edit, _) => edit.UpdateAutomation());
        PlaceholderProperty.Changed.AddClassHandler<ArcInlineEdit>((edit, _) => edit.UpdateRestingText());
    }

    public ArcInlineEdit()
    {
        Focusable = true;
        _savedValue = Value ?? string.Empty;
        UpdateClasses();
    }

    /// <summary>Raised when a commit should persist the new value. Without a handler the value saves immediately.</summary>
    public event EventHandler<ArcInlineEditSaveEventArgs>? Save;

    public static readonly RoutedEvent<ArcInlineEditSaveEventArgs> SaveEvent =
        RoutedEvent.Register<ArcInlineEdit, ArcInlineEditSaveEventArgs>("Save", RoutingStrategies.Bubble);

    /// <summary>Bubbling form of <see cref="Save"/>; the CLR event above keeps firing as before.</summary>
    public event EventHandler<ArcInlineEditSaveEventArgs>? SaveRouted
    {
        add => AddHandler(SaveEvent, value);
        remove => RemoveHandler(SaveEvent, value);
    }

    /// <summary>Raised whenever <see cref="Value"/> changes, from a save or from code.</summary>
    public event EventHandler? ValueChanged;

    /// <summary>Returns an error message to block the save, or null when the draft is valid.</summary>
    public Func<string, string?>? Validate { get; set; }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Accessible name; the resting text reads as "label: value".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>Wraps and grows in height; Shift+Enter inserts a line break.</summary>
    public bool Multiline
    {
        get => GetValue(MultilineProperty);
        set => SetValue(MultilineProperty, value);
    }

    public ArcInlineEditVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public bool IsEditing => _editing;

    /// <summary>Starts editing (used by keyboard activation and by page code that owns a shortcut).</summary>
    public void BeginEdit()
    {
        if (_editing || !IsEnabled)
        {
            return;
        }

        if (_box is null)
        {
            ApplyTemplate();
        }

        if (_box is null)
        {
            _pendingBegin = true;
            return;
        }

        _pendingBegin = false;
        _error = null;
        _editing = true;
        _savedValue = Value ?? string.Empty;
        _box.Text = _savedValue;
        _box.AcceptsReturn = Multiline;
        _box.TextWrapping = Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _box.MinHeight = Multiline ? 72 : 0;
        UpdateClasses();
        ShowEditor();
        _box.Focus();
        _box.SelectAll();
        UpdateMessage();
    }

    /// <summary>Commits the current draft.</summary>
    public void Commit()
    {
        if (!_editing || _saving || _box is null)
        {
            return;
        }

        var draft = _box.Text ?? string.Empty;
        var validation = Validate?.Invoke(draft);
        if (!string.IsNullOrWhiteSpace(validation))
        {
            _error = validation;
            UpdateClasses();
            UpdateMessage();
            return;
        }

        _error = null;
        var args = new ArcInlineEditSaveEventArgs(draft);
        Save?.Invoke(this, args);
        RaiseEvent(new ArcInlineEditSaveEventArgs(SaveEvent, draft));
        if (!string.IsNullOrWhiteSpace(args.Error))
        {
            _error = args.Error;
            UpdateClasses();
            UpdateMessage();
            return;
        }

        if (args.Pending is { } pending)
        {
            _ = RunSaveAsync(draft, pending);
            return;
        }

        FinishSave(draft);
    }

    /// <summary>Cancels editing and restores the saved value.</summary>
    public void CancelEdit()
    {
        if (!_editing)
        {
            return;
        }

        _editing = false;
        _error = null;
        if (_box is not null)
        {
            _box.Text = _savedValue;
        }

        UpdateClasses();
        ShowEditor();
        UpdateMessage();
        Focus();
    }

    private async Task RunSaveAsync(string draft, Task pending)
    {
        _saving = true;
        UpdateClasses();
        ShowEditor();
        UpdateMessage();
        try
        {
            await pending;
            _saving = false;
            FinishSave(draft);
        }
        catch (Exception exception)
        {
            _saving = false;
            _error = string.IsNullOrWhiteSpace(exception.Message) ? "Could not save. Try again." : exception.Message;
            UpdateClasses();
            ShowEditor();
            UpdateMessage();
            if (IsKeyboardFocusWithin && _box is not null)
            {
                _box.Focus();
            }
        }
    }

    private void FinishSave(string draft)
    {
        _savedValue = draft;
        SetCurrentValue(ValueProperty, draft);
        _editing = false;
        UpdateClasses();
        ShowEditor();
        UpdateMessage();
        PlaySavedMark();
        Focus();
    }

    private void PlaySavedMark()
    {
        if (_check is null)
        {
            return;
        }

        _check.IsVisible = true;
        _check.Opacity = 1;
        if (ArcThemeManager.ReducedMotion)
        {
            ArcAnim.Delay(TimeSpan.FromMilliseconds(650), () => _check.IsVisible = false);
            return;
        }

        _check.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _check.RenderTransform = new ScaleTransform(0.6, 0.6);
        ArcAnim.To(0.6, 1, ArcAnim.SnappyDuration, ArcAnim.Snappy, value =>
        {
            if (_check?.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = value;
                scale.ScaleY = value;
            }
        });
        ArcAnim.Delay(TimeSpan.FromMilliseconds(900), () => _check.IsVisible = false);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_box is not null)
        {
            _box.KeyDown -= OnEditorKeyDown;
            _box.LostFocus -= OnEditorLostFocus;
        }

        if (_saveButton is not null)
        {
            _saveButton.Click -= OnSaveClicked;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Click -= OnCancelClicked;
        }

        _resting = e.NameScope.Find<Border>("PART_Resting");
        _restingText = e.NameScope.Find<TextBlock>("PART_RestingText");
        _editor = e.NameScope.Find<Border>("PART_EditorShell");
        _box = e.NameScope.Find<TextBox>("PART_Editor");
        _busy = e.NameScope.Find<ArcSpinner>("PART_Busy");
        _saveButton = e.NameScope.Find<Button>("PART_SaveButton");
        _cancelButton = e.NameScope.Find<Button>("PART_CancelButton");
        _message = e.NameScope.Find<TextBlock>("PART_Message");
        _check = e.NameScope.Find<ArcIcon>("PART_SavedMark");

        if (_box is not null)
        {
            _box.KeyDown += OnEditorKeyDown;
            _box.LostFocus += OnEditorLostFocus;
            AutomationProperties.SetName(_box, Label ?? "Editable text");
        }

        if (_saveButton is not null)
        {
            _saveButton.Click += OnSaveClicked;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.Click += OnCancelClicked;
        }

        ApplyVariant();
        UpdateRestingText();
        UpdateAutomation();
        ShowEditor();
        UpdateMessage();

        if (_pendingBegin)
        {
            _pendingBegin = false;
            Dispatcher.UIThread.Post(BeginEdit, DispatcherPriority.Loaded);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_editing || !IsEnabled)
        {
            return;
        }

        e.Handled = true;
        Focus();
        BeginEdit();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_editing)
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Space or Key.F2)
        {
            e.Handled = true;
            BeginEdit();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Classes.Set("disabled", !IsEnabled);
        }
        else if (change.Property == IsFocusedProperty && !_editing && _resting is not null)
        {
            Classes.Set("focus", IsFocused);
        }
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Commit();

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => CancelEdit();

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelEdit();
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (Multiline && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                return;
            }

            e.Handled = true;
            Commit();
        }
    }

    private void OnEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_editing)
        {
            return;
        }

        // Focus can move to the save button inside the same control; only save when it left the control.
        Dispatcher.UIThread.Post(() =>
        {
            if (_editing && !IsKeyboardFocusWithin)
            {
                Commit();
            }
        }, DispatcherPriority.Background);
    }

    private void OnValueChanged()
    {
        if (!_editing)
        {
            _savedValue = Value ?? string.Empty;
            UpdateRestingText(animate: true);
        }

        UpdateAutomation();
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateRestingText(bool animate = false)
    {
        if (_restingText is null)
        {
            return;
        }

        var value = Value ?? string.Empty;
        _restingText!.Text = string.IsNullOrEmpty(value) ? Placeholder ?? "Untitled" : value;
        _restingText.Foreground = ArcResources.Brush(string.IsNullOrEmpty(value) ? ArcTokens.TextMuted : ArcTokens.Foreground);

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        _restingText.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
        var transform = new TranslateTransform(0, 4);
        _restingText.RenderTransform = transform;
        _restingText.Opacity = 0;
        ArcAnim.To(0, 1, TimeSpan.FromMilliseconds(220), ArcMotion.EaseEnter, value =>
        {
            if (_restingText is null)
            {
                return;
            }

            _restingText.Opacity = value;
            transform.Y = 4 * (1 - value);
        });
    }

    private void ApplyVariant()
    {
        var title = Variant == ArcInlineEditVariant.Title;
        var fontSize = title ? 22d : 14d;
        var weight = title ? FontWeight.Medium : FontWeight.Normal;
        var family = title ? ArcText.Display : ArcText.Body;

        if (_restingText is not null)
        {
            _restingText.FontSize = fontSize;
            _restingText.FontWeight = weight;
            _restingText.FontFamily = family;
        }

        if (_box is not null)
        {
            _box.FontSize = fontSize;
            _box.FontWeight = weight;
            _box.FontFamily = family;
        }

        if (_editor is not null)
        {
            _editor.MinHeight = title ? 52 : 44;
        }

        Classes.Set("title", title);
        Classes.Set("body", !title);
        Classes.Set("multiline", Multiline);
    }

    private void ShowEditor()
    {
        if (_resting is not null)
        {
            _resting.IsVisible = !_editing;
        }

        if (_editor is not null)
        {
            _editor.IsVisible = _editing;
        }

        if (_busy is not null)
        {
            _busy.IsVisible = _saving;
        }

        if (_saveButton is not null)
        {
            _saveButton.IsVisible = !_saving;
        }

        if (_cancelButton is not null)
        {
            _cancelButton.IsVisible = !_saving;
        }
    }

    private void UpdateClasses()
    {
        Classes.Set("editing", _editing);
        Classes.Set("saving", _saving);
        Classes.Set("invalid", !string.IsNullOrWhiteSpace(_error));
        Classes.Set("disabled", !IsEnabled);

        if (_box is not null)
        {
            _box.IsReadOnly = _saving;
        }
    }

    private void UpdateMessage()
    {
        if (_message is null)
        {
            return;
        }

        _message.Text = _error;
        _message.IsVisible = !string.IsNullOrWhiteSpace(_error);
        _message.Foreground = ArcResources.Brush(ArcTokens.Danger);
        AutomationProperties.SetHelpText(
            this,
            _error ?? (_editing ? "Press Enter to save, Escape to cancel." : "Press Enter to rename."));
    }

    private void UpdateAutomation()
    {
        var label = string.IsNullOrWhiteSpace(Label) ? "Editable text" : Label;
        AutomationProperties.SetName(this, $"{label}: {Value}");
        if (string.IsNullOrWhiteSpace(_error))
        {
            AutomationProperties.SetHelpText(this, _editing ? "Press Enter to save, Escape to cancel." : "Press Enter to rename.");
        }
    }
}
