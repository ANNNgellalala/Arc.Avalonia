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

/// <summary>One unfurled link: the address plus the site, title, description and artwork of its preview.</summary>
public sealed class ArcLinkPreview
{
    public string Url { get; set; } = "https://uiarc.dev/components";

    public string Site { get; set; } = "uiarc.dev";

    public string Title { get; set; } = "The Arc component library";

    public string Description { get; set; } =
        "A quiet, precise component library for product teams, with motion and glass that respect the reader.";

    public ArcGlassScene Scene { get; set; } = ArcGlassScene.Coast;
}

/// <summary>
/// A message composer where a pasted link becomes an inline link with a soft loading sweep, then unfurls
/// into a preview card: the link's own box grows width first and height after while the card fades in and
/// the lane below glides down. The link can be clicked to fold the card back, and removed to take the link
/// out of the message.
///
/// Platform approximations: the morph animates the card's height and clip rather than interpolating a clip
/// path from the inline link; the loading sweep is a translated gradient because there is no CSS
/// background-position animation. Images are painted from tokens. No Pro source was used.
/// </summary>
public sealed class ArcLinkUnfurl : Panel
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcLinkUnfurl, string>(nameof(Label), "Message #lisbon-offsite");

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcLinkUnfurl, string>(nameof(Placeholder), "Message #lisbon-offsite");

    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<ArcLinkUnfurl, string>(nameof(Value),
            "Found a place for the offsite, take a look");

    public static readonly StyledProperty<IList<ArcLinkPreview>?> SamplesProperty =
        AvaloniaProperty.Register<ArcLinkUnfurl, IList<ArcLinkPreview>?>(nameof(Samples));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<ArcLinkUnfurl, bool>(nameof(IsExpanded), true);

    public static readonly StyledProperty<bool> ShowSamplesProperty =
        AvaloniaProperty.Register<ArcLinkUnfurl, bool>(nameof(ShowSamples), true);

    private readonly Border _composer;
    private readonly TextBlock _message;
    private readonly Border _link;
    private readonly TextBlock _linkText;
    private readonly ArcIcon _linkFavicon;
    private readonly Border _shimmer;
    private readonly ArcButton _collapse;
    private readonly ArcButton _remove;
    private readonly Panel _lane;
    private readonly Border _card;
    private readonly ArcGlassBackdrop _cardArt;
    private readonly TextBlock _cardSite;
    private readonly TextBlock _cardTitle;
    private readonly TextBlock _cardDescription;
    private readonly StackPanel _samples;
    private readonly TextBlock _status;
    private readonly ArcButton _send;
    private readonly Panel _linkHost;
    private readonly List<ArcButton> _sampleButtons = new();
    private ArcLinkPreview? _preview;
    private bool _loading;
    private bool _removed;
    private bool _sending;
    private DispatcherTimer? _loadTimer;
    private DispatcherTimer? _sendTimer;
    private GlassTween? _tween;
    private bool _draggingCard;
    private double _dragStartY;
    private double _cardDragOffset;
    private double _shimmerOffset = -80;
    private DispatcherTimer? _shimmerTimer;

    static ArcLinkUnfurl()
    {
        ValueProperty.Changed.AddClassHandler<ArcLinkUnfurl>((view, _) => view.SyncMessage());
        SamplesProperty.Changed.AddClassHandler<ArcLinkUnfurl>((view, _) => view.BuildSamples());
        IsExpandedProperty.Changed.AddClassHandler<ArcLinkUnfurl>((view, _) => view.SyncExpanded(animate: true));
        LabelProperty.Changed.AddClassHandler<ArcLinkUnfurl>((view, _) => view.SyncMessage());
    }

    public ArcLinkUnfurl()
    {
        ClipToBounds = false;

        _message = new TextBlock
        {
            Text = Value,
            Classes = { "arc-body-sm" },
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };

        _linkText = new TextBlock
        {
            Text = "uiarc.dev",
            FontSize = 13,
            MaxWidth = 230,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong))
        };
        _linkFavicon = ArcGlass.Icon("globe", 14, ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong)));

        var faviconBox = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(5),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.18)),
            Child = _linkFavicon,
            VerticalAlignment = VerticalAlignment.Center
        };

        _collapse = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Content = ArcGlass.Icon("chevron-up", 12)
        };
        _collapse.Classes.Add("sg-plain");
        AutomationProperties.SetName(_collapse, "Collapse the preview");
        _collapse.Click += (_, _) => SetCurrentValue(IsExpandedProperty, false);

        _remove = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 20,
            Height = 20,
            MinWidth = 20,
            MinHeight = 20,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(7),
            Content = ArcGlass.Icon("x", 12)
        };
        _remove.Classes.Add("sg-plain");
        AutomationProperties.SetName(_remove, "Remove the link");
        _remove.Click += (_, _) => RemoveLink();

        var linkGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"),
            ColumnSpacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { faviconBox, _linkText, _collapse, _remove }
        };
        Grid.SetColumn(_linkText, 1);
        Grid.SetColumn(_collapse, 2);
        Grid.SetColumn(_remove, 3);

        _shimmer = new Border
        {
            Background = ArcGlass.Gradient(
                100,
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0), 0),
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.22), 0.5),
                new GradientStop(ArcGlass.ArcAlpha(ArcTokens.Accent, 0), 1)),
            IsHitTestVisible = false,
            IsVisible = false,
            Opacity = 0.8
        };

        _linkHost = new Panel { Children = { _shimmer, linkGrid } };
        _link = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(5, 4, 5, 4),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.12)),
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Accent, 0.30)),
            BorderThickness = new Thickness(1),
            Child = _linkHost,
            Margin = new Thickness(6, 0, 0, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center
        };
        _link.PointerPressed += (_, e) =>
        {
            if (_removed)
            {
                return;
            }

            SetCurrentValue(IsExpandedProperty, !IsExpanded);
            e.Handled = true;
        };

        var messageRow = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 2, 0, 0),
            Children = { _message, _link }
        };

        _cardArt = new ArcGlassBackdrop { Scene = ArcGlassScene.Coast, Corner = 14, Seed = 4, Vignette = 0.6 };
        _cardSite = new TextBlock
        {
            Text = "uiarc.dev",
            Classes = { "arc-caption", "arc-medium" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)),
            VerticalAlignment = VerticalAlignment.Center
        };
        _cardTitle = new TextBlock
        {
            Text = "The Arc component library",
            FontSize = 15,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };
        _cardDescription = new TextBlock
        {
            Text = string.Empty,
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };

        var cardBody = new StackPanel
        {
            Spacing = 5,
            Children = { _cardSite, _cardTitle, _cardDescription }
        };
        var cardContent = new StackPanel
        {
            Children =
            {
                new Border { Height = 118, ClipToBounds = true, Child = _cardArt },
                new Panel { Margin = new Thickness(14, 10, 14, 12), Children = { cardBody } }
            }
        };

        _card = new Border
        {
            Width = 440,
            CornerRadius = new CornerRadius(16),
            ClipToBounds = true,
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.9)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = cardContent,
            IsVisible = false,
            Height = 0
        };
        _card.PointerPressed += (_, e) =>
        {
            _draggingCard = true;
            _dragStartY = e.GetPosition(this).Y;
        };
        _card.PointerReleased += (_, _) => EndCardDrag();
        _card.PointerMoved += (_, e) =>
        {
            if (!_draggingCard)
            {
                return;
            }

            var dy = e.GetPosition(this).Y - _dragStartY;
            _cardDragOffset = Math.Min(0, dy);
            _card.RenderTransform = TransformOperations.Parse($"translateY({_cardDragOffset:0.##}px)");
        };

        _lane = new Panel { Margin = new Thickness(0, 6, 0, 0), Children = { _card } };

        _samples = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center
        };

        _status = new TextBlock
        {
            Text = string.Empty,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        var attach = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Size = ArcButtonSize.Sm,
            Width = 30,
            Height = 30,
            MinWidth = 30,
            MinHeight = 30,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Content = ArcGlass.Icon("plus", 16)
        };
        attach.Classes.Add("sg-plain");
        AutomationProperties.SetName(attach, "Add an attachment");

        _send = new ArcButton
        {
            Variant = ArcButtonVariant.Primary,
            Size = ArcButtonSize.Sm,
            Content = "Send",
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        AutomationProperties.SetName(_send, "Send message");
        _send.Click += (_, _) => Send();

        var toolbar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        toolbar.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { attach, _samples }
        });
        Grid.SetColumn(_send, 2);
        toolbar.Children.Add(_send);

        var stack = new StackPanel
        {
            Spacing = 0,
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Margin = new Thickness(0, 0, 0, 6),
                    Children =
                    {
                        ArcGlass.Icon("hash", 14, ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))),
                        new TextBlock
                        {
                            Text = "lisbon-offsite",
                            Classes = { "arc-label" },
                            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
                        }
                    }
                },
                messageRow,
                _lane,
                new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children = { _status, toolbar }
                }
            }
        };
        Grid.SetColumn(toolbar, 1);

        _composer = ArcGlass.Panel(22, new Thickness(16), floating: true, fillAlpha: 0.62);
        _composer.Width = 560;
        _composer.HorizontalAlignment = HorizontalAlignment.Center;
        _composer.VerticalAlignment = VerticalAlignment.Center;
        _composer.Child = stack;

        Children.Add(_composer);

        AddHandler(KeyDownEvent, OnKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        BuildSamples();
        SyncMessage();
        _preview = SampleList().FirstOrDefault();
        ApplyPreview(_preview, animate: false);
        SetExpanded(true, animate: false);
    }

    public event EventHandler<ArcLinkPreview?>? PreviewChanged;

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public IList<ArcLinkPreview>? Samples
    {
        get => GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool ShowSamples
    {
        get => GetValue(ShowSamplesProperty);
        set => SetValue(ShowSamplesProperty, value);
    }

    private List<ArcLinkPreview> SampleList() => Samples?.ToList() ?? new List<ArcLinkPreview>
    {
        new()
        {
            Url = "https://uiarc.dev/components",
            Site = "uiarc.dev",
            Title = "The Arc component library",
            Description = "Quiet, precise components with motion and glass that respect the reader.",
            Scene = ArcGlassScene.Coast
        },
        new()
        {
            Url = "https://fieldnotes.example/alfama",
            Site = "fieldnotes.example",
            Title = "A slow walk through Alfama",
            Description = "Steps, shade and the sound of a hundred windows opening at once.",
            Scene = ArcGlassScene.Villa
        }
    };

    private void BuildSamples()
    {
        _samples.Children.Clear();
        _sampleButtons.Clear();
        _samples.IsVisible = ShowSamples;
        if (!ShowSamples)
        {
            return;
        }

        _samples.Children.Add(new TextBlock
        {
            Text = "Try",
            Classes = { "arc-caption" },
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted)),
            Margin = new Thickness(2, 0, 2, 0)
        });

        foreach (var sample in SampleList())
        {
            var button = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Height = 26,
                MinHeight = 26,
                Padding = new Thickness(8, 0),
                CornerRadius = new CornerRadius(8),
                Content = sample.Site,
                FontSize = 11
            };
            button.Classes.Add("sg-plain");
            AutomationProperties.SetName(button, $"Paste the {sample.Site} link");
            button.Click += (_, _) => PasteSample(sample);
            _samples.Children.Add(button);
            _sampleButtons.Add(button);
        }
    }

    private void PasteSample(ArcLinkPreview sample)
    {
        _removed = false;
        _link.IsVisible = true;
        _preview = sample;
        _loading = true;
        _linkText.Text = "Fetching preview…";
        _linkText.Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary));
        _linkFavicon.Icon = "globe";
        _link.Opacity = 0.9;
        SetExpanded(false, animate: false);
        Announce($"Link added and loading: {sample.Url}");
        StartShimmer();

        _loadTimer?.Stop();
        _loadTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(950) };
        _loadTimer.Tick += (_, _) =>
        {
            _loadTimer?.Stop();
            _loading = false;
            StopShimmer();
            _preview = sample;
            _linkText.Text = sample.Title;
            _linkText.Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentStrong));
            _linkFavicon.Icon = "link-2";
            ApplyPreview(sample, animate: true);
            SetExpanded(true, animate: true);
            Announce($"Preview opened: {sample.Site}. {sample.Title}");
        };
        _loadTimer.Start();
    }

    private void ApplyPreview(ArcLinkPreview? preview, bool animate)
    {
        if (preview is null)
        {
            SetExpanded(false, animate: false);
            return;
        }

        _cardSite.Text = preview.Site;
        _cardTitle.Text = preview.Title;
        _cardDescription.Text = preview.Description;
        _cardArt.Scene = preview.Scene;
        _collapse.IsVisible = true;
        AutomationProperties.SetName(_card, $"Preview of {preview.Site}: {preview.Title}");
        PreviewChanged?.Invoke(this, preview);
    }

    private void SyncExpanded(bool animate)
    {
        if (IsExpanded)
        {
            _collapse.Content = ArcGlass.Icon("chevron-up", 12);
            AutomationProperties.SetName(_collapse, "Collapse the preview");
            SetExpanded(true, animate);
        }
        else
        {
            _collapse.Content = ArcGlass.Icon("chevron-down", 12);
            AutomationProperties.SetName(_collapse, "Open the preview");
            SetExpanded(false, animate);
        }
    }

    private void SetExpanded(bool expanded, bool animate)
    {
        if (_preview is null || _removed)
        {
            _card.IsVisible = false;
            _card.Height = 0;
            return;
        }

        var target = expanded ? 214 : 0;
        _card.IsVisible = true;
        _tween?.Stop();

        if (!animate || ArcThemeManager.ReducedMotion)
        {
            _card.Height = target;
            _card.Opacity = expanded ? 1 : 0;
            _card.IsVisible = expanded;
            return;
        }

        var from = _card.Height;
        _tween = GlassTween.Run(GlassMotion.Morph, GlassMotion.MorphEase, progress =>
        {
            _card.Height = from + ((target - from) * progress);
            _card.Opacity = expanded ? progress : 1 - progress;
        }, () =>
        {
            if (!expanded)
            {
                _card.IsVisible = false;
            }
        });
    }

    private void RemoveLink()
    {
        _removed = true;
        _loading = false;
        _loadTimer?.Stop();
        StopShimmer();
        _link.IsVisible = false;
        _preview = null;
        _card.IsVisible = false;
        _card.Height = 0;
        Announce("Link removed from the message");
    }

    private void SyncMessage()
    {
        _message.Text = Value;
        AutomationProperties.SetName(_composer, Label);
        AutomationProperties.SetName(this, Label);
        AutomationProperties.SetHelpText(_composer,
            "Composer. Paste a link to unfurl it. Alt and Enter toggles the preview, Enter sends.");
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
    }

    private void Announce(string message)
    {
        _status.Text = message;
        AutomationProperties.SetName(_status, message);
    }

    private void StartShimmer()
    {
        _shimmer.IsVisible = true;
        if (ArcThemeManager.ReducedMotion)
        {
            _shimmer.Opacity = 0.5;
            _shimmer.RenderTransform = TransformOperations.Parse("translateX(17px)");
            return;
        }

        _shimmer.Opacity = 0.9;
        _shimmerOffset = -80;
        _shimmerTimer?.Stop();
        _shimmerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => { });
        _shimmerTimer.Tick += (_, _) =>
        {
            if (!_loading)
            {
                _shimmerTimer?.Stop();
                return;
            }

            _shimmerOffset += 9;
            if (_shimmerOffset > 240)
            {
                _shimmerOffset = -80;
            }

            _shimmer.RenderTransform = TransformOperations.Parse($"translateX({_shimmerOffset:0.##}px)");
        };
        _shimmerTimer.Start();
    }

    private void StopShimmer()
    {
        _shimmerTimer?.Stop();
        _shimmerTimer = null;
        _shimmer.IsVisible = false;
    }

    private void Send()
    {
        if (_sending)
        {
            return;
        }

        _sending = true;
        _send.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Children =
            {
                ArcGlass.Icon("check", 13, ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentForeground))),
                new TextBlock
                {
                    Text = "Sent",
                    FontSize = 13,
                    Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.AccentForeground))
                }
            }
        };
        Announce(_preview is null
            ? "Message sent"
            : $"Message sent with the preview of {_preview.Site}");

        _sendTimer?.Stop();
        _sendTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1600) };
        _sendTimer.Tick += (_, _) =>
        {
            _sendTimer?.Stop();
            _sending = false;
            _send.Content = "Send";
        };
        _sendTimer.Start();
    }

    private void EndCardDrag()
    {
        if (!_draggingCard)
        {
            return;
        }

        _draggingCard = false;
        _card.RenderTransform = TransformOperations.Parse("translateY(0px)");
        if (_cardDragOffset < -50)
        {
            _cardDragOffset = 0;
            SetCurrentValue(IsExpandedProperty, false);
            Announce("Preview collapsed");
            return;
        }

        _cardDragOffset = 0;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Alt)
        {
            e.Handled = true;
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
            Announce(IsExpanded ? "Preview opened" : "Preview collapsed");
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            Send();
        }
    }
}






