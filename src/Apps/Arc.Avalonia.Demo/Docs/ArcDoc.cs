using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Arc.Avalonia.Demo;

/// <summary>How two catalog entries relate. The reverse direction is derived when rendering.</summary>
public enum ArcRelationType
{
    /// <summary>Same job, pick one.</summary>
    Alternative,

    /// <summary>Different jobs that are frequently used together.</summary>
    Complement,

    /// <summary>A uses or contains B (a page composition or a child control).</summary>
    Uses,

    /// <summary>A is the more capable version of B.</summary>
    Extends
}

/// <summary>One "related component" edge. Authored once on the more specific side; the reverse is derived.</summary>
public sealed class ArcDocRelation
{
    public ArcRelationType Type { get; init; }
    public string Target { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
}

/// <summary>One row of a "difference from a similar component" table.</summary>
public sealed class ArcDocContrastRow
{
    public string Dimension { get; init; } = string.Empty;
    public string Self { get; init; } = string.Empty;
    public string Other { get; init; } = string.Empty;
}

/// <summary>One head-to-head comparison against a similar entry.</summary>
public sealed class ArcDocContrast
{
    public string Target { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<ArcDocContrastRow> Rows { get; init; } = Array.Empty<ArcDocContrastRow>();
}

/// <summary>One declared state of the completeness preview. Matches the page's ImplementedStates.</summary>
public sealed class ArcDocVariant
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
}

/// <summary>A copy-pasteable usage sample. Samples are not compiled; they are documentation.</summary>
public sealed class ArcDocExample
{
    public string Title { get; init; } = string.Empty;
    public string Language { get; init; } = "xaml";
    public string Code { get; init; } = string.Empty;
}

/// <summary>What the API section renders: a reflected property/event table for a control type.</summary>
public sealed class ArcDocApi
{
    /// <summary>Full name of the control type whose public API is documented.</summary>
    public string? Type { get; init; }

    /// <summary>Property names to leave out of the generated table.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = Array.Empty<string>();

    /// <summary>Chinese one-liners keyed by member name.</summary>
    public IReadOnlyDictionary<string, string> Descriptions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Blocks have no public API; their table is generated from "uses" relations instead.</summary>
    public bool IsDeclared => !string.IsNullOrWhiteSpace(Type);
}

/// <summary>
/// The Chinese documentation payload for one catalog entry, loaded from an embedded Docs/&lt;slug&gt;.json
/// file shipped by the project that owns the component. Content lives in data; behaviour lives in code.
/// </summary>
public sealed class ArcDoc
{
    public string Slug { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Purpose { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WhenToUse { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> WhenNotToUse { get; init; } = Array.Empty<string>();
    public IReadOnlyList<ArcDocVariant> Variants { get; init; } = Array.Empty<ArcDocVariant>();
    public IReadOnlyList<ArcDocRelation> Relations { get; init; } = Array.Empty<ArcDocRelation>();
    public IReadOnlyList<ArcDocContrast> Contrasts { get; init; } = Array.Empty<ArcDocContrast>();
    public IReadOnlyList<ArcDocExample> Examples { get; init; } = Array.Empty<ArcDocExample>();
    public ArcDocApi Api { get; init; } = new();

    /// <summary>Diagnostic origin, for example "Arc.Avalonia.Actions.Docs.button.json".</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Parses one document. Returns null and an error message when the JSON is unusable.</summary>
    public static ArcDoc? Parse(string json, string source, out string? error)
    {
        error = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "文档根节点必须是对象";
                return null;
            }

            var slug = String(root, "slug");
            if (string.IsNullOrWhiteSpace(slug))
            {
                error = "缺少 slug";
                return null;
            }

            var relations = new List<ArcDocRelation>();
            foreach (var node in Items(root, "relations"))
            {
                relations.Add(new ArcDocRelation
                {
                    Type = RelationType(String(node, "type")),
                    Target = String(node, "target").Trim(),
                    Note = String(node, "note")
                });
            }

            var contrasts = new List<ArcDocContrast>();
            foreach (var node in Items(root, "contrasts"))
            {
                var rows = new List<ArcDocContrastRow>();
                foreach (var row in Items(node, "rows"))
                {
                    rows.Add(new ArcDocContrastRow
                    {
                        Dimension = String(row, "dimension"),
                        Self = String(row, "self"),
                        Other = String(row, "other")
                    });
                }

                contrasts.Add(new ArcDocContrast
                {
                    Target = String(node, "target").Trim(),
                    Title = String(node, "title"),
                    Rows = rows
                });
            }

            var variants = new List<ArcDocVariant>();
            foreach (var node in Items(root, "variants"))
            {
                variants.Add(new ArcDocVariant
                {
                    Key = String(node, "key"),
                    Label = String(node, "label"),
                    Note = String(node, "note")
                });
            }

            var examples = new List<ArcDocExample>();
            foreach (var node in Items(root, "examples"))
            {
                examples.Add(new ArcDocExample
                {
                    Title = String(node, "title"),
                    Language = String(node, "language", "xaml"),
                    Code = String(node, "code")
                });
            }

            var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            var exclude = new List<string>();
            string? apiType = null;
            if (root.TryGetProperty("api", out var api) && api.ValueKind == JsonValueKind.Object)
            {
                apiType = String(api, "type");
                foreach (var name in Items(api, "exclude"))
                {
                    var text = name.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        exclude.Add(text);
                    }
                }

                if (api.TryGetProperty("descriptions", out var map) && map.ValueKind == JsonValueKind.Object)
                {
                    foreach (var entry in map.EnumerateObject())
                    {
                        descriptions[entry.Name] = entry.Value.GetString() ?? string.Empty;
                    }
                }
            }

            return new ArcDoc
            {
                Slug = slug.Trim(),
                NameZh = String(root, "nameZh"),
                Summary = String(root, "summary"),
                Purpose = Strings(root, "purpose"),
                WhenToUse = Strings(root, "whenToUse"),
                WhenNotToUse = Strings(root, "whenNotToUse"),
                Variants = variants,
                Relations = relations,
                Contrasts = contrasts,
                Examples = examples,
                Api = new ArcDocApi { Type = apiType, Exclude = exclude, Descriptions = descriptions },
                Source = source
            };
        }
        catch (JsonException exception)
        {
            error = "JSON 解析失败：" + exception.Message;
            return null;
        }
    }

    private static ArcRelationType RelationType(string value) => value.Trim().ToLowerInvariant() switch
    {
        "complement" => ArcRelationType.Complement,
        "uses" => ArcRelationType.Uses,
        "extends" => ArcRelationType.Extends,
        "alternative" => ArcRelationType.Alternative,
        _ => ArcRelationType.Alternative
    };

    private static string String(JsonElement node, string property, string fallback = "") =>
        node.ValueKind == JsonValueKind.Object
        && node.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static IReadOnlyList<string> Strings(JsonElement node, string property)
    {
        var result = new List<string>();
        foreach (var item in Items(node, property))
        {
            var text = item.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(text.Trim());
            }
        }

        return result;
    }

    private static IEnumerable<JsonElement> Items(JsonElement node, string property)
    {
        if (node.ValueKind == JsonValueKind.Object
            && node.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                yield return item;
            }
        }
    }
}
