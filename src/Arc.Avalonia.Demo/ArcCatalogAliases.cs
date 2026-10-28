using System;
using System.Collections.Generic;

namespace Arc.Avalonia.Demo;

/// <summary>
/// Catalog entries whose demo lives on a shared page rather than a dedicated section heading. The sidebar
/// navigates to the page (the component is demonstrated there), which keeps every item reachable while the
/// port keeps its grouped demo pages.
/// </summary>
public static class ArcCatalogAliases
{
    private static readonly Dictionary<string, string[]> Map = new(StringComparer.Ordinal)
    {
        ["button"] = new[] { "Kit proof" },
        ["badge"] = new[] { "Kit proof" },
        ["card"] = new[] { "Kit proof" },

        ["textarea"] = new[] { "Inputs" },
        ["password-field"] = new[] { "Inputs" },
        ["password-strength"] = new[] { "Inputs" },
        ["search-field"] = new[] { "Inputs" },
        ["number-field"] = new[] { "Inputs, part two" },
        ["money-input"] = new[] { "Inputs, part two" },
        ["tag-input"] = new[] { "Inputs, part two" },
        ["select"] = new[] { "Inputs, part two" },
        ["combobox"] = new[] { "Inputs, part two" },
        ["multi-select"] = new[] { "Inputs, part three" },
        ["chip-group"] = new[] { "Inputs, part three" },
        ["checkbox"] = new[] { "Inputs, part three" },
        ["radio-group"] = new[] { "Inputs, part three" },
        ["switch"] = new[] { "Inputs, part three" },
        ["segmented-control"] = new[] { "Inputs, part four" },
        ["slider"] = new[] { "Inputs, part four" },
        ["rating"] = new[] { "Inputs, part four" },

        ["toast-stack"] = new[] { "Actions, part two" },

        ["line-chart"] = new[] { "Charts" },
        ["bar-chart"] = new[] { "Charts" },
        ["donut-chart"] = new[] { "Charts" },
        ["sparkline"] = new[] { "Charts" },
        ["gauge"] = new[] { "Charts" },
        ["slope-chart"] = new[] { "Charts" },
        ["streamgraph"] = new[] { "Charts, part two" },
        ["waffle-chart"] = new[] { "Charts, part two" },
        ["treemap"] = new[] { "Charts, part two" },
        ["ridgeline"] = new[] { "Charts, part two" },
        ["split-bar"] = new[] { "Charts, part two" },
        ["brush-chart"] = new[] { "Brush chart" },

        ["sunburst"] = new[] { "Advanced charts" },
        ["sankey-flow"] = new[] { "Advanced charts" },
        ["funnel-chart"] = new[] { "Advanced charts" },
        ["radar-chart"] = new[] { "Advanced charts, part two" },
        ["realtime-stream"] = new[] { "Advanced charts, part two" },
        ["race-bar-chart"] = new[] { "Advanced charts, part two" },
        ["data-grid"] = new[] { "Data grid" }
    };

    /// <summary>Page titles that demonstrate the catalog item when it has no section of its own.</summary>
    public static IReadOnlyList<string> PageTitles(string slug) =>
        Map.TryGetValue(slug, out var titles) ? titles : Array.Empty<string>();
}
