using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using LayoutOrientation = Avalonia.Layout.Orientation;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Controls;

public enum ArcButtonGroupVariant
{
    /// <summary>Sits on a neutral surface with a quiet border.</summary>
    Outline,

    /// <summary>Takes the primary fill, for the main actions of a view.</summary>
    Solid
}

public enum ArcButtonGroupSize
{
    Sm,
    Md
}

public enum ArcButtonGroupOrientation
{
    Horizontal,
    Vertical
}

/// <summary>
/// Related actions joined into one surface: hairline dividers between segments, one highlight gliding
/// between them, and an optional trailing chevron that opens more actions.
/// </summary>
public sealed class ArcButtonGroup : TemplatedControl
{
    public static readonly StyledProperty<IList<ArcButtonGroupItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcButtonGroup, IList<ArcButtonGroupItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcButtonGroup, string?>(nameof(Label));

    public static readonly StyledProperty<ArcMenuDefinition?> MenuProperty =
        AvaloniaProperty.Register<ArcButtonGroup, ArcMenuDefinition?>(nameof(Menu));

    public static readonly StyledProperty<ArcButtonGroupVariant> VariantProperty =
        AvaloniaProperty.Register<ArcButtonGroup, ArcButtonGroupVariant>(nameof(Variant));

    public static readonly StyledProperty<ArcButtonGroupSize> SizeProperty =
        AvaloniaProperty.Register<ArcButtonGroup, ArcButtonGroupSize>(nameof(Size));

    public static readonly StyledProperty<ArcButtonGroupOrientation> OrientationProperty =
        AvaloniaProperty.Register<ArcButtonGroup, ArcButtonGroupOrientation>(nameof(Orientation));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ArcButtonGroup, bool>(nameof(IsOpen));

    private readonly List<ArcButton> _buttons = new();
    private readonly ArcMenuPresenter _menu;
    private StackPanel? _segments;
    private Panel? _highlightLayer;
    private ArcGlide? _highlight;
    private Canvas? _overlay;
    private ArcButton? _chevron;
    private bool _syncing;

    static ArcButtonGroup()
    {
        ItemsProperty.Changed.AddClassHandler<ArcButtonGroup>((group, _) => group.BuildSegments());
        VariantProperty.Changed.AddClassHandler<ArcButtonGroup>((group, _) => group.UpdateClasses());
        SizeProperty.Changed.AddClassHandler<ArcButtonGroup>((group, _) => group.BuildSegments());
        OrientationProperty.Changed.AddClassHandler<ArcButtonGroup>((group, _) => group.BuildSegments());
        MenuProperty.Changed.AddClassHandler<ArcButtonGroup>((group, _) => group.BuildSegments());
        IsOpenProperty.Changed.AddClassHandler<ArcButtonGroup>((group, _) => group.SyncMenu());
    }

    public ArcButtonGroup()
    {
        ClipToBounds = false;
        _menu = new ArcMenuPresenter(this);
        _menu.Closed += (_, _) => SetCurrentValue(IsOpenProperty, false);
        LayoutUpdated += (_, _) =>
        {
            if (IsOpen && !_menu.IsOpen)
            {
                SyncMenu();
            }

            _menu.Reposition();
        };
        UpdateClasses();
    }

    public IList<ArcButtonGroupItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name of the group, such as "Document actions".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Adds a trailing chevron segment that opens more actions.</summary>
    public ArcMenuDefinition? Menu
    {
        get => GetValue(MenuProperty);
        set => SetValue(MenuProperty, value);
    }

