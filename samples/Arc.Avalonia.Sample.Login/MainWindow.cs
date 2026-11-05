using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Sample.Login;

/// <summary>Sign-in screen: ArcInput + ArcPasswordField + ArcCheckbox and a primary ArcButton inside an ArcCard.</summary>
public sealed class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Arc for Avalonia - Sign in";
        Width = 560;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = ArcResources.Brush(ArcTokens.Background);

        var form = new StackPanel { Spacing = 16 };
        form.Children.Add(new TextBlock { Text = "Welcome back", Classes = { "arc-heading" } });
        form.Children.Add(new TextBlock
        {
            Text = "Sign in to the workspace. Every control here comes from the Arc component packages.",
            Classes = { "arc-body-sm", "arc-secondary" },
            TextWrapping = TextWrapping.Wrap
        });
        form.Children.Add(new ArcInput { Label = "Email", Placeholder = "you@example.com", Value = "you@example.com" });
        form.Children.Add(new ArcPasswordField { Label = "Password", Value = "arc-demo" });
        form.Children.Add(new ArcCheckbox { Label = "Remember this device", Checked = true });
        form.Children.Add(new ArcButton
        {
            Content = "Sign in",
            Variant = ArcButtonVariant.Primary,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });
        form.Children.Add(new ArcButton
        {
            Content = "Continue with a passkey",
            Variant = ArcButtonVariant.Secondary,
            HorizontalAlignment = HorizontalAlignment.Stretch
        });

        Content = new Border
        {
            Padding = new Thickness(48),
            Child = new ArcCard { Padding = new Thickness(32), Content = form  } };
    }
}