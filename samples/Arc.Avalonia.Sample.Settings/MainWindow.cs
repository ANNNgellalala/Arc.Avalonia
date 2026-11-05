using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Sample.Settings;

/// <summary>Settings screen: ArcSwitch rows inside an ArcCard, saved with a primary ArcButton.</summary>
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Arc for Avalonia - Settings";
        Width = 640;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ArcResources.Brush(ArcTokens.Background);

        var panel = new StackPanel { Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = "Preferences", Classes = { "arc-heading" } });
        panel.Children.Add(new TextBlock
        {
            Text = "Notifications, density and motion are all Arc toggles using the same token palette.",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(new ArcSwitch { Label = "Email notifications", Checked = true });
        panel.Children.Add(new ArcSwitch { Label = "Compact density" });
        panel.Children.Add(new ArcSwitch { Label = "Reduce motion" });
        panel.Children.Add(new ArcSwitch { Label = "Weekly digest", Checked = true });
        panel.Children.Add(new ArcButton
        {
            Content = "Save changes",
            Variant = ArcButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Left
        });

        Content = new Border
        {
            Padding = new Thickness(48),
            Child = new ArcCard { Padding = new Thickness(32), Content = panel  } };
    }
}