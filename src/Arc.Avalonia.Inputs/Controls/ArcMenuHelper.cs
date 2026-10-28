using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Inputs.Controls;

/// <summary>Shared helpers for the popup menus used by select, combobox and multi-select.</summary>
internal static class ArcMenuHelper
{
    public static ControlTheme? FindTheme(StyledElement owner, string key) =>
        owner.TryFindResource(key, out var resource) && resource is ControlTheme theme ? theme : null;

    public static ListBoxItem CreateItem(string label, string? value, bool selected, bool disabled, bool showCheck = true)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var text = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        grid.Children.Add(text);

        var check = new ArcIcon
        {
            Icon = "check",
            Size = 16,
            StrokeWidth = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ArcResources.Brush(ArcTokens.AccentStrong),
            IsVisible = showCheck && selected
        };
        Grid.SetColumn(check, 1);
        grid.Children.Add(check);

        var item = new ListBoxItem
        {
            Content = grid,
            Tag = value,
            IsEnabled = !disabled,
            Focusable = false,
            IsTabStop = false
        };

        TextSearch.SetText(item, label);
        AutomationProperties.SetName(item, label);
        return item;
    }

    public static void ConfigurePopup(Popup popup, Control target)
    {
        popup.PlacementTarget = target;
        popup.Placement = PlacementMode.BottomEdgeAlignedLeft;
        popup.HorizontalOffset = 0;
        popup.VerticalOffset = 6;
        popup.IsLightDismissEnabled = true;
    }
}
