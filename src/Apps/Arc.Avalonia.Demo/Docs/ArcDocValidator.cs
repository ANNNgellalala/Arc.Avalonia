using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Demo;

/// <summary>One documentation defect found by <see cref="ArcDocValidator"/>.</summary>
/// <param name="Slug">Catalog slug the issue belongs to, or the doc resource when the slug is unknown.</param>
/// <param name="Code">Stable short code, useful for filtering and tests.</param>
/// <param name="Message">Chinese description of what is missing or inconsistent.</param>
/// <param name="IsError">True blocks the documentation build; false is a warning shown in the coverage page.</param>
public sealed record ArcDocIssue(string Slug, string Code, string Message, bool IsError);

/// <summary>
/// Checks the documentation payload against the catalog: required sections, relation targets, contrast
/// targets, declared preview states and the API table declaration. Data-only checks live here (no Avalonia
/// UI is built); page build failures and reflected API tables are checked by the gallery audit.
/// </summary>
public static class ArcDocValidator
{
    private static readonly HashSet<string> ExampleLanguages = new(StringComparer.OrdinalIgnoreCase)
    {
        "xaml", "xml", "csharp", "cs", "bash", "json"
    };

    /// <summary>Validates every catalog entry and every page instance.</summary>
    public static IReadOnlyList<ArcDocIssue> Validate()
    {
        var issues = new List<ArcDocIssue>();
        var pages = ItemPages();

        foreach (var error in ArcDocStore.Errors)
        {
            issues.Add(new ArcDocIssue(error.Source, "doc.load", error.Message, true));
        }

        var known = new HashSet<string>(ArcCatalog.AllItems.Select(item => item.Slug), StringComparer.Ordinal);
        foreach (var slug in ArcDocStore.All.Keys.Where(slug => !known.Contains(slug)))
        {
            issues.Add(new ArcDocIssue(slug, "doc.unknown", "文档对应的 slug 不在目录中", true));
        }

        foreach (var item in ArcCatalog.AllItems)
        {
            var doc = ArcDocStore.Get(item.Slug);
            if (doc is null)
            {
                issues.Add(new ArcDocIssue(item.Slug, "doc.missing", "缺少中文文档（Docs/" + item.Slug + ".json）", true));
            }
            else
            {
                ValidateContent(item, doc, issues);
            }

            if (item.Kind == "component" || item.Kind == "special")
            {
                if (!pages.ContainsKey(item.Slug))
                {
                    issues.Add(new ArcDocIssue(item.Slug, "page.missing", "缺少条目页（ArcItemPage 子类）", true));
                }
            }
        }

        foreach (var (slug, page) in pages)
        {
            ValidatePage(slug, page, issues);
        }

        return issues
            .OrderBy(issue => issue.Slug, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Validates one slug; useful from a focus mode or a single-item test.</summary>
    public static IReadOnlyList<ArcDocIssue> Validate(string slug)
    {
        var issues = new List<ArcDocIssue>();
        var item = ArcCatalog.Get(slug);
        if (item is null)
        {
            issues.Add(new ArcDocIssue(slug, "catalog.missing", "slug 不在目录中", true));
            return issues;
        }

        var doc = ArcDocStore.Get(slug);
        if (doc is null)
        {
            issues.Add(new ArcDocIssue(slug, "doc.missing", "缺少中文文档", true));
        }
        else
        {
            ValidateContent(item, doc, issues);
        }

        if (ItemPages().TryGetValue(slug, out var page))
        {
            ValidatePage(slug, page, issues);
        }

        return issues;
    }

    /// <summary>Item pages keyed by slug. Missing slugs are reported by the caller.</summary>
    public static IReadOnlyDictionary<string, ArcItemPage> ItemPages()
    {
        var pages = new Dictionary<string, ArcItemPage>(StringComparer.Ordinal);
        foreach (var page in ArcPageRegistry.Discover())
        {
            if (page is ArcItemPage item && !pages.ContainsKey(item.ItemSlug))
            {
                pages[item.ItemSlug] = item;
            }
        }

        return pages;
    }

    private static void ValidateContent(ArcCatalogItem item, ArcDoc doc, List<ArcDocIssue> issues)
    {
        var isBlock = string.Equals(item.Kind, "block", StringComparison.Ordinal);

        void Error(string code, string message) => issues.Add(new ArcDocIssue(item.Slug, code, message, true));
        void Warn(string code, string message) => issues.Add(new ArcDocIssue(item.Slug, code, message, false));

        if (!string.Equals(doc.Slug, item.Slug, StringComparison.Ordinal))
        {
            Error("doc.slug", $"文档 slug \"{doc.Slug}\" 与目录 slug \"{item.Slug}\" 不一致");
        }

        if (string.IsNullOrWhiteSpace(doc.NameZh))
        {
            Warn("doc.nameZh", "缺少中文名");
        }

        if (string.IsNullOrWhiteSpace(doc.Summary))
        {
            Error("doc.summary", "缺少一句话摘要");
        }
        else if (!ContainsCjk(doc.Summary))
        {
            Warn("doc.summary.lang", "摘要没有中文内容");
        }

        if (doc.Purpose.Count == 0)
        {
            Error("doc.purpose", "缺少用途说明");
        }
        else if (!doc.Purpose.Any(ContainsCjk))
        {
            Warn("doc.purpose.lang", "用途说明没有中文内容");
        }

        if (doc.WhenToUse.Count < 2)
        {
            Error("doc.whenToUse", "「何时使用」至少需要 2 条");
        }

        if (doc.WhenNotToUse.Count < 2)
        {
            Error("doc.whenNotToUse", "「何时不用」至少需要 2 条");
        }

        if (doc.Variants.Count == 0)
        {
            Error("doc.variants", "没有声明任何变体/状态（预览完整性无法校验）");
        }

        var variantKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variant in doc.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.Key) || string.IsNullOrWhiteSpace(variant.Label))
            {
                Error("doc.variants.entry", "变体缺少 key 或 label");
            }
            else if (!variantKeys.Add(variant.Key))
            {
                Error("doc.variants.duplicate", $"变体 key 重复：{variant.Key}");
            }
        }

