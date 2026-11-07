using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// Wraps a target and opens a menu of secondary actions at the pointer, or under the target from the
/// keyboard. A plain click opens it too, so do not wrap targets that need their own click action.
/// </summary>
[TemplatePart("PART_Target", typeof(Border))]
[TemplatePart("PART_Ring", typeof(Border))]
[TemplatePart("PART_Overlay", typeof(Canvas))]
public sealed class ArcContextMenu : ContentControl
{
    public static readonly StyledProperty<IList<ArcMenuItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcContextMenu, IList<ArcMenuItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcContextMenu, string?>(nameof(Label), "Context menu");

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcContextMenu, bool>(nameof(IsOpen));

    private Border? _target;
    private Border? _ring;
    private Canvas? _overlay;
    private ArcMenuSurface? _surface;
    private Point _position;
    private bool _fromPointer;
    private bool _openedByUser;
    private Rect _lastPlacement;
    private bool _syncing;

    static ArcContextMenu()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcContextMenu>((menu, _) => menu.SyncMenu());
        ItemsProperty.Changed.AddClassHandler<ArcContextMenu>((menu, _) => menu.SyncMenu());
    }

    public ArcContextMenu()
    {
        ClipToBounds = false;
        LayoutUpdated += (_, _) =>
        {
            if (IsOpen && _surface is null)
            {
                SyncMenu();
            }

            PositionSurface();
        };
    }

    /// <summary>Actions in order; a checked item shows a check before its label.</summary>
    public IList<ArcMenuItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name for both the target and the menu.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Whether the menu is open; settable so a page can show the open state.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _target = e.NameScope.Find<Border>("PART_Target");
        _ring = e.NameScope.Find<Border>("PART_Ring");
        _overlay = e.NameScope.Find<Canvas>("PART_Overlay");

        if (_target is not null)
        {
            _target.Focusable = true;
            _target.IsTabStop = true;
            _target.PointerPressed += OnTargetPressed;
            _target.KeyDown += OnTargetKeyDown;
            _target.GotFocus += (_, args) =>
            {
                if (_ring is not null)
                {
                    // Pointer focus draws nothing; keyboard focus gets Arc's shared ring.
                    _ring.IsVisible = args.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
                }
            };
            _target.LostFocus += (_, _) =>
            {
                if (_ring is not null)
                {
                    _ring.IsVisible = false;
                }
            };
            AutomationProperties.SetName(_target, Label ?? "Context menu");
            AutomationProperties.SetHelpText(_target, "Right click or press Shift F10 for actions");
        }

        SyncMenu();
    }

    private void OnTargetPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsRightButtonPressed && !point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _position = point.Position;
        _fromPointer = true;
        _openedByUser = true;
        SetCurrentValue(IsOpenProperty, true);
        e.Handled = true;
    }

    private void OnTargetKeyDown(object? sender, KeyEventArgs e)
    {
        var keyboardMenu = e.Key == Key.Apps ||
                           (e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        if (keyboardMenu)
        {
            _fromPointer = false;
            _openedByUser = true;
            SetCurrentValue(IsOpenProperty, true);
            e.Handled = true;
        }
    }

    private void SyncMenu()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            if (IsOpen)
            {
                OpenSurface();
            }
            else
            {
                CloseSurface(focusTarget: true);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OpenSurface()
    {
        if (_surface is not null || _overlay is null || Bounds.Width <= 0)
        {
            return;
        }

        if (Items is null || Items.Count == 0)
        {
            return;
        }

        var surface = new ArcMenuSurface
        {
            Items = Items,
            Label = Label ?? "Context menu",
            MinWidth = 200,
            MaxWidth = 320
        };
        surface.Closed += OnSurfaceClosed;
        _surface = surface;
        ArcActionVisuals.SetOverlay(_overlay, surface);
        surface.Open(_openedByUser);
        PositionSurface();
        // The menu is hosted in this template's canvas instead of a popup window, so lift the owner branch
        // above the content that follows it.
        ArcOverlayOrder.SetRaised(this, true);
    }

    private void OnSurfaceClosed(object? sender, EventArgs e)
    {
        CloseSurface(focusTarget: true);
        SetCurrentValue(IsOpenProperty, false);
    }

    private void CloseSurface(bool focusTarget)
    {
        if (_surface is null)
        {
            return;
        }

        var surface = _surface;
        _surface = null;
        surface.Closed -= OnSurfaceClosed;
        ArcActionVisuals.SetOverlay(_overlay, null);
        ArcOverlayOrder.SetRaised(this, false);

        if (focusTarget)
        {
            Dispatcher.UIThread.Post(() => _target?.Focus(), DispatcherPriority.Loaded);
        }
    }

    private void PositionSurface()
    {
        var surface = _surface;
        if (surface is null || _overlay is null)
        {
            return;
        }

        var size = surface.Bounds.Width > 0 ? surface.Bounds.Size : surface.DesiredSize;
        var x = _fromPointer ? _position.X : 0;
        var y = _fromPointer ? _position.Y : Bounds.Height + 6;

        x = Math.Clamp(x, 0, Math.Max(0, Bounds.Width - size.Width));
        y = Math.Clamp(y, 0, Math.Max(0, Bounds.Height - 8));

        var rect = new Rect(x, y, size.Width, size.Height);
        if (rect == _lastPlacement)
        {
            return;
        }

        _lastPlacement = rect;
        ArcActionVisuals.Place(surface, x, y);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        CloseSurface(focusTarget: false);
        base.OnDetachedFromVisualTree(e);
    }
}