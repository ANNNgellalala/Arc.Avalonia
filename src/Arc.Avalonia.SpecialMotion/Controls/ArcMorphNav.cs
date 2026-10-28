using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.SpecialMotion.Controls;

/// <summary>
/// A site navigation bar made of one continuous surface: a highlight glides between items, an item with links
/// grows the bar into a panel, and search grows out of the bar itself. Compact tightens the bar for a scrolled
/// page. Reduced motion drops travel, scale and blur and keeps short fades.
/// </summary>
public class ArcMorphNav : ArcMotionCanvas
{
    private sealed record Link(string Label, string Description, string Icon);
    private sealed record NavItem(string Label, Link[]? Links);
    private sealed record SearchEntry(string Label, string Group);

    private static readonly NavItem[] Items =
    {
        new("Product", new[]
        {
            new Link("Components", "Motion-first building blocks", "grid-3x3"),
            new Link("Blocks", "Full sections, ready to compose", "layout-dashboard"),
            new Link("Charts", "Data people can actually read", "chart-line"),
            new Link("Templates", "Start from a finished surface", "file")
        }),
        new("Components", null),
        new("Pricing", null),
        new("Docs", null)
    };

    private static readonly SearchEntry[] SearchIndex =
    {
        new("Dialog", "Components"),
        new("Dock", "Components"),
        new("Action morph", "Components"),
        new("Share sheet", "Special components"),
        new("Site header", "Blocks"),
        new("Command palette", "Blocks"),
        new("Pricing table", "Blocks"),
        new("Date picker", "Components")
    };

    private const int IdBrand = 1;
    private const int IdItem = 10;
    private const int IdSearch = 20;
    private const int IdAction = 21;
    private const int IdLink = 30;
    private const int IdFooter = 40;
    private const int IdResult = 50;
    private const int IdField = 60;

    private int _current; // index into Items, 0 = Product
    private int _open = 0; // panel open for this item, -1 closed
    private double _reveal = 1;
    private double _panelReveal = 1;
    private bool _search;
    private string _query = string.Empty;
    private int _result;
    private int _focus = -1;
    private bool _compact;
    private double _highlightX = double.NaN;
    private double _highlightW;
    private int _highlightIndex = -1;

    public ArcMorphNav()
    {
        Sm.Name(this, "Main", "Site navigation. Left and right arrows move between items, Down opens a panel, Escape closes it. Press / to search.");
    }

