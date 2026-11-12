using System;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace SwipeInteractionProbe;

/// <summary>
/// Minimal theme host for the probe: Fluent baseline (for the plain Button used as row content) plus the
/// Arc Foundation base styles and the Media theme family. Mirrors the aggregate ArcTheme load order but
/// only pulls the two style families the probe needs.
/// </summary>
public partial class ProbeTheme : Styles
{
    private readonly FluentTheme _fluentTheme;

    public ProbeTheme()
    {
        _fluentTheme = new FluentTheme(ThemeServiceProvider.Instance);
        Add(_fluentTheme);
        AvaloniaXamlLoader.Load(this);
    }

    private sealed class ThemeServiceProvider : IServiceProvider
    {
        public static ThemeServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }
}
