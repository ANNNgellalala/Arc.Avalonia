using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.Glass.Controls;

public sealed class ArcArticleItem
{
    public string Id { get; set; } = string.Empty;

    public string Tag { get; set; } = "Journal";

    public string Title { get; set; } = string.Empty;

    public string Meta { get; set; } = string.Empty;

    public ArcGlassScene Scene { get; set; } = ArcGlassScene.Coast;

    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// A grid of story cards where a card opens into a full article with one morph: the article starts as the
/// card's exact rectangle, its hero scales as one piece while the box springs to the sheet, the body rises
/// in after, and dragging the sheet down shrinks it back into its slot. Escape closes and focus returns to
/// the card.
///
/// Platform approximations: the morph animates the article's rectangle between the measured card slot and
/// the sheet target rather than a CSS shared layout animation, so text reflows during the flight. The pull
/// gesture is a pointer drag on the hero. No Pro source was used.
/// </summary>
public sealed class ArcCardToArticle : Panel
{
    public static readonly StyledProperty<IList<ArcArticleItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcCardToArticle, IList<ArcArticleItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> OpenIdProperty =
        AvaloniaProperty.Register<ArcCardToArticle, string?>(nameof(OpenId));

    public static readonly StyledProperty<double> AspectRatioProperty =
        AvaloniaProperty.Register<ArcCardToArticle, double>(nameof(AspectRatio), 16.0 / 9.0);

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcCardToArticle, string>(nameof(Label), "Journal");

    private sealed class CardVisual
    {
        public ArcArticleItem Item = new();
        public ArcButton Button = null!;
        public Border Frame = null!;
        public ArcGlassBackdrop Backdrop = null!;
    }

    private readonly WrapPanel _grid;
    private readonly Canvas _overlay;
    private readonly Border _scrim;
    private readonly Border _article;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _body;
    private readonly Border _hero;
    private readonly ArcGlassBackdrop _heroArt;
    private readonly TextBlock _heroTag;
    private readonly TextBlock _heroTitle;
    private readonly TextBlock _meta;
    private readonly Border _progressTrack;
    private readonly Border _progressFill;
    private readonly ArcButton _close;
    private readonly List<CardVisual> _cards = new();
    private bool _open;
    private bool _dragging;
    private double _dragStartY;
    private double _dragOffset;
    private GlassTween? _tween;
    private Rect _cardSlot;
    private Rect _articleSlot;
    private Control? _returnFocus;

    static ArcCardToArticle()
    {
        ItemsProperty.Changed.AddClassHandler<ArcCardToArticle>((view, _) => view.Rebuild());
        OpenIdProperty.Changed.AddClassHandler<ArcCardToArticle>((view, _) => view.SyncOpen());
        AspectRatioProperty.Changed.AddClassHandler<ArcCardToArticle>((view, _) => view.Rebuild());
    }

    public ArcCardToArticle()
    {
        ClipToBounds = false;
        Height = 470;

        _grid = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemWidth = 300,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _scrim = new Border
        {
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Scrim, 0.6)),
            Opacity = 0,
            IsVisible = false
        };
        _scrim.PointerPressed += (_, _) => CloseArticle();

