using System;
using System.Collections.Generic;
using System.Linq;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Gallery;

/// <summary>One search result: the catalog entry plus the text that matched, for the sidebar list.</summary>
public sealed record DocHit(ArcCatalogItem Item, string Snippet, int Score);

/// <summary>
/// In-memory search over the catalog and the Chinese documentation. Chinese has no word boundaries, so the
/// query is matched as a case-insensitive substring against titles, names and prose; results are ranked
/// title &gt; Chinese name &gt; summary &gt; body. No tokenizer, no third-party index.
/// </summary>
public static class DocSearch
{
    private static IReadOnlyList<DocHit>? _emptyQueryCache;

    public static IReadOnlyList<DocHit> Query(string? query, string tier)
    {
        var text = (query ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return Array.Empty<DocHit>();
        }

        var hits = new List<DocHit>();
        foreach (var item in ArcCatalog.AllItems)
        {
            if (!PassesTier(item, tier))
            {
                continue;
            }

            var doc = ArcDocStore.Get(item.Slug);
            var score = 0;
            var snippet = string.Empty;

            if (item.Title.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                score = 100;
                snippet = item.Description;
            }
            else if (doc is not null && doc.NameZh.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                score = 90;
                snippet = doc.Summary;
            }
            else if (doc is not null && doc.Summary.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                score = 80;
                snippet = doc.Summary;
            }
            else if (doc is not null && doc.Purpose.Any(paragraph =>
                         paragraph.Contains(text, StringComparison.OrdinalIgnoreCase)))
            {
                score = 60;
                snippet = doc.Purpose.First(paragraph =>
                    paragraph.Contains(text, StringComparison.OrdinalIgnoreCase));
            }
            else if (doc is not null && SnippetFrom(doc, text) is { } body)
            {
                score = 40;
                snippet = body;
            }
            else if (item.Slug.Contains(text, StringComparison.OrdinalIgnoreCase)
                     || item.Category.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                score = 30;
                snippet = item.Category;
            }
            else
            {
                continue;
            }

            hits.Add(new DocHit(item, Shorten(snippet, text), score));
        }

        return hits
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Item.Title, StringComparer.Ordinal)
            .ToList();
    }

    public static bool PassesTier(ArcCatalogItem item, string tier) => tier switch
    {
        "free" => !item.IsPro,
        "pro" => item.IsPro,
        _ => true
    };

    private static string? SnippetFrom(ArcDoc doc, string text)
    {
        foreach (var bullet in doc.WhenToUse.Concat(doc.WhenNotToUse))
        {
            if (bullet.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return bullet;
            }
        }

        foreach (var relation in doc.Relations)
        {
            if (relation.Note.Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                return relation.Note;
            }
        }

        return null;
    }

    private static string Shorten(string text, string query)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index <= 30)
        {
            return text.Length <= 90 ? text : text[..90] + "…";
        }

        var start = index - 24;
        return "…" + text.Substring(start, Math.Min(90, text.Length - start));
    }
}
