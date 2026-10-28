using Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Demo;

/// <summary>Context passed to a demo page when the gallery builds it.</summary>
public sealed record ArcDemoContext(ArcColorScheme Scheme, ArcAccent Accent)
{
    public bool Dark => Scheme == ArcColorScheme.Dark;
}

/// <summary>
/// One gallery page. Component projects declare pages so each library ships with its own showcase;
/// the gallery discovers them by reflection and never needs a central registry edit.
/// </summary>
public abstract class ArcPage
{
    /// <summary>Sidebar label and screenshot file stem.</summary>
    public abstract string Title { get; }

    /// <summary>Sidebar section, for example "Inputs" or "Charts".</summary>
    public virtual string Group => "Components";

    /// <summary>One line under the page title.</summary>
    public virtual string? Description => null;

    /// <summary>Sort weight inside the sidebar; leave 100 for new pages and adjust in tens.</summary>
    public virtual int Order => 100;

    /// <summary>Builds the page content. The gallery owns the scroll container and page padding.</summary>
    public abstract Control Build(ArcDemoContext context);

    public string Slug => Title.ToLowerInvariant().Replace(' ', '-');
}
