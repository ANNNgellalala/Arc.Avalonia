using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcSplitButtonVariant
{
    Primary,
    Secondary
}

/// <summary>
/// One default action joined to a chevron that opens the nearby alternatives. The menu half never
/// scales, so the menu always opens from a still anchor.
/// </summary>
[TemplatePart("PART_Main", typeof(ArcButton))]
[TemplatePart("PART_Chevron", typeof(ArcButton))]
[TemplatePart("PART_Divider", typeof(Border))]
[TemplatePart("PART_Overlay", typeof(Canvas))]
public sealed class ArcSplitButton : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcSplitButton, string?>(nameof(Label));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcSplitButton, string?>(nameof(Icon));

    public static readonly StyledProperty<IList<ArcMenuItem>?> ActionsProperty =
        AvaloniaProperty.Register<ArcSplitButton, IList<ArcMenuItem>?>(nameof(Actions));

    public static readonly StyledProperty<ArcSplitButtonVariant> VariantProperty =
        AvaloniaProperty.Register<ArcSplitButton, ArcSplitButtonVariant>(nameof(Variant));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcSplitButton, bool>(nameof(IsOpen));

    private readonly ArcMenuPresenter _menu;
    private ArcButton? _main;
    private ArcButton? _chevron;
    private Border? _divider;
    private Canvas? _overlay;

    static ArcSplitButton()
    {
        LabelProperty.Changed.AddClassHandler<ArcSplitButton>((split, _) => split.UpdateContent());
        IconProperty.Changed.AddClassHandler<ArcSplitButton>((split, _) => split.UpdateContent());
        VariantProperty.Changed.AddClassHandler<ArcSplitButton>((split, _) => split.UpdateVariant());
        IsOpenProperty.Changed.AddClassHandler<ArcSplitButton>((split, _) => split.SyncMenu());
    }

    public ArcSplitButton()
    {
        ClipToBounds = false;
        _menu = new ArcMenuPresenter(this);
        _menu.Closed += (_, _) => SetCurrentValue(IsOpenProperty, false);
        LayoutUpdated += (_, _) =>
        {
            if (IsOpen && !_menu.IsOpen)
            {
                SyncMenu();
            }

            _menu.Reposition();
        };
    }

    /// <summary>Runs the main action.</summary>
    public event EventHandler? Click;
    /// <summary>Bubbled copy of <see cref="Click"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcSplitButtonClickEventArgs> ClickEvent =
        RoutedEvent.Register<ArcSplitButton, ArcSplitButtonClickEventArgs>("Click", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="Click"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcSplitButtonClickEventArgs>? ClickRouted
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>Main action label.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Lucide icon name for the main half.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>The nearby alternatives behind the chevron.</summary>
    public IList<ArcMenuItem>? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public ArcSplitButtonVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    /// <summary>Whether the alternatives menu is open.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _main = e.NameScope.Find<ArcButton>("PART_Main");
        _chevron = e.NameScope.Find<ArcButton>("PART_Chevron");
        _divider = e.NameScope.Find<Border>("PART_Divider");
        _overlay = e.NameScope.Find<Canvas>("PART_Overlay");

        if (_main is not null)
        {
            _main.Click += (_, _) =>
            {
                var clickArgs = new ArcSplitButtonClickEventArgs(ClickEvent);
                Click?.Invoke(this, EventArgs.Empty);
                RaiseEvent(clickArgs);
            };
            _main.KeyDown += OnMainKeyDown;
        }

        if (_chevron is not null)
        {
            _chevron.Content = ArcActionVisuals.InheritIcon("chevron-down", _chevron, 16);
            _chevron.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
            _chevron.KeyDown += OnChevronKeyDown;
            AutomationProperties.SetName(_chevron, $"{Label ?? "Action"} more actions");
        }

        _menu.Attach(_overlay);
        UpdateContent();
        UpdateVariant();
        SyncMenu();
    }

    private void OnMainKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            SetCurrentValue(IsOpenProperty, true);
            e.Handled = true;
        }
    }

    private void OnChevronKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            SetCurrentValue(IsOpenProperty, true);
            e.Handled = true;
        }
    }

    private void UpdateContent()
    {
        if (_main is null)
        {
            return;
        }

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrWhiteSpace(Icon))
        {
            panel.Children.Add(ArcActionVisuals.InheritIcon(Icon, _main, 16));
        }

        panel.Children.Add(ArcActionVisuals.InheritText(Label ?? "Action", _main, medium: true));
        _main.Content = panel;
        AutomationProperties.SetName(_main, Label);
        if (_chevron is not null)
        {
            AutomationProperties.SetName(_chevron, $"{Label ?? "Action"} more actions");
        }
    }

    private void UpdateVariant()
    {
        var variant = Variant == ArcSplitButtonVariant.Primary ? ArcButtonVariant.Primary : ArcButtonVariant.Secondary;
        if (_main is not null)
        {
            _main.Variant = variant;
        }

        if (_chevron is not null)
        {
            _chevron.Variant = variant;
        }

        _divider?.Classes.Set("primary", Variant == ArcSplitButtonVariant.Primary);

        Classes.Set("primary", Variant == ArcSplitButtonVariant.Primary);
        Classes.Set("secondary", Variant == ArcSplitButtonVariant.Secondary);
    }

    private void SyncMenu()
    {
        if (IsOpen)
        {
            if (Actions is not null && _overlay is not null && Bounds.Width > 0)
            {
                _menu.Open(Actions, $"{Label} alternatives", alignEnd: true, focus: _chevron?.IsFocused == true);
                if (_chevron is not null)
                {
                    ArcActionVisuals.Rotate(_chevron.Content as Control ?? _chevron, true);
                }
            }
        }
        else
        {
            _menu.Close();
            if (_chevron is not null)
            {
                ArcActionVisuals.Rotate(_chevron.Content as Control ?? _chevron, false);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _menu.Close();
        base.OnDetachedFromVisualTree(e);
    }
}