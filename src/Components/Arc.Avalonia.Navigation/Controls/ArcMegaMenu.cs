using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Navigation.Controls;

/// <summary>One destination inside a mega menu group.</summary>
public sealed class ArcMegaMenuLink
{
    public string? Label { get; set; }

    /// <summary>One line under the label; keep it to five or six words.</summary>
    public string? Description { get; set; }

    /// <summary>Lucide icon name.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional badge such as "New".</summary>
    public string? Badge { get; set; }

    public string? Href { get; set; }

    /// <summary>Invoked when the link is chosen.</summary>
    public Action? Action { get; set; }
}

/// <summary>A column of links with an optional quiet heading.</summary>
public sealed class ArcMegaMenuGroup
{
    public string? Title { get; set; }

    public IList<ArcMegaMenuLink> Links { get; set; } = new List<ArcMegaMenuLink>();
}

/// <summary>The featured story beside a menu's groups.</summary>
public sealed class ArcMegaMenuFeature
{
    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>Call to action text, for example "Read the announcement".</summary>
    public string? Cta { get; set; }

    /// <summary>Lucide icon name shown above the title; the web original uses media.</summary>
    public string? Icon { get; set; }

    public string? Href { get; set; }

    public Action? Action { get; set; }
}

/// <summary>A section of the header. Sections with groups or a feature open a menu; the rest are links.</summary>
public sealed class ArcMegaMenuSection
{
    /// <summary>Stable value used by <see cref="ArcMegaMenu.ActiveSection"/>.</summary>
    public string? Value { get; set; }

    public string? Label { get; set; }

    public string? Href { get; set; }

    public IList<ArcMegaMenuGroup>? Groups { get; set; }

    public ArcMegaMenuFeature? Feature { get; set; }

    /// <summary>One quiet status line on the muted band at the bottom of the menu.</summary>
    public string? Footer { get; set; }

    /// <summary>Invoked for a section that has no menu of its own.</summary>
    public Action? Action { get; set; }
}

public enum ArcMegaMenuActionVariant
{
    Primary,
    Quiet
}

/// <summary>An action at the end of the header. Quiet actions move into the folded sheet.</summary>
public sealed class ArcMegaMenuAction
{
    public string? Label { get; set; }

    public ArcMegaMenuActionVariant Variant { get; set; } = ArcMegaMenuActionVariant.Quiet;

    public string? Href { get; set; }

    public Action? Action { get; set; }
}

/// <summary>What was chosen: label, optional destination, and the section it came from.</summary>
public sealed class ArcMegaMenuDestination
{
    public string? Label { get; set; }

    public string? Href { get; set; }

    public string? Section { get; set; }
}

public sealed class ArcMegaMenuInvokedEventArgs : EventArgs
{
    public ArcMegaMenuInvokedEventArgs(ArcMegaMenuDestination destination) => Destination = destination;

    public ArcMegaMenuDestination Destination { get; }
}

