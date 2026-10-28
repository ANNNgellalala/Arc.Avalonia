using System;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// A focused surface over a dimmed backdrop for decisions that need attention: title, description, body
/// and footer actions. Escape and a backdrop press close it, focus is trapped while it is open, and it
/// returns focus when it closes. It fills the nearest panel, so a page can host it directly.
/// </summary>
public sealed class ArcDialog : ContentControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcDialog, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArcDialog, string?>(nameof(Title));

    public static readonly StyledProperty<string?> DescriptionProperty =
        AvaloniaProperty.Register<ArcDialog, string?>(nameof(Description));

    public static readonly StyledProperty<object?> FooterProperty =
        AvaloniaProperty.Register<ArcDialog, object?>(nameof(Footer));

    public static readonly StyledProperty<bool> CloseOnBackdropPressProperty =
        AvaloniaProperty.Register<ArcDialog, bool>(nameof(CloseOnBackdropPress), true);

    private Border? _backdrop;
    private Border? _panel;
    private TextBlock? _title;
    private TextBlock? _description;
    private ContentPresenter? _footer;
    private IDisposable? _dismiss;
    private Control? _restoreFocus;
    private bool _wasOpen;

    static ArcDialog()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcDialog>((dialog, _) => dialog.Sync());
        TitleProperty.Changed.AddClassHandler<ArcDialog>((dialog, _) => dialog.UpdateCopy());
        DescriptionProperty.Changed.AddClassHandler<ArcDialog>((dialog, _) => dialog.UpdateCopy());
    }

    public ArcDialog()
    {
        Opacity = 0;
        IsHitTestVisible = false;
        Focusable = true;
    }

    public event EventHandler? Closed;

    /// <summary>Whether the dialog is showing; settable so a page can present the open state.</summary>
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

    /// <summary>Footer actions, usually two ArcButtons aligned to the end.</summary>
    public object? Footer
    {
        get => GetValue(FooterProperty);
        set => SetValue(FooterProperty, value);
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
            _backdrop.Background = ArcActionVisuals.Scrim(0.42);
            _backdrop.PointerPressed += (_, _) =>
            {
                if (CloseOnBackdropPress)
                {
                    Close();
                }
            };
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

        UpdateCopy();
        Sync();
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

        AutomationProperties.SetName(this, Title ?? "Dialog");
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

            if (ArcThemeManager.ReducedMotion)
            {
                _panel.Opacity = 1;
                _panel.RenderTransform = TransformOperations.Parse("translateY(0px) scale(1)");
                if (_backdrop is not null)
                {
                    _backdrop.Opacity = 1;
                }
            }
            else
            {
                _panel.Opacity = 0;
                _panel.RenderTransform = TransformOperations.Parse("translateY(8px) scale(0.96)");
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
                    _panel.RenderTransform = TransformOperations.Parse("translateY(0px) scale(1)");
                    if (_backdrop is not null)
                    {
                        _backdrop.Opacity = 1;
                    }
                }, DispatcherPriority.Render);
            }

            Dispatcher.UIThread.Post(FocusFirst, DispatcherPriority.Loaded);
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
        if (!IsOpen)
        {
            return;
        }

        SetCurrentValue(IsOpenProperty, false);
    }

    private void FocusFirst()
    {
        if (_panel is null || !IsVisible)
        {
            return;
        }

        var target = Focusables().FirstOrDefault() ?? _panel;
        target.Focus();
    }

    private System.Collections.Generic.List<Control> Focusables() =>
        _panel?.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Focusable && control.IsEffectivelyEnabled && control.IsVisible)
            .ToList() ?? new System.Collections.Generic.List<Control>();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Tab || _panel is null)
        {
            return;
        }

        var focusables = Focusables();
        if (focusables.Count == 0)
        {
            return;
        }

        var current = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        var index = focusables.IndexOf(current as Control ?? focusables[0]);
        var next = e.KeyModifiers.HasFlag(KeyModifiers.Shift)
            ? (index - 1 + focusables.Count) % focusables.Count
            : (index + 1) % focusables.Count;
        focusables[next].Focus();
        e.Handled = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _dismiss?.Dispose();
        _dismiss = null;
        base.OnDetachedFromVisualTree(e);
    }
}
