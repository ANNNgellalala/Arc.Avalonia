using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A search entry point: search icon, reserved clear-button slot and an optional keyboard shortcut
/// hint. Escape clears the query, matching native search fields.
/// </summary>
[TemplatePart("PART_ShortcutHint", typeof(TextBlock))]
[TemplatePart("PART_ShortcutPill", typeof(Control))]
[TemplatePart("PART_ClearAction", typeof(Button))]
public class ArcSearchField : ArcTextFieldBase
{
    public static readonly StyledProperty<string?> ShortcutHintProperty =
        AvaloniaProperty.Register<ArcSearchField, string?>(nameof(ShortcutHint));

    static ArcSearchField()
    {
        ShortcutHintProperty.Changed.AddClassHandler<ArcSearchField>((field, _) => field.RefreshShortcut());
    }

    /// <summary>Shortcut rendered at the trailing edge, for example "⌘K".</summary>
    public string? ShortcutHint
    {
        get => GetValue(ShortcutHintProperty);
        set => SetValue(ShortcutHintProperty, value);
    }

    protected override bool ReserveClearSlot => true;

    private TextBlock? _shortcut;
    private Control? _shortcutPill;

    protected override void OnFieldTemplateApplied(TemplateAppliedEventArgs e)
    {
        _shortcut = e.NameScope.Find<TextBlock>("PART_ShortcutHint");
        _shortcutPill = e.NameScope.Find<Control>("PART_ShortcutPill");

        if (InputBox is not null)
        {
            AutomationProperties.SetName(InputBox, Label ?? "Search");
        }

        if (e.NameScope.Find<Button>("PART_ClearAction") is { } clear)
        {
            AutomationProperties.SetName(clear, "Clear search");
        }

        RefreshShortcut();
    }

    protected override void OnInputChanged()
    {
        RefreshShortcut();
    }

    protected override void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !string.IsNullOrEmpty(Value))
        {
            Value = string.Empty;
            RefreshShortcut();
            RaiseValueChanged();
            e.Handled = true;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShortcutHintProperty)
        {
            RefreshShortcut();
        }
    }

    private void RefreshShortcut()
    {
        if (_shortcut is not null)
        {
            _shortcut.Text = ShortcutHint;
        }

        if (_shortcutPill is not null)
        {
            _shortcutPill.IsVisible = !string.IsNullOrEmpty(ShortcutHint) && string.IsNullOrEmpty(Value);
        }
    }
}
