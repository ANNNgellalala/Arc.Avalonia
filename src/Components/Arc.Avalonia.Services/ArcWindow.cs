using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Services;

/// <summary>How <see cref="ArcWindow"/> draws its title area.</summary>
public enum ArcWindowTitleBarMode
{
    /// <summary>Keep the operating system's window chrome (default).</summary>
    System,

    /// <summary>Extend the client area and draw an Arc title bar; the system chrome is disabled.</summary>
    Custom
}

/// <summary>
/// The application window shell: a <see cref="Window"/> with a content page host and a full-window
/// <see cref="OverlayHost"/> for dialogs, toasts and drawers. It registers the default service implementations
/// on construction (never overwriting an app registration made earlier), and exposes
/// <see cref="Services"/> as the per-window registration point.
/// </summary>
/// <remarks>
/// <see cref="Window.Content"/> is the shell root (page host + overlay); assign the page with
/// <see cref="Page"/> or <c>PageHost.Content</c> so the overlay always stays above it. <see cref="TitleBarMode"/>
/// is opt-in: the default keeps the native title bar, and <see cref="ArcWindowTitleBarMode.Custom"/> draws a
/// draggable Arc bar with minimize / maximize / close actions.
/// </remarks>
public class ArcWindow : Window
{
    /// <summary>Identifies the <see cref="TitleBarMode"/> property.</summary>
    public static readonly StyledProperty<ArcWindowTitleBarMode> TitleBarModeProperty =
        AvaloniaProperty.Register<ArcWindow, ArcWindowTitleBarMode>(nameof(TitleBarMode));

    private const double TitleBarHeight = 40;

    private readonly Grid _root;
    private readonly ContentControl _pageHost;
    private readonly Panel _overlayHost;
    private readonly Grid _titleBar;
    private readonly TextBlock _titleText;

    /// <summary>Creates the shell with the overlay host and the default service registrations.</summary>
    public ArcWindow()
    {
        Services = new ArcWindowServices();

        _pageHost = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        _overlayHost = new Panel { ClipToBounds = false };

        _titleText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Classes = { "arc-label" }
        };

        _titleBar = BuildTitleBar();
        _titleBar.IsVisible = false;

        _root = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(_titleBar, 0);
        _root.Children.Add(_titleBar);
        Grid.SetRow(_pageHost, 1);
        _root.Children.Add(_pageHost);
        Grid.SetRowSpan(_overlayHost, 2);
        _root.Children.Add(_overlayHost);

        Background = ArcResources.Brush(ArcTokens.Background);

        Content = _root;

        UpdateTitle();
        UpdateTitleBarMode();

        // Defaults first; an application may override any of them before or after the window is created.
        Services.EnsureRegistered<IDialogService>(() => new ArcDialogService(_overlayHost));
        Services.EnsureRegistered<INotificationService>(() => new ArcNotificationService(_overlayHost));
        Services.EnsureRegistered<INavigationService>(() => new ArcNavigationService());
        Services.EnsureRegistered<IFilePickerService>(() => StorageProviderFilePickerService.Instance);
        Services.EnsureRegistered<IArcThemeService>(() => new ArcThemeService());
    }

    /// <summary>The window-scoped service registration point (a typed facade over <see cref="ArcServiceProvider"/>).</summary>
    public ArcWindowServices Services { get; }

    /// <summary>The transparent layer above the page; dialogs, toasts and drawers are added here.</summary>
    public Panel OverlayHost => _overlayHost;

    /// <summary>The hosted page container; the overlay layer stays above it.</summary>
    public ContentControl PageHost => _pageHost;

    /// <summary>Gets or sets the page shown by the shell (shorthand for <c>PageHost.Content</c>).</summary>
    public Control? Page
    {
        get => _pageHost.Content as Control;
        set => _pageHost.Content = value;
    }

    /// <summary>System titles by default; <see cref="ArcWindowTitleBarMode.Custom"/> draws the Arc bar.</summary>
    public ArcWindowTitleBarMode TitleBarMode
    {
        get => GetValue(TitleBarModeProperty);
        set => SetValue(TitleBarModeProperty, value);
    }

    /// <summary>Shows a page by routing it through the hosted <see cref="INavigationService"/>.</summary>
    public void Navigate(object page) => Services.Get<INavigationService>()?.Navigate(page);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TitleProperty)
        {
            UpdateTitle();
        }
        else if (change.Property == TitleBarModeProperty)
        {
            UpdateTitleBarMode();
        }
    }

    private void UpdateTitle() => _titleText.Text = Title;

    private void UpdateTitleBarMode()
    {
        var custom = TitleBarMode == ArcWindowTitleBarMode.Custom;
        _titleBar.IsVisible = custom;
        ExtendClientAreaToDecorationsHint = custom;
        ExtendClientAreaTitleBarHeightHint = custom ? TitleBarHeight : -1;
    }

    private Grid BuildTitleBar()
    {
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Height = TitleBarHeight,
            Background = ArcResources.Brush(ArcTokens.Surface)
        };
        bar.Children.Add(_titleText);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Grid.SetColumn(actions, 2);
        actions.Children.Add(CaptionButton("minus", "Minimize", () => WindowState = WindowState.Minimized, danger: false));
        actions.Children.Add(CaptionButton("square", "Maximize", ToggleMaximize, danger: false));
        actions.Children.Add(CaptionButton("x", "Close", Close, danger: true));
        bar.Children.Add(actions);

        bar.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                return;
            }

            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                BeginMoveDrag(e);
            }
        };

        return bar;
    }

    private Border CaptionButton(string icon, string label, Action action, bool danger)
    {
        var glyph = new ArcIcon
        {
            Icon = icon,
            Size = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcResources.Brush(danger ? ArcTokens.Danger : ArcTokens.TextSecondary)
        };

        var button = new Border
        {
            Width = 46,
            Height = TitleBarHeight,
            Background = Brushes.Transparent,
            Child = glyph,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        AutomationProperties.SetName(button, label);

        button.PointerPressed += (_, e) => e.Handled = true;
        button.PointerEntered += (_, _) =>
        {
            button.Background = ArcResources.Brush(danger ? ArcTokens.DangerSubtle : ArcTokens.SurfaceMuted);
        };
        button.PointerExited += (_, _) =>
        {
            button.Background = Brushes.Transparent;
        };
        button.PointerReleased += (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonReleased)
            {
                action();
                e.Handled = true;
            }
        };

        return button;
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
