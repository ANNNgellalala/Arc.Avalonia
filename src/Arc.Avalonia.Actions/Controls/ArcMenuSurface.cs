using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Actions.Controls;

/// <summary>
/// The floating action list shared by the dropdown menu, the context menu, the split button and the
/// button group. Rows are native buttons with a single highlight gliding between them; the surface is a
/// panel-radius card with the floating shadow. It hosts itself inside its owner's overlay canvas, so it
/// renders in the gallery (and in screenshots) without a popup window.
/// </summary>
public sealed class ArcMenuSurface : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcMenuItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcMenuSurface, IEnumerable<ArcMenuItem>?>(nameof(Items));

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ArcMenuSurface, string?>(nameof(Label));

    private readonly List<ArcButton> _rows = new();
    private Panel? _host;
    private StackPanel? _items;
    private ArcGlide? _highlight;
    private int _active = -1;
    private bool _closed;
    private IDisposable? _dismiss;

    static ArcMenuSurface()
    {
        FocusableProperty.OverrideDefaultValue<ArcMenuSurface>(true);
        ItemsProperty.Changed.AddClassHandler<ArcMenuSurface>((menu, _) => menu.BuildRows());
    }

    public ArcMenuSurface()
    {
        AutomationProperties.SetName(this, "Menu");
    }

    /// <summary>Raised when the menu should be removed from its overlay, after Escape, an outside press or a choice.</summary>
    public event EventHandler? Closed;

    public IEnumerable<ArcMenuItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>Accessible name of the menu.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set
        {
            SetValue(LabelProperty, value);
            if (!string.IsNullOrWhiteSpace(value))
            {
                AutomationProperties.SetName(this, value!);
            }
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("MenuHost");
        _items = e.NameScope.Find<StackPanel>("Items");
        _highlight = e.NameScope.Find<ArcGlide>("Highlight");
        BuildRows();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dismiss?.Dispose();
        _dismiss = ArcDismiss.Attach(this, Close);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _dismiss?.Dispose();
        _dismiss = null;
        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Opens the menu: builds the rows, focuses the surface and highlights the first enabled item.</summary>
    public void Open(bool focus = true)
    {
        BuildRows();
        SetActive(FirstEnabled(), focus);
    }

    /// <summary>Closes the menu once and reports it to the host.</summary>
    public void Close()
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void BuildRows()
    {
        if (_items is null)
        {
            return;
        }

        _items.Children.Clear();
        _rows.Clear();

        var items = Items?.ToList() ?? new List<ArcMenuItem>();
        foreach (var item in items)
        {
            if (item.SeparatorBefore && _items.Children.Count > 0)
            {
                _items.Children.Add(new Border { Classes = { "arc-menu-separator" } });
            }

            var row = BuildRow(item);
            _rows.Add(row);
            _items.Children.Add(row);
        }

        if (_active >= _rows.Count)
        {
            _active = -1;
        }
    }

    private ArcButton BuildRow(ArcMenuItem item)
    {
        var row = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 0),
            MinHeight = 36,
            IsEnabled = !item.IsDisabled,
            Focusable = false,
            IsTabStop = false
        };

        row.Classes.Add("menu-row");
        row.Classes.Set("destructive", item.IsDestructive);
        row.Classes.Set("checked", item.IsChecked);
        ArcActionVisuals.SetName(row, item.Label);
        row.Content = BuildRowContent(item, row);

        var index = _rows.Count;
        row.Click += (_, _) => Choose(index);
        row.PointerEntered += (_, _) => SetActive(index, focus: false);
        return row;
    }

    private static Control BuildRowContent(ArcMenuItem item, ArcButton row)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var column = 0;
        if (!string.IsNullOrWhiteSpace(item.Icon) || item.IsChecked)
        {
            var icon = ArcActionVisuals.InheritIcon(item.IsChecked ? "check" : item.Icon, row);
            icon.Margin = new Thickness(2, 0, 10, 0);
            icon.Classes.Add("arc-menu-icon");
            Grid.SetColumn(icon, column++);
            grid.Children.Add(icon);
        }

        var label = ArcActionVisuals.InheritText(item.Label ?? string.Empty, row);
        Flex(label);
        Grid.SetColumn(label, column++);
        grid.Children.Add(label);

        if (!string.IsNullOrWhiteSpace(item.Shortcut))
        {
            var shortcut = new TextBlock
            {
                Text = item.Shortcut,
                Classes = { "arc-caption" },
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 2, 0)
            };
            Grid.SetColumn(shortcut, column);
            grid.Children.Add(shortcut);
        }

        return grid;
    }

    private static void Flex(Control control)
    {
        control.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    private int FirstEnabled()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].IsEnabled)
            {
                return i;
            }
        }

        return -1;
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= _rows.Count || !_rows[index].IsEnabled)
        {
            return;
        }

        var item = Items?.ElementAtOrDefault(index);
        Close();
        item?.Action?.Invoke();
    }

    private void SetActive(int index, bool focus)
    {
        _active = index;
        if (_highlight is null || _host is null)
        {
            return;
        }

        if (index < 0 || index >= _rows.Count)
        {
            _highlight.Hide();
            return;
        }

        var row = _rows[index];
        var origin = row.TranslatePoint(new Point(0, 0), _host);
        if (origin is null)
        {
            return;
        }

        _highlight.ShowAt(new Rect(origin.Value, row.Bounds.Size));
        if (focus)
        {
            Focus();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Down:
                SetActive(ArcKeyboard.Next(_rows, _active, 1, row => row.IsEnabled), focus: false);
                e.Handled = true;
                break;
            case Key.Up:
                SetActive(ArcKeyboard.Next(_rows, _active, -1, row => row.IsEnabled), focus: false);
                e.Handled = true;
                break;
            case Key.Home:
                SetActive(ArcKeyboard.Edge(_rows, -1, row => row.IsEnabled), focus: false);
                e.Handled = true;
                break;
            case Key.End:
                SetActive(ArcKeyboard.Edge(_rows, 1, row => row.IsEnabled), focus: false);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                Choose(_active);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Tab:
                Close();
                break;
        }
    }
}
