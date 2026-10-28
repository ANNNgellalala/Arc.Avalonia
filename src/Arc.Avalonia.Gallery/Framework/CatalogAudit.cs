using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Checks that every catalog entry (component, block or special component) is reachable from the gallery:
/// either it has its own ArcItemPage, or an existing demo page has a section heading with the same title.
/// Run with `dotnet Arc.Avalonia.Gallery.dll --audit`.
/// </summary>
public static class CatalogAudit
{
    public static int Run()
    {
        var pages = ArcPageRegistry.Discover();
        var context = new ArcDemoContext(ArcColorScheme.Light, ArcAccent.Neutral);
        var headings = new HashSet<string>(StringComparer.Ordinal);
        var itemPages = new Dictionary<string, ArcPage>(StringComparer.Ordinal);
        var failures = new List<string>();

        foreach (var page in pages)
        {
            try
            {
                var content = page.Build(context);
                if (page is ArcItemPage itemPage)
                {
                    itemPages[itemPage.ItemSlug] = page;
                }

                CollectHeadings(content, headings);
            }
            catch (Exception exception)
            {
                failures.Add($"{page.Title}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        var pageTitles = new HashSet<string>(pages.Select(page => page.Title), StringComparer.Ordinal);
        var missing = new List<ArcCatalogItem>();
        var byItemPage = 0;
        var bySection = 0;
        var byAlias = 0;
        foreach (var item in ArcCatalog.AllItems)
        {
            if (itemPages.ContainsKey(item.Slug))
            {
                byItemPage++;
            }
            else if (headings.Contains(Normalize(item.Title)))
            {
                bySection++;
            }
            else if (ArcCatalogAliases.PageTitles(item.Slug).Any(pageTitles.Contains))
            {
                byAlias++;
            }
            else
            {
                missing.Add(item);
            }
        }

        Console.WriteLine($"CATALOG AUDIT: pages={pages.Count} items={ArcCatalog.AllItems.Count} itemPages={byItemPage} matchedBySection={bySection} matchedByAlias={byAlias} missing={missing.Count} pageFailures={failures.Count}");
        foreach (var group in missing.GroupBy(item => item.Kind + " / " + item.Category))
        {
            Console.WriteLine($"MISSING {group.Key}: {string.Join(", ", group.Select(item => item.Slug))}");
        }

        foreach (var failure in failures.Take(20))
        {
            Console.WriteLine("PAGE FAILED " + failure);
        }

        return missing.Count == 0 && failures.Count == 0 ? 0 : 1;
    }

    private static void CollectHeadings(ILogical root, HashSet<string> headings)
    {
        if (root is TextBlock text
            && text.Classes.Contains("arc-heading")
            && !string.IsNullOrWhiteSpace(text.Text))
        {
            headings.Add(Normalize(text.Text));
        }

        foreach (var child in root.LogicalChildren)
        {
            CollectHeadings(child, headings);
        }
    }

    private static string Normalize(string value)
    {
        var buffer = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Append(char.ToLowerInvariant(character));
            }
        }

        return buffer.ToString();
    }
}



