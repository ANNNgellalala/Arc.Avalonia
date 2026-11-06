using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;

namespace Arc.Avalonia.Internal;

/// <summary>
/// A quiet icon-only button used inside composite controls (row actions, chevrons, remove buttons).
/// It carries its own focus ring so keyboard users can reach every inline action.
/// </summary>
[TemplatePart("PART_Icon", typeof(Arc.Avalonia.Controls.ArcIcon))]
public class ArcIconButton : Button
{
    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcIconButton, string?>(nameof(Icon));

    public static readonly StyledProperty<double> IconSizeProperty =
        AvaloniaProperty.Register<ArcIconButton, double>(nameof(IconSize), 16);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcIconButton, string?>(nameof(Label));

    public static readonly StyledProperty<double> BoxSizeProperty =
        AvaloniaProperty.Register<ArcIconButton, double>(nameof(BoxSize), 28);

    public static readonly StyledProperty<bool> MutedProperty =
        AvaloniaProperty.Register<ArcIconButton, bool>(nameof(Muted));

    static ArcIconButton()
    {
        IconProperty.Changed.AddClassHandler<ArcIconButton>((button, _) => button.ApplyIcon());
        IconSizeProperty.Changed.AddClassHandler<ArcIconButton>((button, _) => button.ApplyIcon());
        LabelProperty.Changed.AddClassHandler<ArcIconButton>((button, _) => button.ApplyLabel());
        BoxSizeProperty.Changed.AddClassHandler<ArcIconButton>((button, _) => button.ApplyBox());
        MutedProperty.Changed.AddClassHandler<ArcIconButton>((button, _) => button.Classes.Set("muted", button.Muted));
    }

    public ArcIconButton()
    {
        Classes.Set("muted", Muted);
    }

    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public double IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Accessible name and tooltip for the icon-only action.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Square hit area; 28 by default, 20 for inline chips.</summary>
    public double BoxSize
    {
        get => GetValue(BoxSizeProperty);
        set => SetValue(BoxSizeProperty, value);
    }

    /// <summary>Keeps the icon on the muted text token until hovered.</summary>
    public bool Muted
    {
        get => GetValue(MutedProperty);
        set => SetValue(MutedProperty, value);
    }

    private Arc.Avalonia.Controls.ArcIcon? _icon;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _icon = e.NameScope.Find<Arc.Avalonia.Controls.ArcIcon>("PART_Icon");
        ApplyIcon();
        ApplyBox();
    }

    private void ApplyIcon()
    {
        if (_icon is null)
        {
            return;
        }

        _icon.Icon = Icon;
        _icon.Size = IconSize;
    }

    private void ApplyBox()
    {
        var size = BoxSize;
        if (size > 0)
        {
            Width = size;
            Height = size;
        }
    }

    private void ApplyLabel()
    {
        AutomationProperties.SetName(this, Label);
        ToolTip.SetTip(this, string.IsNullOrWhiteSpace(Label) ? null : Label);
    }
}

/// <summary>
/// A borderless text button used for header cells, menu rows, and inline links. Same focus ring
/// as <see cref="ArcIconButton"/>, so composite controls never have to re-invent focus.
/// </summary>
/// <remarks>
/// The class overrides <c>OnApplyTemplate</c> only to re-apply <see cref="Text"/>
/// after a template change; it does not query template parts. <c>PART_Ring</c> is still part of the
/// public theme contract: the control theme's <c>:focus-visible</c> selector drives it, so a custom
/// theme must provide that part for the keyboard focus ring (declared here for R10 / custom themes).
/// </remarks>
[TemplatePart("PART_Ring", typeof(Border))]
public class ArcFlatButton : Button
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ArcFlatButton, string?>(nameof(Text));

    public static readonly StyledProperty<bool> SelectedProperty =
        AvaloniaProperty.Register<ArcFlatButton, bool>(nameof(Selected));

    public static readonly StyledProperty<bool> QuietProperty =
        AvaloniaProperty.Register<ArcFlatButton, bool>(nameof(Quiet));

    static ArcFlatButton()
    {
        SelectedProperty.Changed.AddClassHandler<ArcFlatButton>((button, _) => button.Classes.Set("selected", button.Selected));
        QuietProperty.Changed.AddClassHandler<ArcFlatButton>((button, _) => button.Classes.Set("quiet", button.Quiet));
    }

    public ArcFlatButton()
    {
        Classes.Set("selected", Selected);
        Classes.Set("quiet", Quiet);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Draws the accent-subtle selected fill, used by menus and table headers.</summary>
    public bool Selected
    {
        get => GetValue(SelectedProperty);
        set => SetValue(SelectedProperty, value);
    }

    /// <summary>Quieter idle colour for inline text actions.</summary>
    public bool Quiet
    {
        get => GetValue(QuietProperty);
        set => SetValue(QuietProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        ApplyText();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            ApplyText();
        }
    }

    private void ApplyText()
    {
        if (!string.IsNullOrEmpty(Text))
        {
            Content = Text;
        }
    }
}