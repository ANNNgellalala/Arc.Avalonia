using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Navigation.Controls;

/// <summary>
/// Peer views of the same object: a muted tab strip with one selection pill that glides between triggers,
/// and a panel that swaps in place with a short directional fade.
/// </summary>
[TemplatePart("PART_Triggers", typeof(StackPanel))]
[TemplatePart("PART_GlideLayer", typeof(Panel))]
[TemplatePart("PART_Glide", typeof(ArcGlide))]
[TemplatePart("PART_PanelHost", typeof(ContentControl))]
public sealed class ArcTabs : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcTabItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcTabs, IList<ArcTabItem>?>(nameof(Items));

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<ArcTabs, int>(nameof(SelectedIndex));

    public static readonly StyledProperty<string?> SelectedValueProperty =
        AvaloniaProperty.Register<ArcTabs, string?>(nameof(SelectedValue));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcTabs, string?>(nameof(Label));

    private readonly List<ArcButton> _triggers = new();
    private StackPanel? _triggerHost;
    private Panel? _glideLayer;
    private ArcGlide? _glide;
    private ContentControl? _panelHost;
    private int _lastIndex;
    private bool _syncing;

    static ArcTabs()
    {
        ItemsProperty.Changed.AddClassHandler<ArcTabs>((tabs, _) => tabs.Build());
        SelectedIndexProperty.Changed.AddClassHandler<ArcTabs>((tabs, _) => tabs.ApplySelection(animate: true));
        SelectedValueProperty.Changed.AddClassHandler<ArcTabs>((tabs, _) => tabs.SyncFromValue());
        LabelProperty.Changed.AddClassHandler<ArcTabs>((tabs, _) => tabs.UpdateLabel());
    }

    public ArcTabs()
    {
        LayoutUpdated += (_, _) => MoveGlide();
    }

    public event EventHandler<ArcTabItem>? SelectionChanged;

    /// <summary>Bubbled copy of <see cref="SelectionChanged"/>; additive contract for host containers.</summary>
    public static readonly RoutedEvent<ArcTabsSelectionChangedEventArgs> SelectionChangedEvent =
        RoutedEvent.Register<ArcTabs, ArcTabsSelectionChangedEventArgs>("SelectionChanged", RoutingStrategies.Bubble);

    /// <summary>Additive routed notification for <see cref="SelectionChanged"/>; the CLR event above still fires.</summary>
    public event EventHandler<ArcTabsSelectionChangedEventArgs>? SelectionChangedRouted
    {
        add => AddHandler(SelectionChangedEvent, value);
        remove => RemoveHandler(SelectionChangedEvent, value);
    }

    public IList<ArcTabItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Index of the active tab; the selected pill glides to it.</summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>Value of the active tab; kept in step with <see cref="SelectedIndex"/>.</summary>
    public string? SelectedValue
    {
        get => GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    /// <summary>Accessible name of the tab list.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _triggerHost = e.NameScope.Find<StackPanel>("PART_Triggers");
        _glideLayer = e.NameScope.Find<Panel>("PART_GlideLayer");
        _glide = e.NameScope.Find<ArcGlide>("PART_Glide");
        _panelHost = e.NameScope.Find<ContentControl>("PART_PanelHost");
        if (_glide is not null)
        {
            _glide.Classes.Add("pill");
        }

        if (_panelHost is not null && !ArcThemeManager.ReducedMotion)
        {
            _panelHost.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = ArcMotion.Fast,
                    Easing = ArcMotion.EaseStandard
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = ArcMotion.Standard,
                    Easing = ArcMotion.EaseEnter
                }
            };
        }

        Build();
    }

    private void UpdateLabel()
    {
        if (!string.IsNullOrWhiteSpace(Label))
        {
            AutomationProperties.SetName(this, Label!);
        }
    }

    private void Build()
    {
        if (_triggerHost is null)
        {
            return;
        }

        _triggerHost.Children.Clear();
        _triggers.Clear();

        var items = Items?.ToList() ?? new List<ArcTabItem>();
        for (var i = 0; i < items.Count; i++)
        {
            var index = i;
            var item = items[i];
            var trigger = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                MinWidth = 88,
                MinHeight = 36,
                Padding = new Thickness(14, 0),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                IsEnabled = !item.IsDisabled
            };
            trigger.CornerRadius = ArcActionVisuals.Concentric(ArcActionVisuals.Radius(ArcTokens.RadiusControl), 3);
            trigger.Classes.Add("tab-trigger");
            trigger.Content = BuildTriggerContent(trigger, item);
            ArcActionVisuals.SetName(trigger, item.Header);
            TriggerHelp(trigger);
            trigger.Click += (_, _) =>
            {
                SetCurrentValue(SelectedIndexProperty, index);
                trigger.Focus();
            };
            _triggers.Add(trigger);
            _triggerHost.Children.Add(trigger);
        }

        if (SelectedIndex >= _triggers.Count)
        {
            SetCurrentValue(SelectedIndexProperty, 0);
        }

        ApplySelection(animate: false);
    }

    private static void TriggerHelp(ArcButton trigger)
    {
        AutomationProperties.SetHelpText(trigger, "Tab");
    }

    private static Control BuildTriggerContent(ArcButton trigger, ArcTabItem item)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 7,
            VerticalAlignment = VerticalAlignment.Center
        };
        if (!string.IsNullOrWhiteSpace(item.Icon))
        {
            var icon = ArcActionVisuals.InheritIcon(item.Icon, trigger, 15);
            icon.Classes.Add("tab-icon");
            panel.Children.Add(icon);
        }

        panel.Children.Add(ArcActionVisuals.InheritText(item.Header, trigger));
        return panel;
    }

    private void SyncFromValue()
    {
        if (_syncing)
        {
            return;
        }

        var items = Items?.ToList();
        if (items is null || SelectedValue is null)
        {
            return;
        }

        var index = items.ToList().FindIndex(item => item.Value == SelectedValue);
        if (index >= 0 && index != SelectedIndex)
        {
            _syncing = true;
            try
            {
                SetCurrentValue(SelectedIndexProperty, index);
            }
            finally
            {
                _syncing = false;
            }
        }
    }

    private void ApplySelection(bool animate)
    {
        var items = Items?.ToList() ?? new List<ArcTabItem>();
        if (items.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(SelectedIndex, 0, items.Count - 1);
        for (var i = 0; i < _triggers.Count; i++)
        {
            _triggers[i].Classes.Set("selected", i == index);
        }

        if (!_syncing && SelectedValue != items[index].Value)
        {
            _syncing = true;
            try
            {
                SetCurrentValue(SelectedValueProperty, items[index].Value);
            }
            finally
            {
                _syncing = false;
            }
        }

        ShowPanel(items[index], animate, index >= _lastIndex ? 1 : -1);
        _lastIndex = index;
        MoveGlide();
                        var selectionChangedArgs = new ArcTabsSelectionChangedEventArgs(SelectionChangedEvent) { Item = items[index] };
                SelectionChanged?.Invoke(this, items[index]);
                RaiseEvent(selectionChangedArgs);
    }

    private void ShowPanel(ArcTabItem item, bool animate, int direction)
    {
        if (_panelHost is null)
        {
            return;
        }

        _panelHost.Content = item.Content;

        if (!animate || ArcThemeManager.ReducedMotion || _panelHost.Transitions is null)
        {
            _panelHost.Opacity = 1;
            _panelHost.RenderTransform = TransformOperations.Parse("translateX(0px)");
            return;
        }

        _panelHost.Opacity = 0;
        _panelHost.RenderTransform = TransformOperations.Parse($"translateX({8 * direction}px)");
        Dispatcher.UIThread.Post(() =>
        {
            _panelHost.Opacity = 1;
            _panelHost.RenderTransform = TransformOperations.Parse("translateX(0px)");
        }, DispatcherPriority.Render);
    }

    private void MoveGlide()
    {
        if (_glide is null || _glideLayer is null || _triggers.Count == 0)
        {
            return;
        }

        var index = Math.Clamp(SelectedIndex, 0, _triggers.Count - 1);
        var trigger = _triggers[index];
        var origin = trigger.TranslatePoint(new Point(0, 0), _glideLayer);
        if (origin is null)
        {
            return;
        }

        _glide.ShowAt(new Rect(origin.Value, trigger.Bounds.Size));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_triggers.Count == 0 || e.Handled)
        {
            return;
        }

        var index = _triggers.FindIndex(trigger => trigger.IsFocused);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right:
                Move(index + 1);
                e.Handled = true;
                break;
            case Key.Left:
                Move(index - 1);
                e.Handled = true;
                break;
            case Key.Home:
                Move(0);
                e.Handled = true;
                break;
            case Key.End:
                Move(_triggers.Count - 1);
                e.Handled = true;
                break;
        }
    }

    private void Move(int index)
    {
        if (index < 0 || index >= _triggers.Count)
        {
            return;
        }

        SetCurrentValue(SelectedIndexProperty, index);
        _triggers[index].Focus();
    }
}