        _heroArt = new ArcGlassBackdrop { Scene = ArcGlassScene.Villa, Corner = 24, Seed = 2, Vignette = 0.45 };
        _heroTag = new TextBlock
        {
            Text = "Case study",
            FontSize = 10,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
        };
        _heroTitle = new TextBlock
        {
            Text = string.Empty,
            FontSize = 22,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
            Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
        };
        _meta = new TextBlock
        {
            Text = string.Empty,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted)
        };

        var heroText = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(20, 0, 20, 18),
            Children = { _heroTag, _heroTitle, _meta }
        };
        _hero = new Border
        {
            CornerRadius = new CornerRadius(24),
            ClipToBounds = true,
            Child = new Panel
            {
                Children =
                {
                    _heroArt,
                    new Border { Background = ArcGlass.PhotoShade(0.6), IsHitTestVisible = false },
                    heroText
                }
            }
        };

        _progressTrack = new Border
        {
            Height = 3,
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Border, 0.8)),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _progressFill = new Border
        {
            Height = 3,
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Accent)),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0
        };

        _close = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Compact = true,
            Width = 34,
            Height = 34,
            MinWidth = 34,
            MinHeight = 34,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(12),
            Content = ArcGlass.Icon("x", 16),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 12, 0),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.7))
        };
        _close.Classes.Add("sg-plain");
        AutomationProperties.SetName(_close, "Close article");
        _close.Click += (_, _) => CloseArticle();

        _body = new StackPanel { Spacing = 12, Margin = new Thickness(22, 18, 22, 28) };
        _scroll = new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        _scroll.ScrollChanged += (_, _) => UpdateProgress();

        var articleShell = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children = { _hero, _scroll, _progressTrack }
        };
        Grid.SetRow(_scroll, 1);
        Grid.SetRow(_progressTrack, 2);
        articleShell.Children.Add(_close);
        articleShell.Children.Add(_progressFill);
        Grid.SetRow(_progressFill, 2);

        _article = new Border
        {
            Background = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Surface)),
            BorderBrush = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Border)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowFloating),
            IsVisible = false,
            Child = articleShell
        };

        _overlay = new Canvas { ClipToBounds = false, IsVisible = false, Children = { _scrim, _article } };

        Children.Add(_grid);
        Children.Add(_overlay);

        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        AddHandler(KeyDownEvent, OnKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public event EventHandler<string?>? OpenChanged;

    public IList<ArcArticleItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public string? OpenId
    {
        get => GetValue(OpenIdProperty);
        set => SetValue(OpenIdProperty, value);
    }

    public double AspectRatio
    {
        get => GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private List<ArcArticleItem> ItemList() => Items?.ToList() ?? new List<ArcArticleItem>();

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

        _cards.Clear();
        _grid.Children.Clear();
        var ratio = Math.Max(0.5, AspectRatio);
        _grid.ItemWidth = 300;
        _grid.ItemHeight = (300 / ratio) + 24;

        foreach (var item in ItemList())
        {
            BuildCard(item);
        }

        Dispatcher.UIThread.Post(() => SyncOpen(), DispatcherPriority.Loaded);
    }

    private void BuildCard(ArcArticleItem item)
    {
        var ratio = Math.Max(0.5, AspectRatio);
        var art = new ArcGlassBackdrop { Scene = item.Scene, Corner = 20, Seed = item.Id.Length, Vignette = 0.5 };
        var tag = new Border
        {
            CornerRadius = new CornerRadius(9999),
            Padding = new Thickness(8, 3),
            Background = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Background, 0.5)),
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.35)),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12, 12, 0, 0),
            Child = new TextBlock
            {
                Text = item.Tag,
                FontSize = 10,
                Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
            }
        };

        var text = new StackPanel
        {
            Spacing = 0,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(14, 0, 14, 12),
            Children =
            {
                new TextBlock
                {
                    Text = item.Title,
                    FontSize = 16,
                    FontWeight = FontWeight.Medium,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = ArcGlass.Solid(ArcGlass.PhotoInk)
                },
                new TextBlock
                {
                    Text = item.Meta,
                    Classes = { "arc-caption" },
                    Foreground = ArcGlass.Solid(ArcGlass.PhotoMuted),
                    Margin = new Thickness(0, 4, 0, 0)
                }
            }
        };

        var frame = new Border
        {
            Height = 300 / ratio,
            CornerRadius = new CornerRadius(20),
            ClipToBounds = true,
            BorderBrush = ArcGlass.Solid(ArcGlass.ArcAlpha(ArcTokens.Surface, 0.25)),
            BorderThickness = new Thickness(1),
            Child = new Panel
            {
                Children =
                {
                    art,
                    new Border { Background = ArcGlass.PhotoShade(0.55), IsHitTestVisible = false },
                    tag,
                    text
                }
            }
        };

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            MinHeight = 0,
            Height = 300 / ratio,
            CornerRadius = new CornerRadius(20),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = frame,
            Margin = new Thickness(6, 6)
        };
        button.Classes.Add("sg-plain");
        AutomationProperties.SetName(button, $"{item.Tag}: {item.Title}. {item.Meta}");
        AutomationProperties.SetHelpText(button, "Opens the article. Escape closes it.");
        button.Click += (_, _) =>
        {
            _returnFocus = button;
            var origin = button.TranslatePoint(new Point(0, 0), this) ?? new Point(0, 0);
            _cardSlot = new Rect(origin, button.Bounds.Size);
            SetCurrentValue(OpenIdProperty, item.Id);
        };

        _grid.Children.Add(button);
        _cards.Add(new CardVisual { Item = item, Button = button, Frame = frame, Backdrop = art });
    }

    private void SyncOpen()
    {
        var id = OpenId;
        var item = id is null ? null : ItemList().FirstOrDefault(entry => entry.Id == id);
        if (item is null)
        {
            if (_open)
            {
                CloseImmediate();
            }

            return;
        }

        _heroTag.Text = item.Tag;
        _heroTitle.Text = item.Title;
        _meta.Text = item.Meta;
        _heroArt.Scene = item.Scene;
        _body.Children.Clear();
        foreach (var paragraph in item.Body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            _body.Children.Add(new TextBlock
            {
                Text = paragraph,
                Classes = { "arc-body-sm" },
                TextWrapping = TextWrapping.Wrap,
                Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
            });
        }

        _body.Children.Add(new TextBlock
        {
            Text = "Filed in the studio journal. Words and pictures by the Meridian team.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });

        if (_open)
        {
            return;
        }

        _open = true;
        _overlay.IsVisible = true;
        _scrim.IsVisible = true;
        _article.IsVisible = true;
        Dispatcher.UIThread.Post(AnimateIn, DispatcherPriority.Loaded);
        OpenChanged?.Invoke(this, id);
    }

    private void AnimateIn()
    {
        UpdateTargets();
        if (ArcThemeManager.ReducedMotion)
        {
            _scrim.Opacity = 1;
            ApplyRect(_articleSlot);
            _close.Focus();
            return;
        }

        _scrim.Opacity = 0;
        _article.Opacity = 0.72;
        ApplyRect(_cardSlot);
        var from = _cardSlot;
        var to = _articleSlot;
        _tween?.Stop();
        _tween = GlassTween.Run(GlassMotion.Smooth, GlassMotion.SmoothEase, progress =>
        {
            ApplyRect(Lerp(from, to, progress));
            _article.Opacity = 0.72 + (0.28 * progress);
            _scrim.Opacity = progress;
        }, () =>
        {
            ApplyRect(to);
            _article.Opacity = 1;
            _scrim.Opacity = 1;
            _close.Focus();
        });
    }

    private void UpdateTargets()
    {
        var ratio = Math.Max(0.5, AspectRatio);
        var width = Math.Min(640, Math.Max(280, Bounds.Width - 24));
        var height = Math.Max(240, Bounds.Height - 24);
        _articleSlot = new Rect((Bounds.Width - width) / 2, (Bounds.Height - height) / 2, width, height);
        _hero.Height = Math.Min(220, width / ratio);
        _heroArt.Height = _hero.Height;
        if (_cardSlot.Width <= 1 && _cards.Count > 0)
        {
            var card = _cards[0].Button;
            var origin = card.TranslatePoint(new Point(0, 0), this) ?? new Point(0, 0);
            _cardSlot = new Rect(origin, card.Bounds.Size);
        }
    }

    private static Rect Lerp(Rect from, Rect to, double progress) => new(
        from.X + ((to.X - from.X) * progress),
        from.Y + ((to.Y - from.Y) * progress),
        from.Width + ((to.Width - from.Width) * progress),
        from.Height + ((to.Height - from.Height) * progress));

    private void ApplyRect(Rect rect)
    {
        Canvas.SetLeft(_article, rect.X);
        Canvas.SetTop(_article, rect.Y);
        _article.Width = rect.Width;
        _article.Height = rect.Height;
        _article.CornerRadius = new CornerRadius(Math.Clamp(26 * (rect.Width / Math.Max(1, _articleSlot.Width)), 10, 26));
    }

    private void CloseArticle()
    {
        if (!_open)
        {
            return;
        }

        _tween?.Stop();
        _open = false;
        SetCurrentValue(OpenIdProperty, null);
        if (!ArcThemeManager.ReducedMotion)
        {
            _scrim.Transitions = new global::Avalonia.Animation.Transitions
            {
                new global::Avalonia.Animation.DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Exit,
                    Easing = ArcMotion.EaseExit
                }
            };
            _scrim.Opacity = 0;
        }
        else
        {
            _scrim.Opacity = 0;
        }

        _overlay.IsVisible = false;
        _article.IsVisible = false;
        _article.RenderTransform = TransformOperations.Parse("translateY(0px)");
        OpenChanged?.Invoke(this, null);
        (_returnFocus ?? _cards.FirstOrDefault()?.Button)?.Focus();
    }

    private void CloseImmediate()
    {
        _open = false;
        _overlay.IsVisible = false;
        _article.IsVisible = false;
        _scrim.Opacity = 0;
    }

    private void UpdateProgress()
    {
        var max = Math.Max(1, _scroll.Extent.Height - _scroll.Viewport.Height);
        var fraction = Math.Clamp(_scroll.Offset.Y / max, 0, 1);
        _progressFill.Width = Math.Max(0, _progressTrack.Bounds.Width * fraction);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!_open || e.Source is not Visual source)
        {
            return;
        }

        for (Visual? visual = source; visual is not null; visual = visual.GetVisualParent())
        {
            if (ReferenceEquals(visual, _hero))
            {
                _dragging = true;
                _dragStartY = e.GetPosition(this).Y;
                _dragOffset = 0;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            if (ReferenceEquals(visual, _article))
            {
                break;
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var dy = e.GetPosition(this).Y - _dragStartY;
        _dragOffset = Math.Max(0, dy);
        _article.RenderTransform = TransformOperations.Parse($"translateY({_dragOffset:0.##}px)");
        _scrim.Opacity = Math.Clamp(1 - (_dragOffset / 420), 0.2, 1);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        _article.RenderTransform = TransformOperations.Parse("translateY(0px)");
        if (_dragOffset > 110)
        {
            _dragOffset = 0;
            CloseArticle();
            return;
        }

        _dragOffset = 0;
        _scrim.Opacity = 1;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _open)
        {
            e.Handled = true;
            CloseArticle();
        }
    }
}