    /// <summary>Tightens the bar, as if the page had scrolled.</summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            _compact = value;
            InvalidateVisual();
        }
    }

    /// <summary>Opens the search state from a page control.</summary>
    public void OpenSearch()
    {
        _search = true;
        _open = -1;
        _query = string.Empty;
        _result = 0;
        Animate(240, ArcMotion.EaseStandard, progress =>
        {
            _reveal = progress;
            InvalidateVisual();
        });
        Focus();
    }

    protected override double DefaultWidth => 1000;

    protected override double DefaultHeight => 380;

    private Rect Bar(Size size)
    {
        var width = Math.Min(size.Width - 80, 900);
        var height = _compact ? 52.0 : 64.0;
        return new Rect(size.Width / 2 - width / 2, 22, width, height);
    }

    private (double X, double W) ItemAt(Size size, int index)
    {
        var bar = Bar(size);
        var x = bar.X + (index == -1 ? 0 : 156);
        for (var slot = 0; slot < Items.Length; slot++)
        {
            var label = Sm.MeasureWidth(Items[slot].Label, _compact ? 13 : 14, FontWeight.Medium);
            var width = label + 26;
            if (slot == index)
            {
                return (x, width);
            }

            x += width + 2;
        }

        return (x, 0);
    }

    private Rect PanelRect(Size size)
    {
        var bar = Bar(size);
        var anchor = ItemAt(size, _open < 0 ? 0 : _open);
        var width = _search ? 420.0 : 520.0;
        var x = Math.Clamp(anchor.X - 40, bar.X, bar.Right - width);
        var height = _search ? 296.0 : 214.0;
        return new Rect(x, bar.Bottom + 10, width, height);
    }

    protected override void OnRender(DrawingContext context, Size size)
    {
        if (size.Width < 640 || size.Height < 260)
        {
            return;
        }

        DrawPage(context, size);
        var bar = Bar(size);
        var radius = bar.Height / 2;

        Sm.Lift(context, bar, radius, 1.1);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), bar, radius, radius);

        var centerY = bar.Center.Y;
        if (_search)
        {
            DrawSearchField(context, bar, centerY);
        }
        else
        {
            DrawItems(context, size, bar, centerY);
        }

        DrawBrand(context, bar, centerY);
        DrawTrailing(context, bar, centerY, radius);

        if (_open >= 0 || _panelReveal > 0.01)
        {
            DrawPanel(context, size);
        }

        if (ShowFocusRing)
        {
            Sm.FocusRing(context, bar, radius);
        }
    }

    private void DrawPage(DrawingContext context, Size size)
    {
        var band = new Rect(0, 0, size.Width, 150);
        using (context.PushOpacity(0.7))
        {
            context.DrawRectangle(Sm.Brush(ArcTokens.GradientSoft), null, band, 0, 0);
        }

        var headline = Sm.Text("Build the parts people touch", 24, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(headline, new Point(size.Width / 2 - 260, 330));
        var body = Sm.Text("Navigation that folds into the page, one surface at a time.", 13, Sm.Brush(ArcTokens.TextSecondary));
        context.DrawText(body, new Point(size.Width / 2 - 260, 358));
    }

    private void DrawBrand(DrawingContext context, Rect bar, double centerY)
    {
        var tile = new Rect(bar.X + 16, centerY - 14, 28, 28);
        context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, tile, 9, 9);
        var mark = Sm.Text("a", 16, Sm.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
        Sm.TextCentered(context, mark, tile.Center.X, tile.Center.Y);
        var name = Sm.Text("Arc", 15, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
        context.DrawText(name, new Point(tile.Right + 9, centerY - name.Height / 2));
        Hit(new Rect(bar.X + 8, centerY - 18, 108, 36), IdBrand);
    }

    private void DrawTrailing(DrawingContext context, Rect bar, double centerY, double radius)
    {
        var search = new Rect(bar.Right - 190, centerY - 17, 34, 34);
        if (!_search)
        {
            context.DrawRectangle(null, new Pen(Sm.Brush(ArcTokens.Border), 1), search, 17, 17);
        }

        Sm.Icon(context, "search", search.Center, 16, Sm.Brush(_search ? ArcTokens.AccentStrong : ArcTokens.TextSecondary));
        Hit(search, IdSearch);

        var action = new Rect(bar.Right - 146, centerY - 18, 122, 36);
        context.DrawRectangle(Sm.Brush(ArcTokens.Accent), null, action, 18, 18);
        var label = Sm.Text("Sign up", 13, Sm.Brush(ArcTokens.AccentForeground), FontWeight.Medium);
        Sm.TextCentered(context, label, action.Center.X, action.Center.Y);
        Hit(action, IdAction);
    }

    private void DrawItems(DrawingContext context, Size size, Rect bar, double centerY)
    {
        var x = bar.X + 156;
        var hovered = HoverId >= IdItem && HoverId < IdItem + Items.Length ? HoverId - IdItem : -1;
        var highlight = _open >= 0 ? _open : _focus >= 0 ? _focus : hovered;

        for (var index = 0; index < Items.Length; index++)
        {
            var label = Sm.Text(Items[index].Label, _compact ? 13 : 14, Sm.Brush(Items[index].Links is not null ? ArcTokens.Foreground : ArcTokens.TextSecondary), Items[index].Links is not null ? FontWeight.Medium : FontWeight.Normal);
            var width = Sm.MeasureWidth(Items[index].Label, _compact ? 13 : 14, FontWeight.Medium) + 26;
            var rect = new Rect(x, centerY - 17, width, 34);

            if (highlight == index)
            {
                if (double.IsNaN(_highlightX) || _highlightIndex != index)
                {
                    _highlightX = rect.X;
                    _highlightW = rect.Width;
                    _highlightIndex = index;
                }

                context.DrawRectangle(Sm.Alpha(ArcTokens.Accent, 0.14), null, new Rect(_highlightX, rect.Y, _highlightW, rect.Height), 17, 17);
            }

            context.DrawText(label, new Point(rect.Center.X - label.Width / 2, centerY - label.Height / 2));
            if (Items[index].Links is not null)
            {
                var chevron = new Rect(rect.Right - 18, centerY - 5, 10, 10);
                Sm.Icon(context, "chevron-down", chevron, Sm.Brush(ArcTokens.TextMuted), 1.6);
            }


            Hit(rect, IdItem + index);
            x += width + 2;
        }
    }

    private void DrawSearchField(DrawingContext context, Rect bar, double centerY)
    {
        var field = new Rect(bar.X + 14, centerY - 17, bar.Width - 210, 34);
        context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), new Pen(Sm.Brush(ArcTokens.Accent), 1), field, 16, 16);
        Sm.Icon(context, "search", new Rect(field.X + 12, centerY - 8, 16, 16), Sm.Brush(ArcTokens.TextSecondary));
        var text = Sm.Text(string.IsNullOrEmpty(_query) ? "Search components" : _query, 13, Sm.Brush(string.IsNullOrEmpty(_query) ? ArcTokens.TextMuted : ArcTokens.Foreground));
        context.DrawText(text, new Point(field.X + 36, centerY - text.Height / 2));
        if (!string.IsNullOrEmpty(_query))
        {
            var caretX = field.X + 38 + text.Width;
            context.DrawLine(new Pen(Sm.Brush(ArcTokens.Accent), 1.4), new Point(caretX, centerY - 8), new Point(caretX, centerY + 8));
        }

        Hit(field, IdField);
    }

    private void DrawPanel(DrawingContext context, Size size)
    {
        var panel = PanelRect(size);
        var grow = Sm.Clamp01(_panelReveal);
        using (context.PushOpacity(grow))
        {
            var rect = new Rect(panel.X, panel.Bottom - panel.Height * (0.7 + 0.3 * grow), panel.Width, panel.Height);
            Sm.Lift(context, rect, 24, 1.1);
            context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceRaised), new Pen(Sm.Brush(ArcTokens.Border), 1), rect, 24, 24);

            if (_search)
            {
                DrawResults(context, rect);
                return;
            }

            var item = Items[Math.Clamp(_open, 0, Items.Length - 1)];
            if (item.Links is null)
            {
                return;
            }

            var title = Sm.Text(item.Label, 12, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium);
            context.DrawText(title, new Point(rect.X + 20, rect.Y + 16));

            for (var index = 0; index < item.Links.Length; index++)
            {
                var column = index % 2;
                var row = index / 2;
                var cell = new Rect(rect.X + 12 + column * (rect.Width / 2 - 6), rect.Y + 44 + row * 62, rect.Width / 2 - 18, 56);
                var hovered = HoverId == IdLink + index;
                if (hovered)
                {
                    context.DrawRectangle(Sm.Alpha(ArcTokens.Foreground, 0.05), null, cell, 14, 14);
                }

                var tile = new Rect(cell.X + 8, cell.Y + 10, 34, 34);
                context.DrawRectangle(Sm.Brush(ArcTokens.SurfaceMuted), new Pen(Sm.Brush(ArcTokens.Border), 1), tile, 11, 11);
                Sm.Icon(context, item.Links[index].Icon, tile.Center, 17, Sm.Brush(ArcTokens.TextSecondary));

                var label = Sm.Text(item.Links[index].Label, 14, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
                context.DrawText(label, new Point(tile.Right + 10, cell.Y + 8));
                var description = Sm.Text(item.Links[index].Description, 11, Sm.Brush(ArcTokens.TextMuted));
                context.DrawText(description, new Point(tile.Right + 10, cell.Y + 27));
                Hit(cell, IdLink + index);
            }

            var footer = new Rect(rect.X + 20, rect.Bottom - 44, rect.Width - 40, 30);
            var footerLabel = Sm.Text("View all components", 12, Sm.Brush(ArcTokens.AccentStrong), FontWeight.Medium);
            context.DrawText(footerLabel, new Point(footer.X, footer.Center.Y - footerLabel.Height / 2));
            Sm.Icon(context, "arrow-right", new Rect(footer.X + footerLabel.Width + 6, footer.Center.Y - 8, 16, 16), Sm.Brush(ArcTokens.AccentStrong), 1.6);
            Hit(footer, IdFooter);
        }
    }

    private void DrawResults(DrawingContext context, Rect rect)
    {
        var matches = Matches();
        var title = Sm.Text(string.IsNullOrEmpty(_query) ? "Suggested" : $"{matches.Length} results", 12, Sm.Brush(ArcTokens.TextMuted), FontWeight.Medium);
        context.DrawText(title, new Point(rect.X + 20, rect.Y + 16));

        for (var index = 0; index < matches.Length; index++)
        {
            var row = new Rect(rect.X + 10, rect.Y + 42 + index * 34, rect.Width - 20, 30);
            var active = index == _result;
            if (active)
            {
                context.DrawRectangle(Sm.Alpha(ArcTokens.Accent, 0.12), null, row, 12, 12);
            }

            var entry = SearchIndex[matches[index]];
            Sm.Icon(context, "file", new Rect(row.X + 10, row.Center.Y - 8, 16, 16), Sm.Brush(active ? ArcTokens.AccentStrong : ArcTokens.TextSecondary));
            var label = Sm.Text(entry.Label, 13, Sm.Brush(ArcTokens.Foreground), FontWeight.Medium);
            context.DrawText(label, new Point(row.X + 34, row.Center.Y - label.Height / 2));
            var group = Sm.Text(entry.Group, 11, Sm.Brush(ArcTokens.TextMuted));
            Sm.TextRight(context, group, row.Right - 12, row.Center.Y);
            Hit(row, IdResult + index);
        }

        if (matches.Length == 0)
        {
            var empty = Sm.Text("No matches. Try a shorter word.", 12, Sm.Brush(ArcTokens.TextMuted));
            context.DrawText(empty, new Point(rect.X + 20, rect.Y + 52));
        }
    }

    private int[] Matches()
    {
        if (string.IsNullOrWhiteSpace(_query))
        {
            return new[] { 0, 1, 2, 3, 4 };
        }

        var result = new System.Collections.Generic.List<int>();
        for (var index = 0; index < SearchIndex.Length; index++)
        {
            if (SearchIndex[index].Label.Contains(_query, StringComparison.OrdinalIgnoreCase) ||
                SearchIndex[index].Group.Contains(_query, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(index);
            }
        }

        return result.Count > 5 ? result.GetRange(0, 5).ToArray() : result.ToArray();
    }

    // ---- Interaction -------------------------------------------------------------------------------

    private void OpenPanel(int item)
    {
        _search = false;
        _open = item;
        _panelReveal = 0;
        Animate(240, ArcMotion.SpringSmooth, progress =>
        {
            _panelReveal = progress;
            InvalidateVisual();
        });
    }

    private void ClosePanel()
    {
        _open = -1;
        _search = false;
        var from = _panelReveal;
        Animate(160, ArcMotion.EaseExit, progress =>
        {
            _panelReveal = Sm.Lerp(from, 0, progress);
            InvalidateVisual();
        });
    }

    protected override void OnPress(int id, Point point)
    {
        Focus();
        if (id >= IdItem && id < IdItem + Items.Length)
        {
            var index = id - IdItem;
            _focus = index;
            _current = index;
            if (Items[index].Links is not null)
            {
                if (_open == index)
                {
                    ClosePanel();
                }
                else
                {
                    OpenPanel(index);
                }
            }
            else
            {
                _open = -1;
                _search = false;
                InvalidateVisual();
            }

            return;
        }

        if (id == IdSearch)
        {
            if (_search)
            {
                ClosePanel();
            }
            else
            {
                OpenSearch();
            }

            return;
        }

        if (id == IdBrand)
        {
            _current = 0;
            InvalidateVisual();
            return;
        }

        if (id == IdField)
        {
            _search = true;
            InvalidateVisual();
            return;
        }

        if (id >= IdLink && id < IdLink + 4 || id == IdFooter || id == IdAction)
        {
            _current = Math.Max(0, _open);
            ClosePanel();
            return;
        }

        if (id >= IdResult && id < IdResult + 5)
        {
            var matches = Matches();
            if (id - IdResult < matches.Length)
            {
                _query = SearchIndex[matches[id - IdResult]].Label;
                ClosePanel();
            }
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!_search || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        _query += e.Text;
        _result = 0;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_search)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    ClosePanel();
                    e.Handled = true;
                    return;
                case Key.Back:
                    if (_query.Length > 0)
                    {
                        _query = _query[..^1];
                    }

                    e.Handled = true;
                    return;
                case Key.Down:
                    _result = Math.Min(Math.Max(0, Matches().Length - 1), _result + 1);
                    e.Handled = true;
                    return;
                case Key.Up:
                    _result = Math.Max(0, _result - 1);
                    e.Handled = true;
                    return;
                case Key.Enter:
                    var matches = Matches();
                    if (_result < matches.Length)
                    {
                        _query = SearchIndex[matches[_result]].Label;
                    }

                    e.Handled = true;
                    return;
            }

            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                _focus = _focus <= 0 ? 0 : _focus - 1;
                e.Handled = true;
                break;
            case Key.Right:
                _focus = Math.Min(Items.Length - 1, _focus + 1);
                e.Handled = true;
                break;
            case Key.Down:
            case Key.Enter:
            case Key.Space:
                if (_focus >= 0)
                {
                    if (Items[_focus].Links is not null)
                    {
                        OpenPanel(_focus);
                    }

                    e.Handled = true;
                }

                break;
            case Key.Escape:
                ClosePanel();
                e.Handled = true;
                break;
            case Key.OemQuestion:
                OpenSearch();
                e.Handled = true;
                break;
        }

        InvalidateVisual();
    }
}
