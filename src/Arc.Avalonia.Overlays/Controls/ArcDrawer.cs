using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Overlays.Controls;

public enum ArcDrawerSide
{
    Right,
    Left
}

/// <summary>
/// A side sheet that keeps the page in view: it slides in from the chosen edge over the same dimmed
/// backdrop as a dialog, with header and footer slots and the same Escape, backdrop and focus rules.
/// </summary>
public sealed class ArcDrawer : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcDrawer, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcDrawer, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcDrawer, string?>(nameof(Description));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ArcDrawer, object?>(nameof(Footer));

    public static readonly StyledProperty<ArcDrawerSide> SideProperty =
        AvaloniaProperty.Register<ArcDrawer, ArcDrawerSide>(nameof(Side));

    public static readonly StyledProperty<bool> CloseOnBackdropPressProperty =
        AvaloniaProperty.Register<ArcDrawer, bool>(nameof(CloseOnBackdropPress), true);

    private Border? _backdrop;
    private Border? _panel;
    private TextBlock? _title;
    private TextBlock? _description;
    private ContentPresenter? _footer;

    private IDisposable? _dismiss;
    private Control? _restoreFocus;
    private bool _wasOpen;

    static ArcDrawer()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcDrawer>((drawer, _) => drawer.Sync());
        TitleProperty.Changed.AddClassHandler<ArcDrawer>((drawer, _) => drawer.UpdateCopy());
        DescriptionProperty.Changed.AddClassHandler<ArcDrawer>((drawer, _) => drawer.UpdateCopy());
        SideProperty.Changed.AddClassHandler<ArcDrawer>((drawer, _) => drawer.ApplySide());
    }

    public ArcDrawer()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        Focusable = true;
    }

    public event EventHandler? Closed;

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
    }

    /// <summary>Edge the sheet is attached to; right by default.</summary>
    public ArcDrawerSide Side
    {
        get => GetValue(SideProperty);
        set => SetValue(SideProperty, value);
    }

    public bool CloseOnBackdropPress
    {
        get => GetValue(CloseOnBackdropPressProperty);
        set => SetValue(CloseOnBackdropPressProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _backdrop = e.NameScope.Find<Border>("Backdrop");
        _panel = e.NameScope.Find<Border>("Panel");
        _title = e.NameScope.Find<TextBlock>("Title");
        _description = e.NameScope.Find<TextBlock>("Description");
        _footer = e.NameScope.Find<ContentPresenter>("FooterHost");

        if (_backdrop is not null)
        {
            _backdrop.Background = ArcActionVisuals.Scrim(0.36);
            _backdrop.PointerPressed += (_, _) =>
            {
                if (CloseOnBackdropPress)
                {
                    Close();
                }
            };
        }

        if (e.NameScope.Find<ArcButton>("CloseButton") is { } close)
        {
            close.Content = new ArcIcon { Icon = "x", Size = 15 };
            AutomationProperties.SetName(close, "Close drawer");
            close.Click += (_, _) => Close();
        }

        if (_panel is not null && !ArcThemeManager.ReducedMotion)
        {
            _panel.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Standard,
                    Easing = ArcMotion.EaseEnter
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                }
            };
        }

        ApplySide();
        UpdateCopy();
        Sync();
    }

    private void ApplySide()
    {
        if (_panel is null)
        {
            return;
        }

        var radius = ArcActionVisuals.Radius(ArcTokens.RadiusPanel);
        if (Side == ArcDrawerSide.Right)
        {
            _panel.HorizontalAlignment = HorizontalAlignment.Right;
            _panel.CornerRadius = new CornerRadius(radius.TopLeft, 0, 0, radius.BottomLeft);
        }
        else
        {
            _panel.HorizontalAlignment = HorizontalAlignment.Left;
            _panel.CornerRadius = new CornerRadius(0, radius.TopRight, radius.BottomRight, 0);
        }
    }

    private void UpdateCopy()
    {
        if (_title is not null)
        {
            _title.Text = Title;
            _title.IsVisible = !string.IsNullOrWhiteSpace(Title);
        }

        if (_description is not null)
        {
            _description.Text = Description;
            _description.IsVisible = !string.IsNullOrWhiteSpace(Description);
        }

        if (_footer is not null)
        {
            _footer.Content = Footer;
        }

        AutomationProperties.SetName(this, Title ?? "Drawer");
    }

    private void Sync()
    {
        if (_panel is null)
        {
            return;
        }

        if (IsOpen)
        {
            _wasOpen = true;
            Opacity = 1;
            IsHitTestVisible = true;
            _restoreFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            IsVisible = true;
            UpdateCopy();
            _dismiss?.Dispose();
            _dismiss = ArcDismiss.Attach(this, Close, CloseOnBackdropPress);

            var direction = Side == ArcDrawerSide.Right ? 24 : -24;
            if (ArcThemeManager.ReducedMotion)
            {
                _panel.Opacity = 1;
                _panel.RenderTransform = TransformOperations.Parse("translateX(0px)");
                if (_backdrop is not null)
                {
                    _backdrop.Opacity = 1;
                }
            }
            else
            {
                _panel.Opacity = 0;
                _panel.RenderTransform = TransformOperations.Parse($"translateX({direction}px)");
                if (_backdrop is not null)
                {
                    _backdrop.Opacity = 0;
                }

                Dispatcher.UIThread.Post(() =>
                {
                    if (!IsOpen)
                    {
                        return;
                    }

                    _panel.Opacity = 1;
                    _panel.RenderTransform = TransformOperations.Parse("translateX(0px)");
                    if (_backdrop is not null)
                    {
                        _backdrop.Opacity = 1;
                    }
                }, DispatcherPriority.Render);
            }

            Dispatcher.UIThread.Post(() => _panel.Focus(), DispatcherPriority.Loaded);
        }
        else if (_wasOpen)
        {
            _wasOpen = false;
            _dismiss?.Dispose();
            _dismiss = null;
            IsVisible = false;
            Opacity = 0;
            IsHitTestVisible = false;
            Closed?.Invoke(this, EventArgs.Empty);

            if (_restoreFocus is { } restore)
            {
                Dispatcher.UIThread.Post(() => restore.Focus(), DispatcherPriority.Loaded);
            }
        }
        else
        {
            IsVisible = false;
            Opacity = 0;
            IsHitTestVisible = false;
        }
    }

    private void Close()
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _dismiss?.Dispose();
        _dismiss = null;
        base.OnDetachedFromVisualTree(e);
    }
}
