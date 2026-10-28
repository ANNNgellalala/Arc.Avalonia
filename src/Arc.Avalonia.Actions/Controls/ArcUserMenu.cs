using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcUserMenuStatus
{
    None,
    Available,
    Busy,
    Away
}

public enum ArcUserMenuTheme
{
    Light,
    Dark,
    System
}

public enum ArcUserMenuAlign
{
    Start,
    Center,
    End
}

/// <summary>One account destination in the user menu.</summary>
public sealed class ArcUserMenuItem
{
    public string? Label { get; set; }

    /// <summary>Lucide icon name.</summary>
    public string? Icon { get; set; }

    /// <summary>Quiet shortcut hint such as "⌘,".</summary>
    public string? Shortcut { get; set; }

    public Action? Action { get; set; }
}

/// <summary>
/// The account menu behind an avatar: a compact identity header, a short group of destinations, an inline
/// light/dark/system switch, and a destructive sign out. The panel is hosted in the page (a reserved box
/// in demos) so it renders without a popup window. Up and Down move between rows, Left and Right choose a
/// theme, Enter and Space activate, Escape and Tab close and return focus to the avatar, and an outside
/// press closes it where the pointer went. The panel is a panel-radius surface with a hairline border and
/// the floating shadow.
/// </summary>
public sealed class ArcUserMenu : TemplatedControl
{
    public static readonly StyledProperty<string> UserNameProperty =
        AvaloniaProperty.Register<ArcUserMenu, string>(nameof(UserName), "Maya Chen");

    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<ArcUserMenu, string>(nameof(Email), "maya@acme.com");

    public static readonly StyledProperty<string?> PlanProperty =
        AvaloniaProperty.Register<ArcUserMenu, string?>(nameof(Plan));

    public static readonly StyledProperty<ArcUserMenuStatus> StatusProperty =
        AvaloniaProperty.Register<ArcUserMenu, ArcUserMenuStatus>(nameof(Status));

    public static readonly StyledProperty<bool> ShowNameProperty =
        AvaloniaProperty.Register<ArcUserMenu, bool>(nameof(ShowName));

    public static readonly StyledProperty<bool> ShowThemeProperty =
        AvaloniaProperty.Register<ArcUserMenu, bool>(nameof(ShowTheme), true);

    public static readonly StyledProperty<ArcUserMenuTheme> ThemePreferenceProperty =
        AvaloniaProperty.Register<ArcUserMenu, ArcUserMenuTheme>(nameof(ThemePreference), ArcUserMenuTheme.System);

    public static readonly StyledProperty<ArcUserMenuAlign> AlignProperty =
        AvaloniaProperty.Register<ArcUserMenu, ArcUserMenuAlign>(nameof(Align), ArcUserMenuAlign.End);

    public static readonly StyledProperty<IList<ArcUserMenuItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcUserMenu, IList<ArcUserMenuItem>?>(nameof(Items));

    public static readonly StyledProperty<Func<Task>?> OnSignOutProperty =
        AvaloniaProperty.Register<ArcUserMenu, Func<Task>?>(nameof(OnSignOut));

    public static readonly StyledProperty<string?> SignOutShortcutProperty =
        AvaloniaProperty.Register<ArcUserMenu, string?>(nameof(SignOutShortcut));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcUserMenu, bool>(nameof(IsOpen));

    private readonly List<Control> _stops = new();
    private readonly List<(ArcUserMenuTheme Theme, ArcButton Button, ArcIcon Icon)> _themeOptions = new();
    private readonly DispatcherTimer _hideTimer;
    private ArcButton? _trigger;
    private ArcIcon? _chevron;
    private TextBlock? _triggerName;
    private TextBlock? _initials;
    private Border? _presence;
    private Border? _presenceBar;
    private Border? _presenceHole;
    private TextBlock? _nameText;
    private TextBlock? _emailText;
    private TextBlock? _panelInitials;
    private Border? _planBadge;
    private TextBlock? _planText;
    private Canvas? _overlay;
    private Border? _panel;
    private Canvas? _highlightLayer;
    private ArcAdvancedGlide? _highlight;
    private StackPanel? _stopsHost;
    private Border? _themeThumb;
    private ArcButton? _signOutRow;
    private ArcIcon? _signOutIcon;
    private ArcSpinner? _signOutSpinner;
    private TextBlock? _signOutLabel;
    private Control? _focusedStop;
    private IDisposable? _dismiss;
    private bool _openRequestedWithFocus;
    private bool _openFirst;
    private bool _openLast;
    private bool _returnFocus;
    private bool _signingOut;

