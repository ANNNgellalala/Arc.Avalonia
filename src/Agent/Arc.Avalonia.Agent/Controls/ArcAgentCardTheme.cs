using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Loads the card ControlThemes shipped inside this package. Rendered Markdown trees are created by the
/// Markdown renderer, not by a page, so the cards cannot rely on the host adding a theme entry: the first
/// card applies its theme from <c>Themes/ArcAgentCards.axaml</c>. Hosts may still include
/// <c>Themes/ArcAgentTheme.axaml</c> explicitly; in that case the resource lookup below finds the theme and
/// the cards keep the host-provided instance.
/// </summary>
internal static class ArcAgentCardTheme
{
    private const string ThemeUri = "avares://Arc.Avalonia.Agent/Themes/ArcAgentCards.axaml";

    private static readonly object Sync = new();
    private static ResourceDictionary? _themes;

    internal static void EnsureApplied(Control card, Type themeKey)
    {
        if (card.Theme is not null || Application.Current is null)
        {
            return;
        }

        try
        {
            // A host-provided theme (implicit ControlTheme from app styles) always wins.
            if (card.TryFindResource(themeKey, out var existing) && existing is ControlTheme)
            {
                return;
            }

            var theme = Load(themeKey);
            if (theme is not null)
            {
                card.Theme = theme;
            }
        }
        catch (Exception exception)
        {
            // Designer or partially initialised host: leave the control unstyled instead of failing the
            // surrounding Markdown view. The view's fence dispatcher already degrades renderer failures.
            System.Diagnostics.Debug.WriteLine($"ArcAgentCardTheme: {exception.Message}");
        }
    }

    private static ControlTheme? Load(Type themeKey)
    {
        lock (Sync)
        {
            _themes ??= global::Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(
                new Uri(ThemeUri, UriKind.Absolute),
                null) as ResourceDictionary;
        }

        return _themes is System.Collections.Generic.IDictionary<object, object?> map
               && map.TryGetValue(themeKey, out var value)
            ? value as ControlTheme
            : null;
    }
}
