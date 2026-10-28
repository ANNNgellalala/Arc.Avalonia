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
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Special.AIWidgets.Controls;

/// <summary>What fills a context window; the order here is the order the grid fills in.</summary>
public enum ArcContextCategory
{
    System,
    Tools,
    Files,
    Summary,
    Messages
}

/// <summary>One line in the context window's legend and its token cost.</summary>
public sealed class ArcContextItem
{
    public string Id { get; set; } = string.Empty;

    public ArcContextCategory Category { get; set; }

    public string Label { get; set; } = string.Empty;

    public int Tokens { get; set; }
}

/// <summary>A model whose window the grid divides into 200 cells.</summary>
public sealed class ArcContextModel
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Total context in tokens.</summary>
    public int Window { get; set; }

    /// <summary>Held back for the reply, shown as outlined cells at the end.</summary>
    public int Reserve { get; set; }
}

/// <summary>
/// A model's context window as a fixed budget of 200 cells that fills in reading order: system prompt, tools,
/// files and memory, a summary, then the conversation, with the output reserve held as outlined cells at the end
/// and a dashed rule at the compaction threshold. Send message and Attach file add tokens, crossing the threshold
/// folds the older messages into a short summary, and the legend lists every part with its tokens and percent.
///
/// Platform approximations: the cells are one drawn pass rather than animated DOM nodes, so new tokens pulse as a
/// short wave instead of a per-cell transition delay, and the model menu is a plain flyout rather than a dropdown.
/// </summary>
public class ArcContextWindow : Panel
{
    public static readonly StyledProperty<IEnumerable<ArcContextItem>?> ItemsProperty =
        AvaloniaProperty.Register<ArcContextWindow, IEnumerable<ArcContextItem>?>(nameof(Items));

    public static readonly StyledProperty<IEnumerable<ArcContextModel>?> ModelsProperty =
        AvaloniaProperty.Register<ArcContextWindow, IEnumerable<ArcContextModel>?>(nameof(Models));

    public static readonly StyledProperty<string?> ModelIdProperty =
        AvaloniaProperty.Register<ArcContextWindow, string?>(nameof(ModelId));

    /// <summary>Fraction of the window at which compaction runs on its own.</summary>
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<ArcContextWindow, double>(nameof(Threshold), 0.8);

    internal const int Columns = 20;
    internal const int Rows = 10;

    private readonly List<ArcContextItem> _items = new();
    private readonly List<ArcContextModel> _models = new();
    private readonly ArcContextCells _cells = new();
    private readonly StackPanel _legend = new();
    private TextBlock? _readoutBlock;
    private TextBlock? _detailBlock;
    private TextBlock? _statusBlock;
    private int _highlight = -1;
    private bool _highlightActive;
    private int _sendIndex;
    private int _attachIndex;
    private int _previousFilled;
    private double _flash = 1;
    private DispatcherTimer? _flashTimer;
    private Flyout? _flyout;

    static ArcContextWindow()
    {
        ModelIdProperty.Changed.AddClassHandler<ArcContextWindow>((window, _) => window.Rebuild());
        ThresholdProperty.Changed.AddClassHandler<ArcContextWindow>((window, _) => window.Rebuild());
    }

    public ArcContextWindow()
    {
        ClipToBounds = false;
        Rebuild();
    }

    public IEnumerable<ArcContextItem>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public IEnumerable<ArcContextModel>? Models
    {
        get => GetValue(ModelsProperty);
        set => SetValue(ModelsProperty, value);
    }

    public string? ModelId
    {
        get => GetValue(ModelIdProperty);
        set => SetValue(ModelIdProperty, value);
    }

    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    private ArcContextModel Current =>
        _models.FirstOrDefault(model => model.Id == ModelId)
        ?? _models.FirstOrDefault()
        ?? new ArcContextModel { Id = "default", Name = "Model", Window = 200_000, Reserve = 8_000 };

