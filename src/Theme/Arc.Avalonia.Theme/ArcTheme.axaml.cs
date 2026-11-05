using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace Arc.Avalonia.Theme;

/// <summary>
/// The single-line theme entry point (ADR 0003): <c>&lt;arc:ArcTheme/&gt;</c> inside
/// <c>Application.Styles</c> installs the Fluent baseline, the 11 component theme families and keeps the
/// Arc palette / Fluent resource-key overrides in sync with <see cref="ArcThemeManager.Apply"/>.
/// </summary>
/// <remarks>
/// The Fluent baseline is created in code rather than as a <c>&lt;FluentTheme/&gt;</c> element because the
/// R4 gate (tools/check-theme.ps1) forbids that element anywhere in library axaml. Behaviour is identical
/// to the XAML form verified by M0 POC-A: the instance is inserted first, so every StyleInclude loaded
/// afterwards overrides the Fluent defaults.
/// </remarks>
public partial class ArcTheme : Styles
{
    private readonly FluentTheme _fluentTheme;
    private ResourceDictionary? _fluentOverrides;

    public ArcTheme()
    {
        // Avalonia 12's FluentTheme only exposes FluentTheme(IServiceProvider). Measured in M1-T3:
        // Application.Current is still null while App.axaml is being loaded, so pass a minimal provider.
        // FluentTheme loads its own theme resources from embedded assets and receives the active
        // ThemeVariant through every TryGetResource call, so it needs no services from the application.
        _fluentTheme = new FluentTheme(ThemeServiceProvider.Instance);

        // 1) Fluent baseline first - the aggregate's own resources and the includes below win over it.
        Add(_fluentTheme);

        // 2) ArcBaseStyles + the 11 component theme families (declared in ArcTheme.axaml).
        AvaloniaXamlLoader.Load(this);

        // 3) Token dictionaries, Fluent key overrides and accent palettes follow ArcThemeManager.Apply.
        ArcThemeManager.Applied += OnThemeApplied;

        // Applying immediately makes the one-line form usable on its own; a later explicit Apply simply
        // rebuilds the same dictionaries (they are tracked, so nothing accumulates).
        if (Application.Current is { } application)
        {
            ArcThemeManager.Apply(application, ArcThemeManager.Scheme, ArcThemeManager.Accent);
        }
    }

    /// <summary>The Fluent instance owned by this aggregate (exposed for tests/diagnostics).</summary>
    public FluentTheme FluentTheme => _fluentTheme;

    /// <summary>Minimal service provider: FluentTheme needs the parameter, but no services from the app.</summary>
    private sealed class ThemeServiceProvider : IServiceProvider
    {
        public static ThemeServiceProvider Instance { get; } = new();

        public object? GetService(Type serviceType) => null;
    }

    private void OnThemeApplied(Application application, ArcColorScheme scheme, ArcAccent accent)
    {
        // 8 accents map onto Fluent's public palette API instead of a bare SystemAccentColor override.
        ArcThemePalettes.Apply(_fluentTheme, accent);

        if (_fluentOverrides is not null)
        {
            application.Resources.MergedDictionaries.Remove(_fluentOverrides);
        }

        _fluentOverrides = ArcFluentOverrides.Build(accent);
        application.Resources.MergedDictionaries.Add(_fluentOverrides);
    }
}
