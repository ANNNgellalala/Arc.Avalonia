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
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Feedback.Controls;

/// <summary>One node in an <see cref="ArcTreeView"/>.</summary>
public sealed class ArcTreeNode
{
    public string Id { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public List<ArcTreeNode> Children { get; set; } = new();

    /// <summary>Lucide icon name; folders and files get sensible defaults.</summary>
    public string? Icon { get; set; }
}

/// <summary>
/// A hierarchy of folders and files with chevrons, 18 px indentation, a gliding selection, and
/// full tree keyboard navigation (arrows, Home/End, Enter/Space).
/// </summary>
public class ArcTreeView : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<ArcTreeNode>?> NodesProperty =
        AvaloniaProperty.Register<ArcTreeView, IEnumerable<ArcTreeNode>?>(nameof(Nodes));

    public static readonly StyledProperty<IEnumerable<string>?> DefaultExpandedIdsProperty =
        AvaloniaProperty.Register<ArcTreeView, IEnumerable<string>?>(nameof(DefaultExpandedIds));

    public static readonly StyledProperty<IEnumerable<string>?> ExpandedIdsProperty =
        AvaloniaProperty.Register<ArcTreeView, IEnumerable<string>?>(nameof(ExpandedIds));

    public static readonly StyledProperty<string?> SelectedIdProperty =
        AvaloniaProperty.Register<ArcTreeView, string?>(nameof(SelectedId));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcTreeView, string>(nameof(Label), "File tree");

    private sealed record Row(ArcTreeNode Node, int Depth);

    private readonly HashSet<string> _expanded = new(StringComparer.Ordinal);
    private readonly List<Row> _rows = new();
    private readonly List<Border> _frames = new();
    private Panel? _host;
    private int _focusIndex;
    private bool _keyboardFocus;

    static ArcTreeView()
    {
        NodesProperty.Changed.AddClassHandler<ArcTreeView>((tree, _) => tree.ResetExpansion());
        DefaultExpandedIdsProperty.Changed.AddClassHandler<ArcTreeView>((tree, _) => tree.ResetExpansion());
        ExpandedIdsProperty.Changed.AddClassHandler<ArcTreeView>((tree, _) => tree.ApplyControlledExpansion());
        SelectedIdProperty.Changed.AddClassHandler<ArcTreeView>((tree, _) => tree.Rebuild());
        LabelProperty.Changed.AddClassHandler<ArcTreeView>((tree, _) => AutomationProperties.SetName(tree, tree.Label));
    }

    public ArcTreeView()
    {
        Focusable = true;
    }

