using System;
using System.Collections.Generic;
using System.Linq;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Gallery;

/// <summary>
/// Documentation audit: every catalog entry must have a Chinese document and an item page, required sections
/// must be present, relations and contrasts must resolve, preview states must match the document, and every
/// page must build. Run with `dotnet Arc.Avalonia.Gallery.dll --audit`; exit code 0 means clean.
/// </summary>
public static class CatalogAudit
{
    public static int Run()
    {
        var issues = ArcDocValidator.Validate();
        var errors = issues.Where(issue => issue.IsError).ToList();
        var warnings = issues.Where(issue => !issue.IsError).ToList();

        var docPages = ArcDocValidator.ItemPages();
        var failures = new List<string>();
        var context = new ArcDemoContext(ArcColorScheme.Light, ArcAccent.Neutral);
        foreach (var page in docPages.Values)
        {
            try
            {
                _ = page.BuildPreview(context);
            }
            catch (Exception exception)
            {
                failures.Add($"{page.ItemSlug}: {exception.GetType().Name}: {exception.Message}");
            }
        }

        var withDocs = ArcCatalog.AllItems.Count(item => ArcDocStore.Get(item.Slug) is not null);
        Console.WriteLine(
            $"CATALOG AUDIT: items={ArcCatalog.AllItems.Count} docs={withDocs} pages={docPages.Count} " +
            $"errors={errors.Count} warnings={warnings.Count} pageFailures={failures.Count}");

        foreach (var group in errors.GroupBy(issue => issue.Code).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"AUDIT ERROR {group.Key}: {group.Count()}");
            foreach (var issue in group.Take(4))
            {
                Console.WriteLine($"  {issue.Slug}: {issue.Message}");
            }
        }

        foreach (var group in warnings.GroupBy(issue => issue.Code).OrderByDescending(group => group.Count()))
        {
            Console.WriteLine($"AUDIT WARN {group.Key}: {group.Count()}");
        }

        foreach (var error in ArcDocStore.Errors)
        {
            Console.WriteLine($"AUDIT DOC LOAD {error.Source}: {error.Message}");
        }

        foreach (var failure in failures.Take(20))
        {
            Console.WriteLine("AUDIT PAGE FAILED " + failure);
        }

        var ok = errors.Count == 0 && failures.Count == 0 && ArcDocStore.Errors.Count == 0;
        Console.WriteLine(ok ? "AUDIT OK" : "AUDIT FAILED");
        return ok ? 0 : 1;
    }
}
