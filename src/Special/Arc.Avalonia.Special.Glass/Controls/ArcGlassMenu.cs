using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Controls;

/// <summary>One row in a glass menu. A row with <see cref="Items"/> opens a submenu.</summary>
public sealed class ArcGlassMenuItem
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Lucide icon name; it takes the accent while the row is highlighted.</summary>
    public string? Icon { get; set; }

    /// <summary>Display-only shortcut hint, for example "Ctrl D".</summary>
    public string? Shortcut { get; set; }

    /// <summary>Makes the row a checkable item. Null means a plain action.</summary>
    public bool? Checked { get; set; }

    /// <summary>Destructive row: uses the danger colour.</summary>
    public bool Danger { get; set; }

    public bool Disabled { get; set; }

    /// <summary>Keeps the menu open after the row is chosen, for checkable rows.</summary>
    public bool KeepOpen { get; set; }

    /// <summary>Nested rows, one level deep. Present means this row opens a submenu.</summary>
    public IList<ArcGlassMenuItem>? Items { get; set; }
}

public sealed class ArcGlassMenuGroup
{
    /// <summary>Quiet heading above the group; omit for an unlabelled group.</summary>
    public string? Label { get; set; }

    public IList<ArcGlassMenuItem> Items { get; set; } = new List<ArcGlassMenuItem>();
}

public enum ArcGlassMenuAlign
{
    Start,
    End
}

/// <summary>
/// A glass action menu: the trigger's glass and the panel share one box, so opening is a single spring
/// morph from the trigger's size to the panel's, with the rows fading in a beat later. A glass highlight
/// pill glides between rows, one submenu slides in place, and the keyboard covers open, move, submenu,
/// back, close, typeahead and Tab.
///
/// Platform approximations: the panel morph is a scale from the trigger rect to the panel rect rather
/// than a CSS shared layout animation, and the submenu slides in place over the outgoing rows rather than
/// keeping a second live panel. The glass is layered translucency because Avalonia has no backdrop
/// filter, and the highlight pill moves with a transform transition instead of a spring. No Pro source
/// was consulted.
/// </summary>
public sealed class ArcGlassMenu : Panel
{
    public static readonly StyledProperty<IList<ArcGlassMenuGroup>?> GroupsProperty =
        AvaloniaProperty.Register<ArcGlassMenu, IList<ArcGlassMenuGroup>?>(nameof(Groups));

    public static readonly StyledProperty<object?> TriggerProperty =
        AvaloniaProperty.Register<ArcGlassMenu, object?>(nameof(Trigger));

    public static readonly StyledProperty<string?> TriggerLabelProperty =
        AvaloniaProperty.Register<ArcGlassMenu, string?>(nameof(TriggerLabel));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcGlassMenu, string>(nameof(Label), "Actions");

    public static readonly StyledProperty<ArcGlassMenuAlign> AlignProperty =
        AvaloniaProperty.Register<ArcGlassMenu, ArcGlassMenuAlign>(nameof(Align), ArcGlassMenuAlign.Start);

    public static readonly StyledProperty<double> PanelWidthProperty =
        AvaloniaProperty.Register<ArcGlassMenu, double>(nameof(PanelWidth), 272);

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcGlassMenu, bool>(nameof(IsOpen));

    private const double RowHeight = 36;
    private const double TriggerHeight = 40;
    private const double PanelGap = 8;

    private sealed class Row
    {
        public ArcGlassMenuItem Item = new();
        public ArcButton Button = null!;
        public TextBlock LabelBlock = null!;
        public ArcIcon? Icon;
        public TextBlock? Check;
    }

    private readonly Border _triggerShell;
    private readonly ArcButton _triggerButton;
    private readonly ContentControl _triggerContent;
    private readonly ArcIcon _triggerChevron;
    private readonly Canvas _overlay;
    private readonly Border _panel;
    private readonly Canvas _pillHost;
    private readonly Border _pill;
    private readonly Panel _bodyPanel;
    private readonly List<Row> _rows = new();
    private readonly Dictionary<string, bool> _checks = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _typeaheadTimer;

