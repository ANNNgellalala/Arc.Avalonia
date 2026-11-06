using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// A labelled trigger that opens a focused list of actions, with one highlight gliding between the rows
/// and a destructive item at the end. The menu is hosted in the page, so it renders without a popup.
/// </summary>
[TemplatePart("PART_Trigger", typeof(ArcButton))]
[TemplatePart("PART_Overlay", typeof(Canvas))]
public sealed class ArcDropdownMenu : TemplatedControl
{
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcDropdownMenu, string?>(nameof(Label));

    public static readonly StyledProperty<string?> IconProperty =
        AvaloniaProperty.Register<ArcDropdownMenu, string?>(nameof(Icon));

    public static readonly StyledProperty<IList<ArcMenuItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcDropdownMenu, IList<ArcMenuItem>?>(nameof(Items));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcDropdownMenu, bool>(nameof(IsOpen));

    private readonly ArcMenuPresenter _menu;
    private ArcButton? _trigger;
    private Canvas? _overlay;

    static ArcDropdownMenu()
    {
        LabelProperty.Changed.AddClassHandler<ArcDropdownMenu>((menu, _) => menu.UpdateTrigger());
        IconProperty.Changed.AddClassHandler<ArcDropdownMenu>((menu, _) => menu.UpdateTrigger());
        IsOpenProperty.Changed.AddClassHandler<ArcDropdownMenu>((menu, _) => menu.SyncMenu());
    }

    public ArcDropdownMenu()
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

    /// <summary>Trigger text; a new label rises in while the trigger width springs.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Leading icon in the trigger.</summary>
    public string? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Items in order; put destructive items last with a separator before them.</summary>
    public IList<ArcMenuItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _trigger = e.NameScope.Find<ArcButton>("PART_Trigger");
        _overlay = e.NameScope.Find<Canvas>("PART_Overlay");

        if (_trigger is not null)
        {
            _trigger.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
            _trigger.KeyDown += OnTriggerKeyDown;
        }

        _menu.Attach(_overlay);
        UpdateTrigger();
        SyncMenu();
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down)
        {
            SetCurrentValue(IsOpenProperty, true);
            e.Handled = true;
        }
    }

    private void UpdateTrigger()
    {
        if (_trigger is null)
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
            panel.Children.Add(ArcActionVisuals.InheritIcon(Icon, _trigger, 16));
        }

        panel.Children.Add(ArcActionVisuals.InheritText(Label ?? "Actions", _trigger, medium: true));

        var chevron = new ArcIcon
        {
            Icon = "chevron-down",
            Size = 15,
            Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        chevron.Classes.Add("dropdown-chevron");
        panel.Children.Add(chevron);

        _trigger.Content = panel;
        AutomationProperties.SetName(_trigger, Label ?? "Actions");
        AutomationProperties.SetHelpText(_trigger, "Opens a menu of actions");
        _chevron = chevron;
    }

    private ArcIcon? _chevron;

    private void SyncMenu()
    {
        if (IsOpen)
        {
            if (Items is not null && _overlay is not null && Bounds.Width > 0)
            {
                _menu.Open(Items, Label ?? "Actions", alignEnd: false, focus: _trigger?.IsFocused == true);
                if (_chevron is not null)
                {
                    ArcActionVisuals.Rotate(_chevron, true);
                }
            }
        }
        else
        {
            _menu.Close();
            if (_chevron is not null)
            {
                ArcActionVisuals.Rotate(_chevron, false);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _menu.Close();
        base.OnDetachedFromVisualTree(e);
    }
}