    public IEnumerable<ArcTreeNode>? Nodes
    {
        get => GetValue(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    public IEnumerable<string>? DefaultExpandedIds
    {
        get => GetValue(DefaultExpandedIdsProperty);
        set => SetValue(DefaultExpandedIdsProperty, value);
    }

    /// <summary>Controlled open folders; when set, overrides the internal set.</summary>
    public IEnumerable<string>? ExpandedIds
    {
        get => GetValue(ExpandedIdsProperty);
        set => SetValue(ExpandedIdsProperty, value);
    }

    public string? SelectedId
    {
        get => GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    /// <summary>Accessible name for the tree.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public event EventHandler<ArcTreeNode>? Selected;

    public event EventHandler<IReadOnlyList<string>>? ExpandedChanged;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        ResetExpansion();
    }

    private void ResetExpansion()
    {
        _expanded.Clear();
        foreach (var id in DefaultExpandedIds ?? Enumerable.Empty<string>())
        {
            _expanded.Add(id);
        }

        ApplyControlledExpansion();
    }

    private void ApplyControlledExpansion()
    {
        if (ExpandedIds is not null)
        {
            _expanded.Clear();
            foreach (var id in ExpandedIds)
            {
                _expanded.Add(id);
            }
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _rows.Clear();
        _frames.Clear();

        var stack = new StackPanel();
        foreach (var node in Nodes ?? Enumerable.Empty<ArcTreeNode>())
        {
            AddNode(node, 0, stack);
        }

        _host.Children.Add(stack);
        _focusIndex = Math.Clamp(_focusIndex, 0, Math.Max(0, _rows.Count - 1));
        UpdateFocusVisuals();
        AutomationProperties.SetName(this, Label);
    }

    private void AddNode(ArcTreeNode node, int depth, StackPanel stack)
    {
        _rows.Add(new Row(node, depth));
        stack.Children.Add(BuildRow(node, depth));

        if (node.Children.Count > 0 && _expanded.Contains(node.Id))
        {
            foreach (var child in node.Children)
            {
                AddNode(child, depth + 1, stack);
            }
        }
    }

    private Control BuildRow(ArcTreeNode node, int depth)
    {
        var branch = node.Children.Count > 0;
        var expanded = _expanded.Contains(node.Id);
        var selected = node.Id == SelectedId;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition(20, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(22, GridUnitType.Pixel));
        grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        var chevron = new ArcIconButton
        {
            Icon = "chevron-right",
            IconSize = 12,
            BoxSize = 20,
            Muted = true,
            Label = expanded ? $"Collapse {node.Label}" : $"Expand {node.Label}",
            IsVisible = branch,
            Focusable = false,
            IsTabStop = false,
            IsHitTestVisible = false,
            RenderTransform = expanded ? new RotateTransform(90) : null
        };
        grid.Children.Add(chevron);

        var icon = ArcUi.Icon(node.Icon ?? (branch ? "folder" : "file"), 15, selected ? ArcTokens.AccentStrong : ArcTokens.TextSecondary);
        Grid.SetColumn(icon, 1);
        grid.Children.Add(icon);

        var label = ArcUi.Text(node.Label, "arc-body-sm");
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.Paint(TextBlock.ForegroundProperty, selected ? ArcTokens.Foreground : ArcTokens.TextSecondary);
        Grid.SetColumn(label, 2);
        grid.Children.Add(label);

        var frame = new Border
        {
            Height = 36,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 0, 8, 0),
            Margin = new Thickness(depth * 18, 0, 0, 0),
            Background = selected ? ArcResources.Brush(ArcTokens.AccentSubtle) : Brushes.Transparent,
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = grid
        };

        var index = _rows.Count - 1;
        _frames.Add(frame);

        frame.PointerPressed += (_, args) =>
        {
            _keyboardFocus = false;
            _focusIndex = index;
            Focus();
            Select(node);
            if (branch)
            {
                Toggle(node);
            }

            UpdateFocusVisuals();
            args.Handled = true;
        };
        frame.PointerEntered += (_, _) =>
        {
            if (!selected)
            {
                frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
            }
        };
        frame.PointerExited += (_, _) =>
        {
            if (SelectedId != node.Id)
            {
                frame.Background = Brushes.Transparent;
            }
        };

        var kind = branch ? $"folder, {node.Children.Count} items" : "file";
        AutomationProperties.SetName(frame, $"{node.Label}, {kind}{(selected ? ", selected" : string.Empty)}");
        return frame;
    }

    private void Select(ArcTreeNode node)
    {
        SetCurrentValue(SelectedIdProperty, node.Id);
        Selected?.Invoke(this, node);
    }

    private void Toggle(ArcTreeNode node)
    {
        if (node.Children.Count == 0)
        {
            return;
        }

        if (!_expanded.Remove(node.Id))
        {
            _expanded.Add(node.Id);
        }

        RefreshExpansion();
    }

    private void RefreshExpansion()
    {
        if (ExpandedIds is not null)
        {
            SetCurrentValue(ExpandedIdsProperty, _expanded.ToList());
        }

        ExpandedChanged?.Invoke(this, _expanded.ToList());
        Rebuild();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_rows.Count == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                MoveFocus(1);
                e.Handled = true;
                break;
            case Key.Up:
                MoveFocus(-1);
                e.Handled = true;
                break;
            case Key.Home:
                _focusIndex = 0;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                e.Handled = true;
                break;
            case Key.End:
                _focusIndex = _rows.Count - 1;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                e.Handled = true;
                break;
            case Key.Right:
                OpenOrEnter();
                e.Handled = true;
                break;
            case Key.Left:
                CloseOrParent();
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                var node = _rows[_focusIndex].Node;
                Select(node);
                Toggle(node);
                e.Handled = true;
                break;
        }
    }

    private void MoveFocus(int delta)
    {
        _focusIndex = Math.Clamp(_focusIndex + delta, 0, _rows.Count - 1);
        _keyboardFocus = true;
        UpdateFocusVisuals();
        _frames[_focusIndex].BringIntoView();
    }

    private void OpenOrEnter()
    {
        var node = _rows[_focusIndex].Node;
        if (node.Children.Count == 0)
        {
            return;
        }

        if (!_expanded.Contains(node.Id))
        {
            _expanded.Add(node.Id);
            RefreshExpansion();
            return;
        }

        if (_focusIndex + 1 < _rows.Count)
        {
            _focusIndex++;
            _keyboardFocus = true;
            UpdateFocusVisuals();
        }
    }

    private void CloseOrParent()
    {
        var node = _rows[_focusIndex].Node;
        if (node.Children.Count > 0 && _expanded.Contains(node.Id))
        {
            _expanded.Remove(node.Id);
            RefreshExpansion();
            return;
        }

        for (var i = _focusIndex - 1; i >= 0; i--)
        {
            if (_rows[i].Depth < _rows[_focusIndex].Depth)
            {
                _focusIndex = i;
                _keyboardFocus = true;
                UpdateFocusVisuals();
                return;
            }
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        UpdateFocusVisuals();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        UpdateFocusVisuals();
    }

    private void UpdateFocusVisuals()
    {
        for (var i = 0; i < _frames.Count; i++)
        {
            if (IsFocused && _keyboardFocus && i == _focusIndex)
            {
                _frames[i].Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
            }
            else
            {
                _frames[i].BorderBrush = Brushes.Transparent;
            }
        }
    }
}