    static ArcUserMenu()
    {
        UserNameProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.UpdateIdentity());
        EmailProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.UpdateIdentity());
        PlanProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.UpdateIdentity());
        StatusProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.UpdatePresence());
        ShowNameProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.UpdateTriggerFace());
        ShowThemeProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.RebuildMenu());
        ThemePreferenceProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.ApplyThemeSelection());
        AlignProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.Position());
        ItemsProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.RebuildMenu());
        SignOutShortcutProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.RebuildMenu());
        IsOpenProperty.Changed.AddClassHandler<ArcUserMenu>((menu, _) => menu.SyncOpen());
    }

    public ArcUserMenu()
    {
        ClipToBounds = false;
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (_panel is not null && !IsOpen)
            {
                _panel.IsVisible = false;
            }
        };

        LayoutUpdated += (_, _) =>
        {
            if (IsOpen)
            {
                Position();
            }
        };
    }

    /// <summary>Raised with the chosen appearance; applying it to the page is up to the app.</summary>
    public event EventHandler<ArcUserMenuTheme>? ThemeChanged;

    public string UserName
    {
        get => GetValue(UserNameProperty);
        set => SetValue(UserNameProperty, value);
    }

    public string Email
    {
        get => GetValue(EmailProperty);
        set => SetValue(EmailProperty, value);
    }

    /// <summary>Optional plan or role, shown as a small accent badge beside the name.</summary>
    public string? Plan
    {
        get => GetValue(PlanProperty);
        set => SetValue(PlanProperty, value);
    }

    /// <summary>Presence dot on the avatar. Available, busy and away also differ in shape, not only colour.</summary>
    public ArcUserMenuStatus Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Shows the name and a chevron beside the avatar.</summary>
    public bool ShowName
    {
        get => GetValue(ShowNameProperty);
        set => SetValue(ShowNameProperty, value);
    }

    /// <summary>Shows the inline light, dark, and system switch.</summary>
    public bool ShowTheme
    {
        get => GetValue(ShowThemeProperty);
        set => SetValue(ShowThemeProperty, value);
    }

    /// <summary>The theme preference the switch reports.</summary>
    public ArcUserMenuTheme ThemePreference
    {
        get => GetValue(ThemePreferenceProperty);
        set => SetValue(ThemePreferenceProperty, value);
    }

    /// <summary>Panel alignment against the avatar.</summary>
    public ArcUserMenuAlign Align
    {
        get => GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    /// <summary>Account destinations; keep to three or four.</summary>
    public IList<ArcUserMenuItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Sign out handler. A returned promise keeps the row busy until it settles, then closes.</summary>
    public Func<Task>? OnSignOut
    {
        get => GetValue(OnSignOutProperty);
        set => SetValue(OnSignOutProperty, value);
    }

    public string? SignOutShortcut
    {
        get => GetValue(SignOutShortcutProperty);
        set => SetValue(SignOutShortcutProperty, value);
    }

    /// <summary>Whether the account panel is open.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _trigger = e.NameScope.Find<ArcButton>("Trigger");
        _chevron = e.NameScope.Find<ArcIcon>("Chevron");
        _triggerName = e.NameScope.Find<TextBlock>("TriggerName");
        _initials = e.NameScope.Find<TextBlock>("Initials");
        _presence = e.NameScope.Find<Border>("Presence");
        _presenceBar = e.NameScope.Find<Border>("PresenceBar");
        _presenceHole = e.NameScope.Find<Border>("PresenceHole");
        _nameText = e.NameScope.Find<TextBlock>("NameText");
        _panelInitials = e.NameScope.Find<TextBlock>("PanelInitials");
        _emailText = e.NameScope.Find<TextBlock>("EmailText");
        _planBadge = e.NameScope.Find<Border>("PlanBadge");
        _planText = e.NameScope.Find<TextBlock>("PlanText");
        _overlay = e.NameScope.Find<Canvas>("Overlay");
        _panel = e.NameScope.Find<Border>("Panel");
        _highlightLayer = e.NameScope.Find<Canvas>("HighlightLayer");
        _highlight = e.NameScope.Find<ArcAdvancedGlide>("Highlight");
        _stopsHost = e.NameScope.Find<StackPanel>("StopsHost");

        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.KeyDown += OnTriggerKeyDown;
            AutomationProperties.SetHelpText(_trigger, "Opens the account menu");
        }

        if (_panel is not null)
        {
            _panel.Focusable = true;
            _panel.IsTabStop = false;
            _panel.KeyDown += OnMenuKeyDown;
            _panel.PointerExited += (_, _) => HideHighlight();
            _panel.GotFocus += (_, args) =>
            {
                if (ReferenceEquals(args.Source, _panel))
                {
                    _focusedStop = null;
                }
            };
            AutomationProperties.SetName(_panel, "Account menu");
        }

        UpdateIdentity();
        UpdateTriggerFace();
        UpdatePresence();
        RebuildMenu();
        ApplyThemeSelection();

        if (IsOpen)
        {
            OpenPanel(focus: false, first: false, last: false);
        }
    }

    private void OnTriggerClick(object? sender, RoutedEventArgs e)
    {
        if (IsOpen)
        {
            _returnFocus = false;
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        _openRequestedWithFocus = true;
        _openFirst = false;
        _openLast = false;
        SetCurrentValue(IsOpenProperty, true);
    }

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                _openRequestedWithFocus = true;
                _openFirst = true;
                _openLast = false;
                SetCurrentValue(IsOpenProperty, true);
                e.Handled = true;
                break;
            case Key.Up:
                _openRequestedWithFocus = true;
                _openFirst = false;
                _openLast = true;
                SetCurrentValue(IsOpenProperty, true);
                e.Handled = true;
                break;
            case Key.Escape when IsOpen:
            case Key.Tab when IsOpen:
                CloseWithFocus();
                e.Handled = true;
                break;
        }
    }

    private void SyncOpen()
    {
        if (_panel is null)
        {
            return;
        }

        if (IsOpen)
        {
            OpenPanel(_openRequestedWithFocus, _openFirst, _openLast);
        }
        else
        {
            ClosePanel(_returnFocus);
        }

        _openRequestedWithFocus = false;
        _openFirst = false;
        _openLast = false;
        _returnFocus = false;
    }

    private void OpenPanel(bool focus, bool first, bool last)
    {
        if (_panel is null)
        {
            return;
        }

        _hideTimer.Stop();
        _dismiss?.Dispose();
        _dismiss = ArcAdvancedDismiss.Attach(this, CloseFromOutside, outsidePress: true, escape: CloseWithFocus);
        ConfigurePanelMotion();
        _panel.IsVisible = true;
        Position();
        _trigger?.Classes.Set("open", true);
        if (_chevron is not null)
        {
            ArcAdvancedVisuals.Rotate(_chevron, true);
        }

        if (ArcThemeManager.ReducedMotion)
        {
            _panel.Opacity = 1;
            _panel.RenderTransform = TransformOperations.Parse("scale(1)");
        }
        else
        {
            _panel.Opacity = 0;
            _panel.RenderTransform = TransformOperations.Parse("scale(0.94)");
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsOpen || _panel is null)
                {
                    return;
                }

                _panel.Opacity = 1;
                _panel.RenderTransform = TransformOperations.Parse("scale(1)");
            }, DispatcherPriority.Loaded);
        }

        if (focus)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsOpen || _stops.Count == 0)
                {
                    _panel?.Focus();
                    return;
                }

                if (first)
                {
                    FocusStop(0);
                }
                else if (last)
                {
                    FocusStop(_stops.Count - 1);
                }
                else
                {
                    _panel.Focus();
                }
            }, DispatcherPriority.Loaded);
        }
    }

    private void CloseFromOutside()
    {
        _returnFocus = false;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void CloseWithFocus()
    {
        _returnFocus = true;
        SetCurrentValue(IsOpenProperty, false);
    }

    private void ClosePanel(bool returnFocus)
    {
        if (_panel is null)
        {
            return;
        }

        _dismiss?.Dispose();
        _dismiss = null;
        _hideTimer.Stop();
        _trigger?.Classes.Set("open", false);
        if (_chevron is not null)
        {
            ArcAdvancedVisuals.Rotate(_chevron, false);
        }

        if (_highlight is not null)
        {
            _highlight.Hide();
        }

        if (ArcThemeManager.ReducedMotion)
        {
            _panel.IsVisible = false;
            _panel.Opacity = 0;
        }
        else
        {
            ConfigurePanelMotion();
            _panel.Opacity = 0;
            _panel.RenderTransform = TransformOperations.Parse("scale(0.97)");
            _hideTimer.Start();
        }

        if (returnFocus)
        {
            _trigger?.Focus();
        }
    }

    private void ConfigurePanelMotion()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.Transitions = ArcThemeManager.ReducedMotion
            ? null
            : new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                },
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                }
            };
    }

    private void Position()
    {
        if (_panel is null || _trigger is null || _overlay is null)
        {
            return;
        }

        var panelWidth = _panel.Bounds.Width > 0 ? _panel.Bounds.Width : _panel.Width;
        if (double.IsNaN(panelWidth) || panelWidth <= 0)
        {
            panelWidth = 248;
        }

        var panelHeight = _panel.Bounds.Height > 0 ? _panel.Bounds.Height : _panel.DesiredSize.Height;
        var triggerWidth = _trigger.Bounds.Width;
        var triggerHeight = _trigger.Bounds.Height > 0 ? _trigger.Bounds.Height : 40;

        var x = Align switch
        {
            ArcUserMenuAlign.Start => 0,
            ArcUserMenuAlign.Center => (triggerWidth - panelWidth) / 2,
            _ => triggerWidth - panelWidth
        };
        var y = triggerHeight + 8;

        _panel.RenderTransformOrigin = Align switch
        {
            ArcUserMenuAlign.Start => new RelativePoint(0, 0, RelativeUnit.Relative),
            ArcUserMenuAlign.Center => new RelativePoint(0.5, 0, RelativeUnit.Relative),
            _ => new RelativePoint(1, 0, RelativeUnit.Relative)
        };

        if (TopLevel.GetTopLevel(this) is { } top &&
            _trigger.TranslatePoint(new Point(0, 0), top) is { } origin)
        {
            if (origin.Y + y + panelHeight > top.Bounds.Height - 12 && origin.Y - 8 - panelHeight > 12)
            {
                y = -panelHeight - 8;
            }

            var absolute = origin.X + x;
            if (absolute < 12)
            {
                x += 12 - absolute;
            }

            absolute = origin.X + x;
            if (absolute + panelWidth > top.Bounds.Width - 12)
            {
                x -= absolute + panelWidth - (top.Bounds.Width - 12);
            }
        }

        Canvas.SetLeft(_panel, x);
        Canvas.SetTop(_panel, y);
    }

    private void UpdateIdentity()
    {
        var initials = ArcAdvancedVisuals.Initials(UserName);
        if (_initials is not null)
        {
            _initials.Text = initials;
        }

        if (_panelInitials is not null)
        {
            _panelInitials.Text = initials;
        }

        if (_nameText is not null)
        {
            _nameText.Text = UserName;
        }

        if (_emailText is not null)
        {
            _emailText.Text = Email;
            ToolTip.SetTip(_emailText, Email);
        }

        if (_planBadge is not null)
        {
            _planBadge.IsVisible = !string.IsNullOrWhiteSpace(Plan);
        }

        if (_planText is not null)
        {
            _planText.Text = Plan;
        }

        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, $"Account menu, {UserName}{StatusSuffix()}");
        }

        if (_panel is not null)
        {
            AutomationProperties.SetName(_panel, $"Account menu for {UserName}");
        }
    }

    private string StatusSuffix() => Status switch
    {
        ArcUserMenuStatus.Available => ", available",
        ArcUserMenuStatus.Busy => ", busy",
        ArcUserMenuStatus.Away => ", away",
        _ => string.Empty
    };

    private void UpdateTriggerFace()
    {
        if (_trigger is null)
        {
            return;
        }

        if (_triggerName is not null)
        {
            _triggerName.IsVisible = ShowName;
            _triggerName.Text = UserName;
        }

        if (_chevron is not null)
        {
            _chevron.IsVisible = ShowName;
        }

        _trigger.Padding = ShowName ? new Thickness(4, 0, 10, 0) : new Thickness(4);
    }

    private void UpdatePresence()
    {
        if (_presence is null)
        {
            return;
        }

        var visible = Status != ArcUserMenuStatus.None;
        _presence.IsVisible = visible;
        _presence.Classes.Set("status-available", Status == ArcUserMenuStatus.Available);
        _presence.Classes.Set("status-busy", Status == ArcUserMenuStatus.Busy);
        _presence.Classes.Set("status-away", Status == ArcUserMenuStatus.Away);
        if (_presenceBar is not null)
        {
            _presenceBar.IsVisible = Status == ArcUserMenuStatus.Busy;
        }

        if (_presenceHole is not null)
        {
            _presenceHole.IsVisible = Status == ArcUserMenuStatus.Away;
        }

        if (_trigger is not null)
        {
            AutomationProperties.SetName(_trigger, $"Account menu, {UserName}{StatusSuffix()}");
        }
    }

    private void ApplyThemeSelection()
    {
        if (_themeThumb is null)
        {
            return;
        }

        var index = ThemePreference switch
        {
            ArcUserMenuTheme.Light => 0,
            ArcUserMenuTheme.Dark => 1,
            _ => 2
        };

        if (!ArcThemeManager.ReducedMotion)
        {
            _themeThumb.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = ArcMotion.SpringSnappyDuration,
                    Easing = ArcMotion.SpringSnappy
                }
            };
        }
        else
        {
            _themeThumb.Transitions = null;
        }

        _themeThumb.RenderTransform = TransformOperations.Parse($"translateX({index * 28}px)");

        foreach (var option in _themeOptions)
        {
            var selected = option.Theme == ThemePreference;
            option.Button.Classes.Set("selected", selected);
            AutomationProperties.SetItemStatus(option.Button, selected ? "Selected" : string.Empty);
        }
    }

    private void OnThemeChosen(ArcUserMenuTheme theme)
    {
        SetCurrentValue(ThemePreferenceProperty, theme);
        ApplyThemeSelection();
        ThemeChanged?.Invoke(this, theme);
    }

    private void RebuildMenu()
    {
        if (_stopsHost is null)
        {
            return;
        }

        _highlight?.Hide();
        _stopsHost.Children.Clear();
        _stops.Clear();
        _themeOptions.Clear();
        _themeThumb = null;
        _signOutRow = null;
        _signOutIcon = null;
        _signOutSpinner = null;
        _signOutLabel = null;
        _focusedStop = null;

        var items = Items?.ToList() ?? new List<ArcUserMenuItem>();
        foreach (var item in items)
        {
            _stopsHost.Children.Add(BuildRow(item));
        }

        if (items.Count > 0)
        {
            _stopsHost.Children.Add(Separator());
        }

        if (ShowTheme)
        {
            _stopsHost.Children.Add(BuildThemeRow());
            _stopsHost.Children.Add(Separator());
        }

        var signOut = BuildSignOutRow();
        _stopsHost.Children.Add(signOut);

        ApplyThemeSelection();
    }

    private static Border Separator() => new()
    {
        Classes = { "user-menu-separator" }
    };

    private ArcButton CreateRowBase(bool danger)
    {
        var row = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = 32,
            MinHeight = 32,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(8, 0),
            CornerRadius = ArcAdvancedVisuals.Radius(ArcTokens.RadiusControl),
            FontSize = 13,
            FontWeight = FontWeight.Normal,
            IsTabStop = false,
            Focusable = true
        };
        row.Classes.Add("user-menu-row");
        if (danger)
        {
            row.Classes.Add("danger");
        }

        return row;
    }

    private void HookRow(ArcButton row)
    {
        _stops.Add(row);
        row.GotFocus += (_, _) =>
        {
            _focusedStop = row;
            ShowHighlight(row);
        };
        row.PointerEntered += (_, _) => ShowHighlight(row);
    }

    private ArcButton BuildRow(ArcUserMenuItem item)
    {
        var row = CreateRowBase(danger: false);
        HookRow(row);
        var label = item.Label ?? "Action";
        ArcAdvancedVisuals.SetName(row, label);

        var icon = ArcAdvancedVisuals.InheritIcon(item.Icon, row, 16);
        icon.Margin = new Thickness(0, 0, 10, 0);
        var text = ArcAdvancedVisuals.InheritText(label, row, 13, medium: false);
        text.HorizontalAlignment = HorizontalAlignment.Stretch;
        var shortcut = BuildShortcut(item.Shortcut);
        row.Content = RowGrid(icon, text, shortcut);

        row.Click += (_, _) =>
        {
            CloseWithFocus();
            item.Action?.Invoke();
        };
        return row;
    }

    private ArcButton BuildSignOutRow()
    {
        var row = CreateRowBase(danger: true);
        HookRow(row);
        _signOutRow = row;
        AutomationProperties.SetName(row, "Sign out");

        _signOutIcon = ArcAdvancedVisuals.InheritIcon("log-out", row, 16);
        _signOutSpinner = new ArcSpinner { Width = 16, Height = 16, StrokeWidth = 2, IsVisible = false };
        _signOutSpinner.Bind(ArcSpinner.StrokeProperty, new Binding("Foreground") { Source = row });
        var iconHost = new Panel
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        iconHost.Children.Add(_signOutIcon);
        iconHost.Children.Add(_signOutSpinner);

        _signOutLabel = ArcAdvancedVisuals.InheritText("Sign out", row, 13, medium: false);
        _signOutLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
        row.Content = RowGrid(iconHost, _signOutLabel, BuildShortcut(SignOutShortcut));
        row.Click += (_, _) => _ = SignOutAsync();
        return row;
    }

    private Control BuildThemeRow()
    {
        var row = new Grid
        {
            Height = 36,
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Background = Brushes.Transparent
        };
        row.PointerEntered += (_, _) => HideHighlight();

        var icon = ArcAdvancedVisuals.Icon("sun", 16);
        icon.Classes.Add("user-menu-theme-icon");
        icon.Margin = new Thickness(8, 0, 10, 0);
        Grid.SetColumn(icon, 0);
        row.Children.Add(icon);

        var label = new TextBlock
        {
            Text = "Theme",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "user-menu-theme-label" }
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        var thumb = new Border
        {
            Width = 28,
            Height = 22,
            CornerRadius = new CornerRadius(6),
            Classes = { "user-menu-thumb" },
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        };

        var grid = new Grid
        {
            Width = 84,
            Height = 22,
            ColumnDefinitions = new ColumnDefinitions("28,28,28")
        };
        grid.Children.Add(thumb);

        var options = new[]
        {
            (ArcUserMenuTheme.Light, "sun", "Light theme"),
            (ArcUserMenuTheme.Dark, "moon", "Dark theme"),
            (ArcUserMenuTheme.System, "eye", "System theme")
        };

        for (var i = 0; i < options.Length; i++)
        {
            var (theme, iconName, accessible) = options[i];
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Width = 28,
                Height = 22,
                MinWidth = 28,
                MinHeight = 22,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                IsTabStop = false,
                Focusable = true
            };
            button.Classes.Add("user-menu-segment");
            button.Content = ArcAdvancedVisuals.InheritIcon(iconName, button, 14);
            ArcAdvancedVisuals.SetName(button, accessible);
            Grid.SetColumn(button, i);
            grid.Children.Add(button);
            button.Click += (_, _) => OnThemeChosen(theme);
            button.GotFocus += (_, _) =>
            {
                _focusedStop = button;
                HideHighlight();
            };
            button.PointerEntered += (_, _) => HideHighlight();
            _themeOptions.Add((theme, button, (ArcIcon)button.Content));
            _stops.Add(button);
        }

        var segments = new Border
        {
            Height = 26,
            Padding = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Classes = { "user-menu-segments" },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            Child = grid
        };
        Grid.SetColumn(segments, 2);
        row.Children.Add(segments);
        _themeThumb = thumb;
        return row;
    }

    private static Control RowGrid(Control icon, Control label, Control? shortcut)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);
        if (shortcut is not null)
        {
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        return grid;
    }

    private static TextBlock? BuildShortcut(string? shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
        {
            return null;
        }

        return new TextBlock
        {
            Text = shortcut,
            Classes = { "arc-caption" },
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 2, 0)
        };
    }

    private async Task SignOutAsync()
    {
        if (_signingOut)
        {
            return;
        }

        if (OnSignOut is null)
        {
            CloseWithFocus();
            return;
        }

        _signingOut = true;
        UpdateSignOutFace();
        try
        {
            await OnSignOut().ConfigureAwait(true);
        }
        catch
        {
            // The menu closes once the handler settles, whether it succeeded or not.
        }
        finally
        {
            _signingOut = false;
            UpdateSignOutFace();
        }

        CloseWithFocus();
    }

    private void UpdateSignOutFace()
    {
        if (_signOutIcon is not null)
        {
            _signOutIcon.IsVisible = !_signingOut;
        }

        if (_signOutSpinner is not null)
        {
            _signOutSpinner.IsVisible = _signingOut;
        }

        if (_signOutLabel is not null)
        {
            _signOutLabel.Text = _signingOut ? "Signing out" : "Sign out";
        }

        if (_signOutRow is not null)
        {
            AutomationProperties.SetName(_signOutRow, _signingOut ? "Signing out" : "Sign out");
            AutomationProperties.SetItemStatus(_signOutRow, _signingOut ? "Busy" : string.Empty);
        }
    }

    private void ShowHighlight(Control stop)
    {
        if (_highlight is null || _highlightLayer is null || stop.Bounds.Width <= 0)
        {
            return;
        }

        var origin = stop.TranslatePoint(new Point(0, 0), _highlightLayer);
        if (origin is null)
        {
            return;
        }

        _highlight.Danger = stop.Classes.Contains("danger");
        _highlight.ShowAt(new Rect(origin.Value, stop.Bounds.Size), grow: true);
    }

    private void HideHighlight() => _highlight?.Hide();

    private void FocusStop(int index)
    {
        if (index < 0 || index >= _stops.Count)
        {
            return;
        }

        _stops[index].Focus();
    }

    private void OnMenuKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveStop(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveStop(-1);
                e.Handled = true;
                break;
            case Key.Home:
                MoveStop(int.MinValue);
                e.Handled = true;
                break;
            case Key.End:
                MoveStop(int.MaxValue);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Right:
                MoveTheme(e.Key == Key.Right ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Escape:
            case Key.Tab:
                CloseWithFocus();
                e.Handled = true;
                break;
        }
    }

    private void MoveStop(int delta)
    {
        if (_stops.Count == 0)
        {
            return;
        }

        if (delta == int.MinValue)
        {
            FocusStop(0);
            return;
        }

        if (delta == int.MaxValue)
        {
            FocusStop(_stops.Count - 1);
            return;
        }

        var index = _focusedStop is null ? -1 : _stops.IndexOf(_focusedStop);
        if (index < 0)
        {
            FocusStop(delta > 0 ? 0 : _stops.Count - 1);
            return;
        }

        FocusStop((index + delta + _stops.Count) % _stops.Count);
    }

    private void MoveTheme(int delta)
    {
        if (_themeOptions.Count == 0)
        {
            return;
        }

        var index = _focusedStop is null
            ? -1
            : _themeOptions.FindIndex(option => ReferenceEquals(option.Button, _focusedStop));
        if (index < 0)
        {
            return;
        }

        var next = (index + delta + _themeOptions.Count) % _themeOptions.Count;
        var option = _themeOptions[next];
        OnThemeChosen(option.Theme);
        option.Button.Focus();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _dismiss?.Dispose();
        _dismiss = null;
        _hideTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }
}
