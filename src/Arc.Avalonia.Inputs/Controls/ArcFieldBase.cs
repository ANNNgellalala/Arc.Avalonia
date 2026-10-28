using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>Arc's three control heights: 36, 44, 50.</summary>
public enum ArcFieldSize
{
    Sm,
    Md,
    Lg
}

/// <summary>
/// Shared chrome for every labelled field: visible label, helper copy, an error line and the three
/// control sizes. Colours and radii live in the control themes; this class only owns state classes.
/// </summary>
public abstract class ArcFieldBase : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcFieldBase, string?>(nameof(Label));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcFieldBase, string?>(nameof(Description));

    public static readonly StyledProperty<string?> ErrorProperty =
        AvaloniaProperty.Register<ArcFieldBase, string?>(nameof(Error));

    public static readonly StyledProperty<ArcFieldSize> SizeProperty =
        AvaloniaProperty.Register<ArcFieldBase, ArcFieldSize>(nameof(Size), ArcFieldSize.Md);

    static ArcFieldBase()
    {
        LabelProperty.Changed.AddClassHandler<ArcFieldBase>((control, _) => control.UpdateFieldClasses());
        DescriptionProperty.Changed.AddClassHandler<ArcFieldBase>((control, _) =>
        {
            control.UpdateFieldClasses();
            control.OnMessageChanged();
        });
        ErrorProperty.Changed.AddClassHandler<ArcFieldBase>((control, _) =>
        {
            control.UpdateFieldClasses();
            control.OnMessageChanged();
        });
        SizeProperty.Changed.AddClassHandler<ArcFieldBase>((control, _) => control.UpdateFieldClasses());
        IsEnabledProperty.Changed.AddClassHandler<ArcFieldBase>((control, _) => control.UpdateFieldClasses());
    }

    protected ArcFieldBase()
    {
        UpdateFieldClasses();
    }

    /// <summary>Visible label, always present in product forms.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Helper copy under the field; replaced by the error when one is set.</summary>
    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Error copy; the presence of text turns the field into its error state.</summary>
    public string? Error
    {
        get => GetValue(ErrorProperty);
        set => SetValue(ErrorProperty, value);
    }

    public ArcFieldSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    protected void UpdateFieldClasses()
    {
        Classes.Set("sm", Size == ArcFieldSize.Sm);
        Classes.Set("md", Size == ArcFieldSize.Md);
        Classes.Set("lg", Size == ArcFieldSize.Lg);
        Classes.Set("invalid", HasError);
        Classes.Set("disabled", !IsEnabled);
    }

    /// <summary>Called after Description or Error changes so a derived field can refresh its message row.</summary>
    protected virtual void OnMessageChanged()
    {
    }

    /// <summary>Writes the current error or helper copy into the template's message slot.</summary>
    protected void ApplyMessage(TextBlock? message)
    {
        if (message is null)
        {
            return;
        }

        var text = HasError ? Error : Description;
        message.Text = text;
        message.IsVisible = !string.IsNullOrWhiteSpace(text);
        message.Foreground = ArcResources.Brush(HasError ? ArcTokens.Danger : ArcTokens.TextMuted);
    }
}