        // Relations.
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var relation in doc.Relations)
        {
            if (string.IsNullOrWhiteSpace(relation.Target))
            {
                Error("doc.relations.target", "关系缺少 target");
                continue;
            }

            if (string.Equals(relation.Target, item.Slug, StringComparison.Ordinal))
            {
                Error("doc.relations.self", "关系不能指向自己");
                continue;
            }

            if (ArcCatalog.Get(relation.Target) is null)
            {
                Error("doc.relations.dangling", $"关系目标不存在：{relation.Target}");
                continue;
            }

            if (!targets.Add(relation.Type + "/" + relation.Target))
            {
                Warn("doc.relations.duplicate", $"重复关系：{relation.Type} → {relation.Target}");
            }

            if (string.IsNullOrWhiteSpace(relation.Note))
            {
                Warn("doc.relations.note", $"关系 {relation.Target} 缺少一句话说明");
            }
        }

        if (doc.Relations.Count == 0)
        {
            Error("doc.relations.empty", "没有任何组件关系");
        }

        if (isBlock && doc.Relations.Count(relation => relation.Type == ArcRelationType.Uses) < 3)
        {
            Error("doc.composition", "区块需要至少 3 条 uses 关系作为组成依赖");
        }

        // Contrasts.
        if (doc.Contrasts.Count is < 1 or > 3)
        {
            Error("doc.contrasts", "「与相似组件的区别」需要 1–3 组");
        }

        foreach (var contrast in doc.Contrasts)
        {
            if (ArcCatalog.Get(contrast.Target) is null)
            {
                Error("doc.contrasts.dangling", $"对照目标不存在：{contrast.Target}");
                continue;
            }

            if (contrast.Rows.Count < 3)
            {
                Error("doc.contrasts.rows", $"对照「{contrast.Target}」至少需要 3 个维度");
            }

            if (!targets.Any(target => target.EndsWith("/" + contrast.Target, StringComparison.Ordinal)))
            {
                Error("doc.contrasts.relation", $"对照「{contrast.Target}」没有对应的关系声明");
            }

            foreach (var row in contrast.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.Dimension)
                    || string.IsNullOrWhiteSpace(row.Self)
                    || string.IsNullOrWhiteSpace(row.Other))
                {
                    Error("doc.contrasts.row", $"对照「{contrast.Target}」存在空单元格");
                }
            }
        }

        // Examples.
        if (doc.Examples.Count == 0)
        {
            Error("doc.examples", "缺少用法示例");
        }

        foreach (var example in doc.Examples)
        {
            if (string.IsNullOrWhiteSpace(example.Code) || string.IsNullOrWhiteSpace(example.Title))
            {
                Error("doc.examples.entry", "示例缺少 title 或 code");
            }

            if (!ExampleLanguages.Contains(example.Language))
            {
                Warn("doc.examples.language", $"示例语言未知：{example.Language}");
            }
        }

        // API declaration: components and special components document a control type; blocks do not.
        if (isBlock)
        {
            if (doc.Api.IsDeclared)
            {
                Warn("doc.api.block", "区块不应声明 api.type（组成依赖来自 uses 关系）");
            }
        }
        else if (!doc.Api.IsDeclared)
        {
            var severity = item.Kind == "component";
            issues.Add(new ArcDocIssue(item.Slug, "doc.api", "缺少 api.type（无法生成属性表）", severity));
        }
        else if (ResolveType(doc.Api.Type!) is null)
        {
            Error("doc.api.type", $"api.type 无法解析：{doc.Api.Type}");
        }
    }

    private static void ValidatePage(string slug, ArcItemPage page, List<ArcDocIssue> issues)
    {
        var doc = ArcDocStore.Get(slug);
        if (doc is null || doc.Variants.Count == 0)
        {
            return;
        }

        var declared = new HashSet<string>(doc.Variants.Select(variant => variant.Key), StringComparer.Ordinal);
        var implemented = new HashSet<string>(page.ImplementedStates, StringComparer.Ordinal);
        foreach (var missing in declared.Where(key => !implemented.Contains(key)))
        {
            issues.Add(new ArcDocIssue(slug, "preview.missing", $"预览未实现声明的状态：{missing}", true));
        }

        foreach (var extra in implemented.Where(key => !declared.Contains(key)))
        {
            issues.Add(new ArcDocIssue(slug, "preview.extra", $"预览实现了未声明的状态：{extra}", false));
        }
    }

    /// <summary>Resolves a control type from the loaded Arc assemblies; null when it does not exist.</summary>
    public static Type? ResolveType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null || !name.StartsWith("Arc.Avalonia", StringComparison.Ordinal))
            {
                continue;
            }

            var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type is not null)
            {
                return type;
            }
        }

        return null;
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var character in value)
        {
            if (character is >= '\u4E00' and <= '\u9FFF'
                or >= '\u3400' and <= '\u4DBF'
                or >= '\uF900' and <= '\uFAFF')
            {
                return true;
            }
        }

        return false;
    }
}
