using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialGlass.Controls;

public sealed class ArcWalletTransaction
{
    public string Merchant { get; set; } = string.Empty;

    public string Detail { get; set; } = string.Empty;

    /// <summary>Negative for spending, positive for money in.</summary>
    public double Amount { get; set; }

    /// <summary>Lucide icon name; a person avatar replaces it when <see cref="Avatar"/> is set.</summary>
    public string? Icon { get; set; }

    public string? Avatar { get; set; }
}

public sealed class ArcWalletCardData
{
    public string Id { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Product { get; set; } = string.Empty;

    public string LastFour { get; set; } = "0000";

    public string Holder { get; set; } = string.Empty;

    public string Expires { get; set; } = "09/29";

    /// <summary>metal, black, glass or color.</summary>
    public string Variant { get; set; } = "metal";

    public double Balance { get; set; }

    public string BalanceLabel { get; set; } = "Available";

    public IList<ArcWalletTransaction> Transactions { get; set; } = new List<ArcWalletTransaction>();
}

/// <summary>
/// A wallet stack: a few payment cards fan out on hover, the front card tilts with a specular light, and
/// choosing a card lifts it out of the stack into its balance and recent activity. Arrow keys cycle cards,
/// Enter opens or closes one, Escape puts it back, and dragging a lifted card down returns it.
///
/// Platform approximations: the cards keep a fixed z order and transform from a stack position function, as
/// documented, so they never pop over each other; the materials are gradient paintings rather than brushed
/// metal textures because Avalonia has no shader or blend pipeline here. The numeric balance is shown in
/// full instead of counting up, and the device tilt light is driven by the pointer. No Pro source was used.
/// </summary>
public sealed class ArcWalletStack : Panel
{
    public static readonly StyledProperty<IList<ArcWalletCardData>?> CardsProperty =
        AvaloniaProperty.Register<ArcWalletStack, IList<ArcWalletCardData>?>(nameof(Cards));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcWalletStack, string>(nameof(Label), "Wallet");

    public static readonly StyledProperty<string> CurrencySymbolProperty =
        AvaloniaProperty.Register<ArcWalletStack, string>(nameof(CurrencySymbol), "$");

    public static readonly StyledProperty<int> FrontIndexProperty =
        AvaloniaProperty.Register<ArcWalletStack, int>(nameof(FrontIndex));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcWalletStack, bool>(nameof(IsOpen));

    private const double CardWidth = 320;
    private const double CardHeight = 202;

    private sealed class CardVisual
    {
        public ArcWalletCardData Data = new();
        public ArcButton Button = null!;
        public Border Face = null!;
        public TextBlock HolderBlock = null!;
        public TextBlock NumberBlock = null!;
        public TextBlock IssuerBlock = null!;
        public TextBlock ProductBlock = null!;
        public TextBlock ExpiresBlock = null!;
        public Border Chip = null!;
        public ArcIcon Contactless = null!;
        public Border Light = null!;
    }

    private readonly Canvas _stage;
    private readonly Border _activity;
    private readonly TextBlock _activityLabel;
    private readonly TextBlock _balance;
    private readonly TextBlock _balanceLabel;
    private readonly TextBlock _cardLine;
    private readonly StackPanel _transactions;
    private readonly ArcButton _allCards;
    private readonly TextBlock _emptyHint;
    private readonly List<CardVisual> _visuals = new();
    private readonly List<Border> _rows = new();
    private readonly GlassSpring _lightX = new(0.3, 110);
    private readonly GlassSpring _lightY = new(0.2, 110);
    private readonly ArcFrameLoop _loop;
    private int _hoverIndex = -1;
    private bool _dragging;
    private double _dragStartY;
    private double _dragOffset;
    private bool _built;

    static ArcWalletStack()
    {
        CardsProperty.Changed.AddClassHandler<ArcWalletStack>((wallet, _) => wallet.Rebuild());
        FrontIndexProperty.Changed.AddClassHandler<ArcWalletStack>((wallet, _) => wallet.SyncState(animate: true));
        IsOpenProperty.Changed.AddClassHandler<ArcWalletStack>((wallet, _) => wallet.SyncState(animate: true));
        CurrencySymbolProperty.Changed.AddClassHandler<ArcWalletStack>((wallet, _) => wallet.UpdateActivity());
        LabelProperty.Changed.AddClassHandler<ArcWalletStack>((wallet, _) => wallet.UpdateActivity());
    }