/// <summary>
/// A site header whose sections share one floating menu. Moving between sections morphs the panel's size
/// and position, slides the next menu in from the side you came from on the smooth spring, and glides the
/// accent caret under the active trigger on the morph spring; the muted bar highlight follows the pointer.
/// Hover intent (open delay, close delay) and a safe triangle keep a diagonal move toward the open panel
/// from switching sections. Under <see cref="ForceNarrow"/>, or when the header is narrower than its own
/// row, the sections fold into an accordion sheet with one open section at a time.
///
/// The panel and the sheet live in the header's own overlay canvas, so they render in the page and in
/// screenshots without a popup window. Approximations of the public description: the web original cross
/// fades menus with a 2 px blur and springs independent width/height values with retargeting; Avalonia has
/// no animatable blur radius, so the crossfade is opacity plus a 36 px slide, and the surface morphs on the
/// shared smooth spring (no overshoot on growth). The footer band travels with the incoming menu instead of
/// sprouting as a separately sprung element, and the development-only safe area overlay is not drawn. The
/// folded sheet sizes itself from its laid out content, so a section opens in one step rather than growing
/// its height rows one by one. No Pro source was consulted.
/// </summary>
public sealed class ArcMegaMenu : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcMegaMenuSection>?> SectionsProperty =
        AvaloniaProperty.Register<ArcMegaMenu, IList<ArcMegaMenuSection>?>(nameof(Sections));

    public static readonly StyledProperty<string?> BrandProperty =
        AvaloniaProperty.Register<ArcMegaMenu, string?>(nameof(Brand));

    public static readonly StyledProperty<string?> BrandIconProperty =
        AvaloniaProperty.Register<ArcMegaMenu, string?>(nameof(BrandIcon));

    public static readonly StyledProperty<IList<ArcMegaMenuAction>?> ActionsProperty =
        AvaloniaProperty.Register<ArcMegaMenu, IList<ArcMegaMenuAction>?>(nameof(Actions));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcMegaMenu, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string?> ActiveSectionProperty =
        AvaloniaProperty.Register<ArcMegaMenu, string?>(nameof(ActiveSection));

    public static readonly StyledProperty<string?> CurrentProperty =
        AvaloniaProperty.Register<ArcMegaMenu, string?>(nameof(Current));

    /// <summary>Milliseconds the pointer rests on a trigger before the first menu opens.</summary>
    public static readonly StyledProperty<int> OpenDelayProperty =
        AvaloniaProperty.Register<ArcMegaMenu, int>(nameof(OpenDelay), 90);

    /// <summary>Grace period after the pointer leaves the header and menu before they close.</summary>
    public static readonly StyledProperty<int> CloseDelayProperty =
        AvaloniaProperty.Register<ArcMegaMenu, int>(nameof(CloseDelay), 240);

    /// <summary>
    /// Width in px below which the sections fold into the accordion sheet. 0 measures the natural width of
    /// the full row instead, which matches the web component's default.
    /// </summary>
    public static readonly StyledProperty<double> CollapseBelowProperty =
        AvaloniaProperty.Register<ArcMegaMenu, double>(nameof(CollapseBelow));

    /// <summary>Forces the folded accordion layout, for demos and narrow hosts.</summary>
    public static readonly StyledProperty<bool> ForceNarrowProperty =
        AvaloniaProperty.Register<ArcMegaMenu, bool>(nameof(ForceNarrow));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<ArcMegaMenu, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcMegaMenu, string>(nameof(Label), "Main");

    private const double PanelInset = 8;
    private const double PanelGap = 8;
    private const double SheetMaxHeight = 460;

    private readonly List<ArcButton> _triggerButtons = new();
    private readonly List<ArcIcon?> _chevrons = new();
    private readonly List<Control> _linkRows = new();
    private readonly List<ArcButton> _sheetHeaders = new();
    private readonly List<Border> _sheetBodies = new();
    private readonly List<ArcIcon> _sheetChevrons = new();
    private readonly DispatcherTimer _openTimer;
    private readonly DispatcherTimer _closeTimer;
    private readonly DispatcherTimer _hideTimer;
    private readonly DispatcherTimer _outgoingTimer;
    private readonly DispatcherTimer _sheetHideTimer;

    private Border? _bar;
    private ContentControl? _brandHost;
    private Panel? _triggerZone;
    private StackPanel? _triggers;
    private StackPanel? _actionsHost;
    private ArcButton? _menuButton;
    private Canvas? _overlay;
    private ArcGlide? _barHighlight;
    private ArcGlide? _caret;
    private Border? _panel;
    private Canvas? _panelHost;
    private Control? _body;
    private Control? _outgoing;
    private Border? _sheet;
    private ScrollViewer? _sheetScroll;
    private StackPanel? _sheetStack;

    private int _activeIndex = -1;
    private int _shownIndex = -1;
    private int _hoverIndex = -1;
    private int _pendingIndex = -1;
    private int _currentIndex = -1;
    private int _sheetOpenIndex = -1;
    private bool _narrow;
    private bool _syncing;
    private bool _built;
    private double _panelX;
    private double _panelTargetWidth;
    private double _panelTargetHeight;
    private double _panelScale = 1;
    private double _panelLift;
    private double _naturalWidth;
    private double _appliedWidth = -1;
    private double _availableWidth = -1;
    private Point? _lastExitPoint;
    private IDisposable? _dismiss;

    static ArcMegaMenu()
    {
        SectionsProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.RebuildHeader());
        BrandProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.RebuildBrand());
        BrandIconProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.RebuildBrand());
        ActionsProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.RebuildActions());
        CurrentProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.ApplyCurrent());
        ForceNarrowProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.UpdateMode(force: true));
        CollapseBelowProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.UpdateMode(force: true));
        IsOpenProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.SyncOpen());
        ActiveSectionProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.SyncSection());
        LabelProperty.Changed.AddClassHandler<ArcMegaMenu>((menu, _) => menu.UpdateLabel());
    }

    public ArcMegaMenu()
    {
        ClipToBounds = false;

        _openTimer = new DispatcherTimer();
        _openTimer.Tick += (_, _) =>
        {
            _openTimer.Stop();
            if (!IsOpen && !_narrow && _pendingIndex >= 0 && _pendingIndex == _hoverIndex)
            {
                RequestOpen(_pendingIndex);
            }
        };

        _closeTimer = new DispatcherTimer();
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            if (IsOpen)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
        };

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            if (_panel is not null && !IsOpen)
            {
                _panel.IsVisible = false;
            }
        };

        _outgoingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(480) };
        _outgoingTimer.Tick += (_, _) =>
        {
            _outgoingTimer.Stop();
            if (_outgoing is not null && _panelHost is not null)
            {
                _panelHost.Children.Remove(_outgoing);
            }

            _outgoing = null;
        };

        _sheetHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _sheetHideTimer.Tick += (_, _) =>
        {
            _sheetHideTimer.Stop();
            if (_sheet is not null && !IsOpen)
            {
                _sheet.IsVisible = false;
            }
        };

        PointerExited += OnPointerExitedRoot;

        LayoutUpdated += (_, _) =>
        {
            if (Math.Abs(Bounds.Width - _appliedWidth) > 0.5)
            {
                UpdateMode(force: false);
            }

            if (_sheet is { IsVisible: true } && _sheetStack is not null)
            {
                var target = Math.Clamp(_sheetStack.DesiredSize.Height, 60, SheetMaxHeight);
                if (Math.Abs(target - _sheet.Height) > 0.5)
                {
                    _sheet.Height = target;
                    if (_sheetScroll is not null)
                    {
                        _sheetScroll.Height = target;
                    }
                }
            }

            if (IsOpen && !_narrow && _panel is { IsVisible: true })
            {
                var available = MeasureAvailableWidth();
                if (Math.Abs(available - _availableWidth) > 1)
                {
                    _availableWidth = available;
                    OpenPanel(animate: false);
                }
                else
                {
                    // The surface follows the body once the templated rows have their real height.
                    if (_body is not null && _body.Bounds.Height > 1 &&
                        Math.Abs(_body.Bounds.Height - _panelTargetHeight) > 1)
                    {
                        _panelTargetHeight = _body.Bounds.Height;
                        _panel.Height = _body.Bounds.Height;
                    }

                    PositionPanel();
                }
            }
        };
    }

    /// <summary>Raised with the open section's value, or null when every menu closes.</summary>
    public event EventHandler<string?>? SectionChanged;

    /// <summary>Raised for every destination: links, feature cards, plain sections, actions and the brand.</summary>
    public event EventHandler<ArcMegaMenuInvokedEventArgs>? ItemInvoked;

    public IList<ArcMegaMenuSection>? Sections
    {
        get => GetValue(SectionsProperty);
        set => SetValue(SectionsProperty, value);
    }

    /// <summary>Mark and name at the start of the header.</summary>
    public string? Brand
    {
        get => GetValue(BrandProperty);
        set => SetValue(BrandProperty, value);
    }

    /// <summary>Lucide icon used as the brand mark.</summary>
    public string? BrandIcon
    {
        get => GetValue(BrandIconProperty);
        set => SetValue(BrandIconProperty, value);
    }

    /// <summary>Actions at the end of the header.</summary>
    public IList<ArcMegaMenuAction>? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Whether a menu (or the folded sheet) is showing.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The open section in the wide layout, or the expanded accordion section when folded.</summary>
    public string? ActiveSection
    {
        get => GetValue(ActiveSectionProperty);
        set => SetValue(ActiveSectionProperty, value);
    }

    /// <summary>Value of the section that holds the current page; its trigger carries the accent caret at rest.</summary>
    public string? Current
    {
        get => GetValue(CurrentProperty);
        set => SetValue(CurrentProperty, value);
    }

    public int OpenDelay
    {
        get => GetValue(OpenDelayProperty);
        set => SetValue(OpenDelayProperty, value);
    }

    public int CloseDelay
    {
        get => GetValue(CloseDelayProperty);
        set => SetValue(CloseDelayProperty, value);
    }

    public double CollapseBelow
    {
        get => GetValue(CollapseBelowProperty);
        set => SetValue(CollapseBelowProperty, value);
    }

    public bool ForceNarrow
    {
        get => GetValue(ForceNarrowProperty);
        set => SetValue(ForceNarrowProperty, value);
    }

    /// <summary>Whether the header is currently folded; measured from the header's own width.</summary>
    public bool IsNarrow
    {
        get => GetValue(IsNarrowProperty);
        private set => SetValue(IsNarrowProperty, value);
    }

    /// <summary>Accessible name of the navigation landmark.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _bar = e.NameScope.Find<Border>("Bar");
        _brandHost = e.NameScope.Find<ContentControl>("BrandHost");
        _triggerZone = e.NameScope.Find<Panel>("TriggerZone");
        _triggers = e.NameScope.Find<StackPanel>("Triggers");
        _actionsHost = e.NameScope.Find<StackPanel>("ActionsHost");
        _menuButton = e.NameScope.Find<ArcButton>("MenuButton");
        _overlay = e.NameScope.Find<Canvas>("Overlay");
        _barHighlight = e.NameScope.Find<ArcGlide>("BarHighlight");
        _caret = e.NameScope.Find<ArcGlide>("Caret");

        if (_menuButton is not null)
        {
            _menuButton.Click += (_, _) => ToggleSheet();
        }

        if (_bodiesBuilt)
        {
            return;
        }

        _bodiesBuilt = true;
        BuildPanel();
        UpdateLabel();
        RebuildBrand();
        RebuildHeader();
        RebuildActions();
        UpdateMode(force: true);
        _built = true;
        SyncOpen();
    }

    private bool _bodiesBuilt;

    private void BuildPanel()
    {
        _panel = new Border
        {
            Classes = { "mm-panel" },
            IsVisible = false,
            Opacity = 0,
            ClipToBounds = true,
            RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative)
        };
        AutomationProperties.SetName(_panel, Label + " menu");

        _panelHost = new Canvas { ClipToBounds = true };
        _panel.Child = _panelHost;
        _panel.PointerEntered += OnPointerEnteredPanel;
        _panel.PointerExited += OnPointerExitedPanel;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _openTimer.Stop();
        _closeTimer.Stop();
        _hideTimer.Stop();
        _outgoingTimer.Stop();
        _sheetHideTimer.Stop();
        _dismiss?.Dispose();
        _dismiss = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, Label);
        if (_panel is not null)
        {
            AutomationProperties.SetName(_panel, Label + " menu");
        }

        if (_sheet is not null)
        {
            AutomationProperties.SetName(_sheet, Label + " menu");
        }
    }

    private List<ArcMegaMenuSection> SectionList() => Sections?.ToList() ?? new List<ArcMegaMenuSection>();

    private ArcMegaMenuSection? SectionAt(int index)
    {
        var list = SectionList();
        return index >= 0 && index < list.Count ? list[index] : null;
    }

    private static bool HasMenu(ArcMegaMenuSection? section) =>
        section is not null && ((section.Groups?.Count ?? 0) > 0 || section.Feature is not null);

    private int IndexOfValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return -1;
        }

        var list = SectionList();
        for (var i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Value, value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private int FirstMenuIndex()
    {
        var list = SectionList();
        for (var i = 0; i < list.Count; i++)
        {
            if (HasMenu(list[i]))
            {
                return i;
            }
        }

        return -1;
    }

    // ---------------------------------------------------------------- header

    private void RebuildBrand()
    {
        if (_brandHost is null)
        {
            return;
        }

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Padding = new Thickness(6, 0, 10, 0),
            Height = 38,
            MinHeight = 38,
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Center
        };
        button.Classes.Add("mm-brand");

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 9,
            VerticalAlignment = VerticalAlignment.Center
        };

        var mark = new Border
        {
            Width = 26,
            Height = 26,
            CornerRadius = new CornerRadius(9),
            VerticalAlignment = VerticalAlignment.Center
        };
        mark.Classes.Add("mm-brand-mark");
        var icon = new ArcIcon
        {
            Icon = string.IsNullOrWhiteSpace(BrandIcon) ? "layers" : BrandIcon,
            Size = 15,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        icon.Classes.Add("mm-brand-mark-icon");
        mark.Child = icon;
        content.Children.Add(mark);

        var name = new TextBlock
        {
            Text = Brand ?? "Arc",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        content.Children.Add(name);

        button.Content = content;
        name.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = button });
        ArcActionVisuals.SetName(button, Brand ?? "Home");
        AutomationProperties.SetHelpText(button, "Goes to the home page");
        button.Click += (_, _) => Invoke(new ArcMegaMenuDestination { Label = Brand }, null);

        _brandHost.Content = button;
    }

    private void RebuildHeader()
    {
        if (_triggers is null)
        {
            return;
        }

        _triggers.Children.Clear();
        _triggerButtons.Clear();
        _chevrons.Clear();
        _activeIndex = -1;
        _shownIndex = -1;
        _currentIndex = IndexOfValue(Current);
        var wasOpen = IsOpen;

        if (_sheet is not null)
        {
            _overlay?.Children.Remove(_sheet);
            _sheet = null;
            _sheetStack = null;
            _sheetScroll = null;
            _sheetHeaders.Clear();
            _sheetBodies.Clear();
            _sheetChevrons.Clear();
            _sheetOpenIndex = -1;
        }

        var list = SectionList();
        for (var i = 0; i < list.Count; i++)
        {
            var section = list[i];
            var index = i;
            var hasMenu = HasMenu(section);

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new TextBlock
            {
                Text = section.Label ?? "Section",
                FontWeight = FontWeight.Medium,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(label);

            ArcIcon? chevron = null;
            if (hasMenu)
            {
                chevron = new ArcIcon
                {
                    Icon = "chevron-down",
                    Size = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                chevron.Classes.Add("mm-chevron");
                row.Children.Add(chevron);
            }

            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Height = 38,
                MinHeight = 38,
                Padding = new Thickness(11, 0),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(1, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = row,
                Focusable = true,
                IsTabStop = true
            };
            button.Classes.Add("mm-trigger");
            button.Classes.Set("link", !hasMenu);
            label.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = button });

            ArcActionVisuals.SetName(button, section.Label ?? "Section");
            AutomationProperties.SetHelpText(button, hasMenu ? "Opens a menu" : "Goes to this page");

            button.Click += (_, _) => OnTriggerClick(index);
            button.KeyDown += (_, args) => OnTriggerKeyDown(index, args);
            button.PointerEntered += (_, _) => OnTriggerEnter(index);
            button.PointerExited += (_, args) => OnTriggerLeave(index, args);
            button.GotFocus += (_, _) => ShowBarHighlight(index);

            _triggerButtons.Add(button);
            _chevrons.Add(chevron);
            _triggers.Children.Add(button);
        }

        ApplyCurrent();
        UpdateCaretForRest();

        if (wasOpen && _built)
        {
            SyncOpen();
        }
    }

    private void RebuildActions()
    {
        if (_actionsHost is null)
        {
            return;
        }

        _actionsHost.Children.Clear();
        var actions = Actions?.ToList() ?? new List<ArcMegaMenuAction>();
        foreach (var action in actions)
        {
            var primary = action.Variant == ArcMegaMenuActionVariant.Primary;
            var button = new ArcButton
            {
                Variant = primary ? ArcButtonVariant.Primary : ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Content = action.Label ?? "Action",
                Height = 36,
                MinHeight = 36,
                VerticalAlignment = VerticalAlignment.Center
            };
            button.Classes.Add("mm-action");
            button.Classes.Set("quiet", !primary);
            ArcActionVisuals.SetName(button, action.Label ?? "Action");
            button.Click += (_, _) =>
            {
                Invoke(new ArcMegaMenuDestination { Label = action.Label, Href = action.Href }, null);
                action.Action?.Invoke();
                SetCurrentValue(IsOpenProperty, false);
            };
            button.IsVisible = !_narrow || primary;
            _actionsHost.Children.Add(button);
        }
    }

    private void ApplyCurrent()
    {
        _currentIndex = IndexOfValue(Current);
        var list = SectionList();
        for (var i = 0; i < _triggerButtons.Count && i < list.Count; i++)
        {
            var current = i == _currentIndex;
            _triggerButtons[i].Classes.Set("current", current);
            var name = list[i].Label ?? "Section";
            ArcActionVisuals.SetName(_triggerButtons[i], current ? name + ", current page" : name);
            AutomationProperties.SetItemStatus(_triggerButtons[i], current ? "Current page" : string.Empty);
        }

        UpdateCaretForRest();
    }

    // ---------------------------------------------------------------- folded mode

    private void UpdateMode(bool force)
    {
        var width = Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        _appliedWidth = width;

        if (!_narrow)
        {
            var natural = MeasureNaturalWidth();
            if (natural > 0)
            {
                _naturalWidth = natural;
            }
        }

        bool narrow;
        if (ForceNarrow)
        {
            narrow = true;
        }
        else if (CollapseBelow > 0)
        {
            narrow = width < CollapseBelow;
        }
        else
        {
            narrow = _naturalWidth > 0 && width < _naturalWidth;
        }

        if (narrow == _narrow)
        {
            if (force)
            {
                RebuildActions();
            }

            return;
        }

        SetNarrow(narrow);
    }

    private double MeasureNaturalWidth()
    {
        if (_bar is null || _brandHost is null || _triggers is null || _actionsHost is null)
        {
            return 0;
        }

        _brandHost.Measure(Size.Infinity);
        var brand = _brandHost.DesiredSize.Width;

        var triggers = 0d;
        foreach (var trigger in _triggerButtons)
        {
            trigger.Measure(Size.Infinity);
            triggers += trigger.DesiredSize.Width + trigger.Margin.Left + trigger.Margin.Right;
        }

        var actions = 0d;
        foreach (var child in _actionsHost.Children)
        {
            if (child is Control { IsVisible: true } control)
            {
                control.Measure(Size.Infinity);
                actions += control.DesiredSize.Width + 8;
            }
        }

        var padding = _bar.Padding.Left + _bar.Padding.Right;
        return brand + triggers + actions + padding + 32;
    }

    private void SetNarrow(bool narrow)
    {
        _narrow = narrow;
        IsNarrow = narrow;

        if (_triggerZone is not null)
        {
            _triggerZone.IsVisible = !narrow;
        }

        if (_menuButton is not null)
        {
            _menuButton.IsVisible = narrow;
            ArcActionVisuals.SetName(_menuButton, "Open menu");
            AutomationProperties.SetHelpText(_menuButton, "Shows the sections as an accordion sheet");
        }

        _barHighlight?.Hide(animate: false);
        _caret?.Hide(animate: false);

        if (narrow)
        {
            // Keep IsOpen: the same state now shows the folded sheet.
            ClosePanel(returnFocus: false);
        }
        else
        {
            CloseSheet(returnFocus: false);
        }

        RebuildActions();

        Dispatcher.UIThread.Post(() =>
        {
            if (_built)
            {
                SyncOpen();
            }
        }, DispatcherPriority.Loaded);
    }

    // ---------------------------------------------------------------- open state

    private void SyncOpen()
    {
        if (!_built)
        {
            return;
        }

        if (IsOpen)
        {
            if (_narrow)
            {
                OpenSheet();
            }
            else
            {
                OpenDefault();
            }
        }
        else
        {
            ClosePanel(returnFocus: false);
            CloseSheet(returnFocus: false);
        }
    }

    private void SyncSection()
    {
        if (!_built || _syncing)
        {
            return;
        }

        var index = IndexOfValue(ActiveSection);
        if (index < 0)
        {
            return;
        }

        if (_narrow)
        {
            if (IsOpen)
            {
                ExpandSheetSection(index, animate: true);
            }
        }
        else if (IsOpen)
        {
            RequestOpen(index);
        }
        else
        {
            UpdateCaretForRest();
        }
    }

    private void OpenDefault()
    {
        var index = IndexOfValue(ActiveSection);
        if (index < 0 || !HasMenu(SectionAt(index)))
        {
            index = FirstMenuIndex();
        }

        if (index >= 0 && HasMenu(SectionAt(index)))
        {
            OpenPanelFor(index, animate: true);
        }
    }

    private void RequestOpen(int index)
    {
        var section = SectionAt(index);
        if (section is null)
        {
            return;
        }

        if (!HasMenu(section))
        {
            Invoke(new ArcMegaMenuDestination { Label = section.Label, Href = section.Href, Section = section.Value }, section.Action);
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        if (!IsOpen)
        {
            SetCurrentValue(ActiveSectionProperty, section.Value);
            SetCurrentValue(IsOpenProperty, true);
            return;
        }

        if (_activeIndex == index)
        {
            return;
        }

        OpenPanelFor(index, animate: true);
    }

    private void OpenPanelFor(int index, bool animate)
    {
        var section = SectionAt(index);
        if (section is null || !HasMenu(section))
        {
            return;
        }

        _syncing = true;
        _activeIndex = index;
        SetCurrentValue(ActiveSectionProperty, section.Value);
        _syncing = false;

        OpenPanel(animate);
    }

    private void OpenPanel(bool animate)
    {
        if (_panel is null || _panelHost is null || _overlay is null || _activeIndex < 0)
        {
            return;
        }

        var section = SectionAt(_activeIndex);
        if (!HasMenu(section))
        {
            return;
        }

        if (!_overlay.Children.Contains(_panel))
        {
            _overlay.Children.Add(_panel);
        }

        var first = _shownIndex < 0 || !_panel.IsVisible;
        var direction = first ? 0 : Math.Sign(_activeIndex - _shownIndex);

        ApplyPanelMotion(animate && !first);
        BuildBody(section!, direction, animate && !first);
        _shownIndex = _activeIndex;

        _hideTimer.Stop();
        _panel.IsVisible = true;
        _panelScale = 1;
        _panelLift = 0;
        PositionPanel();

        if (first)
        {
            if (ArcThemeManager.ReducedMotion || !animate)
            {
                _panel.Opacity = 1;
                ApplyPanelTransform();
            }
            else
            {
                _panel.Opacity = 0;
                _panelScale = 0.97;
                _panelLift = 6;
                ApplyPanelTransform();
                Dispatcher.UIThread.Post(() =>
                {
                    if (!IsOpen || _panel is null)
                    {
                        return;
                    }

                    _panel.Opacity = 1;
                    _panelScale = 1;
                    _panelLift = 0;
                    ApplyPanelTransform();
                }, DispatcherPriority.Render);
            }
        }
        else
        {
            _panel.Opacity = 1;
        }

        ApplyTriggerState();
        ShowCaret(_activeIndex, animate);
        _dismiss?.Dispose();
        _dismiss = ArcDismiss.Attach(this, () =>
        {
            if (IsOpen)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
        });

        SectionChanged?.Invoke(this, section!.Value);
    }

    private void ClosePanel(bool returnFocus)
    {
        if (_panel is null || !_panel.IsVisible)
        {
            if (returnFocus && _activeIndex >= 0 && _activeIndex < _triggerButtons.Count)
            {
                _triggerButtons[_activeIndex].Focus();
            }

            return;
        }

        _dismiss?.Dispose();
        _dismiss = null;
        _closeTimer.Stop();
        _openTimer.Stop();
        _hideTimer.Stop();

        if (ArcThemeManager.ReducedMotion)
        {
            _panel.IsVisible = false;
            _panel.Opacity = 0;
        }
        else
        {
            _panel.Opacity = 0;
            _panelScale = 0.985;
            _panelLift = 4;
            ApplyPanelTransform();
            _hideTimer.Start();
        }

        _caret?.Hide();
        _barHighlight?.Hide();
        foreach (var chevron in _chevrons)
        {
            if (chevron is not null)
            {
                ArcActionVisuals.Rotate(chevron, false);
            }
        }

        foreach (var trigger in _triggerButtons)
        {
            trigger.Classes.Set("open", false);
        }

        _shownIndex = -1;
        SectionChanged?.Invoke(this, null);
        UpdateCaretForRest();

        if (returnFocus && _activeIndex >= 0 && _activeIndex < _triggerButtons.Count)
        {
            _triggerButtons[_activeIndex].Focus();
        }
    }

    private void ApplyTriggerState()
    {
        for (var i = 0; i < _triggerButtons.Count; i++)
        {
            var open = IsOpen && i == _activeIndex && !_narrow;
            _triggerButtons[i].Classes.Set("open", open);
            if (i < _chevrons.Count && _chevrons[i] is { } chevron)
            {
                ArcActionVisuals.Rotate(chevron, open);
            }

            AutomationProperties.SetItemStatus(
                _triggerButtons[i],
                i == _currentIndex
                    ? "Current page"
                    : open
                        ? "Expanded"
                        : string.Empty);
        }
    }

    private void ApplyPanelMotion(bool animate)
    {
        if (_panel is null)
        {
            return;
        }

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _panel.Transitions = null;
            return;
        }

        _panel.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = WidthProperty,
                Duration = ArcMotion.SpringSmoothDuration,
                Easing = ArcMotion.SpringSmooth
            },
            new DoubleTransition
            {
                Property = HeightProperty,
                Duration = ArcMotion.SpringSmoothDuration,
                Easing = ArcMotion.SpringSmooth
            },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = ArcMotion.SpringSmoothDuration,
                Easing = ArcMotion.SpringSmooth
            },
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = ArcMotion.Fast,
                Easing = ArcMotion.EaseStandard
            }
        };
    }

    private void ApplyPanelTransform()
    {
        if (_panel is null)
        {
            return;
        }

        _panel.RenderTransform = TransformOperations.Parse(
            $"translateX({_panelX}px) translateY({_panelLift}px) scale({_panelScale})");
    }

    private void PositionPanel()
    {
        if (_panel is null || _bar is null || _activeIndex < 0 || _activeIndex >= _triggerButtons.Count)
        {
            return;
        }

        if (_panelTargetWidth <= 0)
        {
            return;
        }

        var trigger = _triggerButtons[_activeIndex];
        if (trigger.Bounds.Width <= 0)
        {
            return;
        }

        var origin = trigger.TranslatePoint(new Point(0, 0), this) ?? new Point(0, 0);
        var target = origin.X + (trigger.Bounds.Width / 2) - (_panelTargetWidth / 2);
        var max = Math.Max(PanelInset, Bounds.Width - _panelTargetWidth - PanelInset);
        target = Math.Clamp(target, PanelInset, max);

        var y = _bar.Bounds.Height + PanelGap;
        var currentTop = Canvas.GetTop(_panel);
        if (double.IsNaN(currentTop) || Math.Abs(currentTop - y) > 0.5)
        {
            Canvas.SetTop(_panel, y);
        }

        if (Math.Abs(target - _panelX) > 0.5)
        {
            _panelX = target;
            ApplyPanelTransform();
        }
    }

    private double MeasureAvailableWidth()
    {
        var width = Bounds.Width;
        return Math.Max(280, width - (PanelInset * 2));
    }

    // ---------------------------------------------------------------- panel body

    private void BuildBody(ArcMegaMenuSection section, int direction, bool animate)
    {
        if (_panelHost is null)
        {
            return;
        }

        _linkRows.Clear();
        var (body, width) = BuildSectionBody(section, MeasureAvailableWidth());
        body.Width = width;
        _availableWidth = MeasureAvailableWidth();
        _panelTargetWidth = width;

        var sliding = animate && direction != 0 && _body is not null && !ArcThemeManager.ReducedMotion;
        if (sliding)
        {
            var previous = _body!;
            previous.Height = Math.Max(80, previous.DesiredSize.Height);
            previous.Transitions = BodyMotion();
            previous.RenderTransform = TransformOperations.Parse($"translateX({-direction * 36}px)");
            previous.Opacity = 0;

            body.Opacity = 0;
            body.RenderTransform = TransformOperations.Parse($"translateX({direction * 36}px)");
            body.Transitions = BodyMotion();

            _panelHost.Children.Add(body);
            _outgoing = previous;
            _outgoingTimer.Stop();
            _outgoingTimer.Start();
        }
        else
        {
            _panelHost.Children.Clear();
            _panelHost.Children.Add(body);
            body.Opacity = 1;
            body.RenderTransform = TransformOperations.Parse("translateX(0px)");
            _outgoing = null;
        }

        _body = body;

        // The body is in the tree now, so its templated children report their real size.
        body.Measure(new Size(width, double.PositiveInfinity));
        var height = Math.Max(96, body.DesiredSize.Height);
        body.Height = height;
        _panelTargetHeight = height;

        if (_panel is not null)
        {
            _panel.Width = width;
            _panel.Height = height;
        }

        if (sliding)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!_panelHost.Children.Contains(body))
                {
                    return;
                }

                body.Opacity = 1;
                body.RenderTransform = TransformOperations.Parse("translateX(0px)");
            }, DispatcherPriority.Render);
        }
    }

    private static Transitions BodyMotion() => new()
    {
        new TransformOperationsTransition
        {
            Property = RenderTransformProperty,
            Duration = ArcMotion.Standard,
            Easing = ArcMotion.EaseEnter
        },
        new DoubleTransition
        {
            Property = OpacityProperty,
            Duration = ArcMotion.Fast,
            Easing = ArcMotion.EaseStandard
        }
    };

    private (Control Body, double Width) BuildSectionBody(ArcMegaMenuSection section, double available)
    {
        var root = new StackPanel { Orientation = Orientation.Vertical };

        var groups = section.Groups?.ToList() ?? new List<ArcMegaMenuGroup>();
        var feature = section.Feature;
        var featureWidth = feature is not null ? 224d : 0d;
        var padding = 40d;
        var gap = feature is not null && groups.Count > 1 ? 12d : 0d;
        var count = Math.Max(1, groups.Count);
        var groupWidth = Math.Clamp((available - padding - featureWidth - (feature is not null ? 12 : 0)) / count, 156, 236);

        var content = new Grid
        {
            Margin = new Thickness(20, 20, 20, section.Footer is null ? 20 : 14),
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto")
        };

        var columnHost = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = gap,
            VerticalAlignment = VerticalAlignment.Top
        };

        for (var i = 0; i < groups.Count; i++)
        {
            if (i > 0)
            {
                var divider = new Border
                {
                    Width = 1,
                    Margin = new Thickness(0, 6, 0, 6)
                };
                divider.Classes.Add("mm-group-divider");
                columnHost.Children.Add(divider);
            }

            columnHost.Children.Add(BuildGroup(groups[i], groupWidth));
        }

        Grid.SetColumn(columnHost, 0);
        content.Children.Add(columnHost);

        if (feature is not null)
        {
            var card = BuildFeature(feature, featureWidth);
            card.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(card, 1);
            content.Children.Add(card);
        }

        root.Children.Add(content);

        if (!string.IsNullOrWhiteSpace(section.Footer))
        {
            root.Children.Add(BuildFooter(section.Footer!));
        }

        // Natural width is arithmetic: the surface morphs between the shapes rather than chasing a measure.
        var columns = (groupWidth * count) + (count - 1) + ((2 * count) - 2) * gap;
        var natural = padding + columns + (feature is not null ? featureWidth + 12 : 0);
        var width = Math.Min(available, Math.Max(280, natural));
        return (root, width);
    }

    private Control BuildGroup(ArcMegaMenuGroup group, double width)
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Width = width };

        if (!string.IsNullOrWhiteSpace(group.Title))
        {
            panel.Children.Add(new TextBlock
            {
                Text = group.Title,
                Classes = { "arc-caption" },
                Margin = new Thickness(10, 2, 0, 6)
            });
        }

        foreach (var link in group.Links)
        {
            panel.Children.Add(BuildLink(link, width));
        }

        return panel;
    }

    private Control BuildLink(ArcMegaMenuLink link, double rowWidth)
    {
        var button = LinkButton();
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconBox = new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(9),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Top
        };
        iconBox.Classes.Add("mm-icon-box");
        var icon = new ArcIcon
        {
            Icon = string.IsNullOrWhiteSpace(link.Icon) ? "circle-dot" : link.Icon,
            Size = 16,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        icon.Classes.Add("mm-link-icon");
        iconBox.Child = icon;
        Grid.SetColumn(iconBox, 0);
        grid.Children.Add(iconBox);

        var text = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            // The button template measures its content unconstrained, so the row wraps at a set width.
            Width = Math.Max(80, rowWidth - 56)
        };
        text.Children.Add(new TextBlock
        {
            Text = link.Label ?? "Link",
            Classes = { "arc-body-sm", "arc-medium" }
        });
        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            text.Children.Add(new TextBlock
            {
                Text = link.Description,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);

        if (!string.IsNullOrWhiteSpace(link.Badge))
        {
            var badge = new ArcBadge
            {
                Content = link.Badge,
                Tone = ArcBadgeTone.Accent,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);
        }

        button.Content = grid;
        button.KeyDown += OnLinkKeyDown;
        ArcActionVisuals.SetName(button, link.Label ?? "Link");
        if (!string.IsNullOrWhiteSpace(link.Description))
        {
            AutomationProperties.SetHelpText(button, link.Description!);
        }

        button.Click += (_, _) =>
        {
            var section = SectionAt(_activeIndex);
            Invoke(new ArcMegaMenuDestination
            {
                Label = link.Label,
                Href = link.Href,
                Section = section?.Value
            }, link.Action);
            SetCurrentValue(IsOpenProperty, false);
        };

        _linkRows.Add(button);
        return button;
    }

    private Control BuildFeature(ArcMegaMenuFeature feature, double width)
    {
        var button = LinkButton();
        button.Width = width;
        button.HorizontalAlignment = HorizontalAlignment.Left;
        button.Classes.Add("mm-feature-button");
        button.Content = FeatureCard(feature, width);
        button.KeyDown += OnLinkKeyDown;
        ArcActionVisuals.SetName(button, feature.Title ?? "Featured");
        if (!string.IsNullOrWhiteSpace(feature.Description))
        {
            AutomationProperties.SetHelpText(button, feature.Description!);
        }

        button.Click += (_, _) =>
        {
            var section = SectionAt(_activeIndex);
            Invoke(new ArcMegaMenuDestination
            {
                Label = feature.Cta ?? feature.Title,
                Href = feature.Href,
                Section = section?.Value
            }, feature.Action);
            SetCurrentValue(IsOpenProperty, false);
        };

        _linkRows.Add(button);
        return button;
    }

    private static Control FeatureCard(ArcMegaMenuFeature feature, double width)
    {
        var card = new Border
        {
            Width = width,
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(18)
        };
        card.Classes.Add("mm-feature");

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 6 };
        var icon = new ArcIcon
        {
            Icon = string.IsNullOrWhiteSpace(feature.Icon) ? "sparkles" : feature.Icon,
            Size = 18,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 6)
        };
        icon.Classes.Add("mm-feature-icon");
        stack.Children.Add(icon);

        stack.Children.Add(new TextBlock
        {
            Text = feature.Title ?? "Featured",
            Classes = { "arc-body-sm", "arc-medium" },
            TextWrapping = TextWrapping.Wrap
        });

        if (!string.IsNullOrWhiteSpace(feature.Description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = feature.Description,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(feature.Cta))
        {
            var cta = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var ctaText = new TextBlock
            {
                Text = feature.Cta,
                Classes = { "arc-caption", "arc-medium" }
            };
            ctaText.Classes.Add("mm-feature-cta");
            cta.Children.Add(ctaText);
            var arrow = new ArcIcon { Icon = "arrow-right", Size = 14, VerticalAlignment = VerticalAlignment.Center };
            arrow.Classes.Add("mm-feature-cta");
            cta.Children.Add(arrow);
            stack.Children.Add(cta);
        }

        card.Child = stack;
        return card;
    }

    private static Control BuildFooter(string text)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3) };
        dot.Classes.Add("mm-status-dot");
        row.Children.Add(dot);
        row.Children.Add(new TextBlock { Text = text, Classes = { "arc-caption" } });

        var band = new Border
        {
            Padding = new Thickness(20, 9, 20, 10),
            CornerRadius = new CornerRadius(0, 0, 25, 25),
            Child = row
        };
        band.Classes.Add("mm-band");
        return band;
    }

    private static ArcButton LinkButton() => new()
    {
        Variant = ArcButtonVariant.Ghost,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        Padding = new Thickness(8, 6),
        MinHeight = 44,
        CornerRadius = new CornerRadius(14),
        Focusable = true,
        IsTabStop = false,
        VerticalContentAlignment = VerticalAlignment.Center
    };

    // ---------------------------------------------------------------- caret and highlight

    private void ShowCaret(int index, bool animate)
    {
        if (_caret is null || _triggerZone is null || index < 0 || index >= _triggerButtons.Count || _narrow)
        {
            _caret?.Hide(animate);
            return;
        }

        var trigger = _triggerButtons[index];
        if (trigger.Bounds.Width <= 0)
        {
            return;
        }

        var origin = trigger.TranslatePoint(new Point(0, 0), _triggerZone);
        if (origin is null)
        {
            return;
        }

        var width = Math.Min(24, Math.Max(12, trigger.Bounds.Width - 22));
        var x = origin.Value.X + ((trigger.Bounds.Width - width) / 2);
        var y = origin.Value.Y + trigger.Bounds.Height - 4;
        _caret.ShowAt(new Rect(x, y, width, 2), animate);
    }

    private void UpdateCaretForRest()
    {
        if (_narrow || IsOpen)
        {
            return;
        }

        if (_currentIndex >= 0)
        {
            ShowCaret(_currentIndex, animate: true);
        }
        else
        {
            _caret?.Hide();
        }
    }

    private void ShowBarHighlight(int index)
    {
        if (_barHighlight is null || _triggerZone is null || index < 0 || index >= _triggerButtons.Count)
        {
            return;
        }

        var trigger = _triggerButtons[index];
        if (trigger.Bounds.Width <= 0)
        {
            return;
        }

        var origin = trigger.TranslatePoint(new Point(0, 0), _triggerZone);
        if (origin is null)
        {
            return;
        }

        _barHighlight.ShowAt(new Rect(origin.Value.X, origin.Value.Y + 3, trigger.Bounds.Width, trigger.Bounds.Height - 6));
    }

    // ---------------------------------------------------------------- pointer

    private void OnTriggerClick(int index)
    {
        _openTimer.Stop();
        var section = SectionAt(index);
        if (section is null)
        {
            return;
        }

        if (!HasMenu(section))
        {
            Invoke(new ArcMegaMenuDestination { Label = section.Label, Href = section.Href, Section = section.Value }, section.Action);
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        if (IsOpen && _activeIndex == index)
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        RequestOpen(index);
    }

    private void OnTriggerEnter(int index)
    {
        _closeTimer.Stop();
        _hoverIndex = index;
        ShowBarHighlight(index);

        if (_narrow)
        {
            return;
        }

        if (IsOpen)
        {
            if (index == _activeIndex || SafeTriangleHolds(index))
            {
                return;
            }

            RequestOpen(index);
            return;
        }

        _pendingIndex = index;
        _openTimer.Stop();
        _openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, OpenDelay));
        _openTimer.Start();
    }

    private void OnTriggerLeave(int index, PointerEventArgs args)
    {
        if (_hoverIndex == index)
        {
            _hoverIndex = -1;
        }

        _lastExitPoint = args.GetPosition(this);
        if (IsOpen)
        {
            StartCloseTimer();
        }
        else
        {
            _openTimer.Stop();
        }
    }

    private void StartCloseTimer()
    {
        _closeTimer.Stop();
        _closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, CloseDelay));
        _closeTimer.Start();
    }

    private bool SafeTriangleHolds(int index)
    {
        if (_lastExitPoint is null || _panel is null || _bar is null || _panelTargetWidth <= 0)
        {
            return false;
        }

        var apex = _lastExitPoint.Value;
        var top = _bar.Bounds.Height + PanelGap;
        var left = new Point(_panelX, top);
        var right = new Point(_panelX + _panelTargetWidth, top);

        var trigger = _triggerButtons[index];
        var current = trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, trigger.Bounds.Height / 2), this);
        if (current is null)
        {
            return false;
        }

        return InTriangle(current.Value, apex, left, right);
    }

    /// <summary>Point in triangle with a small tolerance, the same test the web component runs on entry.</summary>
    private static bool InTriangle(Point point, Point a, Point b, Point c)
    {
        if (Inside(point, a, b, c))
        {
            return true;
        }

        // A few pixels of tolerance along the pointer's travel.
        return Inside(new Point(point.X, point.Y + 6), a, b, c);
    }

    private static bool Inside(Point point, Point a, Point b, Point c)
    {
        static double Sign(Point p1, Point p2, Point p3) =>
            (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);

        var d1 = Sign(point, a, b);
        var d2 = Sign(point, b, c);
        var d3 = Sign(point, c, a);
        var negative = d1 < 0 || d2 < 0 || d3 < 0;
        var positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }

    private void OnPointerExitedRoot(object? sender, PointerEventArgs e)
    {
        if (!IsOpen || _narrow)
        {
            return;
        }

        var point = e.GetPosition(this);
        var inside = point.X >= 0 && point.Y >= 0 && point.X <= Bounds.Width && point.Y <= Bounds.Height;
        if (!inside)
        {
            StartCloseTimer();
        }
    }

    private void OnPointerEnteredPanel(object? sender, PointerEventArgs e) => _closeTimer.Stop();

    private void OnPointerExitedPanel(object? sender, PointerEventArgs e)
    {
        if (!IsOpen)
        {
            return;
        }

        var point = e.GetPosition(this);
        if (point.X < 0 || point.Y < 0 || point.X > Bounds.Width || point.Y > (_bar?.Bounds.Height ?? 0))
        {
            StartCloseTimer();
        }
    }

    // ---------------------------------------------------------------- keyboard

    private void OnTriggerKeyDown(int index, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
            case Key.Right:
                MoveTrigger(index, e.Key == Key.Right ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Home:
                FocusTrigger(0);
                e.Handled = true;
                break;
            case Key.End:
                FocusTrigger(_triggerButtons.Count - 1);
                e.Handled = true;
                break;
            case Key.Down:
                RequestOpen(index);
                FocusFirstLink();
                e.Handled = true;
                break;
            case Key.Up when IsOpen:
            case Key.Escape when IsOpen:
                SetCurrentValue(IsOpenProperty, false);
                _triggerButtons[index].Focus();
                e.Handled = true;
                break;
            case Key.Tab when IsOpen && !e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                FocusFirstLink();
                e.Handled = true;
                break;
        }
    }

    private void MoveTrigger(int index, int delta)
    {
        if (_triggerButtons.Count == 0)
        {
            return;
        }

        var next = (index + delta + _triggerButtons.Count) % _triggerButtons.Count;
        _triggerButtons[next].Focus();
        if (IsOpen && !_narrow)
        {
            RequestOpen(next);
            FocusFirstLink();
        }
    }

    private void FocusTrigger(int index)
    {
        if (index >= 0 && index < _triggerButtons.Count)
        {
            _triggerButtons[index].Focus();
        }
    }

    private void FocusFirstLink()
    {
        if (_narrow || _linkRows.Count == 0)
        {
            return;
        }

        var first = _linkRows[0];
        Dispatcher.UIThread.Post(() => first.Focus(), DispatcherPriority.Loaded);
    }

    private void OnLinkKeyDown(object? sender, KeyEventArgs e)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        var index = focused is null ? -1 : _linkRows.IndexOf(focused);

        if (e.Key == Key.Escape)
        {
            if (_activeIndex >= 0 && _activeIndex < _triggerButtons.Count)
            {
                SetCurrentValue(IsOpenProperty, false);
                _triggerButtons[_activeIndex].Focus();
                e.Handled = true;
            }

            return;
        }

        if (e.Key == Key.Tab)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                if (index <= 0)
                {
                    SetCurrentValue(IsOpenProperty, false);
                    FocusTrigger(_activeIndex);
                    e.Handled = true;
                }

                return;
            }

            if (index >= _linkRows.Count - 1)
            {
                var next = Math.Min(_triggerButtons.Count - 1, _activeIndex + 1);
                SetCurrentValue(IsOpenProperty, false);
                FocusTrigger(next);
                e.Handled = true;
            }

            return;
        }

        if (e.Key is Key.Left or Key.Right)
        {
            MoveTrigger(_activeIndex, e.Key == Key.Right ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Down or Key.Up)
        {
            var delta = e.Key == Key.Down ? 1 : -1;
            var next = index < 0 ? 0 : index + delta;
            if (next < 0)
            {
                SetCurrentValue(IsOpenProperty, false);
                FocusTrigger(_activeIndex);
            }
            else if (next >= _linkRows.Count)
            {
                _linkRows[^1].Focus();
            }
            else
            {
                _linkRows[next].Focus();
            }

            e.Handled = true;
        }
    }

    // ---------------------------------------------------------------- sheet

    private void ToggleSheet()
    {
        if (IsOpen)
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        SetCurrentValue(IsOpenProperty, true);
    }

    private void OpenSheet()
    {
        if (_overlay is null || Bounds.Width <= 0)
        {
            return;
        }

        BuildSheet();
        if (_sheet is null)
        {
            return;
        }

        if (!_overlay.Children.Contains(_sheet))
        {
            _overlay.Children.Add(_sheet);
        }

        _sheetHideTimer.Stop();
        _sheet.Width = Math.Max(240, Bounds.Width - (PanelInset * 2));
        Canvas.SetLeft(_sheet, PanelInset);
        Canvas.SetTop(_sheet, (_bar?.Bounds.Height ?? 56) + PanelGap);
        _sheet.IsVisible = true;
        _sheet.Opacity = 1;
        _sheet.RenderTransform = TransformOperations.Parse("translateY(0px)");

        _sheet.Transitions = ArcThemeManager.ReducedMotion
            ? null
            : new Transitions
            {
                new DoubleTransition
                {
                    Property = HeightProperty,
                    Duration = ArcMotion.SpringSmoothDuration,
                    Easing = ArcMotion.SpringSmooth
                },
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                }
            };

        if (_menuButton is not null)
        {
            ArcActionVisuals.SetName(_menuButton, "Close menu");
            _menuButton.Classes.Set("open", true);
        }

        _dismiss?.Dispose();
        _dismiss = ArcDismiss.Attach(this, () =>
        {
            if (IsOpen)
            {
                SetCurrentValue(IsOpenProperty, false);
            }
        });

        var index = IndexOfValue(ActiveSection);
        ExpandSheetSection(index, animate: false);

        if (_sheetHeaders.Count > 0)
        {
            var first = _sheetHeaders[0];
            Dispatcher.UIThread.Post(() => first.Focus(), DispatcherPriority.Loaded);
        }
    }

    private void CloseSheet(bool returnFocus)
    {
        if (_sheet is null || !_sheet.IsVisible)
        {
            return;
        }

        _dismiss?.Dispose();
        _dismiss = null;

        if (ArcThemeManager.ReducedMotion)
        {
            _sheet.IsVisible = false;
        }
        else
        {
            _sheet.Transitions = null;
            _sheet.Opacity = 0;
            _sheet.RenderTransform = TransformOperations.Parse("translateY(-4px)");
            _sheetHideTimer.Start();
        }

        if (_menuButton is not null)
        {
            ArcActionVisuals.SetName(_menuButton, "Open menu");
            _menuButton.Classes.Set("open", false);
        }

        _sheetOpenIndex = -1;
        foreach (var header in _sheetHeaders)
        {
            header.Classes.Set("open", false);
            AutomationProperties.SetItemStatus(header, "Collapsed");
        }

        for (var i = 0; i < _sheetBodies.Count; i++)
        {
            _sheetBodies[i].Height = 0;
            _sheetBodies[i].Opacity = 0;
        }

        if (returnFocus)
        {
            _menuButton?.Focus();
        }
    }

    private void BuildSheet()
    {
        if (_overlay is null || _sheet is not null)
        {
            return;
        }

        _sheet = new Border
        {
            Classes = { "mm-sheet" },
            IsVisible = false,
            Opacity = 0,
            ClipToBounds = true,
            RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative)
        };
        AutomationProperties.SetName(_sheet, Label + " menu");

        _sheetStack = new StackPanel { Orientation = Orientation.Vertical };
        _sheetScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0),
            Content = _sheetStack
        };

        _sheet.Child = _sheetScroll;

        var sections = SectionList();
        for (var i = 0; i < sections.Count; i++)
        {
            var index = i;
            var section = sections[i];
            var hasMenu = HasMenu(section);

            var chevron = new ArcIcon
            {
                Icon = "chevron-down",
                Size = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(8, 0, 0, 0),
                IsVisible = hasMenu
            };
            chevron.Classes.Add("mm-chevron");

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(new TextBlock
            {
                Text = section.Label ?? "Section",
                Classes = { "arc-body", "arc-medium" },
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(chevron, 1);
            grid.Children.Add(chevron);

            var header = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(18, 0),
                MinHeight = 52,
                CornerRadius = new CornerRadius(0),
                Content = grid
            };
            header.Classes.Add("mm-sheet-header");
            ArcActionVisuals.SetName(header, section.Label ?? "Section");
            AutomationProperties.SetHelpText(header, hasMenu ? "Expands this section" : "Goes to this page");
            header.Click += (_, _) =>
            {
                if (!hasMenu)
                {
                    Invoke(new ArcMegaMenuDestination { Label = section.Label, Href = section.Href, Section = section.Value }, section.Action);
                    SetCurrentValue(IsOpenProperty, false);
                    return;
                }

                ExpandSheetSection(_sheetOpenIndex == index ? -1 : index, animate: true);
            };

            var body = new Border { ClipToBounds = true, Height = 0, Opacity = 0 };
            body.Classes.Add("mm-sheet-body");
            if (!ArcThemeManager.ReducedMotion)
            {
                body.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = HeightProperty,
                        Duration = ArcMotion.SpringSmoothDuration,
                        Easing = ArcMotion.SpringSmooth
                    },
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = ArcMotion.Standard,
                        Easing = ArcMotion.EaseStandard
                    }
                };
            }

            _sheetHeaders.Add(header);
            _sheetBodies.Add(body);
            _sheetChevrons.Add(chevron);
            _sheetStack.Children.Add(header);
            _sheetStack.Children.Add(body);

            if (i < sections.Count - 1)
            {
                var divider = new Border { Height = 1, Margin = new Thickness(18, 0) };
                divider.Classes.Add("mm-sheet-divider");
                _sheetStack.Children.Add(divider);
            }
        }

        var quiet = (Actions?.ToList() ?? new List<ArcMegaMenuAction>())
            .Where(action => action.Variant != ArcMegaMenuActionVariant.Primary)
            .ToList();
        if (quiet.Count > 0)
        {
            var divider = new Border { Height = 1, Margin = new Thickness(18, 0) };
            divider.Classes.Add("mm-sheet-divider");
            _sheetStack.Children.Add(divider);

            foreach (var action in quiet)
            {
                var button = new ArcButton
                {
                    Variant = ArcButtonVariant.Ghost,
                    Content = action.Label ?? "Action",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(18, 0),
                    MinHeight = 48,
                    CornerRadius = new CornerRadius(0)
                };
                button.Classes.Add("mm-sheet-action");
                ArcActionVisuals.SetName(button, action.Label ?? "Action");
                button.Click += (_, _) =>
                {
                    Invoke(new ArcMegaMenuDestination { Label = action.Label, Href = action.Href }, null);
                    action.Action?.Invoke();
                    SetCurrentValue(IsOpenProperty, false);
                };
                _sheetStack.Children.Add(button);
            }
        }

        ApplySheetHeight();
    }

    private void ExpandSheetSection(int index, bool animate)
    {
        _sheetOpenIndex = index;
        var sections = SectionList();

        for (var i = 0; i < _sheetHeaders.Count; i++)
        {
            var open = i == index;
            var body = _sheetBodies[i];
            var header = _sheetHeaders[i];
            header.Classes.Set("open", open);

            if (i < _sheetChevrons.Count)
            {
                ArcActionVisuals.Rotate(_sheetChevrons[i], open);
            }

            if (open && i < sections.Count)
            {
                body.Child = BuildSheetBody(sections[i]);
                body.Padding = new Thickness(18, 0, 18, 16);
                // Auto height: the sheet sizes itself from the laid out stack in LayoutUpdated, because a
                // templated row reports its real height only after it has been attached and laid out.
                body.Height = double.NaN;
                body.Opacity = 1;
            }
            else
            {
                body.Height = 0;
                body.Opacity = 0;
                body.Child = null;
            }

            AutomationProperties.SetItemStatus(header, open ? "Expanded" : "Collapsed");
        }

        if (index >= 0 && index < sections.Count)
        {
            _syncing = true;
            SetCurrentValue(ActiveSectionProperty, sections[index].Value);
            _syncing = false;
            SectionChanged?.Invoke(this, sections[index].Value);
        }
        else if (index < 0)
        {
            SectionChanged?.Invoke(this, null);
        }

        ApplySheetHeight();
    }

    private Control BuildSheetBody(ArcMegaMenuSection section)
    {
        _linkRows.Clear();
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2 };
        var width = Math.Max(120, (_sheet?.Width ?? 320) - 36);

        foreach (var group in section.Groups?.ToList() ?? new List<ArcMegaMenuGroup>())
        {
            if (!string.IsNullOrWhiteSpace(group.Title))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = group.Title,
                    Classes = { "arc-caption" },
                    Margin = new Thickness(10, 6, 0, 4)
                });
            }

            foreach (var link in group.Links)
            {
                stack.Children.Add(BuildLink(link, width));
            }
        }

        if (section.Feature is { } feature)
        {
            var card = BuildFeature(feature, width);
            card.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(card);
        }

        return stack;
    }

    private void ApplySheetHeight()
    {
        if (_sheet is null || _sheetStack is null)
        {
            return;
        }

        if (double.IsNaN(_sheet.Width) || _sheet.Width <= 0)
        {
            _sheet.Width = Math.Max(240, Bounds.Width - (PanelInset * 2));
        }

        var width = Math.Max(200, _sheet.Width - 2);
        _sheetStack.Width = width;
        _sheetStack.Measure(new Size(width, double.PositiveInfinity));
        var height = Math.Clamp(_sheetStack.DesiredSize.Height, 60, SheetMaxHeight);
        if (_sheetScroll is not null)
        {
            _sheetScroll.Width = _sheet.Width;
            _sheetScroll.Height = height;
        }

        _sheet.Height = height;
    }

    // ---------------------------------------------------------------- invocation

    private void Invoke(ArcMegaMenuDestination destination, Action? action)
    {
        ItemInvoked?.Invoke(this, new ArcMegaMenuInvokedEventArgs(destination));
        action?.Invoke();
    }
}







