using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;

namespace Arc.Avalonia.Charts.Controls;

/// <summary>
/// A compact chart surface: title, optional subtitle, the chart, and one caption line.
/// It uses <see cref="ArcCard"/> so the hairline border and concentric surface radius come from the theme.
/// </summary>
public class ArcChartCard : ArcCard
{
    protected override Type StyleKeyOverride => typeof(ArcCard);

    private readonly TextBlock _title;
    private readonly TextBlock _subtitle;
    private readonly TextBlock _caption;
    private readonly Border _chartHost;

    public ArcChartCard()
    {
        Padding = new Thickness(16, 14, 16, 12);
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _title = new TextBlock { Classes = { "arc-label" } };
        _subtitle = new TextBlock
        {
            Classes = { "arc-caption" },
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        _caption = new TextBlock
        {
            Classes = { "arc-caption" },
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };
        _chartHost = new Border { Margin = new Thickness(0, 14, 0, 0) };

        var panel = new StackPanel();
        panel.Children.Add(_title);
        panel.Children.Add(_subtitle);
        panel.Children.Add(_chartHost);
        panel.Children.Add(_caption);
        Content = panel;
    }

    public string? Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    public string? Subtitle
    {
        get => _subtitle.Text;
        set
        {
            _subtitle.Text = value;
            _subtitle.IsVisible = !string.IsNullOrWhiteSpace(value);
            _subtitle.Margin = new Thickness(0, 3, 0, 0);
        }
    }

    public string? Caption
    {
        get => _caption.Text;
        set
        {
            _caption.Text = value;
            _caption.IsVisible = !string.IsNullOrWhiteSpace(value);
            _caption.Margin = new Thickness(0, 8, 0, 0);
        }
    }

    public Control? Chart
    {
        get => _chartHost.Child;
        set => _chartHost.Child = value;
    }
}