    public ArcWalletStack()
    {
        ClipToBounds = false;
        Width = 748;
        Height = 432;

        _stage = new Canvas { ClipToBounds = false, Width = CardWidth + 40 };

        _activityLabel = new TextBlock
        {
            Text = "Activity",
            Classes = { "arc-label" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)),
            VerticalAlignment = VerticalAlignment.Center
        };
        _allCards = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Height = 28,
            MinHeight = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(9),
            Content = "All cards"
        };
        _allCards.Classes.Add("sg-plain");
        AutomationProperties.SetName(_allCards, "All cards");
        _allCards.Click += (_, _) => SetCurrentValue(IsOpenProperty, false);

        _balance = new TextBlock
        {
            Text = "$0.00",
            FontSize = 30,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground))
        };
        _balanceLabel = new TextBlock
        {
            Text = "Available",
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        };
        _cardLine = new TextBlock
        {
            Text = string.Empty,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        _transactions = new StackPanel { Spacing = 2 };
        _emptyHint = new TextBlock
        {
            Text = "Choose a card to lift it out of the stack and see its recent activity.",
            Classes = { "arc-body-sm" },
            TextWrapping = TextWrapping.Wrap,
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextSecondary))
        };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(_activityLabel);
        Grid.SetColumn(_allCards, 1);
        header.Children.Add(_allCards);

        var body = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                header,
                new StackPanel { Spacing = 2, Children = { _balance, _balanceLabel, _cardLine } },
                _transactions,
                _emptyHint
            }
        };

        _activity = ArcGlass.Panel(26, new Thickness(20), floating: true, fillAlpha: 0.6);
        _activity.Width = 356;
        _activity.VerticalAlignment = VerticalAlignment.Center;
        _activity.HorizontalAlignment = HorizontalAlignment.Right;
        _activity.Child = body;

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 24,
            Children = { _stage, _activity }
        };
        Grid.SetColumn(_activity, 1);
        Children.Add(layout);

        _loop = new ArcFrameLoop(OnFrame);
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
        KeyDown += OnKeyDown;
        Focusable = false;
        UpdateActivity();
    }

    public event EventHandler<ArcWalletCardData?>? SelectedChanged;

    public IList<ArcWalletCardData>? Cards
    {
        get => GetValue(CardsProperty);
        set => SetValue(CardsProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string CurrencySymbol
    {
        get => GetValue(CurrencySymbolProperty);
        set => SetValue(CurrencySymbolProperty, value);
    }

    /// <summary>Index of the card at the front of the stack.</summary>
    public int FrontIndex
    {
        get => GetValue(FrontIndexProperty);
        set => SetValue(FrontIndexProperty, value);
    }

    /// <summary>Whether a card is lifted into its activity.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private List<ArcWalletCardData> CardList() => Cards?.ToList() ?? new List<ArcWalletCardData>();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _loop.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    private void Rebuild()
    {
        if (!this.IsAttachedToVisualTree())
        {
            return;
        }

        _visuals.Clear();
        _stage.Children.Clear();
        var cards = CardList();
        for (var i = 0; i < cards.Count; i++)
        {
            BuildCard(cards[i], i);
        }

        _built = true;
        FrontIndex = Math.Clamp(FrontIndex, 0, Math.Max(0, cards.Count - 1));
        SyncState(animate: false);
    }

    private void BuildCard(ArcWalletCardData data, int index)
    {
        var (top, bottom, ink, muted, isLight) = Material(data.Variant);

        var chip = new Border
        {
            Width = 27,
            Height = 20,
            CornerRadius = new CornerRadius(5),
            Background = ArcGlass.Gradient(
                140,
                new GradientStop(ArcGlass.Lift(top, isLight ? -0.18 : 0.22), 0),
                new GradientStop(ArcGlass.Lift(top, isLight ? -0.30 : 0.10), 1)),
            BorderBrush = ArcGlass.Solid(ArcColor.WithAlpha(ink, 0.28)),
            BorderThickness = new Thickness(1)
        };

        var contactless = ArcGlass.Icon("wifi", 15, ArcGlass.Solid(ArcColor.WithAlpha(ink, 0.7)));
        contactless.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        contactless.RenderTransform = TransformOperations.Parse("rotate(90deg)");

        var issuer = new TextBlock
        {
            Text = data.Issuer,
            FontSize = 12,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ink)
        };
        var network = new TextBlock
        {
            Text = "HALO",
            FontSize = 10,
            LetterSpacing = 2,
            FontWeight = FontWeight.Medium,
            Foreground = ArcGlass.Solid(ArcColor.WithAlpha(ink, 0.8))
        };
        var product = new TextBlock
        {
            Text = data.Product,
            FontSize = 10,
            Foreground = ArcGlass.Solid(muted)
        };
        var number = new TextBlock
        {
            Text = $"••••  ••••  ••••  {data.LastFour}",
            FontSize = 14,
            LetterSpacing = 1.4,
            Foreground = ArcGlass.Solid(ink)
        };
        var holder = new TextBlock
        {
            Text = data.Holder.ToUpperInvariant(),
            FontSize = 9,
            LetterSpacing = 1,
            Foreground = ArcGlass.Solid(muted)
        };
        var expires = new TextBlock
        {
            Text = $"VALID THRU {data.Expires}",
            FontSize = 9,
            LetterSpacing = 0.8,
            Foreground = ArcGlass.Solid(muted)
        };

        var topRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            VerticalAlignment = VerticalAlignment.Top
        };
        var issuerStack = new StackPanel { Spacing = 1, Children = { issuer, product } };
        topRow.Children.Add(issuerStack);
        Grid.SetColumn(network, 1);
        topRow.Children.Add(network);

        var bottomRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            VerticalAlignment = VerticalAlignment.Bottom
        };
        var holderStack = new StackPanel { Spacing = 1, Children = { holder, expires } };
        bottomRow.Children.Add(holderStack);
        var chipRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Bottom,
            Children = { chip, contactless }
        };
        Grid.SetColumn(chipRow, 1);
        bottomRow.Children.Add(chipRow);

        var layers = new Panel();
        layers.Children.Add(new Border
        {
            Background = ArcGlass.Gradient(
                140,
                new GradientStop(top, 0),
                new GradientStop(bottom, 1))
        });

        // A soft specular band so the stock reads as a material rather than a flat fill.
        layers.Children.Add(new Border
        {
            Background = ArcGlass.Gradient(
                118,
                new GradientStop(ArcColor.WithAlpha(ink, isLight ? 0.10 : 0.16), 0),
                new GradientStop(ArcColor.WithAlpha(ink, 0), 0.42),
                new GradientStop(ArcColor.WithAlpha(ink, isLight ? 0.03 : 0.06), 1)),
            IsHitTestVisible = false
        });

        var light = new Border
        {
            Width = 220,
            Height = 220,
            CornerRadius = new CornerRadius(110),
            Background = new RadialGradientBrush
            {
                Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
                RadiusX = new RelativeScalar(0.5, RelativeUnit.Relative),
                RadiusY = new RelativeScalar(0.5, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(ArcColor.WithAlpha(ink, isLight ? 0.16 : 0.20), 0),
                    new GradientStop(ArcColor.WithAlpha(ink, 0.08), 0.5),
                    new GradientStop(ArcColor.WithAlpha(ink, 0), 1)
                }
            },
            IsHitTestVisible = false,
            Opacity = 0.9
        };
        layers.Children.Add(light);

        layers.Children.Add(new StackPanel
        {
            Spacing = 0,
            Margin = new Thickness(20, 16, 20, 16),
            Children = { topRow, new Panel { Height = 55 }, number, new Panel { Height = 16 }, bottomRow }
        });

        var face = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            BorderBrush = ArcGlass.Solid(ArcColor.WithAlpha(ink, 0.18)),
            BorderThickness = new Thickness(1),
            BoxShadow = ArcGlass.Shadow(ArcTokens.ShadowRaised),
            Child = layers
        };

        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            Width = CardWidth,
            Height = CardHeight,
            MinHeight = CardHeight,
            CornerRadius = new CornerRadius(18),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = face
        };
        button.Classes.Add("sg-plain");
        Canvas.SetLeft(button, 20);
        Canvas.SetTop(button, 18);

        AutomationProperties.SetName(button,
            $"{data.Issuer} {data.Product}, ending {data.LastFour}, card {index + 1} of {CardList().Count}, {data.BalanceLabel} {CurrencySymbol}{data.Balance:N2}");
        AutomationProperties.SetHelpText(button, "Press Enter to lift this card out of the stack.");

        var visual = new CardVisual
        {
            Data = data,
            Button = button,
            Face = face,
            HolderBlock = holder,
            NumberBlock = number,
            IssuerBlock = issuer,
            ProductBlock = product,
            ExpiresBlock = expires,
            Chip = chip,
            Contactless = contactless,
            Light = light
        };

        button.PointerEntered += (_, _) =>
        {
            _hoverIndex = index;
            ApplyStack(animate: true);
        };
        button.PointerExited += (_, _) =>
        {
            if (_hoverIndex == index)
            {
                _hoverIndex = -1;
                ApplyStack(animate: true);
            }
        };
        button.Click += (_, _) => Activate(index);
        button.GotFocus += (_, _) =>
        {
            if (!IsOpen)
            {
                _hoverIndex = index;
                ApplyStack(animate: true);
            }
        };
        button.LostFocus += (_, _) =>
        {
            if (!IsOpen && _hoverIndex == index)
            {
                _hoverIndex = -1;
                ApplyStack(animate: true);
            }
        };
        button.KeyDown += (_, e) => OnCardKeyDown(index, e);
        button.PointerPressed += (_, e) => OnCardPressed(index, e);
        button.PointerMoved += (_, e) => OnCardMoved(e);
        button.PointerReleased += (_, e) => OnCardReleased(e);

        _visuals.Add(visual);
        _stage.Children.Insert(0, button);
    }

    private (Color Top, Color Bottom, Color Ink, Color Muted, bool IsLight) Material(string variant)
    {
        var background = ArcGlass.Color(ArcTokens.Background);
        var foreground = ArcGlass.Color(ArcTokens.Foreground);
        var surface = ArcGlass.Color(ArcTokens.Surface);
        var accent = ArcGlass.Color(ArcTokens.Accent);
        var series = ArcGlass.Color(ArcTokens.Series2);
        var photoInk = ArcGlass.PhotoInk;
        var photoMuted = ArcGlass.PhotoMuted;

        switch (variant.ToLowerInvariant())
        {
            case "black":
            {
                var stock = ArcColor.ReOklch(foreground, ArcGlass.Dark ? 0.13 : 0.17, 0.01, 300);
                return (ArcGlass.Lift(stock, 0.04), stock, photoInk, photoMuted, false);
            }
            case "glass":
            {
                return (ArcGlass.ArcAlpha(ArcTokens.Surface, 0.62), ArcGlass.ArcAlpha(ArcTokens.Surface, 0.42),
                    foreground, ArcGlass.Color(ArcTokens.TextSecondary), ArcGlass.Dark ? false : true);
            }
            case "color":
            {
                var hue = ArcColor.ToOklch(accent).Chroma < 0.03 ? series : accent;
                var stock = ArcColor.ReOklch(hue, ArcGlass.Dark ? 0.40 : 0.46, 0.17, ArcColor.ToOklch(hue).Hue);
                return (ArcGlass.Lift(stock, 0.08), stock, photoInk, photoMuted, false);
            }
            default:
            {
                var mid = ArcColor.Mix(background, foreground, ArcGlass.Dark ? 0.30 : 0.16);
                var deep = ArcColor.Mix(background, foreground, ArcGlass.Dark ? 0.42 : 0.28);
                var ink = ArcGlass.Dark ? background : foreground;
                return (mid, deep, ink, ArcColor.WithAlpha(ink, 0.68), true);
            }
        }
    }

    private void Activate(int index)
    {
        var cards = CardList();
        if (index < 0 || index >= cards.Count)
        {
            return;
        }

        if (IsOpen && index == FrontIndex)
        {
            SetCurrentValue(IsOpenProperty, false);
            return;
        }

        SetCurrentValue(FrontIndexProperty, index);
        SetCurrentValue(IsOpenProperty, true);
        SelectedChanged?.Invoke(this, cards[index]);
    }

    private void SyncState(bool animate)
    {
        if (!_built)
        {
            return;
        }

        var cards = CardList();
        if (cards.Count == 0)
        {
            return;
        }

        FrontIndex = Math.Clamp(FrontIndex, 0, cards.Count - 1);
        for (var i = 0; i < _visuals.Count; i++)
        {
            _visuals[i].Button.IsTabStop = i == FrontIndex;
            AutomationProperties.SetItemStatus(_visuals[i].Button, IsOpen && i == FrontIndex ? "Expanded" : string.Empty);
        }

        ApplyStack(animate);
        ReorderCards();
        UpdateActivity();
        if (IsOpen)
        {
            SelectedChanged?.Invoke(this, cards[FrontIndex]);
        }
    }

    private void ApplyStack(bool animate)
    {
        var cards = CardList();
        for (var i = 0; i < _visuals.Count; i++)
        {
            var button = _visuals[i].Button;
            double x;
            double y;
            double scale;
            double rotation;
            double opacity = 1;

            if (IsOpen)
            {
                if (i == FrontIndex)
                {
                    x = 20;
                    y = 18 + _dragOffset;
                    scale = 1.06;
                    rotation = 0;
                }
                else
                {
                    x = 28 + ((i % 3) * 3);
                    y = 244 + (i * 6);
                    scale = 0.9;
                    rotation = (i - FrontIndex) * 2.2;
                    opacity = 1;
                }
            }
            else
            {
                x = 20 + (i * 5);
                y = 18 + (i * 22) - (i == _hoverIndex ? 10 : 0);
                scale = 1 - (i * 0.04);
                rotation = (i - 1) * 2.4;
                opacity = 1 - (i * 0.04);
            }

            button.Transitions = animate && !ArcThemeManager.ReducedMotion
                ? new Transitions
                {
                    new TransformOperationsTransition
                    {
                        Property = RenderTransformProperty,
                        Duration = GlassMotion.Smooth,
                        Easing = GlassMotion.SmoothEase
                    },
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = GlassMotion.Fast,
                        Easing = ArcMotion.EaseStandard
                    }
                }
                : null;

            button.Opacity = opacity;
            button.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
            button.RenderTransform = TransformOperations.Parse(
                $"translate({x - 20}px, {y - 18}px) rotate({rotation:0.##}deg) scale({scale:0.###})");
        }
    }

    /// <summary>
    /// Canvas does not sort by ZIndex in this version, so draw order is explicit: farthest cards first,
    /// the next card behind the front one, and the front card last.
    /// </summary>
    private void ReorderCards()
    {
        if (_visuals.Count == 0)
        {
            return;
        }

        var count = _visuals.Count;
        var front = Math.Clamp(FrontIndex, 0, count - 1);
        var ordered = _visuals
            .Select((visual, index) => (Visual: visual, Score: index == front ? 100 : count - ((index - front + count) % count)))
            .OrderBy(entry => entry.Score)
            .ToList();

        foreach (var entry in ordered)
        {
            _stage.Children.Remove(entry.Visual.Button);
        }

        foreach (var entry in ordered)
        {
            _stage.Children.Add(entry.Visual.Button);
        }
    }

    private void UpdateActivity()
    {
        var cards = CardList();
        var card = IsOpen && FrontIndex >= 0 && FrontIndex < cards.Count ? cards[FrontIndex] : null;

        _allCards.IsVisible = card is not null;
        _activityLabel.Text = card is null ? "Wallet" : "Activity";

        if (card is null)
        {
            var total = cards.Sum(item => item.Balance);
            _balance.Text = $"{CurrencySymbol}{total:N2}";
            _balanceLabel.Text = cards.Count == 1 ? "1 card" : $"{cards.Count} cards";
            _cardLine.Text = "Total balance across the stack";
            _transactions.Children.Clear();
            _rows.Clear();
            _emptyHint.IsVisible = true;
            AutomationProperties.SetName(this, $"{Label}, {cards.Count} cards, total {CurrencySymbol}{total:N2}");
            return;
        }

        _balance.Text = $"{CurrencySymbol}{card.Balance:N2}";
        _balanceLabel.Text = card.BalanceLabel;
        _cardLine.Text = $"{card.Issuer} · {card.Product} · {card.LastFour}";
        _emptyHint.IsVisible = false;
        BuildTransactions(card);
        AutomationProperties.SetName(this,
            $"{Label}, {card.Issuer} {card.Product} ending {card.LastFour}, {card.BalanceLabel} {CurrencySymbol}{card.Balance:N2}");
    }

    private void BuildTransactions(ArcWalletCardData card)
    {
        _transactions.Children.Clear();
        _rows.Clear();
        var index = 0;
        foreach (var transaction in card.Transactions)
        {
            var row = BuildTransactionRow(transaction, index++);
            _rows.Add(row);
            _transactions.Children.Add(row);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            if (ArcThemeManager.ReducedMotion)
            {
                row.Opacity = 1;
                continue;
            }

            row.Opacity = 0;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90 * (i + 1)) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                row.Transitions = new Transitions
                {
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = GlassMotion.Standard,
                        Easing = ArcMotion.EaseStandard
                    }
                };
                row.Opacity = 1;
            };
            timer.Start();
        }
    }

    private Border BuildTransactionRow(ArcWalletTransaction transaction, int index)
    {
        var glyph = transaction.Avatar is not null
            ? ArcGlass.Avatar(transaction.Avatar, 30)
            : ArcGlass.GlyphTile(transaction.Icon ?? "credit-card", 30, iconSize: 14);

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = transaction.Merchant,
            Classes = { "arc-body-sm", "arc-medium" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.Foreground)),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        text.Children.Add(new TextBlock
        {
            Text = transaction.Detail,
            Classes = { "arc-caption" },
            Foreground = ArcGlass.Solid(ArcGlass.Color(ArcTokens.TextMuted))
        });

        var amount = new TextBlock
        {
            Text = transaction.Amount < 0
                ? $"-{CurrencySymbol}{Math.Abs(transaction.Amount):N2}"
                : $"+{CurrencySymbol}{transaction.Amount:N2}",
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcGlass.Solid(ArcGlass.Color(
                transaction.Amount >= 0 ? ArcTokens.Success : ArcTokens.Foreground))
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 10,
            Margin = new Thickness(0, 5)
        };
        grid.Children.Add(glyph);
        Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        Grid.SetColumn(amount, 2);
        grid.Children.Add(amount);

        AutomationProperties.SetName(grid,
            $"{transaction.Merchant}, {transaction.Detail}, {amount.Text}");

        return new Border
        {
            Child = grid,
            Padding = new Thickness(0),
            Background = Brushes.Transparent
        };
    }

    private void OnCardPressed(int index, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (!IsOpen || index != FrontIndex)
        {
            return;
        }

        _dragging = true;
        _dragStartY = e.GetPosition(this).Y;
        e.Pointer.Capture(_visuals[index].Button);
        e.Handled = true;
    }

    private void OnCardMoved(PointerEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        var dy = e.GetPosition(this).Y - _dragStartY;
        _dragOffset = dy > 0 ? dy * 0.9 : dy * 0.25;
        ApplyStack(animate: false);
        e.Handled = true;
    }

    private void OnCardReleased(PointerReleasedEventArgs e)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        e.Pointer.Capture(null);
        var shouldClose = _dragOffset > 80;
        _dragOffset = 0;
        if (shouldClose)
        {
            SetCurrentValue(IsOpenProperty, false);
        }
        else
        {
            ApplyStack(animate: true);
        }
    }

    private void OnCardKeyDown(int index, KeyEventArgs e)
    {
        var count = _visuals.Count;
        if (count == 0)
        {
            return;
        }

        var next = index;
        switch (e.Key)
        {
            case Key.Down:
            case Key.Right:
                next = (index + 1) % count;
                break;
            case Key.Up:
            case Key.Left:
                next = (index - 1 + count) % count;
                break;
            case Key.Home:
                next = 0;
                break;
            case Key.End:
                next = count - 1;
                break;
            case Key.Escape when IsOpen:
                e.Handled = true;
                SetCurrentValue(IsOpenProperty, false);
                return;
            default:
                return;
        }

        e.Handled = true;
        SetCurrentValue(FrontIndexProperty, next);
        _visuals[next].Button.Focus();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && IsOpen)
        {
            e.Handled = true;
            SetCurrentValue(IsOpenProperty, false);
            if (FrontIndex >= 0 && FrontIndex < _visuals.Count)
            {
                _visuals[FrontIndex].Button.Focus();
            }
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (e.Pointer.Type != PointerType.Mouse || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        var point = e.GetPosition(this);
        var width = Math.Max(1, Bounds.Width);
        var height = Math.Max(1, Bounds.Height);
        _lightX.Target = Math.Clamp(point.X / width, 0, 1);
        _lightY.Target = Math.Clamp(point.Y / height, 0, 1);
        _loop.Start();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        _lightX.Target = 0.3;
        _lightY.Target = 0.2;
        _loop.Start();
    }

    private void OnFrame(double dt)
    {
        _lightX.Update(dt);
        _lightY.Update(dt);

        for (var i = 0; i < _visuals.Count; i++)
        {
            var light = _visuals[i].Light;
            var isFront = i == FrontIndex;
            light.Opacity = isFront ? 1 : 0.35;
            light.RenderTransform = TransformOperations.Parse(
                $"translate({(_lightX.Value * CardWidth) - 110:0.##}px, {(_lightY.Value * CardHeight) - 110:0.##}px)");
        }

        if (_lightX.Settled && _lightY.Settled)
        {
            _loop.Stop();
        }
    }
}