    public ArcButtonGroupVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public ArcButtonGroupSize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public ArcButtonGroupOrientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Whether the trailing menu is open; also lets a page show the open state.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _segments = e.NameScope.Find<StackPanel>("Segments");
        _highlightLayer = e.NameScope.Find<Panel>("HighlightLayer");
        _highlight = e.NameScope.Find<ArcGlide>("Highlight");
        _overlay = e.NameScope.Find<Canvas>("Overlay");
        _menu.Attach(_overlay);
        BuildSegments();
        SyncMenu();
    }

    private void UpdateClasses()
    {
        Classes.Set("solid", Variant == ArcButtonGroupVariant.Solid);
        Classes.Set("outline", Variant == ArcButtonGroupVariant.Outline);
        Classes.Set("vertical", Orientation == ArcButtonGroupOrientation.Vertical);
        if (!string.IsNullOrWhiteSpace(Label))
        {
            AutomationProperties.SetName(this, Label!);
        }
    }

    private void BuildSegments()
    {
        if (_segments is null)
        {
            return;
        }

        _segments.Children.Clear();
        _buttons.Clear();
        _chevron = null;

        var vertical = Orientation == ArcButtonGroupOrientation.Vertical;
        var compact = Size == ArcButtonGroupSize.Sm;
        _segments.Orientation = vertical ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;
        _segments.HorizontalAlignment = vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;

        var items = Items?.ToList() ?? new List<ArcButtonGroupItem>();
        foreach (var item in items)
        {
            if (_buttons.Count > 0)
            {
                _segments.Children.Add(Divider(vertical));
            }

            var button = BuildSegment(item, compact);
            _buttons.Add(button);
            _segments.Children.Add(button);
        }

        if (Menu is not null)
        {
            if (_buttons.Count > 0)
            {
                _segments.Children.Add(Divider(vertical));
            }

            _chevron = BuildChevron(compact);
            _segments.Children.Add(_chevron);
        }

        Dispatcher.UIThread.Post(HideHighlight, DispatcherPriority.Loaded);
    }

    private ArcButton BuildSegment(ArcButtonGroupItem item, bool compactSize)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = compactSize ? ArcButtonSize.Sm : ArcButtonSize.Md,
            Padding = new Thickness(compactSize ? 12 : 16, 0),
            MinHeight = compactSize ? 32 : 40,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            IsEnabled = !item.IsDisabled,
            Content = BuildSegmentContent(item)
        };
        button.CornerRadius = ArcActionVisuals.Concentric(ArcActionVisuals.Radius(ArcTokens.RadiusControl), 2);
        button.Classes.Add("group-segment");
        button.Classes.Set("solid", Variant == ArcButtonGroupVariant.Solid);
        button.Classes.Set("icon-only", item.IconOnly || item.Content is not null);
        ArcActionVisuals.SetName(button, item.Label);

        button.Click += (_, _) => item.Action?.Invoke();
        button.PointerEntered += (_, _) => GlideTo(button);
        button.GotFocus += (_, _) => GlideTo(button);
        return button;
    }

    private object BuildSegmentContent(ArcButtonGroupItem item)
    {
        if (item.Content is not null)
        {
            return item.Content;
        }

        if (string.IsNullOrWhiteSpace(item.Icon))
        {
            return new TextBlock
            {
                Text = item.Label,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
        }

        var panel = new StackPanel
        {
            Orientation = LayoutOrientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new ArcIcon
        {
            Icon = item.Icon,
            Size = 16,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "group-icon" }
        });

        if (!item.IconOnly)
        {
            panel.Children.Add(new TextBlock
            {
                Text = item.Label,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        return panel;
    }

    private ArcButton BuildChevron(bool compactSize)
    {
        var button = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = compactSize ? ArcButtonSize.Sm : ArcButtonSize.Md,
            Compact = true,
            MinHeight = compactSize ? 32 : 40,
            Width = compactSize ? 32 : 40,
            Content = new ArcIcon { Icon = "chevron-down", Size = 16 }
        };
        button.CornerRadius = ArcActionVisuals.Concentric(ArcActionVisuals.Radius(ArcTokens.RadiusControl), 2);
        button.Classes.Add("group-segment");
        button.Classes.Add("group-chevron");
        button.Classes.Set("solid", Variant == ArcButtonGroupVariant.Solid);
        ArcActionVisuals.SetName(button, Menu?.Label ?? "More actions");
        button.Click += (_, _) => SetCurrentValue(IsOpenProperty, !IsOpen);
        button.PointerEntered += (_, _) => GlideTo(button);
        button.GotFocus += (_, _) => GlideTo(button);
        button.KeyDown += (_, args) =>
        {
            if (args.Key is Key.Down)
            {
                SetCurrentValue(IsOpenProperty, true);
                args.Handled = true;
            }
        };
        return button;
    }

    private Border Divider(bool vertical)
    {
        var divider = new Border { Classes = { "group-divider" } };
        if (vertical)
        {
            divider.Height = 1;
            divider.Width = 20;
            divider.HorizontalAlignment = HorizontalAlignment.Center;
        }
        else
        {
            divider.Width = 1;
            divider.Height = 18;
            divider.VerticalAlignment = VerticalAlignment.Center;
        }

        divider.Classes.Set("solid", Variant == ArcButtonGroupVariant.Solid);
        return divider;
    }

    private void GlideTo(ArcButton button)
    {
        if (_highlight is null || _highlightLayer is null || _buttons.Count == 0)
        {
            return;
        }

        var origin = button.TranslatePoint(new Point(0, 0), _highlightLayer);
        if (origin is null)
        {
            return;
        }

        _highlight.ShowAt(new Rect(origin.Value, button.Bounds.Size));
        foreach (var other in _buttons)
        {
            other.Classes.Set("highlighted", ReferenceEquals(other, button));
        }
    }

    private Control ChevronIcon() => _chevron?.Content as Control ?? _chevron!;

    private void HideHighlight()
    {
        if (_highlight is null || IsFocused)
        {
            return;
        }

        _highlight.Hide();
        foreach (var button in _buttons)
        {
            button.Classes.Set("highlighted", false);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        HideHighlight();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _buttons.Count == 0)
        {
            return;
        }

        var vertical = Orientation == ArcButtonGroupOrientation.Vertical;
        var index = _buttons.FindIndex(button => button.IsFocused);
        if (index < 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Right when !vertical:
            case Key.Down when vertical:
                _buttons[(index + 1) % _buttons.Count].Focus();
                e.Handled = true;
                break;
            case Key.Left when !vertical:
            case Key.Up when vertical:
                _buttons[(index - 1 + _buttons.Count) % _buttons.Count].Focus();
                e.Handled = true;
                break;
            case Key.Home:
                _buttons[0].Focus();
                e.Handled = true;
                break;
            case Key.End:
                _buttons[^1].Focus();
                e.Handled = true;
                break;
        }
    }

    private void SyncMenu()
    {
        if (_syncing)
        {
            return;
        }

        _syncing = true;
        try
        {
            if (IsOpen)
            {
                if (Menu is not null && _overlay is not null && Bounds.Width > 0)
                {
                    _menu.Open(Menu.Items, Menu.Label, alignEnd: Orientation == ArcButtonGroupOrientation.Horizontal, focus: _chevron?.IsFocused == true);
                    ArcActionVisuals.Rotate(ChevronIcon(), true);
                }
            }
            else
            {
                _menu.Close();
                if (_chevron is not null)
                {
                    ArcActionVisuals.Rotate(ChevronIcon(), false);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _menu.Close();
        base.OnDetachedFromVisualTree(e);
    }
}
