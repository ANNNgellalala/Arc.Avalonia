using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

public enum ArcRadioCardsLayout
{
    Grid,
    List
}

public sealed class ArcRadioCardOption
{
    public ArcRadioCardOption(
        string value,
        string label,
        string? description = null,
        string? meta = null,
        string? icon = null,
        bool disabled = false,
        string? disabledReason = null)
    {
        Value = value;
        Label = label;
        Description = description;
        Meta = meta;
        Icon = icon;
        Disabled = disabled;
        DisabledReason = disabledReason;
    }

    public string Value { get; }

    public string Label { get; }

    public string? Description { get; }

    /// <summary>Trailing detail such as a price or an estimate.</summary>
    public string? Meta { get; }

    /// <summary>Lucide icon name shown beside the label.</summary>
    public string? Icon { get; }

    public bool Disabled { get; }

    /// <summary>Explains why the card can not be picked; replaces the description while disabled.</summary>
    public string? DisabledReason { get; }
}

public sealed class ArcRadioCardsValueEventArgs : EventArgs
{
    public ArcRadioCardsValueEventArgs(string value) => Value = value;

    public string Value { get; }
}

/// <summary>
/// Arc's radio cards: selectable option cards with a sliding selection ring, price and description
/// slots, and roving radio keyboard behaviour.
/// </summary>
public class ArcRadioCards : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcRadioCardOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcRadioCards, IList<ArcRadioCardOption>?>(nameof(Options));

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ArcRadioCards, string?>(nameof(Value));

    public static readonly StyledProperty<ArcRadioCardsLayout> LayoutProperty =
        AvaloniaProperty.Register<ArcRadioCards, ArcRadioCardsLayout>(nameof(Layout));

    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<ArcRadioCards, double>(nameof(MinColumnWidth), 180d);

    private Panel? _host;
    private Canvas? _ringHost;
    private Border? _ring;
    private Panel? _container;
    private readonly List<CardVisual> _visuals = new();
    private bool _syncing;
    private int _columns;
    private bool _wasSelected;

    static ArcRadioCards()
    {
        OptionsProperty.Changed.AddClassHandler<ArcRadioCards>((cards, _) => cards.Build());
        LayoutProperty.Changed.AddClassHandler<ArcRadioCards>((cards, _) => cards.Build());
        ValueProperty.Changed.AddClassHandler<ArcRadioCards>((cards, _) => cards.ApplyValue(animate: true));
        MinColumnWidthProperty.Changed.AddClassHandler<ArcRadioCards>((cards, _) => cards.Build());
    }

    public ArcRadioCards()
    {
        ClipToBounds = false;
    }

    /// <summary>Raised when a new card is selected.</summary>
    public event EventHandler<ArcRadioCardsValueEventArgs>? ValueChanged;

    /// <summary>Choices: value, label, description, meta, icon and an optional disabled reason.</summary>
    public IList<ArcRadioCardOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Grid places cards in responsive columns; List stacks full width rows.</summary>
    public ArcRadioCardsLayout Layout
    {
        get => GetValue(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    /// <summary>Narrowest a grid column may get before the grid drops a column.</summary>
    public double MinColumnWidth
    {
        get => GetValue(MinColumnWidthProperty);
        set => SetValue(MinColumnWidthProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("Host");
        _ringHost = e.NameScope.Find<Canvas>("RingHost");
        _ring = e.NameScope.Find<Border>("Ring");
        Build();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty)
        {
            if (Layout == ArcRadioCardsLayout.Grid)
            {
                UpdateColumns();
            }

            UpdateRing(animate: false);
        }
    }

    private ControlTheme? CardTheme =>
        Application.Current is { } app && app.TryFindResource("ArcCardOptionTheme", out var value)
            ? value as ControlTheme
            : null;

    private void Build()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _visuals.Clear();
        _syncing = true;

        var options = Options ?? Array.Empty<ArcRadioCardOption>();
        var group = "arc-cards-" + Guid.NewGuid().ToString("N");
        var selected = options.FirstOrDefault(option =>
            string.Equals(option.Value, Value, StringComparison.Ordinal));

        _container = Layout == ArcRadioCardsLayout.Grid
            ? new UniformGrid { HorizontalAlignment = HorizontalAlignment.Stretch }
            : new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Stretch };

        if (Layout == ArcRadioCardsLayout.Grid)
        {
            _columns = ColumnsFor(Bounds.Width);
            ((UniformGrid)_container).Columns = _columns;
        }

        foreach (var option in options)
        {
            var card = CreateCard(option, group);
            _container.Children.Add(card.Button);
            _visuals.Add(card);

            if (card.Button.IsChecked == true)
            {
                selected = option;
            }
        }

        _host.Children.Add(_container);
        _syncing = false;

        if (selected is null && options.Count > 0)
        {
            SetCurrentValue(ValueProperty, options[0].Value);
            _visuals[0].Button.IsChecked = true;
        }

        ApplyValue(animate: false);
    }

    private int ColumnsFor(double width)
    {
        var options = Options?.Count ?? 0;
        if (width <= 0)
        {
            return Math.Max(1, Math.Min(3, options));
        }

        return Math.Max(1, Math.Min(options, (int)Math.Floor(width / Math.Max(80, MinColumnWidth))));
    }

    private void UpdateColumns()
    {
        if (_container is not UniformGrid grid)
        {
            return;
        }

        var columns = ColumnsFor(Bounds.Width);
        if (columns == _columns)
        {
            return;
        }

        _columns = columns;
        grid.Columns = columns;
        UpdateRing(animate: false);
    }

    private CardVisual CreateCard(ArcRadioCardOption option, string group)
    {
        var content = new Panel();
        var stack = new StackPanel { Spacing = 3 };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (!string.IsNullOrEmpty(option.Icon))
        {
            titleRow.Children.Add(new ArcIcon
            {
                Icon = option.Icon,
                Size = 16,
                Foreground = ArcResources.Brush(ArcTokens.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        titleRow.Children.Add(new TextBlock
        {
            Text = option.Label,
            FontSize = 14,
            FontWeight = FontWeight.Medium,
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(titleRow);

        var description = option.Disabled && !string.IsNullOrEmpty(option.DisabledReason)
            ? option.DisabledReason
            : option.Description;
        if (!string.IsNullOrWhiteSpace(description))
        {
            stack.Children.Add(new TextBlock
            {
                Text = description,
                Classes = { "arc-caption" },
                TextWrapping = TextWrapping.Wrap,
                Foreground = ArcResources.Brush(option.Disabled ? ArcTokens.TextMuted : ArcTokens.TextMuted)
            });
        }

        if (Layout == ArcRadioCardsLayout.List)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(stack);

            if (!string.IsNullOrEmpty(option.Meta))
            {
                var meta = new TextBlock
                {
                    Text = option.Meta,
                    FontSize = 14,
                    FontWeight = FontWeight.Medium,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0)
                };
                Grid.SetColumn(meta, 1);
                row.Children.Add(meta);
            }

            content.Children.Add(row);
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = option.Meta,
                FontSize = 14,
                FontWeight = FontWeight.Medium,
                Margin = new Thickness(0, 8, 0, 0),
                IsVisible = !string.IsNullOrEmpty(option.Meta)
            });
            content.Children.Add(stack);
        }

        var button = new RadioButton
        {
            Theme = CardTheme,
            Content = content,
            GroupName = group,
            Tag = option,
            IsEnabled = IsEnabled && !option.Disabled,
            IsChecked = string.Equals(option.Value, Value, StringComparison.Ordinal),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        button.AddHandler(KeyDownEvent, OnCardKeyDown, RoutingStrategies.Tunnel);
        button.IsCheckedChanged += (_, _) => { if (button.IsChecked == true) { OnCardChecked(option); } };

        return new CardVisual(option, button);
    }

    private void OnCardChecked(ArcRadioCardOption option)
    {
        if (_syncing)
        {
            return;
        }

        if (string.Equals(Value, option.Value, StringComparison.Ordinal))
        {
            return;
        }

        SetCurrentValue(ValueProperty, option.Value);
        ApplyValue(animate: true);
        ValueChanged?.Invoke(this, new ArcRadioCardsValueEventArgs(option.Value));
    }

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (_visuals.Count == 0 || !IsEnabled)
        {
            return;
        }

        var enabled = _visuals.Where(visual => visual.Button.IsEnabled).ToList();
        if (enabled.Count == 0)
        {
            return;
        }

        var index = Math.Max(0, enabled.FindIndex(visual => visual.Button.IsChecked == true));
        var columns = Layout == ArcRadioCardsLayout.Grid ? Math.Max(1, _columns) : 1;

        switch (e.Key)
        {
            case Key.Right:
                e.Handled = true;
                Choose(enabled[(index + 1) % enabled.Count]);
                break;
            case Key.Left:
                e.Handled = true;
                Choose(enabled[(index - 1 + enabled.Count) % enabled.Count]);
                break;
            case Key.Down:
                e.Handled = true;
                Choose(enabled[Math.Min(enabled.Count - 1, index + columns)]);
                break;
            case Key.Up:
                e.Handled = true;
                Choose(enabled[Math.Max(0, index - columns)]);
                break;
            case Key.Home:
                e.Handled = true;
                Choose(enabled[0]);
                break;
            case Key.End:
                e.Handled = true;
                Choose(enabled[^1]);
                break;
            case Key.Enter:
                e.Handled = true;
                Choose(enabled[index]);
                break;
        }
    }

    private void Choose(CardVisual visual)
    {
        if (!visual.Button.IsEnabled)
        {
            return;
        }

        visual.Button.IsChecked = true;
        visual.Button.Focus();
        OnCardChecked(visual.Option);
    }

    private void ApplyValue(bool animate)
    {
        if (_visuals.Count == 0)
        {
            return;
        }

        var selected = _visuals.FirstOrDefault(visual =>
            string.Equals(visual.Option.Value, Value, StringComparison.Ordinal));

        _syncing = true;
        foreach (var visual in _visuals)
        {
            var active = ReferenceEquals(visual, selected);
            visual.Button.IsChecked = active;
            visual.Button.IsTabStop = active;
        }

        _syncing = false;

        UpdateRing(animate);
        AutomationProperties.SetName(this, "Options");
        AutomationProperties.SetHelpText(this, selected?.Option.Label ?? string.Empty);
    }

    private void UpdateRing(bool animate)
    {
        if (_ring is null || _ringHost is null)
        {
            return;
        }

        var selected = _visuals.FirstOrDefault(visual =>
            string.Equals(visual.Option.Value, Value, StringComparison.Ordinal));

        if (selected is null)
        {
            _ring.IsVisible = false;
            _wasSelected = false;
            return;
        }

        var bounds = selected.Button.Bounds;
        if (bounds.Width <= 0)
        {
            return;
        }

        _ring.IsVisible = true;
        var fromX = Canvas.GetLeft(_ring);
        var fromY = Canvas.GetTop(_ring);
        var fromW = _ring.Width;
        var fromH = _ring.Height;

        if (!animate || !_wasSelected || ArcThemeManager.ReducedMotion ||
            double.IsNaN(fromX) || double.IsNaN(fromW) || fromW <= 0)
        {
            SetRing(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            _wasSelected = true;
            return;
        }

        ArcAnim.To(0, 1, ArcAnim.MorphDuration, ArcAnim.Morph, value =>
        {
            if (_ring is null)
            {
                return;
            }

            Canvas.SetLeft(_ring, fromX + (bounds.X - fromX) * value);
            Canvas.SetTop(_ring, fromY + (bounds.Y - fromY) * value);
            _ring.Width = fromW + (bounds.Width - fromW) * value;
            _ring.Height = fromH + (bounds.Height - fromH) * value;
        });
        _wasSelected = true;
    }

    private void SetRing(double x, double y, double width, double height)
    {
        if (_ring is null)
        {
            return;
        }

        Canvas.SetLeft(_ring, x);
        Canvas.SetTop(_ring, y);
        _ring.Width = width;
        _ring.Height = height;
    }

    private sealed record CardVisual(ArcRadioCardOption Option, RadioButton Button);
}