    private IList<ArcGlassMenuItem> _level = new List<ArcGlassMenuItem>();
    private ArcGlassMenuItem? _submenuParent;
    private string _typeahead = string.Empty;
    private int _highlight = -1;
    private bool _built;
    private bool _wasOpen;
    private GlassTween? _tween;
    private TopLevel? _dismissTarget;
    private EventHandler<PointerPressedEventArgs>? _dismissHandler;

    static ArcGlassMenu()
    {
        GroupsProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.Rebuild());
        TriggerProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.UpdateTrigger());
        TriggerLabelProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.UpdateTrigger());
        LabelProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.UpdateLabel());
        AlignProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.PositionPanel());
        PanelWidthProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.Rebuild());
        IsOpenProperty.Changed.AddClassHandler<ArcGlassMenu>((menu, _) => menu.SyncOpen());
    }

    public ArcGlassMenu()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        _triggerChevron = ArcGlass.Icon("chevron-down", 14, ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary)));
        _triggerContent = new ContentControl { VerticalAlignment = VerticalAlignment.Center };
        _triggerButton = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Height = TriggerHeight,
            MinHeight = TriggerHeight,
            Padding = new Thickness(14, 0, 12, 0),
            CornerRadius = new CornerRadius(20),
            Background = Brushes.Transparent,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _triggerContent, _triggerChevron }
            }
        };
        _triggerButton.Classes.Add("sg-plain");
        _triggerButton.Click += (_, _) => Toggle();
        _triggerButton.KeyDown += OnTriggerKeyDown;

        _triggerShell = ArcGlass.Panel(20, new Thickness(0), floating: true, fillAlpha: 0.55);
        _triggerShell.Height = TriggerHeight;
        _triggerShell.Child = _triggerButton;
        _triggerShell.HorizontalAlignment = HorizontalAlignment.Left;

        _overlay = new Canvas { ClipToBounds = false };

        _pillHost = new Canvas { ClipToBounds = false, IsHitTestVisible = false };
        _pill = new Border
        {
            Height = RowHeight,
            CornerRadius = new CornerRadius(10),
            Background = ArcGlass.HoverTint(ArcTokens.Accent, 0.18),
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.28)),
            BorderThickness = new Thickness(1),
            IsVisible = false,
            RenderTransform = TransformOperations.Parse("translateY(0px)")
        };
        _pill.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = GlassMotion.Morph,
                Easing = GlassMotion.MorphEase
            }
        };
        _pillHost.Children.Add(_pill);

        _bodyPanel = new Panel { ClipToBounds = false };
        _panel = ArcGlass.Panel(24, new Thickness(6), floating: true, fillAlpha: 0.68);
        _panel.Width = PanelWidth;
        _panel.IsVisible = false;
        _panel.Opacity = 0;
        _panel.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        _panel.Child = new Panel { ClipToBounds = false, Children = { _pillHost, _bodyPanel } };
        AutomationProperties.SetLiveSetting(_panel, AutomationLiveSetting.Polite);

        _typeaheadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _typeaheadTimer.Tick += (_, _) =>
        {
            _typeaheadTimer.Stop();
            _typeahead = string.Empty;
        };

        Children.Add(_triggerShell);
        Children.Add(_overlay);

        UpdateTrigger();
        UpdateLabel();
    }

    /// <summary>Raised when the menu opens or closes.</summary>
    public event EventHandler<bool>? OpenChanged;

    /// <summary>Raised with the chosen row when it is not a submenu row.</summary>
    public event EventHandler<ArcGlassMenuItem>? ItemInvoked;

    public IList<ArcGlassMenuGroup>? Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }

    /// <summary>Trigger content, such as a title and a thumbnail.</summary>
    public object? Trigger
    {
        get => GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <summary>Accessible name of the trigger when it only shows an icon.</summary>
    public string? TriggerLabel
    {
        get => GetValue(TriggerLabelProperty);
        set => SetValue(TriggerLabelProperty, value);
    }

    /// <summary>Accessible name of the menu.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public ArcGlassMenuAlign Align
    {
        get => GetValue(AlignProperty);
        set => SetValue(AlignProperty, value);
    }

    public double PanelWidth
    {
        get => GetValue(PanelWidthProperty);
        set => SetValue(PanelWidthProperty, value);
    }

    /// <summary>Whether the panel is showing; settable so a page can present the open state.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>The row whose submenu is open, or null at the top level.</summary>
    public ArcGlassMenuItem? OpenSubmenuRow => _submenuParent;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _tween?.Stop();
        _typeaheadTimer.Stop();
        DetachDismiss();
        base.OnDetachedFromVisualTree(e);
    }

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetName(_panel, Label);
    }

    private void UpdateTrigger()
    {
        if (Trigger is Control custom)
        {
            _triggerContent.Content = custom;
        }
        else
        {
            _triggerContent.Content = new TextBlock
            {
                Text = TriggerLabel ?? "Actions",
                FontSize = 13,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
            };
        }

        AutomationProperties.SetName(_triggerButton, TriggerLabel ?? Label);
        AutomationProperties.SetHelpText(_triggerButton,
            "Opens the action menu. Use the arrow keys to move through the rows.");
        _triggerChevron.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        _triggerChevron.RenderTransform = TransformOperations.Parse(IsOpen ? "rotate(180deg)" : "rotate(0deg)");
    }

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        _built = true;
        _panel.Width = PanelWidth;
        _level = Flatten(Groups);
        _submenuParent = null;
        BuildRows(_level, direction: 0, animate: false);
        UpdateTrigger();
        if (IsOpen)
        {
            _wasOpen = false;
            SyncOpen();
        }
    }

    private static List<ArcGlassMenuItem> Flatten(IList<ArcGlassMenuGroup>? groups)
    {
        var items = new List<ArcGlassMenuItem>();
        if (groups is null)
        {
            return items;
        }

        foreach (var group in groups)
        {
            items.AddRange(group.Items);
        }

        return items;
    }

    private void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    private void OnTriggerKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
                e.Handled = true;
                if (!IsOpen)
                {
                    SetCurrentValue(IsOpenProperty, true);
                }

                global::Avalonia.Threading.Dispatcher.UIThread.Post(
                    () => Highlight(e.Key == Key.Down ? FirstEnabled() : LastEnabled(), focus: true),
                    DispatcherPriority.Loaded);
                break;
            case Key.Escape when IsOpen:
                e.Handled = true;
                Close(returnFocus: true);
                break;
        }
    }

    private void SyncOpen()
    {
        if (!_built)
        {
            return;
        }

        if (IsOpen)
        {
            _wasOpen = true;
            ShowPanel();
        }
        else if (_wasOpen)
        {
            _wasOpen = false;
            HidePanel();
        }
    }

    private void ShowPanel()
    {
        _tween?.Stop();
        _overlay.Children.Clear();
        _overlay.Children.Add(_panel);
        _level = Flatten(Groups);
        _submenuParent = null;
        BuildRows(_level, direction: 0, animate: false);
        _panel.IsVisible = true;
        _panel.Opacity = 1;
        _panel.Measure(new Size(PanelWidth, double.PositiveInfinity));
        var height = Math.Max(RowHeight + 12, _panel.DesiredSize.Height);
        _panel.Height = height;
        PositionPanel();
        _pill.IsVisible = true;
        Highlight(FirstEnabled(), focus: true);

        var triggerWidth = Math.Max(1, Bounds.Width);
        var triggerHeight = Math.Max(1, _triggerShell.Bounds.Height > 0 ? _triggerShell.Bounds.Height : TriggerHeight);
        var targetX = Canvas.GetLeft(_panel);
        var targetY = Canvas.GetTop(_panel);
        targetX = double.IsNaN(targetX) ? 0 : targetX;
        targetY = double.IsNaN(targetY) ? 0 : targetY;

        if (ArcThemeManager.ReducedMotion)
        {
            _panel.RenderTransform = TransformOperations.Parse("translate(0px, 0px) scale(1, 1)");
            _bodyPanel.Opacity = 1;
            _bodyPanel.RenderTransform = TransformOperations.Parse("translateX(0px)");
        }
        else
        {
            var sx = triggerWidth / Math.Max(1, PanelWidth);
            var sy = triggerHeight / Math.Max(1, height);
            var dx = -targetX;
            var dy = -targetY;
            _bodyPanel.Opacity = 0;
            ApplyMorph(dx, dy, sx, sy);
            _tween = GlassTween.Run(GlassMotion.Morph, GlassMotion.MorphEase, progress =>
            {
                ApplyMorph(
                    dx * (1 - progress),
                    dy * (1 - progress),
                    sx + ((1 - sx) * progress),
                    sy + ((1 - sy) * progress));
                _bodyPanel.Opacity = Math.Clamp((progress - 0.28) / 0.72, 0, 1);
            });
        }

        AttachDismiss();
        UpdateTrigger();
        global::Avalonia.Threading.Dispatcher.UIThread.Post(RepositionPill, DispatcherPriority.Loaded);
        OpenChanged?.Invoke(this, true);
    }

    private void ApplyMorph(double dx, double dy, double sx, double sy)
    {
        _panel.RenderTransform = TransformOperations.Parse(
            $"translate({dx:0.##}px, {dy:0.##}px) scale({sx:0.####}, {sy:0.####})");
    }

    private void HidePanel()
    {
        _tween?.Stop();
        DetachDismiss();
        _pill.IsVisible = false;
        UpdateTrigger();

        if (ArcThemeManager.ReducedMotion || !_panel.IsVisible)
        {
            _overlay.Children.Clear();
            _panel.IsVisible = false;
            _panel.Opacity = 0;
            _submenuParent = null;
            _level = Flatten(Groups);
            OpenChanged?.Invoke(this, false);
            return;
        }

        var from = _panel.RenderTransform;
        _tween = GlassTween.Run(GlassMotion.Exit, GlassMotion.SmoothEase, progress =>
        {
            _panel.Opacity = 1 - progress;
            _bodyPanel.Opacity = 1 - progress;
            _panel.RenderTransform = TransformOperations.Parse(
                $"translate(0px, {(-6 * progress):0.##}px) scale({1 - (0.04 * progress):0.###}, {1 - (0.04 * progress):0.###})");
        }, () =>
        {
            _overlay.Children.Clear();
            _panel.IsVisible = false;
            _panel.Opacity = 0;
            _panel.RenderTransform = from;
            _bodyPanel.Opacity = 1;
            _submenuParent = null;
            _level = Flatten(Groups);
            OpenChanged?.Invoke(this, false);
        });
    }

    private void Close(bool returnFocus)
    {
        SetCurrentValue(IsOpenProperty, false);
        if (returnFocus)
        {
            _triggerButton.Focus();
        }
    }

    private void PositionPanel()
    {
        if (!_panel.IsVisible)
        {
            return;
        }

        var x = Align == ArcGlassMenuAlign.End ? Bounds.Width - PanelWidth : 0;
        var y = (_triggerShell.Bounds.Height > 0 ? _triggerShell.Bounds.Height : TriggerHeight) + PanelGap;
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            var origin = this.TranslatePoint(new Point(0, 0), top);
            var height = _panel.Height > 0 ? _panel.Height : _panel.DesiredSize.Height;
            if (origin is { } point && point.Y + y + height > top.Bounds.Height - 12)
            {
                y = -height - PanelGap;
            }
        }

        Canvas.SetLeft(_panel, x);
        Canvas.SetTop(_panel, y);
    }

    // ---------------------------------------------------------------- rows

    private void BuildRows(IList<ArcGlassMenuItem> items, int direction, bool animate)
    {
        _rows.Clear();
        var host = new StackPanel { Orientation = Orientation.Vertical, Spacing = 2, ClipToBounds = false };
        var submenu = _submenuParent is not null;

        if (submenu)
        {
            host.Children.Add(BuildBackRow());
            foreach (var item in items)
            {
                host.Children.Add(BuildRow(item, _rows.Count));
            }
        }
        else
        {
            var groupIndex = 0;
            foreach (var group in Groups ?? new List<ArcGlassMenuGroup>())
            {
                if (group.Items.Count == 0)
                {
                    continue;
                }

                if (groupIndex > 0)
                {
                    host.Children.Add(new Border
                    {
                        Height = 1,
                        Margin = new Thickness(6, 4),
                        Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Border, 0.9))
                    });
                }

                if (!string.IsNullOrWhiteSpace(group.Label))
                {
                    host.Children.Add(new TextBlock
                    {
                        Text = group.Label,
                        Classes = { "arc-caption" },
                        Margin = new Thickness(10, 4, 0, 3)
                    });
                }

                foreach (var item in group.Items)
                {
                    host.Children.Add(BuildRow(item, _rows.Count));
                }

                groupIndex++;
            }
        }

        _bodyPanel.Children.Clear();
        _bodyPanel.Children.Add(host);
        _level = items;
        host.Measure(new Size(Math.Max(120, PanelWidth - 12), double.PositiveInfinity));
        var targetHeight = Math.Max(RowHeight + 12, host.DesiredSize.Height + 12);

        _tween?.Stop();
        if (animate && !ArcThemeManager.ReducedMotion)
        {
            var fromHeight = _panel.Height > 0 ? _panel.Height : targetHeight;
            host.Opacity = 0;
            host.RenderTransform = TransformOperations.Parse($"translateX({direction * 26}px)");
            _tween = GlassTween.Run(GlassMotion.Morph, GlassMotion.SmoothEase, progress =>
            {
                _panel.Height = fromHeight + ((targetHeight - fromHeight) * progress);
                host.Opacity = Math.Clamp((progress - 0.18) / 0.82, 0, 1);
                host.RenderTransform = TransformOperations.Parse(
                    $"translateX({direction * 26 * (1 - progress):0.##}px)");
            });
        }
        else
        {
            _panel.Height = targetHeight;
            host.Opacity = 1;
            host.RenderTransform = TransformOperations.Parse("translateX(0px)");
        }

        global::Avalonia.Threading.Dispatcher.UIThread.Post(RepositionPill, DispatcherPriority.Loaded);
    }

    private Control BuildBackRow()
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(ArcGlass.Icon("chevron-left", 16));
        row.Children.Add(new TextBlock
        {
            Text = _submenuParent?.Label ?? "Back",
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center
        });

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Content = row,
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Left
        };
        button.Classes.Add("sg-row");
        AutomationProperties.SetName(button, $"Back to {(_submenuParent?.Label ?? "menu")}");
        button.Click += (_, _) => GoBack();
        button.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Escape:
                case Key.Back:
                    e.Handled = true;
                    GoBack();
                    break;
                case Key.Down:
                    e.Handled = true;
                    Highlight(1, focus: true);
                    break;
            }
        };
        return button;
    }

    private Control BuildRow(ArcGlassMenuItem item, int index)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };

        ArcIcon? icon = null;
        if (!string.IsNullOrWhiteSpace(item.Icon))
        {
            icon = ArcGlass.Icon(item.Icon!, 16, ArcGlass.Solid(
                ArcGlass.Color(item.Danger ? ArcTokens.Danger : item.Disabled ? ArcTokens.TextMuted : ArcTokens.TextSecondary)));
            Grid.SetColumn(icon, 0);
            grid.Children.Add(icon);
        }

        var label = new TextBlock
        {
            Text = item.Label,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = ArcGlass.Solid(ArcGlass.Color(
                item.Danger ? ArcTokens.Danger : item.Disabled ? ArcTokens.TextMuted : ArcTokens.TextSecondary))
        };
        Grid.SetColumn(label, 1);
        grid.Children.Add(label);

        if (!string.IsNullOrWhiteSpace(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                Classes = { "arc-caption" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(shortcut, 2);
            grid.Children.Add(shortcut);
        }

        TextBlock? check = null;
        if (item.Checked is not null)
        {
            check = new TextBlock
            {
                Text = IsChecked(item) ? "✓" : string.Empty,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 2, 0),
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong))
            };
            Grid.SetColumn(check, 3);
            grid.Children.Add(check);
        }
        else if (item.Items is { Count: > 0 })
        {
            var chevron = ArcGlass.Icon("chevron-right", 14);
            Grid.SetColumn(chevron, 3);
            grid.Children.Add(chevron);
        }

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Content = grid,
            Background = Brushes.Transparent,
            IsEnabled = !item.Disabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        button.Classes.Add("sg-row");
        button.Classes.Set("danger", item.Danger);
        AutomationProperties.SetName(button, item.Label);
        if (item.Checked is not null)
        {
            AutomationProperties.SetItemStatus(button, IsChecked(item) ? "Checked" : "Not checked");
        }

        if (item.Items is { Count: > 0 })
        {
            AutomationProperties.SetHelpText(button, "Opens a submenu");
        }
        else if (!string.IsNullOrWhiteSpace(item.Shortcut))
        {
            AutomationProperties.SetHelpText(button, item.Shortcut!);
        }

        var row = new Row { Item = item, Button = button, LabelBlock = label, Icon = icon, Check = check };
        button.PointerEntered += (_, _) => Highlight(index, focus: false);
        button.GotFocus += (_, _) => Highlight(index, focus: false);
        button.Click += (_, _) => Activate(item, index);
        button.KeyDown += (_, e) => OnRowKeyDown(index, e);
        _rows.Add(row);
        return button;
    }

    private bool IsChecked(ArcGlassMenuItem item) =>
        _checks.TryGetValue(item.Id, out var value) ? value : item.Checked == true;

    private void Activate(ArcGlassMenuItem item, int index)
    {
        if (item.Disabled)
        {
            return;
        }

        if (item.Items is { Count: > 0 })
        {
            OpenSubmenu(item);
            return;
        }

        if (item.Checked is not null)
        {
            var next = !IsChecked(item);
            _checks[item.Id] = next;
            if (index >= 0 && index < _rows.Count && _rows[index].Check is { } check)
            {
                check.Text = next ? "✓" : string.Empty;
                AutomationProperties.SetItemStatus(_rows[index].Button, next ? "Checked" : "Not checked");
            }

            ItemInvoked?.Invoke(this, item);
            if (item.KeepOpen)
            {
                return;
            }
        }
        else
        {
            ItemInvoked?.Invoke(this, item);
        }

        Close(returnFocus: true);
    }

    private void OpenSubmenu(ArcGlassMenuItem parent)
    {
        if (parent.Items is null || parent.Items.Count == 0)
        {
            return;
        }

        _submenuParent = parent;
        _rows.Clear();
        BuildRows(parent.Items, direction: 1, animate: true);
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Highlight(FirstEnabled(), focus: true);
            RepositionPill();
        }, DispatcherPriority.Loaded);
    }

    private void GoBack()
    {
        _submenuParent = null;
        _rows.Clear();
        BuildRows(Flatten(Groups), direction: -1, animate: true);
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Highlight(FirstEnabled(), focus: true);
            RepositionPill();
        }, DispatcherPriority.Loaded);
    }

    private void OnRowKeyDown(int index, KeyEventArgs e)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                MoveHighlight(index, 1);
                break;
            case Key.Up:
                e.Handled = true;
                MoveHighlight(index, -1);
                break;
            case Key.Home:
                e.Handled = true;
                Highlight(FirstEnabled(), focus: true);
                break;
            case Key.End:
                e.Handled = true;
                Highlight(LastEnabled(), focus: true);
                break;
            case Key.Right when index >= 0 && index < _rows.Count && _rows[index].Item.Items is { Count: > 0 }:
                e.Handled = true;
                OpenSubmenu(_rows[index].Item);
                break;
            case Key.Left when _submenuParent is not null:
                e.Handled = true;
                GoBack();
                break;
            case Key.Escape:
                e.Handled = true;
                if (_submenuParent is not null)
                {
                    GoBack();
                }
                else
                {
                    Close(returnFocus: true);
                }

                break;
            case Key.Tab:
                e.Handled = true;
                Close(returnFocus: true);
                break;
            default:
                if (e.Key is >= Key.A and <= Key.Z)
                {
                    e.Handled = true;
                    Typeahead(((char)('a' + (e.Key - Key.A))).ToString());
                }

                break;
        }
    }

    private void Typeahead(string text)
    {
        _typeaheadTimer.Stop();
        _typeahead += text;
        _typeaheadTimer.Start();
        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Item.Disabled &&
                _rows[i].Item.Label.StartsWith(_typeahead, StringComparison.OrdinalIgnoreCase))
            {
                Highlight(i, focus: true);
                return;
            }
        }
    }

    private int FirstEnabled()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (!_rows[i].Item.Disabled)
            {
                return i;
            }
        }

        return -1;
    }

    private int LastEnabled()
    {
        for (var i = _rows.Count - 1; i >= 0; i--)
        {
            if (!_rows[i].Item.Disabled)
            {
                return i;
            }
        }

        return -1;
    }

    private void MoveHighlight(int from, int delta)
    {
        if (_rows.Count == 0)
        {
            return;
        }

        var index = from;
        for (var step = 0; step < _rows.Count; step++)
        {
            index = (index + delta + _rows.Count) % _rows.Count;
            if (!_rows[index].Item.Disabled)
            {
                Highlight(index, focus: true);
                return;
            }
        }
    }

    private void Highlight(int index, bool focus)
    {
        if (index < 0 || index >= _rows.Count)
        {
            _highlight = -1;
            _pill.IsVisible = false;
            return;
        }

        _highlight = index;
        _pill.IsVisible = true;
        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var active = i == index;
            row.LabelBlock.Foreground = ArcGlass.Solid(ArcGlass.Color(
                row.Item.Danger ? ArcTokens.Danger
                : row.Item.Disabled ? ArcTokens.TextMuted
                : active ? ArcTokens.Foreground
                : ArcTokens.TextSecondary));
            row.LabelBlock.FontWeight = active ? FontWeight.Medium : FontWeight.Normal;

            if (row.Icon is not null)
            {
                row.Icon.Foreground = ArcGlass.Solid(ArcGlass.Color(
                    row.Item.Danger ? ArcTokens.Danger
                    : row.Item.Disabled ? ArcTokens.TextMuted
                    : active ? ArcTokens.Accent : ArcTokens.TextSecondary));
            }
        }

        RepositionPill();
        if (focus && index < _rows.Count)
        {
            _rows[index].Button.Focus();
        }
    }

    private void RepositionPill()
    {
        if (_highlight < 0 || _highlight >= _rows.Count || _pillHost.Bounds.Width <= 0)
        {
            return;
        }

        var point = _rows[_highlight].Button.TranslatePoint(new Point(0, 0), _pillHost);
        if (point is null)
        {
            return;
        }

        _pill.Width = Math.Max(40, _pillHost.Bounds.Width);
        Canvas.SetLeft(_pill, 0);
        Canvas.SetTop(_pill, 0);
        _pill.Transitions = ArcThemeManager.ReducedMotion
            ? null
            : new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = GlassMotion.Morph,
                    Easing = GlassMotion.MorphEase
                }
            };
        _pill.RenderTransform = TransformOperations.Parse($"translateY({point.Value.Y:0.##}px)");
    }

    // ---------------------------------------------------------------- dismissal

    private void AttachDismiss()
    {
        DetachDismiss();
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        _dismissTarget = top;
        _dismissHandler = (_, e) =>
        {
            if (e.Source is Visual source && IsWithin(source, this))
            {
                return;
            }

            Close(returnFocus: false);
        };
        top.AddHandler(PointerPressedEvent, _dismissHandler, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachDismiss()
    {
        if (_dismissTarget is not null && _dismissHandler is not null)
        {
            _dismissTarget.RemoveHandler(PointerPressedEvent, _dismissHandler);
        }

        _dismissTarget = null;
        _dismissHandler = null;
    }

    private static bool IsWithin(Visual source, Visual owner)
    {
        for (Visual? current = source; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, owner))
            {
                return true;
            }
        }

        return false;
    }
}




