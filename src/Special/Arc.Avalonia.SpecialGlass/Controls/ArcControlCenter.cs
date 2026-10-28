using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

public sealed class ArcFocusMode
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Icon { get; set; } = "moon";

    public string Description { get; set; } = string.Empty;

    public int DefaultMinutes { get; set; } = 45;
}

public sealed class ArcChannelOption
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool DefaultOn { get; set; }
}

/// <summary>
/// Workspace quick settings: tiles for focus, notifications, presenting and quiet hours that morph into a
/// detail, a focus duration dial, and rubber-banded meters for alert volume and text size.
///
/// Platform approximations: the tile-to-detail morph is a fade and scale within the fixed panel instead of
/// a shared layout animation, because the panel keeps one height; the detail dims the grid behind it with
/// an in-panel scrim. Glass is layered translucency. No Pro source was used.
/// </summary>
public sealed class ArcControlCenter : Panel
{
    public static readonly StyledProperty<IList<ArcFocusMode>?> FocusModesProperty =
        AvaloniaProperty.Register<ArcControlCenter, IList<ArcFocusMode>?>(nameof(FocusModes));

    public static readonly StyledProperty<IList<ArcChannelOption>?> ChannelsProperty =
        AvaloniaProperty.Register<ArcControlCenter, IList<ArcChannelOption>?>(nameof(Channels));

    public static readonly StyledProperty<IList<string>?> PeopleProperty =
        AvaloniaProperty.Register<ArcControlCenter, IList<string>?>(nameof(People));

    public static readonly StyledProperty<double> DefaultVolumeProperty =
        AvaloniaProperty.Register<ArcControlCenter, double>(nameof(DefaultVolume), 60);

    public static readonly StyledProperty<double> DefaultTextSizeProperty =
        AvaloniaProperty.Register<ArcControlCenter, double>(nameof(DefaultTextSize), 100);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcControlCenter, string>(nameof(Label), "Quick settings");

    private sealed class Tile
    {
        public string Id = string.Empty;
        public Border Shell = null!;
        public ArcButton Button = null!;
        public ArcIcon Icon = null!;
        public TextBlock Value = null!;
    }

    private readonly Border _panel;
    private readonly StackPanel _main;
    private readonly Panel _body;
    private readonly Panel _detailHost;
    private readonly Border _detailScrim;
    private readonly Border _detailCard;
    private readonly StackPanel _detailBody;
    private readonly ArcButton _detailBack;
    private readonly TextBlock _detailTitle;
    private readonly TextBlock _status;
    private readonly List<Tile> _tiles = new();
    private readonly Dictionary<string, bool> _channels = new(StringComparer.Ordinal);
    private readonly List<Border> _channelSwitches = new();
    private readonly List<ArcChannelOption> _channelList = new();

    private ArcDurationDial? _dial;
    private string? _modeId;
    private bool _focusActive;
    private int _focusMinutes;
    private bool _notifications = true;
    private bool _presenting;
    private bool _quiet;
    private double _volume;
    private double _textSize;
    private string? _detail;
    private Control? _detailReturn;
    private bool _built;

    static ArcControlCenter()
    {
        FocusModesProperty.Changed.AddClassHandler<ArcControlCenter>((center, _) => center.BuildTiles());
        ChannelsProperty.Changed.AddClassHandler<ArcControlCenter>((center, _) => center.BuildTiles());
        PeopleProperty.Changed.AddClassHandler<ArcControlCenter>((center, _) => center.BuildTiles());
        DefaultVolumeProperty.Changed.AddClassHandler<ArcControlCenter>((center, _) => center.SeedDefaults());
        DefaultTextSizeProperty.Changed.AddClassHandler<ArcControlCenter>((center, _) => center.SeedDefaults());
    }