    private int CellTokens => Math.Max(1, Current.Window / (Columns * Rows));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Load();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _flashTimer?.Stop();
        _flashTimer = null;
    }

    private void Load()
    {
        if (Items is not null)
        {
            _items.Clear();
            _items.AddRange(Items);
        }

        if (Models is not null)
        {
            _models.Clear();
            _models.AddRange(Models);
        }

        Rebuild();
    }

    private void Rebuild()
    {
        if (_items.Count == 0 && Items is not null)
        {
            _items.AddRange(Items);
        }

        if (_models.Count == 0 && Models is not null)
        {
            _models.AddRange(Models);
        }

        Children.Clear();

        var model = Current;
        var cellTokens = CellTokens;
        var layout = BuildLayout(model, cellTokens);
        _cells.Cells = layout.Cells;
        _cells.ThresholdCell = (int)Math.Round(Threshold * (Columns * Rows));
        _cells.Highlight = _highlightActive ? _highlight : -1;
        _cells.Flash = _flash;
        _cells.FlashFrom = _previousFilled;
        _cells.FlashTo = layout.Filled;
        _previousFilled = layout.Filled;

        var root = new StackPanel { Spacing = 10 };

        // Header: the model menu and the token readout.
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        var modelButton = new ArcButton
        {
            Variant = ArcButtonVariant.Secondary,
            Size = ArcButtonSize.Sm,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new ArcAgentMark { Width = 14, Height = 14 },
                    new TextBlock { Text = $"{model.Name} · {Sv.Tokens(model.Window)}", FontSize = 13 },
                    Sv.Icon("chevron-down", 13, ArcTokens.TextMuted)
                }
            }
        };
        _flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft, Content = BuildModelMenu(model) };
        modelButton.Flyout = _flyout;
        AutomationProperties.SetName(modelButton, $"Model: {model.Name}, {Sv.Tokens(model.Window)} tokens. Press to switch model");
        header.Children.Add(modelButton);

        _readoutBlock = Sv.Numeric($"{Sv.Tokens(Used())} of {Sv.Tokens(model.Window)} · {Math.Round(Share() * 100)}%", "arc-label");
        Grid.SetColumn(_readoutBlock, 2);
        header.Children.Add(_readoutBlock);
        root.Children.Add(header);

        // Grid plus legend.
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 18 };
        var left = new StackPanel { Spacing = 6 };
        Sv.Detach(_cells);
        _cells.MaxWidth = 470;
        _cells.Height = 224;
        _cells.HorizontalAlignment = HorizontalAlignment.Left;
        left.Children.Add(_cells);

        var note = Sv.Caption($"Each cell is {Sv.Tokens(cellTokens)} tokens · the dashed rule at {Math.Round(Threshold * 100)}% is where compaction starts");
        left.Children.Add(note);

        _detailBlock = Sv.Caption(_highlightActive ? DetailFor(_highlight) : "Point at a cell or a legend row to see what it holds");
        _detailBlock.Paint(TextBlock.ForegroundProperty, _highlightActive ? ArcTokens.TextSecondary : ArcTokens.TextMuted);
        left.Children.Add(_detailBlock);
        body.Children.Add(left);

        Sv.Detach(_legend);
        BuildLegend(layout, model, cellTokens);
        Grid.SetColumn(_legend, 1);
        body.Children.Add(_legend);
        root.Children.Add(body);

        // Actions and the status line.
        var footer = new StackPanel { Spacing = 6 };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var send = new ArcButton { Variant = ArcButtonVariant.Primary, Size = ArcButtonSize.Sm, Content = "Send message" };
        send.Click += (_, _) => Send();
        var attach = new ArcButton { Variant = ArcButtonVariant.Secondary, Size = ArcButtonSize.Sm, Content = "Attach file" };
        attach.Click += (_, _) => Attach();
        var compact = new ArcButton { Variant = ArcButtonVariant.Ghost, Size = ArcButtonSize.Sm, Content = "Compact now" };
        compact.Click += (_, _) => Compact(automatic: false);
        actions.Children.Add(send);
        actions.Children.Add(attach);
        actions.Children.Add(compact);
        footer.Children.Add(actions);

        _statusBlock = Sv.Caption(_statusText);
        _statusBlock.Paint(TextBlock.ForegroundProperty, _statusTone);
        _statusBlock.IsVisible = !string.IsNullOrEmpty(_statusText);
        footer.Children.Add(_statusBlock);
        root.Children.Add(footer);

        Children.Add(Sv.Card(root, 16, ArcTokens.Surface));
        _cells.InvalidateVisual();
        UpdateAutomation();
    }

    private string _statusText = string.Empty;
    private string _statusTone = ArcTokens.TextSecondary;

    private void SetStatus(string text, string tone)
    {
        _statusText = text;
        _statusTone = tone;
        if (_statusBlock is not null)
        {
            _statusBlock.Text = text;
            _statusBlock.IsVisible = !string.IsNullOrEmpty(text);
            _statusBlock.Paint(TextBlock.ForegroundProperty, tone);
        }
    }

    private Control BuildModelMenu(ArcContextModel selected)
    {
        var panel = new StackPanel { Spacing = 2, MinWidth = 240 };
        foreach (var model in _models)
        {
            var text = new StackPanel { Spacing = 1 };
            var name = new TextBlock { Text = model.Name, FontSize = Sv.Size(ArcTokens.TextSm), FontWeight = FontWeight.Medium };
            text.Children.Add(name);
            text.Children.Add(Sv.Caption($"{Sv.Tokens(model.Window)} window · {Sv.Tokens(model.Reserve)} reserved for the reply"));

            var row = new Border
            {
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 7, 10, 7),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = text
            };
            if (model.Id == selected.Id)
            {
                row.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
                name.Paint(TextBlock.ForegroundProperty, ArcTokens.Accent);
            }

            var id = model.Id;
            row.PointerPressed += (_, _) =>
            {
                _flyout?.Hide();
                SetCurrentValue(ModelIdProperty, id);
                SetStatus($"Switched to {model.Name} · {Sv.Tokens(model.Window)} window", ArcTokens.TextSecondary);
                Rebuild();
            };
            panel.Children.Add(row);
        }

        return panel;
    }

    private void UpdateAutomation()
    {
        var parts = _items
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key} {Sv.Tokens(group.Sum(item => item.Tokens))} tokens");
        AutomationProperties.SetName(this, $"Context window, {Math.Round(Share() * 100)} percent used");
        AutomationProperties.SetHelpText(this,
            $"{Current.Name}, {Sv.Tokens(Used())} of {Sv.Tokens(Current.Window)} tokens. {string.Join(", ", parts)}. Compaction at {Math.Round(Threshold * 100)} percent.");
    }

    private int Used() => _items.Sum(item => Math.Max(0, item.Tokens));

    private double Share() => Current.Window <= 0 ? 0 : (double)Used() / Current.Window;

    private sealed class GridLayout
    {
        public int[] Cells { get; init; } = Array.Empty<int>();

        public int Filled { get; init; }

        public Dictionary<ArcContextCategory, (int Cells, int Tokens)> Categories { get; } = new();
    }

    private GridLayout BuildLayout(ArcContextModel model, int cellTokens)
    {
        var total = Columns * Rows;
        var cells = Enumerable.Repeat(-1, total).ToArray();
        var reserveCells = Math.Clamp((int)Math.Round((double)model.Reserve / cellTokens), 0, total / 3);
        var limit = total - reserveCells;

        var cursor = 0;
        foreach (var category in CategoriesInOrder)
        {
            var tokens = _items.Where(item => item.Category == category).Sum(item => Math.Max(0, item.Tokens));
            var count = (int)Math.Round((double)tokens / cellTokens);
            var placed = Math.Min(count, Math.Max(0, limit - cursor));
            for (var i = 0; i < placed; i++)
            {
                cells[cursor++] = (int)category;
            }

            // The reserve never gives up its outlined cells, so the fills stop at the limit.
            cursor = Math.Min(cursor, limit);
        }

        for (var i = limit; i < total; i++)
        {
            cells[i] = -2;
        }

        var layout = new GridLayout { Cells = cells, Filled = cursor };
        foreach (var category in CategoriesInOrder)
        {
            var placed = cells.Count(cell => cell == (int)category);
            var tokens = _items.Where(item => item.Category == category).Sum(item => Math.Max(0, item.Tokens));
            layout.Categories[category] = (placed, tokens);
        }

        return layout;
    }

    private static readonly ArcContextCategory[] CategoriesInOrder =
    {
        ArcContextCategory.System,
        ArcContextCategory.Tools,
        ArcContextCategory.Files,
        ArcContextCategory.Summary,
        ArcContextCategory.Messages
    };

    private void BuildLegend(GridLayout layout, ArcContextModel model, int cellTokens)
    {
        _legend.Children.Clear();
        _legend.Width = 252;
        var heading = Sv.Caption("What fills the window");
        heading.Margin = new Thickness(6, 0, 0, 2);
        _legend.Children.Add(heading);

        foreach (var category in CategoriesInOrder)
        {
            var (cells, tokens) = layout.Categories.TryGetValue(category, out var entry) ? entry : (0, 0);
            var percent = model.Window <= 0 ? 0 : Math.Round((double)tokens / model.Window * 100);
            _legend.Children.Add(LegendRow(
                ArcContextCells.CategoryToken(category),
                Label(category),
                $"{Sv.Tokens(tokens)} · {percent:0}%",
                (int)category,
                $"{cells} cells"));
        }

        var reservePercent = model.Window <= 0 ? 0 : Math.Round((double)model.Reserve / model.Window * 100);
        _legend.Children.Add(LegendRow(null, "Output reserve", $"{Sv.Tokens(model.Reserve)} · {reservePercent:0}%", -2, "outlined cells"));

        var freeTokens = Math.Max(0, model.Window - Used() - model.Reserve);
        var freeCells = Math.Max(0, Columns * Rows - (int)Math.Round((double)Used() / cellTokens) - (int)Math.Round((double)model.Reserve / cellTokens));
        _legend.Children.Add(LegendRow(null, "Free", $"{Sv.Tokens(freeTokens)} · {Math.Round((double)freeTokens / model.Window * 100):0}%", -1, $"{freeCells} cells"));
    }

    private static string Label(ArcContextCategory category) => category switch
    {
        ArcContextCategory.System => "System prompt",
        ArcContextCategory.Tools => "Tools",
        ArcContextCategory.Files => "Files and memory",
        ArcContextCategory.Summary => "Summary",
        _ => "Messages"
    };

    private Control LegendRow(string? token, string label, string value, int category, string note)
    {
        var dot = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(3), VerticalAlignment = VerticalAlignment.Center };
        if (token is null)
        {
            dot.BorderThickness = new Thickness(1);
            dot.Paint(Border.BorderBrushProperty, ArcTokens.BorderStrong);
        }
        else
        {
            dot.Paint(Border.BackgroundProperty, token);
        }

        var name = new TextBlock { Text = label, FontSize = Sv.Size(ArcTokens.TextXs), VerticalAlignment = VerticalAlignment.Center };
        if (_highlightActive && _highlight != category)
        {
            name.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        }
        else
        {
            name.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        }

        var amount = Sv.Numeric(value, "arc-caption");
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(dot);
        name.Margin = new Thickness(7, 0, 6, 0);
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);
        Grid.SetColumn(amount, 2);
        grid.Children.Add(amount);

        var row = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 3, 6, 3),
            Child = grid,
            Background = _highlightActive && _highlight == category ? Sv.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand)
        };
        row.PointerEntered += (_, _) => SetHighlight(category);
        row.PointerExited += (_, _) => SetHighlight(-1);
        AutomationProperties.SetName(row, $"{label}, {value} of the window, {note}");
        return row;
    }

    private void SetHighlight(int category)
    {
        if (_highlight == category)
        {
            return;
        }

        _highlight = category;
        _highlightActive = true;
        _cells.Highlight = category;
        _cells.InvalidateVisual();
        if (_detailBlock is not null)
        {
            _detailBlock.Text = DetailFor(category);
            _detailBlock.Paint(TextBlock.ForegroundProperty, category == -1 && _highlight != -1 ? ArcTokens.TextMuted : ArcTokens.TextSecondary);
        }
    }

    private static string DetailFor(int category) => category switch
    {
        (int)ArcContextCategory.System => "The system prompt and product instructions, counted first.",
        (int)ArcContextCategory.Tools => "Tool and MCP definitions the model can call in this turn.",
        (int)ArcContextCategory.Files => "Attached files, retrieved memory and their extracted text.",
        (int)ArcContextCategory.Summary => "A short summary standing in for earlier turns after compaction.",
        (int)ArcContextCategory.Messages => "The conversation as it stands, oldest first.",
        -2 => "Held back for the reply, so the model never fills the window completely.",
        -1 => "Space that is still free.",
        _ => "Point at a cell or a legend row to see what it holds"
    };

    private void Send()
    {
        var tokens = _sendIndex switch
        {
            0 => 45_000,
            1 => 60_000,
            _ => 30_000
        };
        _sendIndex++;
        _items.Add(new ArcContextItem { Id = "turn-" + _sendIndex, Category = ArcContextCategory.Messages, Label = $"Turn {_sendIndex}", Tokens = tokens });
        SetCurrentValue(ItemsProperty, _items.ToList());
        SetStatus($"Sent a message · {Sv.Tokens(tokens)} tokens added", ArcTokens.TextSecondary);
        Flash();
        if (Share() >= Threshold)
        {
            Compact(automatic: true);
        }
        else
        {
            Rebuild();
        }
    }

    private void Attach()
    {
        _attachIndex++;
        var tokens = 18_000 + _attachIndex * 4_000;
        _items.Add(new ArcContextItem { Id = "file-" + _attachIndex, Category = ArcContextCategory.Files, Label = $"attached-{_attachIndex}.pdf", Tokens = tokens });
        SetCurrentValue(ItemsProperty, _items.ToList());
        SetStatus($"Attached a file · {Sv.Tokens(tokens)} tokens added", ArcTokens.TextSecondary);
        Flash();
        if (Share() >= Threshold)
        {
            Compact(automatic: true);
        }
        else
        {
            Rebuild();
        }
    }

    private void Compact(bool automatic)
    {
        var messages = _items.Where(item => item.Category == ArcContextCategory.Messages).ToList();
        if (messages.Count == 0)
        {
            SetStatus("Nothing to fold yet · messages are already empty", ArcTokens.TextMuted);
            Rebuild();
            return;
        }

        var foldCount = Math.Max(1, (int)Math.Ceiling(messages.Count * 0.6));
        var folded = messages.Take(foldCount).ToList();
        var foldedTokens = folded.Sum(item => item.Tokens);
        var summaryTokens = Math.Clamp((int)(foldedTokens * 0.06), 4_000, 24_000);

        foreach (var item in folded)
        {
            _items.Remove(item);
        }

        var existing = _items.FirstOrDefault(item => item.Category == ArcContextCategory.Summary);
        if (existing is not null)
        {
            existing.Tokens = summaryTokens;
            existing.Label = $"Summary of {foldCount} earlier turns";
        }
        else
        {
            _items.Insert(0, new ArcContextItem { Id = "summary", Category = ArcContextCategory.Summary, Label = $"Summary of {foldCount} earlier turns", Tokens = summaryTokens });
        }

        SetCurrentValue(ItemsProperty, _items.ToList());
        SetStatus(automatic
            ? $"Compacted automatically · {foldCount} turns folded into {Sv.Tokens(summaryTokens)}"
            : $"Compacted · {foldCount} turns folded into {Sv.Tokens(summaryTokens)}", ArcTokens.Accent);
        Flash();
        Rebuild();
    }

    private void Flash()
    {
        _flash = ArcThemeManager.ReducedMotion ? 1 : 0;
        if (ArcThemeManager.ReducedMotion)
        {
            return;
        }

        _flashTimer?.Stop();
        var started = DateTime.UtcNow;
        _flashTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) =>
        {
            _flash = Sv.Clamp01((DateTime.UtcNow - started).TotalMilliseconds / 420);
            _cells.Flash = _flash;
            _cells.InvalidateVisual();
            if (_flash >= 1)
            {
                _flashTimer?.Stop();
            }
        });
        _flashTimer.Start();
    }
}

