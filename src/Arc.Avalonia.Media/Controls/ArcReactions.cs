using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Media.Controls;

/// <summary>A reaction chip; styled as a pill and marked with the <c>on</c> class when you reacted.</summary>
public sealed class ArcReactionPill : Button
{
}

/// <summary>
/// One short burst when a reaction is added: a ring plus six dots in the series colours. Purely
/// decorative, skipped entirely under reduced motion.
/// </summary>
internal sealed class ArcReactionBurst : Control
{
    private static readonly TimeSpan Life = TimeSpan.FromMilliseconds(430);
    private readonly DispatcherTimer _timer;
    private DateTime _started;
    private double _progress;

    public ArcReactionBurst()
    {
        Width = 64;
        Height = 64;
        IsHitTestVisible = false;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
    }

    public event EventHandler? Completed;

    public void Begin()
    {
        if (ArcThemeManager.ReducedMotion)
        {
            Completed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _started = DateTime.UtcNow;
        _progress = 0;
        _timer.Start();
    }

    private void Tick()
    {
        _progress = Math.Clamp((DateTime.UtcNow - _started).TotalMilliseconds / Life.TotalMilliseconds, 0, 1);
        InvalidateVisual();
        if (_progress < 1)
        {
            return;
        }

        _timer.Stop();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    public override void Render(DrawingContext context)
    {
        if (_progress <= 0)
        {
            return;
        }

        var p = _progress;
        var eased = 1 - Math.Pow(1 - p, 3);
        var center = new Point(32, 32);
        var alpha = 1 - p;

        var ringBrush = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(ArcTokens.Accent), 0.45 * alpha));
        context.DrawEllipse(null, new Pen(ringBrush, 2), center, 8 + 16 * eased, 8 + 16 * eased);

        for (var index = 0; index < 6; index++)
        {
            var angle = index * Math.PI / 3 - Math.PI / 2;
            var distance = 7 + 17 * eased;
            var dot = new Point(center.X + Math.Cos(angle) * distance, center.Y + Math.Sin(angle) * distance);
            var token = (index % 4) switch
            {
                0 => ArcTokens.Series1,
                1 => ArcTokens.Series2,
                2 => ArcTokens.Series3,
                _ => ArcTokens.Series4
            };
            var brush = new SolidColorBrush(ArcColor.WithAlpha(ArcMediaUi.Color(token), alpha));
            var radius = 2.6 * (1 - p * 0.45);
            context.DrawEllipse(brush, null, dot, radius, radius);
        }
    }
}

