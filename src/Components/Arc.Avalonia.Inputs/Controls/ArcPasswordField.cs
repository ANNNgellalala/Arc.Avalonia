using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>
/// A password field with an eye toggle. The character stays masked by the native TextBox; revealing is
/// a property on the field, so the caret and selection behave exactly like a plain input.
/// </summary>
public class ArcPasswordField : ArcTextFieldBase
{
    public static readonly StyledProperty<bool> RevealedProperty =
        AvaloniaProperty.Register<ArcPasswordField, bool>(nameof(Revealed));

    public static readonly StyledProperty<bool> CapsLockHintProperty =
        AvaloniaProperty.Register<ArcPasswordField, bool>(nameof(CapsLockHint), true);

    public static readonly StyledProperty<string> ShowPasswordLabelProperty =
        AvaloniaProperty.Register<ArcPasswordField, string>(nameof(ShowPasswordLabel), "Show password");

    public static readonly StyledProperty<string> HidePasswordLabelProperty =
        AvaloniaProperty.Register<ArcPasswordField, string>(nameof(HidePasswordLabel), "Hide password");

    static ArcPasswordField()
    {
        RevealedProperty.Changed.AddClassHandler<ArcPasswordField>((field, _) => field.UpdateReveal());
        CapsLockHintProperty.Changed.AddClassHandler<ArcPasswordField>((field, _) => field.UpdateCapsHint());
        ShowPasswordLabelProperty.Changed.AddClassHandler<ArcPasswordField>((field, _) => field.UpdateReveal());
        HidePasswordLabelProperty.Changed.AddClassHandler<ArcPasswordField>((field, _) => field.UpdateReveal());
    }

    /// <summary>Shows the typed characters instead of the mask.</summary>
    public bool Revealed
    {
        get => GetValue(RevealedProperty);
        set => SetValue(RevealedProperty, value);
    }

    /// <summary>Shows "Caps Lock is on" under the field while it is focused with caps lock down.</summary>
    public bool CapsLockHint
    {
        get => GetValue(CapsLockHintProperty);
        set => SetValue(CapsLockHintProperty, value);
    }

    public string ShowPasswordLabel
    {
        get => GetValue(ShowPasswordLabelProperty);
        set => SetValue(ShowPasswordLabelProperty, value);
    }

    public string HidePasswordLabel
    {
        get => GetValue(HidePasswordLabelProperty);
        set => SetValue(HidePasswordLabelProperty, value);
    }

    /// <inheritdoc />
    protected override string DisabledDisplay
    {
        get
        {
            var text = Value ?? string.Empty;
            return Revealed ? text : new string('\u25CF', text.Length);
        }
    }

    private Button? _revealButton;
    private ArcIcon? _revealGlyph;
    private TextBlock? _capsHint;

    protected override void OnFieldTemplateApplied(TemplateAppliedEventArgs e)
    {
        _revealButton = e.NameScope.Find<Button>("RevealAction");
        _revealGlyph = e.NameScope.Find<ArcIcon>("RevealGlyph");
        _capsHint = e.NameScope.Find<TextBlock>("CapsHint");

        if (_revealButton is not null)
        {
            _revealButton.Click += (_, _) =>
            {
                Revealed = !Revealed;
                InputBox?.Focus();
            };
        }

        if (InputBox is not null)
        {
            InputBox.PasswordChar = '\u25CF';
            InputBox.KeyDown += (_, _) => UpdateCapsHint();
            InputBox.KeyUp += (_, _) => UpdateCapsHint();
        }

        UpdateReveal();
        UpdateCapsHint();
        UpdateDisabledOverlay();
    }

    protected override void OnInputFocused()
    {
        UpdateCapsHint();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RevealedProperty)
        {
            UpdateReveal();
        }
    }

    private void UpdateReveal()
    {
        if (InputBox is not null)
        {
            InputBox.RevealPassword = Revealed;
        }

        if (_revealGlyph is not null)
        {
            _revealGlyph.Icon = Revealed ? "eye-off" : "eye";
        }

        if (_revealButton is not null)
        {
            var label = Revealed ? HidePasswordLabel : ShowPasswordLabel;
            AutomationProperties.SetName(_revealButton, label);
            ToolTip.SetTip(_revealButton, label);
        }
    }

    private void UpdateCapsHint()
    {
        if (_capsHint is null)
        {
            return;
        }

        _capsHint.IsVisible = CapsLockHint && IsInputFocused && CapsLockState.IsOn();
    }
}
