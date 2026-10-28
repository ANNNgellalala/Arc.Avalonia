using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Arc.Avalonia.Demo;

/// <summary>One item from the Arc catalog: a component, a special component, or a block.</summary>
public sealed class ArcCatalogItem
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Kind { get; init; } = "component";
    public string Category { get; init; } = string.Empty;
    public string Tier { get; init; } = "free";
    public string Description { get; init; } = string.Empty;
    public string Docs { get; init; } = string.Empty;

    public bool IsPro => string.Equals(Tier, "pro", StringComparison.OrdinalIgnoreCase);

    /// <summary>Breadcrumb label for the item's catalog section.</summary>
    public string KindLabel => Kind switch
    {
        "block" => "Blocks",
        "special" => "Special components",
        _ => "Components"
    };
}

/// <summary>A catalog category (for example Buttons) with its ordered items.</summary>
public sealed class ArcCatalogGroup
{
    public ArcCatalogGroup(string name, IReadOnlyList<ArcCatalogItem> items)
    {
        Name = name;
        Items = items;
    }

    public string Name { get; }

    public IReadOnlyList<ArcCatalogItem> Items { get; }
}

/// <summary>One sidebar entry: either a demo page (overview) or a catalog item row.</summary>
public sealed class ArcCatalogSection
{
    public ArcCatalogSection(string name, IReadOnlyList<ArcCatalogGroup> groups)
    {
        Name = name;
        Groups = groups;
    }

    public string Name { get; }

    public IReadOnlyList<ArcCatalogGroup> Groups { get; }
}

/// <summary>
/// The public Arc catalog, loaded from the embedded docs/arc-catalog.json generated from uiarc.dev's
/// llms-small.txt and the introduction sidebar. It mirrors the site's taxonomy so the gallery navigation
/// reads like the documentation: Components by category, Blocks, then Special components.
/// </summary>
public static class ArcCatalog
{
    private const string ResourceName = "Arc.Avalonia.Demo.Catalog.arc-catalog.json";

    private static readonly Lazy<CatalogData> Data = new(Load);

    public static IReadOnlyList<ArcCatalogGroup> ComponentGroups => Data.Value.ComponentGroups;

    public static IReadOnlyList<ArcCatalogGroup> BlockGroups => Data.Value.BlockGroups;

    public static IReadOnlyList<ArcCatalogGroup> SpecialGroups => Data.Value.SpecialGroups;

    public static IReadOnlyList<ArcCatalogItem> AllItems => Data.Value.AllItems;

    public static ArcCatalogItem? Get(string slug) =>
        Data.Value.BySlug.TryGetValue(slug, out var item) ? item : null;

    public static int OrderOf(string slug) =>
        Data.Value.Order.TryGetValue(slug, out var order) ? order : 5000;

    /// <summary>Every catalog section in sidebar order: Components, Blocks, Special components.</summary>
    public static IReadOnlyList<ArcCatalogSection> Sections => Data.Value.Sections;

    private static CatalogData Load()
    {
        var assembly = typeof(ArcCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded catalog resource {ResourceName}");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;

        var items = new List<ArcCatalogItem>();
        items.AddRange(ReadItems(root, "items"));
        items.AddRange(ReadItems(root, "specialItems"));
        items.AddRange(ReadItems(root, "blockItems"));

        var bySlug = items.GroupBy(item => item.Slug, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        var order = new Dictionary<string, int>(StringComparer.Ordinal);
        var orderIndex = 0;
        foreach (var item in items)
        {
            if (!order.ContainsKey(item.Slug))
            {
                order[item.Slug] = orderIndex++;
            }
        }

        var componentGroups = ReadGroups(root, "componentCategories", bySlug);
        var blockGroups = ReadGroups(root, "blockGroups", bySlug);
        var specialGroups = ReadGroups(root, "specialGroups", bySlug);

        var sections = new[]
        {
            new ArcCatalogSection("Components", componentGroups),
            new ArcCatalogSection("Blocks", blockGroups),
            new ArcCatalogSection("Special components", specialGroups)
        };

        return new CatalogData(componentGroups, blockGroups, specialGroups, items, bySlug, order, sections);
    }

    private static List<ArcCatalogItem> ReadItems(JsonElement root, string property)
    {
        var result = new List<ArcCatalogItem>();
        if (!root.TryGetProperty(property, out var array))
        {
            return result;
        }

        foreach (var node in array.EnumerateArray())
        {
            result.Add(new ArcCatalogItem
            {
                Slug = node.GetProperty("slug").GetString() ?? string.Empty,
                Title = node.GetProperty("title").GetString() ?? string.Empty,
                Kind = node.TryGetProperty("kind", out var kind) ? kind.GetString() ?? "component" : "component",
                Category = node.TryGetProperty("category", out var category) ? category.GetString() ?? string.Empty : string.Empty,
                Tier = node.TryGetProperty("tier", out var tier) ? tier.GetString() ?? "free" : "free",
                Description = node.TryGetProperty("description", out var description) ? description.GetString() ?? string.Empty : string.Empty,
                Docs = node.TryGetProperty("docs", out var docs) ? docs.GetString() ?? string.Empty : string.Empty
            });
        }

        return result;
    }

    private static List<ArcCatalogGroup> ReadGroups(JsonElement root, string property, IReadOnlyDictionary<string, ArcCatalogItem> bySlug)
    {
        var result = new List<ArcCatalogGroup>();
        if (!root.TryGetProperty(property, out var groups))
        {
            return result;
        }

        foreach (var group in groups.EnumerateArray())
        {
            var name = group.GetProperty("name").GetString() ?? string.Empty;
            var groupItems = new List<ArcCatalogItem>();
            foreach (var slugNode in group.GetProperty("items").EnumerateArray())
            {
                var slug = slugNode.GetString();
                if (slug is not null && bySlug.TryGetValue(slug, out var item))
                {
                    groupItems.Add(item);
                }
            }

            result.Add(new ArcCatalogGroup(name, groupItems));
        }

        return result;
    }

    private sealed record CatalogData(
        IReadOnlyList<ArcCatalogGroup> ComponentGroups,
        IReadOnlyList<ArcCatalogGroup> BlockGroups,
        IReadOnlyList<ArcCatalogGroup> SpecialGroups,
        IReadOnlyList<ArcCatalogItem> AllItems,
        IReadOnlyDictionary<string, ArcCatalogItem> BySlug,
        IReadOnlyDictionary<string, int> Order,
        IReadOnlyList<ArcCatalogSection> Sections);
}