    public ArcControlCenter()
    {
        ClipToBounds = false;
        Width = 360;
        Height = 470;

        _volume = DefaultVolume;
        _textSize = DefaultTextSize;

        _status = new TextBlock
        {
            Text = "Focus off",
            Classes = { "arc-caption" },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        };

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(2, 0, 2, 12)
        };
        header.Children.Add(new TextBlock
        {
            Text = "Quick settings",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        });
        Grid.SetColumn(_status, 1);
        header.Children.Add(_status);

        _main = new StackPanel { Spacing = 10 };
        _detailBody = new StackPanel { Spacing = 10 };

        _detailBack = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 28,
            Height = 28,
            MinWidth = 28,
            MinHeight = 28,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(9),
            Content = ArcGlass.Icon("chevron-left", 15)
        };
        _detailBack.Classes.Add("sg-plain");
        _detailBack.Click += (_, _) => CloseDetail();
        _detailTitle = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };
        var detailHeader = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 8,
            Children = { _detailBack, _detailTitle }
        };
        Grid.SetColumn(_detailTitle, 1);

        _detailCard = ArcGlass.Panel(20, new Thickness(14), floating: true, fillAlpha: 0.9);
        _detailCard.Child = new StackPanel { Spacing = 10, Children = { detailHeader, _detailBody } };
        _detailCard.HorizontalAlignment = HorizontalAlignment.Stretch;
        _detailCard.VerticalAlignment = VerticalAlignment.Stretch;
        _detailCard.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        _detailScrim = new Border
        {
            CornerRadius = new CornerRadius(20),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Scrim, 0.45))
        };
        _detailScrim.PointerPressed += (_, _) => CloseDetail();

        _detailHost = new Panel
        {
            IsVisible = false,
            Opacity = 0,
            Children = { _detailScrim, _detailCard }
        };

        _body = new Panel { Children = { _main, _detailHost } };
        _panel = ArcGlass.Panel(26, new Thickness(18), floating: true, fillAlpha: 0.62);
        _panel.Child = new StackPanel { Children = { header, _body } };
        Children.Add(_panel);

        AddHandler(KeyDownEvent, OnKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public event EventHandler? StateChanged;

    public IList<ArcFocusMode>? FocusModes
    {
        get => GetValue(FocusModesProperty);
        set => SetValue(FocusModesProperty, value);
    }

    public IList<ArcChannelOption>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public IList<string>? People
    {
        get => GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    public double DefaultVolume
    {
        get => GetValue(DefaultVolumeProperty);
        set => SetValue(DefaultVolumeProperty, value);
    }

    public double DefaultTextSize
    {
        get => GetValue(DefaultTextSizeProperty);
        set => SetValue(DefaultTextSizeProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string? OpenDetailId => _detail;

    /// <summary>Opens a detail face; the page uses this to present the focus dial in a screenshot.</summary>
    public void Open(string id) => OpenDetail(id);

    private void SeedDefaults()
    {
        if (_built)
        {
            return;
        }

        _volume = DefaultVolume;
        _textSize = DefaultTextSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        BuildTiles();
    }

    private List<ArcFocusMode> ModeList() => FocusModes?.ToList() ?? new List<ArcFocusMode>
    {
        new() { Id = "deep", Label = "Deep work", Icon = "briefcase", Description = "Silences everything but mentions", DefaultMinutes = 50 },
        new() { Id = "rest", Label = "Rest", Icon = "moon", Description = "Silences all alerts", DefaultMinutes = 30 }
    };

    private List<ArcChannelOption> ChannelList() => Channels?.ToList() ?? new List<ArcChannelOption>
    {
        new() { Id = "mentions", Label = "Mentions", Description = "Direct mentions and replies", DefaultOn = true },
        new() { Id = "deploys", Label = "Deploys", Description = "Production releases only" }
    };

    private bool _defaultsApplied;

    private void BuildTiles()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        _built = true;
        if (!_defaultsApplied)
        {
            _defaultsApplied = true;
            _volume = DefaultVolume;
            _textSize = DefaultTextSize;
        }

        if (_channels.Count == 0)
        {
            foreach (var channel in ChannelList())
            {
                _channels[channel.Id] = channel.DefaultOn;
            }
        }

        _main.Children.Clear();
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        var focus = BuildTile("focus", "moon", "Focus", opensDetail: true);
        var notifications = BuildTile("notifications", "bell", "Notifications", opensDetail: true);
        var presenting = BuildTile("presenting", "panel-top", "Presenting", opensDetail: false);
        var quiet = BuildTile("quiet", "clock", "Quiet hours", opensDetail: false);

        grid.Children.Add(focus);
        Grid.SetColumn(notifications, 1);
        grid.Children.Add(notifications);
        Grid.SetRow(presenting, 1);
        grid.Children.Add(presenting);
        Grid.SetRow(quiet, 1);
        Grid.SetColumn(quiet, 1);
        grid.Children.Add(quiet);
        _main.Children.Add(grid);

        var volume = new ArcRubberMeter
        {
            Label = "Alert volume",
            Minimum = 0,
            Maximum = 100,
            Step = 1,
            Value = _volume
        };
        volume.ValueChanged += (_, value) =>
        {
            _volume = value;
            FireState();
        };
        _main.Children.Add(volume);

        var textSize = new ArcRubberMeter
        {
            Label = "Text size",
            Minimum = 85,
            Maximum = 130,
            Step = 5,
            Value = _textSize
        };
        textSize.ValueChanged += (_, value) =>
        {
            _textSize = value;
            FireState();
        };
        _main.Children.Add(textSize);

        UpdateTiles();
    }

    private Control BuildTile(string id, string icon, string label, bool opensDetail)
    {
        var value = new TextBlock
        {
            Text = "Off",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var iconTile = ArcGlass.GlyphTile(icon, 30, iconSize: 15);

        var content = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                iconTile,
                new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    FontWeight = FontWeight.Medium,
                    Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                },
                value
            }
        };

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Background = Brushes.Transparent,
            Padding = new Thickness(9, 8),
            CornerRadius = new CornerRadius(16),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = content
        };
        button.Classes.Add("sg-plain");
        AutomationProperties.SetName(button, label);

        var shell = ArcGlass.Inset(18, alpha: 0.55);
        shell.Height = 94;

        var host = new Panel { Children = { button } };
        if (opensDetail)
        {
            var open = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Compact = true,
                Size = ArcButtonSize.Sm,
                Width = 24,
                Height = 24,
                MinWidth = 24,
                MinHeight = 24,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(8),
                Content = ArcGlass.Icon("chevron-right", 13),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0)
            };
            open.Classes.Add("sg-plain");
            AutomationProperties.SetName(open, $"{label} options");
            AutomationProperties.SetHelpText(open, "Opens the detail for this setting");
            open.Click += (_, _) => OpenDetail(id);
            host.Children.Add(open);
            button.Click += (_, _) => OpenDetail(id);
        }
        else
        {
            button.Click += (_, _) => Toggle(id);
        }

        shell.Child = host;
        _tiles.Add(new Tile { Id = id, Shell = shell, Button = button, Value = value });
        return shell;
    }

    private void UpdateTiles()
    {
        foreach (var tile in _tiles)
        {
            var (text, on) = tile.Id switch
            {
                "focus" => (_focusActive ? $"{_focusMinutes} min" : "Off", _focusActive),
                "notifications" => (_notifications ? "On" : "Off", _notifications),
                "presenting" => (_presenting ? "On" : "Off", _presenting),
                _ => (_quiet ? "On" : "Off", _quiet)
            };

            tile.Value.Text = text;
            tile.Value.Foreground = ArcGlass.Solid(ArcGlass.Color(on ? ArcTokens.AccentStrong : ArcTokens.TextMuted));
            tile.Value.FontWeight = on ? FontWeight.Medium : FontWeight.Normal;
            tile.Shell.Background = ArcGlass.Solid(ArcGlass.ArcAlpha(on ? ArcTokens.Accent : ArcTokens.Surface, on ? 0.18 : 0.30));
            tile.Shell.BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(on ? ArcTokens.Accent : ArcTokens.Surface, on ? 0.34 : 0.24));
            AutomationProperties.SetItemStatus(tile.Button, on ? "On" : "Off");
        }

        _status.Text = _focusActive ? $"Focus {_focusMinutes} min" : "Focus off";
        AutomationProperties.SetName(_panel, Label);
    }

    private void Toggle(string id)
    {
        switch (id)
        {
            case "focus":
                StartFocus(ModeList()[0]);
                break;
            case "notifications":
                _notifications = !_notifications;
                break;
            case "presenting":
                _presenting = !_presenting;
                break;
            default:
                _quiet = !_quiet;
                break;
        }

        UpdateTiles();
        FireState();
    }

    private void StartFocus(ArcFocusMode mode)
    {
        _modeId = mode.Id;
        _focusMinutes = mode.DefaultMinutes;
        _focusActive = true;
        UpdateTiles();
        FireState();
    }

    private void FireState() => StateChanged?.Invoke(this, EventArgs.Empty);

    private void OpenDetail(string id)
    {
        if (!_built || _detail == id)
        {
            return;
        }

        _detail = id;
        _detailReturn = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        _detailTitle.Text = id == "focus" ? "Focus" : "Notifications";
        _detailBody.Children.Clear();
        if (id == "focus")
        {
            BuildFocusDetail();
        }
        else
        {
            BuildNotificationsDetail();
        }

        _detailHost.IsVisible = true;
        _main.Opacity = 0.35;
        if (ArcThemeManager.ReducedMotion)
        {
            _detailHost.Opacity = 1;
            _detailCard.RenderTransform = TransformOperations.Parse("scale(1)");
            _detailBack.Focus();
            return;
        }

        _detailHost.Opacity = 0;
        _detailCard.RenderTransform = TransformOperations.Parse("scale(0.96)");
        Dispatcher.UIThread.Post(() =>
        {
            _detailHost.Opacity = 1;
            _detailCard.Transitions = new global::Avalonia.Animation.Transitions
            {
                new global::Avalonia.Animation.TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = GlassMotion.Smooth,
                    Easing = GlassMotion.SmoothEase
                },
                new global::Avalonia.Animation.DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = GlassMotion.Fast,
                    Easing = Arc.Avalonia.Motion.ArcMotion.EaseStandard
                }
            };
            _detailCard.RenderTransform = TransformOperations.Parse("scale(1)");
            _detailBack.Focus();
        }, DispatcherPriority.Render);
    }

    private void CloseDetail()
    {
        if (_detail is null)
        {
            return;
        }

        _detail = null;
        _detailHost.IsVisible = false;
        _detailHost.Opacity = 0;
        _main.Opacity = 1;
        _detailReturn?.Focus();
    }

    private void BuildFocusDetail()
    {
        var modes = ModeList();
        _modeId ??= modes[0].Id;
        var mode = modes.FirstOrDefault(item => item.Id == _modeId) ?? modes[0];
        _focusMinutes = _focusActive ? _focusMinutes : mode.DefaultMinutes;

        _detailBody.Children.Add(new TextBlock
        {
            Text = "Mode",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });

        foreach (var option in modes)
        {
            var check = new TextBlock
            {
                Text = option.Id == _modeId ? "✓" : string.Empty,
                FontSize = 13,
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong)),
                VerticalAlignment = VerticalAlignment.Center
            };
            var icon = ArcGlass.GlyphTile(option.Icon, 28, accent: option.Id == _modeId, iconSize: 14);
            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = option.Label,
                Classes = { "arc-body-sm", "arc-medium" },
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
            });
            text.Children.Add(new TextBlock
            {
                Text = option.Description,
                Classes = { "arc-caption" },
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
            });
            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                ColumnSpacing = 8,
                Children = { icon, text, check }
            };
            Grid.SetColumn(text, 1);
            Grid.SetColumn(check, 2);

            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Content = row,
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(6, 5),
                CornerRadius = new CornerRadius(10),
                Height = 46,
                MinHeight = 46
            };
            button.Classes.Add("sg-plain");
            AutomationProperties.SetName(button, $"{option.Label}. {option.Description}");
            button.Click += (_, _) =>
            {
                _modeId = option.Id;
                _focusMinutes = option.DefaultMinutes;
                if (_dial is not null)
                {
                    _dial.Value = option.DefaultMinutes;
                }

                _detailBody.Children.Clear();
                BuildFocusDetail();
                UpdateTiles();
            };
            _detailBody.Children.Add(button);
        }

        var dial = new ArcDurationDial
        {
            Width = 132,
            Height = 132,
            Value = _focusMinutes,
            Caption = "minutes",
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 2)
        };
        dial.ValueChanged += (_, value) => _focusMinutes = (int)Math.Round(value);
        _dial = dial;
        _detailBody.Children.Add(dial);

        var people = People is { Count: > 0 } list
            ? list
            : new List<string> { "Ana Ruiz", "Sam Lee", "Noah Park" };
        var peopleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };
        foreach (var person in people.Take(3))
        {
            peopleRow.Children.Add(ArcGlass.Avatar(person, 24));
        }

        peopleRow.Children.Add(new TextBlock
        {
            Text = "can still reach you",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0)
        });
        _detailBody.Children.Add(peopleRow);

        var start = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = _focusActive ? "Update session" : "Start session",
            Height = 36,
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        AutomationProperties.SetName(start, _focusActive ? "Update session" : "Start session");
        start.Click += (_, _) =>
        {
            _focusActive = true;
            _focusMinutes = (int)Math.Round(_dial?.Value ?? _focusMinutes);
            CloseDetail();
            UpdateTiles();
            FireState();
        };
        _detailBody.Children.Add(start);
    }

    private void BuildNotificationsDetail()
    {
        foreach (var channel in ChannelList())
        {
            if (!_channels.ContainsKey(channel.Id))
            {
                _channels[channel.Id] = channel.DefaultOn;
            }

            var on = _channels[channel.Id];
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
            var track = new Border
            {
                Width = 40,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                Background = ArcGlass.Solid(ArcGlass.Color(on ? ArcTokens.ControlOn : ArcTokens.ControlTrack)),
                BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
                BorderThickness = new Thickness(1),
                Child = knob,
                VerticalAlignment = VerticalAlignment.Center
            };

            var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = channel.Label,
                Classes = { "arc-body-sm", "arc-medium" },
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
            });
            text.Children.Add(new TextBlock
            {
                Text = channel.Description,
                Classes = { "arc-caption" },
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
            });

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { text, track }
            };
            Grid.SetColumn(track, 1);

            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Content = row,
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(6, 6),
                CornerRadius = new CornerRadius(10),
                Height = 50,
                MinHeight = 50
            };
            button.Classes.Add("sg-plain");
            AutomationProperties.SetName(button, channel.Label);
            var id = channel.Id;
            button.Click += (_, _) =>
            {
                _channels[id] = !_channels[id];
                var isOn = _channels[id];
                knob.HorizontalAlignment = isOn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
                knob.Background = ArcGlass.Solid(ArcGlass.Color(isOn ? ArcTokens.ControlGlyph : ArcTokens.ControlThumb));
                track.Background = ArcGlass.Solid(ArcGlass.Color(isOn ? ArcTokens.ControlOn : ArcTokens.ControlTrack));
                AutomationProperties.SetItemStatus(button, isOn ? "On" : "Off");
                FireState();
            };
            _detailBody.Children.Add(button);
        }

        var done = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = "Done",
            Height = 36,
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        done.Click += (_, _) => CloseDetail();
        _detailBody.Children.Add(done);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _detail is not null)
        {
            e.Handled = true;
            CloseDetail();
        }
    }
}