/// <summary>
/// Arc's reaction bar: one-tap pills with counts that tick to their new value in tabular figures, a
/// burst when you add one, and an add button that opens a keyboard-navigable picker tray.
/// </summary>
public class ArcReactions : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcReactionOption>?> OptionsProperty =
        AvaloniaProperty.Register<ArcReactions, IList<ArcReactionOption>?>(nameof(Options));

    public static readonly StyledProperty<IList<ArcReaction>?> ValueProperty =
        AvaloniaProperty.Register<ArcReactions, IList<ArcReaction>?>(nameof(Value));

    public static readonly StyledProperty<IList<ArcReaction>?> DefaultValueProperty =
        AvaloniaProperty.Register<ArcReactions, IList<ArcReaction>?>(nameof(DefaultValue));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcReactions, string>(nameof(Label), "Reactions");

    private readonly List<ArcReaction> _reactions = new();
    private readonly List<PillView> _views = new();
    private WrapPanel? _host;
    private Canvas? _burstLayer;
    private Button? _addButton;
    private TextBlock? _status;
    private Popup? _picker;
    private bool _pickerWired;
    private readonly List<Button> _pickerButtons = new();

    private sealed class PillView
    {
        public string Id { get; init; } = string.Empty;

        public ArcReactionPill Pill { get; init; } = null!;

        public TextBlock CountText { get; init; } = null!;

        public double Displayed { get; set; }

        public ArcMediaTween Tween { get; } = new();
    }

    static ArcReactions()
    {
        OptionsProperty.Changed.AddClassHandler<ArcReactions>((bar, _) => bar.Sync(animateCounts: false));
        ValueProperty.Changed.AddClassHandler<ArcReactions>((bar, _) => bar.ResetFromValue());
        DefaultValueProperty.Changed.AddClassHandler<ArcReactions>((bar, _) =>
        {
            if (bar.Value is null)
            {
                bar.ResetFromValue();
            }
        });
        LabelProperty.Changed.AddClassHandler<ArcReactions>((bar, _) => bar.UpdateLabel());
    }

    /// <summary>Every reaction the picker offers.</summary>
    public IList<ArcReactionOption>? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Controlled reactions; a count of zero leaves the bar.</summary>
    public IList<ArcReaction>? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Initial reactions when the bar manages its own values.</summary>
    public IList<ArcReaction>? DefaultValue
    {
        get => GetValue(DefaultValueProperty);
        set => SetValue(DefaultValueProperty, value);
    }

    /// <summary>Accessible name for the bar.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raised with the next list and the single change that produced it.</summary>
    public event EventHandler<ArcReactionsChangedEventArgs>? ValueChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<WrapPanel>("Pills");
        _burstLayer = e.NameScope.Find<Canvas>("BurstLayer");
        _status = e.NameScope.Find<TextBlock>("Status");
        _picker = e.NameScope.Find<Popup>("PART_Picker");
        if (_picker is not null && !_pickerWired)
        {
            _pickerWired = true;
            _picker.Closed += (_, _) =>
            {
                _pickerButtons.Clear();
                _addButton?.Focus();
            };
        }

        UpdateLabel();
        ResetFromValue();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_host is not null && _views.Count == 0)
        {
            ResetFromValue();
        }
    }

    private void UpdateLabel()
    {
        AutomationProperties.SetName(this, string.IsNullOrWhiteSpace(Label) ? "Reactions" : Label);
    }

    private void ResetFromValue()
    {
        _reactions.Clear();
        var source = Value ?? DefaultValue;
        if (source is not null)
        {
            foreach (var reaction in source)
            {
                _reactions.Add(new ArcReaction(reaction.Id, reaction.Count, reaction.Reacted));
            }
        }

        Sync(animateCounts: false);
    }

    private ArcReactionOption? Option(string id)
    {
        if (Options is null)
        {
            return null;
        }

        foreach (var option in Options)
        {
            if (string.Equals(option.Id, id, StringComparison.Ordinal))
            {
                return option;
            }
        }

        return null;
    }

    private void Sync(bool animateCounts)
    {
        if (_host is null)
        {
            return;
        }

        var previous = _views.ToDictionary(view => view.Id);
        var focused = _views.FirstOrDefault(view => view.Pill.IsKeyboardFocusWithin);
        var focusedIndex = focused is null ? -1 : _views.IndexOf(focused);
        _host.Children.Clear();
        _views.Clear();

        foreach (var reaction in _reactions)
        {
            if (reaction.Count <= 0)
            {
                continue;
            }

            var option = Option(reaction.Id) ?? new ArcReactionOption(reaction.Id, reaction.Id, "heart");
            var displayed = previous.TryGetValue(reaction.Id, out var old) ? old.Displayed : reaction.Count;
            var view = CreatePill(reaction, option, displayed);
            _views.Add(view);
            _host.Children.Add(view.Pill);
        }

        _addButton = CreateAddButton();
        _addButton.IsVisible = Options is { Count: > 0 };
        _host.Children.Add(_addButton);

        if (animateCounts)
        {
            foreach (var view in _views)
            {
                AnimateCount(view);
            }
        }

        if (focused is not null)
        {
            Control? replacement = _views.FirstOrDefault(view => view.Id == focused.Id)?.Pill;
            if (replacement is null && _views.Count > 0)
            {
                replacement = _views[Math.Clamp(focusedIndex, 0, _views.Count - 1)].Pill;
            }

            replacement ??= _addButton is { IsVisible: true } ? _addButton : null;
            replacement?.Focus();
        }

        UpdateLabel();
    }

    private PillView CreatePill(ArcReaction reaction, ArcReactionOption option, double displayed)
    {
        var pill = new ArcReactionPill
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(10, 6),
            MinHeight = 34,
            IsTabStop = true
        };

        var icon = new ArcIcon { Icon = option.Icon, Size = 16, HorizontalAlignment = HorizontalAlignment.Center };
        icon.Bind(ArcIcon.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = pill });

        var countText = new TextBlock
        {
            Text = Format(displayed),
            FontSize = 13,
            FontWeight = FontWeight.Medium,
            MinWidth = 14,
            TextAlignment = TextAlignment.Right,
            FontFeatures = ArcMediaUi.Tabular,
            VerticalAlignment = VerticalAlignment.Center
        };
        countText.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Button.Foreground)) { Source = pill });

        pill.Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { icon, countText }
        };

        pill.Classes.Set("on", reaction.Reacted);
        pill.Click += (_, _) => Toggle(reaction.Id);
        var view = new PillView { Id = reaction.Id, Pill = pill, CountText = countText, Displayed = displayed };
        UpdatePillName(view, option, reaction);
        return view;
    }

    private void UpdatePillName(PillView view, ArcReactionOption option, ArcReaction reaction)
    {
        var count = Math.Max(0, reaction.Count);
        var text = $"{option.Label}, {count} {(count == 1 ? "reaction" : "reactions")}";
        if (reaction.Reacted)
        {
            text += ", including yours";
        }

        AutomationProperties.SetName(view.Pill, text);
    }

    private Button CreateAddButton()
    {
        var button = new ArcReactionPill
        {
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(1, 6),
            MinHeight = 34,
            Width = 34,
            Classes = { "add" },
            Content = ArcMediaUi.Icon("plus", 15)
        };
        AutomationProperties.SetName(button, "Add reaction");
        ToolTip.SetTip(button, "Add reaction");
        button.Click += (_, _) => TogglePicker();
        return button;
    }

    private static string Format(double value) =>
        Math.Round(value).ToString("0", CultureInfo.InvariantCulture);

    private void AnimateCount(PillView view)
    {
        var target = _reactions.FirstOrDefault(reaction => reaction.Id == view.Id)?.Count ?? 0;
        if (Math.Abs(target - view.Displayed) < 0.001)
        {
            return;
        }

        var from = view.Displayed;
        view.Tween.Run(from, target, ArcMediaUi.Duration(ArcTokens.DurationStandard), value =>
        {
            view.Displayed = value;
            view.CountText.Text = Format(value);
        }, () =>
        {
            view.Displayed = target;
            view.CountText.Text = Format(target);
        });
    }

    private void Toggle(string id)
    {
        var index = _reactions.FindIndex(reaction => string.Equals(reaction.Id, id, StringComparison.Ordinal));
        ArcReactionChange change;

        if (index >= 0)
        {
            var reaction = _reactions[index];
            if (reaction.Reacted)
            {
                reaction.Count--;
                reaction.Reacted = false;
                change = new ArcReactionChange(id, false);
                if (reaction.Count <= 0)
                {
                    _reactions.RemoveAt(index);
                }
            }
            else
            {
                reaction.Count++;
                reaction.Reacted = true;
                change = new ArcReactionChange(id, true);
            }
        }
        else
        {
            _reactions.Add(new ArcReaction(id, 1, true));
            change = new ArcReactionChange(id, true);
        }

        Sync(animateCounts: true);
        if (change.Reacted)
        {
            PlayBurst(id);
        }

        Announce(change, id);
        ValueChanged?.Invoke(this, new ArcReactionsChangedEventArgs(
            _reactions.Select(reaction => new ArcReaction(reaction.Id, reaction.Count, reaction.Reacted)).ToList(),
            change));
    }

    private void Announce(ArcReactionChange change, string id)
    {
        if (_status is null)
        {
            return;
        }

        var option = Option(id);
        var name = option?.Label ?? id;
        _status.Text = change.Reacted ? $"{name} reaction added" : $"{name} reaction removed";
    }

    private void PlayBurst(string id)
    {
        if (_burstLayer is null || ArcThemeManager.ReducedMotion)
        {
            return;
        }

        var view = _views.FirstOrDefault(candidate => candidate.Id == id);
        if (view is null)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_burstLayer is null || !view.Pill.IsAttachedToVisualTree())
            {
                return;
            }

            if (view.Pill.TranslatePoint(new Point(view.Pill.Bounds.Width / 2, view.Pill.Bounds.Height / 2), _burstLayer) is not { } center)
            {
                return;
            }

            var burst = new ArcReactionBurst();
            Canvas.SetLeft(burst, center.X - 32);
            Canvas.SetTop(burst, center.Y - 32);
            _burstLayer.Children.Add(burst);
            burst.Completed += (_, _) => _burstLayer?.Children.Remove(burst);
            burst.Begin();
        }, DispatcherPriority.Loaded);
    }

    private void TogglePicker()
    {
        if (_picker is { IsOpen: true })
        {
            ClosePicker();
            return;
        }

        OpenPicker();
    }

    private void OpenPicker()
    {
        if (_addButton is null || Options is null || Options.Count == 0 || _picker is null)
        {
            return;
        }

        var tray = new WrapPanel { MaxWidth = 232 };
        _pickerButtons.Clear();
        foreach (var option in Options)
        {
            var button = new ArcReactionPill
            {
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(8),
                Content = ArcMediaUi.Icon(option.Icon, 20),
                IsTabStop = true
            };
            AutomationProperties.SetName(button, $"React with {option.Label}");
            ToolTip.SetTip(button, option.Label);
            var id = option.Id;
            button.Click += (_, _) =>
            {
                Toggle(id);
                ClosePicker();
            };
            _pickerButtons.Add(button);
            tray.Children.Add(button);
        }

        var surface = new Border
        {
            Background = ArcMediaUi.Brush(ArcTokens.SurfaceRaised),
            BorderBrush = ArcMediaUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcMediaUi.Radius(ArcTokens.RadiusPanel),
            Padding = new Thickness(8),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            Child = tray
        };
        AutomationProperties.SetName(surface, "Reaction picker");
        surface.KeyDown += OnPickerKeyDown;

        _picker.PlacementTarget = _addButton;
        _picker.Placement = PlacementMode.Top;
        _picker.VerticalOffset = -6;
        _picker.Child = surface;
        _picker.IsOpen = true;
        Dispatcher.UIThread.Post(() => _pickerButtons.FirstOrDefault()?.Focus(), DispatcherPriority.Loaded);
    }

    private void ClosePicker()
    {
        if (_picker is { IsOpen: true })
        {
            _picker.IsOpen = false;
        }
    }

    private void OnPickerKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ClosePicker();
            _addButton?.Focus();
            e.Handled = true;
            return;
        }

        if (_pickerButtons.Count == 0)
        {
            return;
        }

        var current = _pickerButtons.FindIndex(button => button.IsFocused);
        var delta = e.Key switch
        {
            Key.Left or Key.Up => -1,
            Key.Right or Key.Down => 1,
            _ => 0
        };

        if (e.Key == Key.Home)
        {
            _pickerButtons[0].Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.End)
        {
            _pickerButtons[^1].Focus();
            e.Handled = true;
            return;
        }

        if (delta == 0)
        {
            return;
        }

        var start = current < 0 ? 0 : current;
        var next = (start + delta + _pickerButtons.Count) % _pickerButtons.Count;
        _pickerButtons[next].Focus();
        e.Handled = true;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ClosePicker();
        foreach (var view in _views)
        {
            view.Tween.Stop();
        }
    }
}