/// <summary>The 200 cell grid: category fills, free space, the outlined reserve and the dashed threshold rule.</summary>
public sealed class ArcContextCells : Control
{
    private IReadOnlyList<int> _cells = Array.Empty<int>();

    public IReadOnlyList<int> Cells
    {
        get => _cells;
        set => _cells = value;
    }

    public int ThresholdCell { get; set; }

    public int Highlight { get; set; } = -1;

    public double Flash { get; set; } = 1;

    public int FlashFrom { get; set; } = -1;

    public int FlashTo { get; set; } = -1;

    public static string CategoryToken(ArcContextCategory category) => category switch
    {
        ArcContextCategory.System => ArcTokens.Series2,
        ArcContextCategory.Tools => ArcTokens.Series3,
        ArcContextCategory.Files => ArcTokens.Series4,
        ArcContextCategory.Summary => ArcTokens.TextMuted,
        _ => ArcTokens.Series1
    };

    public ArcContextCells()
    {
        Height = 224;
        ClipToBounds = false;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 520 : availableSize.Width;
        return new Size(width, 224);
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        const double gap = 3;
        var cellWidth = Math.Min(25, (Bounds.Width - gap * (ArcContextWindow.Columns - 1)) / ArcContextWindow.Columns);
        var cellHeight = Math.Min(cellWidth, (Bounds.Height - gap * (ArcContextWindow.Rows - 1)) / ArcContextWindow.Rows);
        var usedWidth = cellWidth * ArcContextWindow.Columns + gap * (ArcContextWindow.Columns - 1);
        var usedHeight = cellHeight * ArcContextWindow.Rows + gap * (ArcContextWindow.Rows - 1);
        var offsetX = (Bounds.Width - usedWidth) / 2;
        var offsetY = (Bounds.Height - usedHeight) / 2;
        var radius = Math.Max(2, Math.Min(4, cellWidth * 0.24));

        for (var index = 0; index < ArcContextWindow.Columns * ArcContextWindow.Rows; index++)
        {
            var column = index % ArcContextWindow.Columns;
            var row = index / ArcContextWindow.Columns;
            var rect = new Rect(
                offsetX + column * (cellWidth + gap),
                offsetY + row * (cellHeight + gap),
                cellWidth,
                cellHeight);
            var category = index < _cells.Count ? _cells[index] : -1;

            if (category == -2)
            {
                context.DrawRectangle(null, new Pen(Sv.AlphaBrush(ArcTokens.BorderStrong, 0.85), 1), rect.Deflate(0.5), radius, radius);
                continue;
            }

            if (category < 0)
            {
                context.DrawRectangle(Sv.AlphaBrush(ArcTokens.ControlTrack, 0.5), null, rect, radius, radius);
            }
            else
            {
                var alpha = Highlight < 0 || Highlight == category ? 0.92 : 0.28;
                context.DrawRectangle(Sv.AlphaBrush(CategoryToken((ArcContextCategory)category), alpha), null, rect, radius, radius);
                if (Highlight >= 0 && Highlight == category)
                {
                    context.DrawRectangle(null, new Pen(Sv.AlphaBrush(ArcTokens.Foreground, 0.24), 1), rect.Deflate(0.5), radius, radius);
                }
            }

            if (Flash < 1 && index >= FlashFrom && index < FlashTo)
            {
                context.DrawRectangle(null, new Pen(Sv.AlphaBrush(ArcTokens.Accent, (1 - Flash) * 0.6), 1.2), rect.Deflate(0.6), radius, radius);
            }
        }

        // Threshold row: the grid row where compaction starts, so the rule reads across the fill order.
        if (ThresholdCell > 0 && ThresholdCell < ArcContextWindow.Columns * ArcContextWindow.Rows)
        {
            var row = ThresholdCell / ArcContextWindow.Columns;
            var y = offsetY + row * (cellHeight + gap) - gap / 2;
            var pen = new Pen(Sv.AlphaBrush(ArcTokens.TextMuted, 0.8), 1, new DashStyle(new double[] { 3, 3 }, 0));
            context.DrawLine(pen, new Point(offsetX - 4, y), new Point(offsetX + usedWidth + 4, y));
            var label = SvDraw.Text($"compaction at {Math.Round(ThresholdCell / 200.0 * 100)}%", 9.5, Sv.Brush(ArcTokens.TextMuted));
            SvDraw.DrawText(context, label, offsetX + usedWidth - label.Width, y - 12);
        }
    }
}
