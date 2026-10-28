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
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Controls;

/// <summary>One row in a sheet: either a drill down to another sheet or an immediate toggle.</summary>
public sealed class ArcSheetRow
{
    public string Label { get; set; } = string.Empty;

    public string? Detail { get; set; }

    public string? Icon { get; set; }

    /// <summary>Id of the sheet this row pushes. Ignored when <see cref="Toggle"/> is true.</summary>
    public string? TargetSheet { get; set; }

    public bool Toggle { get; set; }

    public bool ToggleDefault { get; set; }
}

/// <summary>One declared level of the stack.</summary>
public sealed class ArcSheetDef
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? FooterLabel { get; set; }

    public IList<ArcSheetRow> Rows { get; set; } = new List<ArcSheetRow>();
}

public enum ArcSheetStackMode
{
    Auto,
    Sheet,
    Dialog
}

/// <summary>
/// Nested sheets that stack with depth: a new sheet rises from the bottom edge while the one below scales
/// back and peeks above it, the top sheet follows a vertical drag with rubber banding and pops on a fling,
/// Escape closes only the top level, and the trigger that opened a level gets focus back when it pops.
/// Above the breakpoint the same declaration becomes stacked centred dialogs.
///
/// Platform approximations: Avalonia has no shared-layout depth animation, so the stack positions are one
/// function of the level and animate with transform transitions; focus containment is applied with
/// KeyboardNavigation cycling rather than a custom trap. No Pro source was used.
/// </summary>
public sealed class ArcSheetStack : Panel
{
    public static readonly StyledProperty<IList<ArcSheetDef>?> SheetsProperty =
        AvaloniaProperty.Register<ArcSheetStack, IList<ArcSheetDef>?>(nameof(Sheets));

    public static readonly StyledProperty<ArcSheetStackMode> ModeProperty =
        AvaloniaProperty.Register<ArcSheetStack, ArcSheetStackMode>(nameof(Mode));

    public static readonly StyledProperty<double> BreakpointProperty =
        AvaloniaProperty.Register<ArcSheetStack, double>(nameof(Breakpoint), 640);

    public static readonly StyledProperty<IList<string>?> InitialStackProperty =
        AvaloniaProperty.Register<ArcSheetStack, IList<string>?>(nameof(InitialStack));

    public static readonly StyledProperty<object?> PageContentProperty =
        AvaloniaProperty.Register<ArcSheetStack, object?>(nameof(PageContent));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcSheetStack, string>(nameof(Label), "Sheets");

    private sealed class SheetPanel
    {
        public ArcSheetDef Def = new();
        public Border Shell = null!;
        public Border Scrim = null!;
        public StackPanel Body = null!;
        public ArcButton Back = null!;
        public ArcButton Close = null!;
        public TextBlock Title = null!;
        public TextBlock Description = null!;
        public Control Footer = null!;
        public bool Built;
    }

    private readonly Border _page;
    private readonly ContentControl _pageHost;
    private readonly Panel _layer;
    private readonly Border _scrim;
    private readonly Canvas _sheetHost;
    private readonly List<string> _stack = new();
    private readonly Dictionary<string, SheetPanel> _panels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _toggles = new(StringComparer.Ordinal);
    private bool _dragging;
    private double _dragStartY;
    private double _dragOffset;
    private ulong _lastTime;
    private double _lastY;
    private double _velocity;
    private bool _built;
    private Control? _lastTrigger;

    static ArcSheetStack()
    {
        SheetsProperty.Changed.AddClassHandler<ArcSheetStack>((stack, _) => stack.Rebuild());
        InitialStackProperty.Changed.AddClassHandler<ArcSheetStack>((stack, _) => stack.Rebuild());
        ModeProperty.Changed.AddClassHandler<ArcSheetStack>((stack, _) => stack.Position(false));
        PageContentProperty.Changed.AddClassHandler<ArcSheetStack>((stack, _) => stack.UpdatePage());
        LabelProperty.Changed.AddClassHandler<ArcSheetStack>((stack, _) => stack.UpdateLabel());
    }

    public ArcSheetStack()
    {
        ClipToBounds = false;
        Focusable = false;

        _pageHost = new ContentControl();
        _page = new Border
        {
            CornerRadius = new CornerRadius(20),
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Surface)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            Child = _pageHost
        };

