using System;
using Avalonia;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Services;

/// <summary>
/// Lets a component switch the application theme without referencing <see cref="ArcThemeManager"/> directly.
/// Applications that keep their own theme facade can implement this interface and register it instead.
/// </summary>
public interface IArcThemeService
{
    /// <summary>The active colour scheme.</summary>
    ArcColorScheme Scheme { get; }

    /// <summary>The active accent.</summary>
    ArcAccent Accent { get; }

    /// <summary>Applies a scheme and an accent to the application.</summary>
    void Apply(ArcColorScheme scheme, ArcAccent accent);

    /// <summary>Changes the colour scheme, keeping the accent.</summary>
    void SetScheme(ArcColorScheme scheme);

    /// <summary>Changes the accent, keeping the scheme.</summary>
    void SetAccent(ArcAccent accent);

    /// <summary>Switches between light and dark.</summary>
    void ToggleScheme();
}

/// <summary>The default <see cref="IArcThemeService"/>: a thin facade over <see cref="ArcThemeManager"/>.</summary>
public sealed class ArcThemeService : IArcThemeService
{
    private readonly Application? _application;

    /// <summary>Creates the facade over the current application.</summary>
    public ArcThemeService()
        : this(Application.Current)
    {
    }

    /// <summary>Creates the facade over an explicit application (headless hosts and tests).</summary>
    public ArcThemeService(Application? application)
    {
        _application = application ?? Application.Current;
    }

    /// <inheritdoc />
    public ArcColorScheme Scheme => ArcThemeManager.Scheme;

    /// <inheritdoc />
    public ArcAccent Accent => ArcThemeManager.Accent;

    /// <inheritdoc />
    public void Apply(ArcColorScheme scheme, ArcAccent accent)
    {
        if (_application is not null)
        {
            ArcThemeManager.Apply(_application, scheme, accent);
        }
    }

    /// <inheritdoc />
    public void SetScheme(ArcColorScheme scheme) => Apply(scheme, Accent);

    /// <inheritdoc />
    public void SetAccent(ArcAccent accent) => Apply(Scheme, accent);

    /// <inheritdoc />
    public void ToggleScheme() =>
        Apply(Scheme == ArcColorScheme.Dark ? ArcColorScheme.Light : ArcColorScheme.Dark, Accent);
}
