using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace ArcThemePoc;

/// <summary>
/// The POC-A "one-line" aggregate theme entry point: FluentTheme + Arc token ThemeDictionaries +
/// component StyleIncludes in a single <see cref="Styles"/> subclass.
/// </summary>
public partial class ArcTheme : Styles
{
    public ArcTheme()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
