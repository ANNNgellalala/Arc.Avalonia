using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

public sealed class ArcSharePerson
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

public sealed class ArcShareAccessOption
{
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Icon { get; set; } = "lock";
}

public sealed class ArcShareChannel
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Icon { get; set; } = "message-circle";

    public string DoneLabel { get; set; } = "Posted";
}

/// <summary>
/// A Share button that springs open into a glass share panel: copy link with in-place confirmation, a list
/// of access levels, channel buttons that keep their own pending and done state, recent people that turn
/// into chips, and a send button that folds the panel into a sent confirmation.
///
/// Platform approximations: the panel morph is a scale from the trigger rectangle rather than a CSS
/// auto-height layout animation; the access list stays open instead of the Escape-closes-list-first
/// detail; the clipboard can be unavailable, in which case the row reports a failure. No Pro source was
/// used.
/// </summary>
public sealed class ArcShareSheet : Panel
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ArcShareSheet, string>(nameof(Title), "Q3 roadmap");

    public static readonly StyledProperty<string> LinkProperty =
        AvaloniaProperty.Register<ArcShareSheet, string>(nameof(Link), "https://example.com/d/q3-roadmap");

    public static readonly StyledProperty<IList<ArcSharePerson>?> PeopleProperty =
        AvaloniaProperty.Register<ArcShareSheet, IList<ArcSharePerson>?>(nameof(People));

    public static readonly StyledProperty<IList<ArcShareAccessOption>?> AccessProperty =
        AvaloniaProperty.Register<ArcShareSheet, IList<ArcShareAccessOption>?>(nameof(Access));

    public static readonly StyledProperty<IList<ArcShareChannel>?> ChannelsProperty =
        AvaloniaProperty.Register<ArcShareSheet, IList<ArcShareChannel>?>(nameof(Channels));

    public static readonly StyledProperty<string?> AccessValueProperty =
        AvaloniaProperty.Register<ArcShareSheet, string?>(nameof(AccessValue));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcShareSheet, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcShareSheet, string>(nameof(Label), "Share");

    private const double PanelWidth = 384;

    private readonly ArcButton _trigger;
    private readonly Canvas _overlay;
    private readonly Border _panel;
    private readonly StackPanel _panelBody;
    private readonly HashSet<string> _picked = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _channelDone = new(StringComparer.Ordinal);
    private readonly List<ArcButton> _accessButtons = new();
    private readonly List<string> _accessValues = new();
    private readonly StackPanel _chips;
    private readonly StackPanel _peopleRow;
    private readonly StackPanel _channelRow;
    private readonly ArcButton _send;
    private TextBlock _copyLabel = null!;
    private ArcIcon _copyIcon = null!;
    private readonly TextBlock _status;
    private readonly Control _sentFace;
    private bool _copied;
    private bool _sending;
    private bool _sent;
    private bool _built;
    private bool _wasOpen;
    private DispatcherTimer? _copyTimer;
    private DispatcherTimer? _sendTimer;
    private TopLevel? _dismissTarget;
    private EventHandler<PointerPressedEventArgs>? _dismissHandler;

    static ArcShareSheet()
    {
        IsOpenProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.SyncOpen());
        TitleProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.UpdateHeading());
        LinkProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.UpdateLink());
        PeopleProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.BuildPeople());
        AccessProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.BuildAccess());
        ChannelsProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.BuildChannels());
        AccessValueProperty.Changed.AddClassHandler<ArcShareSheet>((sheet, _) => sheet.SyncAccess());
    }

    public ArcShareSheet()
    {
        ClipToBounds = false;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;

        _trigger = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Height = 36,
            MinHeight = 36,
            Padding = new Thickness(13, 0),
            CornerRadius = new CornerRadius(12),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    ArcGlass.Icon("share", 15, ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))),
                    new TextBlock
                    {
                        Text = "Share",
                        FontSize = 13,
                        FontWeight = FontWeight.Medium,
                        Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                    }
                }
            }
        };
        AutomationProperties.SetName(_trigger, Label);
        AutomationProperties.SetHelpText(_trigger, "Opens the share panel");
        _trigger.Click += (_, _) => Toggle();

        _overlay = new Canvas { ClipToBounds = false };

        _status = new TextBlock
        {
            Text = string.Empty,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        };
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);

        _panelBody = new StackPanel { Spacing = 9 };
        _panel = ArcGlass.Panel(26, new Thickness(18), floating: true, fillAlpha: 1.0);
        _panel.Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Surface));
        _panel.Width = PanelWidth;
        _panel.IsVisible = false;
        _panel.Opacity = 0;
        _panel.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
        _panel.Child = _panelBody;
        AutomationProperties.SetLiveSetting(_panel, AutomationLiveSetting.Polite);


        _chips = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _peopleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _channelRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        _send = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = "Send",
            Height = 38,
            MinHeight = 38,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        AutomationProperties.SetName(_send, "Send");
        _send.Click += (_, _) => Send();

        _sentFace = BuildSentFace();
        _sentFace.IsVisible = false;

        Children.Add(_trigger);
        Children.Add(_overlay);

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        UpdateHeading();
        BuildAccess();
        BuildChannels();
        BuildPeople();
        BuildPanel();
    }

    public event EventHandler? Sent;

    public event EventHandler<string>? Copied;

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Link
    {
        get => GetValue(LinkProperty);
        set => SetValue(LinkProperty, value);
    }

    public IList<ArcSharePerson>? People
    {
        get => GetValue(PeopleProperty);
        set => SetValue(PeopleProperty, value);
    }

    public IList<ArcShareAccessOption>? Access
    {
        get => GetValue(AccessProperty);
        set => SetValue(AccessProperty, value);
    }

    public IList<ArcShareChannel>? Channels
    {
        get => GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public string? AccessValue
    {
        get => GetValue(AccessValueProperty);
        set => SetValue(AccessValueProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private List<ArcSharePerson> PersonList() => People?.ToList() ?? new List<ArcSharePerson>
    {
        new() { Id = "ana", Name = "Ana Ruiz" },
        new() { Id = "sam", Name = "Sam Lee" },
        new() { Id = "noah", Name = "Noah Park" }
    };

    private List<ArcShareAccessOption> AccessList() => Access?.ToList() ?? new List<ArcShareAccessOption>
    {
        new() { Value = "team", Label = "Team", Description = "Anyone on the team can view", Icon = "lock" },
        new() { Value = "public", Label = "Anyone with the link", Description = "Can view and comment", Icon = "globe" }
    };

    private List<ArcShareChannel> ChannelList() => Channels?.ToList() ?? new List<ArcShareChannel>
    {
        new() { Id = "slack", Label = "Slack", Icon = "message-circle", DoneLabel = "Posted" },
        new() { Id = "email", Label = "Email", Icon = "mail", DoneLabel = "Sent" }
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _built = true;
        if (string.IsNullOrWhiteSpace(AccessValue))
        {
            SetCurrentValue(AccessValueProperty, AccessList()[0].Value);
        }

        BuildPanel();
        _wasOpen = false;
        if (IsOpen)
        {
            SyncOpen();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _copyTimer?.Stop();
        _sendTimer?.Stop();
        DetachDismiss();
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Picks a recent person for the Send to row; the page uses this to present a filled state.</summary>
    public void Pick(string id)
    {
        _picked.Add(id);
        if (_built)
        {
            BuildPanel();
        }
    }

    private void Toggle() => SetCurrentValue(IsOpenProperty, !IsOpen);

    private void UpdateHeading()
    {
        AutomationProperties.SetName(_trigger, $"{Label} “{Title}”");
        AutomationProperties.SetName(_panel, $"Share “{Title}”");
        if (_built)
        {
            BuildPanel();
        }
    }

    private void UpdateLink()
    {
        if (_built)
        {
            BuildPanel();
        }
    }

    private void BuildPanel()
    {
        if (!_built)
        {
            return;
        }

        _panelBody.Children.Clear();
        if (_sent)
        {
            _panelBody.Children.Add(_sentFace);
            return;
        }

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8
        };
        header.Children.Add(new TextBlock
        {
            Text = $"Share “{Title}”",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        });

        var close = new ArcButton
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
            Content = ArcGlass.Icon("x", 15),
            VerticalAlignment = VerticalAlignment.Top
        };
        close.Classes.Add("sg-plain");
        AutomationProperties.SetName(close, "Close share panel");
        close.Click += (_, _) => Close(returnFocus: true);
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        _panelBody.Children.Add(header);

        _panelBody.Children.Add(BuildLinkRow());

        var accessTitle = new TextBlock
        {
            Text = "General access",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted)),
            Margin = new Thickness(0, 2, 0, 0)
        };
        _panelBody.Children.Add(accessTitle);

        _accessButtons.Clear();
        _accessValues.Clear();
        foreach (var option in AccessList())
        {
            var row = BuildAccessRow(option);
            _panelBody.Children.Add(row);
        }

        _panelBody.Children.Add(new TextBlock
        {
            Text = "Post to",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });
        BuildChannelsButtons();
        _panelBody.Children.Add(_channelRow);

        _panelBody.Children.Add(new TextBlock
        {
            Text = "Send to",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });
        BuildPeopleRow();
        _panelBody.Children.Add(_peopleRow);
        if (_picked.Count > 0)
        {
            _panelBody.Children.Add(_chips);
        }

        if (!string.IsNullOrWhiteSpace(_status.Text))
        {
            _panelBody.Children.Add(_status);
        }

        _panelBody.Children.Add(_send);
        UpdateSendLabel();
        UpdateHeading2();
    }

    private void UpdateHeading2()
    {
        AutomationProperties.SetName(_panel, $"Share “{Title}”");
        AutomationProperties.SetName(_send, _picked.Count > 0 ? $"Send to {_picked.Count}" : "Send");
        AutomationProperties.SetHelpText(_send, "Sends the link to the picked people");
    }

    private Control BuildLinkRow()
    {
        _copyIcon = ArcGlass.Icon("copy", 13, ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)));
        _copyLabel = new TextBlock
        {
            Text = "Copy",
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };

        var linkText = new TextBlock
        {
            Text = Link,
            FontSize = 12,
            Width = 158,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };
        AutomationProperties.SetName(linkText, Link);

        var copyButton = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(9),
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Children = { _copyIcon, _copyLabel }
            }
        };
        copyButton.Classes.Add("sg-plain");
        AutomationProperties.SetName(copyButton, "Copy link");
        copyButton.Click += (_, _) => _ = CopyLinkAsync();

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8
        };
        grid.Children.Add(ArcGlass.GlyphTile("link", 26, iconSize: 13));
        Grid.SetColumn(linkText, 1);
        grid.Children.Add(linkText);
        Grid.SetColumn(copyButton, 2);
        grid.Children.Add(copyButton);

        var row = ArcGlass.Inset(14, alpha: 0.5);
        row.Padding = new Thickness(8, 5, 6, 5);
        row.Child = grid;
        return row;
    }

    private Control BuildAccessRow(ArcShareAccessOption option)
    {
        var selected = string.Equals(option.Value, AccessValue, StringComparison.Ordinal);
        var check = new TextBlock
        {
            Text = selected ? "✓" : string.Empty,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong))
        };

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

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 9
        };
        grid.Children.Add(ArcGlass.GlyphTile(option.Icon, 30, accent: selected, iconSize: 14));
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Grid.SetColumn(check, 2);
        grid.Children.Add(check);

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Content = grid,
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(6, 5),
            CornerRadius = new CornerRadius(12),
            Height = 48,
            MinHeight = 48,
            IsTabStop = true
        };
        button.Classes.Add("sg-plain");
        AutomationProperties.SetName(button, $"{option.Label}. {option.Description}");
        AutomationProperties.SetItemStatus(button, selected ? "Selected" : string.Empty);
        var value = option.Value;
        button.Click += (_, _) =>
        {
            SetCurrentValue(AccessValueProperty, value);
            Announce($"Access set to {option.Label}");
        };
        button.KeyDown += (_, e) => OnAccessKeyDown(button, e);

        _accessButtons.Add(button);
        _accessValues.Add(option.Value);
        return button;
    }

    private void OnAccessKeyDown(ArcButton button, KeyEventArgs e)
    {
        var index = _accessButtons.IndexOf(button);
        if (index < 0)
        {
            return;
        }

        var next = e.Key switch
        {
            Key.Down or Key.Right => (index + 1) % _accessButtons.Count,
            Key.Up or Key.Left => (index - 1 + _accessButtons.Count) % _accessButtons.Count,
            Key.Home => 0,
            Key.End => _accessButtons.Count - 1,
            _ => -1
        };
        if (next < 0)
        {
            return;
        }

        e.Handled = true;
        _accessButtons[next].Focus();
        SetCurrentValue(AccessValueProperty, _accessValues[next]);
    }

    private void SyncAccess()
    {
        if (!_built)
        {
            return;
        }

        BuildPanel();
    }

    private void BuildAccess()
    {
        if (_built)
        {
            BuildPanel();
        }
    }

    private void BuildChannels()
    {
        if (_built)
        {
            BuildPanel();
        }
    }

    private void BuildChannelsCore()
    {
        _channelRow.Children.Clear();
        foreach (var channel in ChannelList())
        {
            var done = _channelDone.TryGetValue(channel.Id, out var value) && value;
            var icon = ArcGlass.Icon(done ? "check" : channel.Icon, 14);
            var label = new TextBlock
            {
                Text = done ? channel.DoneLabel : channel.Label,
                FontSize = 12,
                FontWeight = FontWeight.Medium,
                VerticalAlignment = VerticalAlignment.Center
            };
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Height = 30,
                MinHeight = 30,
                Padding = new Thickness(10, 0),
                CornerRadius = new CornerRadius(10),
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Children = { icon, label }
                }
            };
            button.Classes.Add("sg-plain");
            AutomationProperties.SetName(button, done ? $"{channel.Label}, {channel.DoneLabel}" : $"Share to {channel.Label}");
            var id = channel.Id;
            button.Click += (_, _) =>
            {
                if (_channelDone.TryGetValue(id, out var already) && already)
                {
                    Announce($"{channel.DoneLabel} on {channel.Label}");
                    return;
                }

                Announce($"Sending to {channel.Label}");
                button.IsEnabled = false;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    _channelDone[id] = true;
                    button.IsEnabled = true;
                    BuildPanel();
                    Announce($"{channel.Label}: {channel.DoneLabel}");
                };
                timer.Start();
            };
            _channelRow.Children.Add(button);
        }
    }

    private void BuildChannels2()
    {
        BuildChannelsCore();
    }

    private void BuildChannelsButtons()
    {
        BuildChannelsCore();
    }

    private void BuildPeople()
    {
        if (_built)
        {
            BuildPanel();
        }
    }

    private void BuildPeopleRow()
    {
        _peopleRow.Children.Clear();
        foreach (var person in PersonList())
        {
            var picked = _picked.Contains(person.Id);
            var avatarButton = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Width = 44,
                Height = 52,
                MinHeight = 52,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Spacing = 4,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        ArcGlass.Avatar(person.Name, 28, strong: picked),
                        new TextBlock
                        {
                            Text = person.Name.Split(' ')[0],
                            FontSize = 10,
                            Foreground = ArcGlass.Solid(ArcGlass.Color(
                                picked ? ArcTokens.Foreground : ArcTokens.TextMuted))
                        }
                    }
                }
            };
            avatarButton.Classes.Add("sg-plain");
            AutomationProperties.SetName(avatarButton, person.Name);
            AutomationProperties.SetItemStatus(avatarButton, picked ? "Picked" : string.Empty);
            var id = person.Id;
            var name = person.Name;
            avatarButton.Click += (_, _) =>
            {
                if (!_picked.Remove(id))
                {
                    _picked.Add(id);
                }

                BuildPanel();
                Announce(_picked.Contains(id) ? $"{name} added" : $"{name} removed");
            };
            _peopleRow.Children.Add(avatarButton);
        }

        if (_chips is null)
        {
            return;
        }

        _chips.Children.Clear();
        foreach (var id in _picked.ToList())
        {
            var person = PersonList().FirstOrDefault(entry => entry.Id == id);
            if (person is null)
            {
                continue;
            }

            var remove = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Compact = true,
                Size = ArcButtonSize.Sm,
                Width = 18,
                Height = 18,
                MinWidth = 18,
                MinHeight = 18,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                Content = ArcGlass.Icon("x", 11)
            };
            remove.Classes.Add("sg-plain");
            AutomationProperties.SetName(remove, $"Remove {person.Name}");
            var personId = person.Id;
            remove.Click += (_, _) =>
            {
                _picked.Remove(personId);
                BuildPanel();
                Announce($"{person.Name} removed");
            };

            var chip = new Border
            {
                CornerRadius = new CornerRadius(9999),
                Padding = new Thickness(4, 2, 3, 2),
                Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.16)),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 5,
                    Children =
                    {
                        ArcGlass.Avatar(person.Name, 20),
                        new TextBlock
                        {
                            Text = person.Name.Split(' ')[0],
                            FontSize = 11,
                            VerticalAlignment = VerticalAlignment.Center,
                            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                        },
                        remove
                    }
                }
            };
            _chips.Children.Add(chip);
        }
    }

    private void UpdateSendLabel()
    {
        _send.Content = _picked.Count == 0 ? "Send" : $"Send to {_picked.Count}";
        _send.IsEnabled = _picked.Count > 0;
    }

    private Control BuildSentFace()
    {
        var stack = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 14, 4, 8)
        };
        var ring = new Border
        {
            Width = 48,
            Height = 48,
            CornerRadius = new CornerRadius(24),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.20)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = ArcGlass.Icon("check", 24, ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong)))
        };
        stack.Children.Add(ring);
        var heading = new TextBlock
        {
            Text = "Shared",
            FontSize = 16,
            FontWeight = FontWeight.Medium,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };
        var detail = new TextBlock
        {
            Text = "Everyone you picked has the link.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };
        stack.Children.Add(heading);
        stack.Children.Add(detail);
        var done = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = "Done",
            Height = 36,
            MinHeight = 36,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(22, 0)
        };
        done.Click += (_, _) =>
        {
            _sent = false;
            _picked.Clear();
            _channelDone.Clear();
            Close(returnFocus: true);
            BuildPanel();
        };
        stack.Children.Add(done);
        AutomationProperties.SetName(stack, "Shared");
        return stack;
    }

    private void Announce(string message)
    {
        _status.Text = message;
        AutomationProperties.SetName(_status, message);
    }

    private async Task CopyLinkAsync()
    {
        if (_copied)
        {
            return;
        }

        _copied = true;
        var copied = false;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetValueAsync(DataFormat.Text, Link).ConfigureAwait(true);
                copied = true;
            }
        }
        catch
        {
            copied = false;
        }

        _copyIcon.Icon = copied ? "check" : "circle-alert";
        _copyLabel.Text = copied ? "Copied" : "Copy failed";
        Announce(copied ? $"Link copied with {AccessValue} access" : "Could not copy the link");
        if (copied)
        {
            Copied?.Invoke(this, AccessValue ?? string.Empty);
        }

        _copyTimer?.Stop();
        _copyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };
        _copyTimer.Tick += (_, _) =>
        {
            _copyTimer?.Stop();
            _copied = false;
            _copyIcon.Icon = "copy";
            _copyLabel.Text = "Copy";
        };
        _copyTimer.Start();
    }

    private void Send()
    {
        if (_sending)
        {
            return;
        }

        if (_picked.Count == 0)
        {
            Announce("Pick someone to send to");
            _peopleRow.Children.FirstOrDefault()?.Focus();
            return;
        }

        _sending = true;
        var names = PersonList().Where(person => _picked.Contains(person.Id)).Select(person => person.Name).ToList();
        var summary = names.Count == 1
            ? names[0]
            : names.Count == 2
                ? $"{names[0]} and {names[1]}"
                : $"{names[0]} and {names.Count - 1} others";
        Announce($"Shared with {summary}");
        Sent?.Invoke(this, EventArgs.Empty);

        _sendTimer?.Stop();
        _sendTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
        _sendTimer.Tick += (_, _) =>
        {
            _sendTimer?.Stop();
            _sending = false;
            _sent = true;
            BuildPanel();
        };
        _sendTimer.Start();
    }

    // ---------------------------------------------------------------- open and close

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
        _overlay.Children.Clear();
        _overlay.Children.Add(_panel);
        BuildPanel();
        _panel.IsVisible = true;
        _panel.Opacity = 1;
        _panel.Measure(new Size(PanelWidth, double.PositiveInfinity));
        var height = Math.Max(120, _panel.DesiredSize.Height);
        _panel.Height = height;
        var x = Bounds.Width - PanelWidth;
        var y = 44d;
        if (TopLevel.GetTopLevel(this) is { } top)
        {
            var origin = this.TranslatePoint(new Point(0, 0), top);
            if (origin is { } point && point.Y + y + height > top.Bounds.Height - 12)
            {
                y = -height - 8;
            }
        }

        Canvas.SetLeft(_panel, x);
        Canvas.SetTop(_panel, y);
        AttachDismiss();

        if (ArcThemeManager.ReducedMotion)
        {
            _panel.RenderTransform = TransformOperations.Parse("translate(0px, 0px) scale(1, 1)");
            return;
        }

        var triggerWidth = Math.Max(1, Bounds.Width);
        var triggerHeight = 36.0;
        var sx = triggerWidth / PanelWidth;
        var sy = triggerHeight / height;
        _panel.RenderTransform = TransformOperations.Parse(
            $"translate({-x}px, {-y}px) scale({sx:0.###}, {sy:0.###})");
        _panel.Opacity = 0.9;
        Dispatcher.UIThread.Post(() =>
        {
            _panel.Transitions = new global::Avalonia.Animation.Transitions
            {
                new global::Avalonia.Animation.TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = GlassMotion.Morph,
                    Easing = GlassMotion.MorphEase
                },
                new global::Avalonia.Animation.DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = GlassMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                }
            };
            _panel.RenderTransform = TransformOperations.Parse("translate(0px, 0px) scale(1, 1)");
            _panel.Opacity = 1;
        }, DispatcherPriority.Render);
    }

    private void HidePanel()
    {
        DetachDismiss();
        _overlay.Children.Clear();
        _panel.IsVisible = false;
        _panel.Transitions = null;
        _panel.RenderTransform = TransformOperations.Parse("translate(0px, 0px) scale(1, 1)");
        _panel.Opacity = 0;
    }

    private void Close(bool returnFocus)
    {
        SetCurrentValue(IsOpenProperty, false);
        if (returnFocus)
        {
            _trigger.Focus();
        }
    }

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
            if (e.Source is Visual source && IsWithin(source, _panel))
            {
                return;
            }

            if (e.Source is Visual trigger && IsWithin(trigger, _trigger))
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

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            Close(returnFocus: true);
        }
    }
}