        _scrim = new Border
        {
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Scrim, 0.55)),
            Opacity = 0,
            IsVisible = false
        };
        _scrim.PointerPressed += (_, _) => Pop();

        _sheetHost = new Canvas { ClipToBounds = false };
        _layer = new Panel { Children = { _scrim, _sheetHost }, IsVisible = false };

        Children.Add(_page);
        Children.Add(_layer);

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        LayoutUpdated += (_, _) =>
        {
            if (_stack.Count > 0)
            {
                Position(animate: false);
            }
        };
        UpdateLabel();
    }

    /// <summary>Raised with the ids of the open sheets, bottom first.</summary>
    public event EventHandler<IReadOnlyList<string>>? StackChanged;

    public event EventHandler<string>? Popped;

    public IList<ArcSheetDef>? Sheets
    {
        get => GetValue(SheetsProperty);
        set => SetValue(SheetsProperty, value);
    }

    public ArcSheetStackMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public double Breakpoint
    {
        get => GetValue(BreakpointProperty);
        set => SetValue(BreakpointProperty, value);
    }

    /// <summary>Sheets open at first, bottom first.</summary>
    public IList<string>? InitialStack
    {
        get => GetValue(InitialStackProperty);
        set => SetValue(InitialStackProperty, value);
    }

    /// <summary>Content behind the sheet layer, such as the settings page with its triggers.</summary>
    public object? PageContent
    {
        get => GetValue(PageContentProperty);
        set => SetValue(PageContentProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public IReadOnlyList<string> OpenSheets => _stack;

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetName(_scrim, "Dismiss the top sheet");
    }

    private void UpdatePage()
    {
        _pageHost.Content = PageContent;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        _sheetHost.Children.Clear();
        _panels.Clear();
        _stack.Clear();
        foreach (var id in InitialStack ?? (IList<string>)Array.Empty<string>())
        {
            if (Find(id) is not null && !_stack.Contains(id))
            {
                _stack.Add(id);
            }
        }

        _built = true;
        UpdatePage();
        foreach (var id in _stack)
        {
            EnsurePanel(id);
        }

        Position(animate: false);
        SyncLayer();
    }

    private List<ArcSheetDef> SheetList() => Sheets?.ToList() ?? new List<ArcSheetDef>();

    private ArcSheetDef? Find(string id) =>
        SheetList().FirstOrDefault(sheet => string.Equals(sheet.Id, id, StringComparison.Ordinal));

    private bool IsSheetMode => Mode switch
    {
        ArcSheetStackMode.Sheet => true,
        ArcSheetStackMode.Dialog => false,
        _ => Bounds.Width < Breakpoint
    };

    /// <summary>Pushes a sheet; an id already in the stack pops back to it instead.</summary>
    public void Push(string id)
    {
        if (Find(id) is null)
        {
            return;
        }

        var existing = _stack.IndexOf(id);
        if (existing >= 0)
        {
            while (_stack.Count > existing + 1)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }

            Position(animate: true);
            SyncLayer();
            return;
        }

        _lastTrigger = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        _stack.Add(id);
        EnsurePanel(id);
        Position(animate: true);
        SyncLayer();
        Dispatcher.UIThread.Post(() => FocusTop(), DispatcherPriority.Loaded);
        StackChanged?.Invoke(this, _stack);
    }

    /// <summary>Pops the top sheet.</summary>
    public void Pop()
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var id = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        Position(animate: true);
        SyncLayer();
        Popped?.Invoke(this, id);
        StackChanged?.Invoke(this, _stack);
        Dispatcher.UIThread.Post(() =>
        {
            if (_stack.Count == 0)
            {
                var target = _lastTrigger ?? _pageHost;
                target?.Focus();
            }
            else
            {
                FocusTop();
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>Closes every sheet.</summary>
    public void CloseAll()
    {
        if (_stack.Count == 0)
        {
            return;
        }

        _stack.Clear();
        Position(animate: true);
        SyncLayer();
        StackChanged?.Invoke(this, _stack);
        Dispatcher.UIThread.Post(() => (_lastTrigger ?? _pageHost)?.Focus(), DispatcherPriority.Loaded);
    }

    private void SyncLayer()
    {
        _layer.IsVisible = _stack.Count > 0;
        if (!_layer.IsVisible)
        {
            _scrim.IsVisible = false;
            _scrim.Opacity = 0;
            return;
        }

        _scrim.IsVisible = true;
        var target = IsSheetMode ? 0.55 : 0.42;
        if (ArcThemeManager.ReducedMotion)
        {
            _scrim.Opacity = target;
            return;
        }

        _scrim.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = ArcMotion.Standard,
                Easing = ArcMotion.EaseStandard
            }
        };
        _scrim.Opacity = target;
    }

    private SheetPanel EnsurePanel(string id)
    {
        if (_panels.TryGetValue(id, out var existing))
        {
            return existing;
        }

        var def = Find(id) ?? new ArcSheetDef { Id = id, Title = id };
        var panel = new SheetPanel { Def = def };

        panel.Back = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 32,
            Height = 32,
            MinWidth = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Content = ArcGlass.Icon("chevron-left", 16),
            IsVisible = false
        };
        panel.Back.Classes.Add("sg-plain");
        panel.Back.Click += (_, _) => Pop();

        panel.Close = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 32,
            Height = 32,
            MinWidth = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Content = ArcGlass.Icon("x", 16)
        };
        panel.Close.Classes.Add("sg-plain");
        panel.Close.Click += (_, _) => CloseFrom();

        panel.Title = new TextBlock
        {
            Text = def.Title,
            FontSize = 18,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var titleRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleRow.Children.Add(panel.Back);
        Grid.SetColumn(panel.Title, 1);
        titleRow.Children.Add(panel.Title);
        Grid.SetColumn(panel.Close, 2);
        titleRow.Children.Add(panel.Close);

        panel.Description = new TextBlock
        {
            Text = def.Description,
            Classes = { "arc-body-sm" },
            TextWrapping = TextWrapping.Wrap,
            IsVisible = !string.IsNullOrWhiteSpace(def.Description),
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };

        panel.Body = new StackPanel { Spacing = 2 };
        panel.Footer = BuildFooter(def);

        var content = new StackPanel
        {
            Spacing = 12,
            Children = { titleRow, panel.Description, panel.Body, panel.Footer }
        };

        panel.Shell = new Border
        {
            CornerRadius = new CornerRadius(22),
            Background = ArcGlass.GlassFill(alpha: 0.86),
            BorderBrush = ArcGlass.RimBrush(),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowFloating),
            ClipToBounds = true,
            Padding = new Thickness(18, 14, 18, 16),
            RenderTransformOrigin = new RelativePoint(0.5, 0, RelativeUnit.Relative),
            Child = content
        };
        KeyboardNavigation.SetTabNavigation(panel.Shell, KeyboardNavigationMode.Cycle);

        panel.Shell.PointerPressed += (_, e) => OnSheetPressed(panel, e);
        panel.Shell.PointerMoved += (_, e) => OnSheetMoved(panel, e);
        panel.Shell.PointerReleased += (_, e) => OnSheetReleased(panel, e);
        panel.Shell.PointerCaptureLost += (_, _) =>
        {
            _dragging = false;
            _dragOffset = 0;
            Position(animate: false);
        };

        panel.Scrim = new Border
        {
            CornerRadius = new CornerRadius(22),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Scrim, 0.28)),
            IsHitTestVisible = false,
            Opacity = 0
        };

        AutomationProperties.SetName(panel.Shell, def.Title);
        AutomationProperties.SetLiveSetting(panel.Shell, AutomationLiveSetting.Polite);
        AutomationProperties.SetHelpText(panel.Shell, "Drag down or press Escape to close this sheet.");

        _panels[id] = panel;
        _sheetHost.Children.Add(panel.Shell);
        return panel;
    }

    private Control BuildFooter(ArcSheetDef def)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Height = 38,
            MinHeight = 38,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = def.FooterLabel ?? "Done",
            Margin = new Thickness(0, 6, 0, 0)
        };
        AutomationProperties.SetName(button, def.FooterLabel ?? "Done");
        button.Click += (_, _) => CloseAll();
        return button;
    }

    private void RebuildRows(SheetPanel panel)
    {
        panel.Body.Children.Clear();
        foreach (var row in panel.Def.Rows)
        {
            panel.Body.Children.Add(BuildRow(panel, row));
        }
    }

    private Control BuildRow(SheetPanel panel, ArcSheetRow row)
    {
        var glyph = row.Icon is not null
            ? ArcGlass.GlyphTile(row.Icon, 30, accent: !row.Toggle, iconSize: 15)
            : null;

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = row.Label,
            Classes = { "arc-body-sm", "arc-medium" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        if (!string.IsNullOrWhiteSpace(row.Detail))
        {
            text.Children.Add(new TextBlock
            {
                Text = row.Detail,
                Classes = { "arc-caption" },
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted)),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        var trailing = new Border();
        if (row.Toggle)
        {
            trailing = BuildSwitchTrack(panel, row);
        }
        else
        {
            trailing.Child = ArcGlass.Icon("chevron-right", 15);
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (glyph is not null)
        {
            grid.Children.Add(glyph);
        }

        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Grid.SetColumn(trailing, 2);
        grid.Children.Add(trailing);

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Content = grid,
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(8, 6),
            CornerRadius = new CornerRadius(12)
        };
        button.Classes.Add("sg-plain");
        AutomationProperties.SetName(button, row.Label);

        if (row.Toggle)
        {
            UpdateSwitchState(panel, row);
            AutomationProperties.SetItemStatus(button, IsSwitchOn(panel, row) ? "On" : "Off");
            button.Click += (_, _) =>
            {
                ToggleSwitch(panel, row);
                AutomationProperties.SetItemStatus(button, IsSwitchOn(panel, row) ? "On" : "Off");
            };
        }
        else
        {
            AutomationProperties.SetHelpText(button, row.TargetSheet is not null ? "Opens the next sheet" : string.Empty);
            button.Click += (_, _) =>
            {
                if (row.TargetSheet is not null)
                {
                    Push(row.TargetSheet);
                }
            };
        }

        return button;
    }

    private Border BuildSwitchTrack(SheetPanel panel, ArcSheetRow row)
    {
        var key = panel.Def.Id + "/" + row.Label;
        if (!_toggles.ContainsKey(key))
        {
            _toggles[key] = row.ToggleDefault;
        }

        var on = _toggles[key];
        var knob = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = ArcGlass.Solid(ArcGlass.Color(on ? ArcTokens.ControlGlyph : ArcTokens.ControlThumb)),
            HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ControlThumbShadow)
        };
        return new Border
        {
            Width = 40,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = ArcGlass.Solid(ArcGlass.Color(on ? ArcTokens.ControlOn : ArcTokens.ControlTrack)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
            BorderThickness = new Thickness(1),
            Child = knob,
            Tag = key,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private bool IsSwitchOn(SheetPanel panel, ArcSheetRow row)
    {
        var key = panel.Def.Id + "/" + row.Label;
        return _toggles.TryGetValue(key, out var value) && value;
    }

    private void ToggleSwitch(SheetPanel panel, ArcSheetRow row)
    {
        var key = panel.Def.Id + "/" + row.Label;
        _toggles[key] = !IsSwitchOn(panel, row);
        UpdateSwitchState(panel, row);
    }

    private void UpdateSwitchState(SheetPanel panel, ArcSheetRow row)
    {
        var key = panel.Def.Id + "/" + row.Label;
        var on = _toggles.TryGetValue(key, out var value) && value;
        foreach (var child in panel.Body.Children.OfType<ArcButton>())
        {
            if (child.Content is Grid grid && grid.Children.OfType<Border>().LastOrDefault() is { } trailing
                && trailing.Child is Border track && Equals(track.Tag, key))
            {
                track.Background = ArcGlass.Solid(ArcGlass.Color(on ? ArcTokens.ControlOn : ArcTokens.ControlTrack));
                if (track.Child is Border knob)
                {
                    knob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                    knob.Background = ArcGlass.Solid(ArcGlass.Color(on ? ArcTokens.ControlGlyph : ArcTokens.ControlThumb));
                }
            }
        }
    }

    private void CloseFrom()
    {
        if (_stack.Count > 1)
        {
            CloseAll();
        }
        else
        {
            Pop();
        }
    }

    private void OnSheetPressed(SheetPanel panel, PointerPressedEventArgs e)
    {
        if (_stack.Count == 0 || !ReferenceEquals(panel, _panels[_stack[^1]]))
        {
            return;
        }

        if (!e.GetCurrentPoint(panel.Shell).Properties.IsLeftButtonPressed)
        {
            return;
        }

        // Only the header drags, so the body can still scroll and use its controls.
        if (e.GetPosition(panel.Shell).Y > 84)
        {
            return;
        }

        _dragging = true;
        _dragStartY = e.GetPosition(this).Y;
        _lastY = _dragStartY;
        _lastTime = e.Timestamp;
        _dragOffset = 0;
        _velocity = 0;
        e.Pointer.Capture(panel.Shell);
        e.Handled = true;
    }

    private void OnSheetMoved(SheetPanel panel, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var y = e.GetPosition(this).Y;
        var dy = y - _dragStartY;
        _dragOffset = dy > 0 ? dy : dy * 0.2;
        var delta = e.Timestamp - _lastTime;
        if (delta > 0)
        {
            _velocity = (y - _lastY) / delta;
        }

        _lastY = y;
        _lastTime = e.Timestamp;
        Position(animate: false);
        e.Handled = true;
    }

    private void OnSheetReleased(SheetPanel panel, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        var height = Math.Max(1, panel.Shell.Bounds.Height);
        if (_dragOffset > height * 0.35 || _velocity > 0.5)
        {
            _dragOffset = 0;
            Pop();
            return;
        }

        _dragOffset = 0;
        Position(animate: true);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _stack.Count > 0)
        {
            e.Handled = true;
            Pop();
        }
    }

    private void FocusTop()
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var panel = _panels[_stack[^1]];
        panel.Back.IsVisible = _stack.Count > 1;
        panel.Back.Focus();
    }

    private void Position(bool animate)
    {
        if (!_built || Bounds.Width <= 1)
        {
            return;
        }

        var sheetMode = IsSheetMode;
        var width = Math.Min(sheetMode ? 430 : 420, Bounds.Width - 24);
        var top = _stack.Count - 1;

        for (var i = 0; i < _stack.Count; i++)
        {
            var id = _stack[i];
            var panel = _panels[id];
            if (!panel.Built)
            {
                RebuildRows(panel);
                panel.Built = true;
            }

            var levelsAbove = top - i;
            var isTop = levelsAbove == 0;
            var shell = panel.Shell;
            shell.Width = width;
            shell.Measure(new Size(width, double.PositiveInfinity));
            // Sheets in a stack share one height so the parent is only a thin lip above the child;
            // dialogs size from their own content instead.
            var maxHeight = Bounds.Height * 0.86;
            var height = sheetMode
                ? Math.Min(Bounds.Height * 0.80, Bounds.Height - 36)
                : Math.Min(maxHeight, Math.Max(160, shell.DesiredSize.Height));
            shell.Height = height;
            shell.CornerRadius = sheetMode
                ? new CornerRadius(24, 24, 0, 0)
                : new CornerRadius(24);
            shell.Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Surface));

            var x = (Bounds.Width - width) / 2;
            var y = sheetMode
                ? Bounds.Height - height - 10 - (levelsAbove * 16)
                : Math.Max(12, ((Bounds.Height - height) / 2) - (levelsAbove * 14));
            if (isTop)
            {
                y += _dragOffset;
            }

            var scale = 1 - (0.045 * levelsAbove);
            Canvas.SetLeft(shell, x);
            Canvas.SetTop(shell, y);
            Canvas.SetLeft(panel.Scrim, x);
            Canvas.SetTop(panel.Scrim, y);
            panel.Scrim.Width = width;
            panel.Scrim.Height = height;
            panel.Scrim.CornerRadius = shell.CornerRadius;
            panel.Scrim.Opacity = isTop ? 0 : 0.55;
            panel.Scrim.IsVisible = false;

            shell.IsHitTestVisible = isTop;
            shell.Opacity = 1;
            shell.Transitions = animate && !ArcThemeManager.ReducedMotion && !_dragging
                ? new Transitions
                {
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
                }
                : null;
            shell.RenderTransform = TransformOperations.Parse($"scale({Math.Max(0.8, scale):0.###})");
            panel.Back.IsVisible = i > 0;
            AutomationProperties.SetName(panel.Close, _stack.Count > 1 ? "Close all sheets" : "Close sheet");
            AutomationProperties.SetItemStatus(shell, $"Sheet {i + 1} of {_stack.Count}");
        }

        SyncLayer();
    }

    // __PART3__
}









