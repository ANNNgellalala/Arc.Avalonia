using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Demo;

/// <summary>
/// Base class for a single catalog item page (a component, special component or block). Documentation text
/// lives in the item's embedded <c>Docs/&lt;slug&gt;.json</c>; the page class only owns behaviour, which is the
/// completeness preview: every variant, size and state declared by the document must be implemented here.
/// </summary>
public abstract class ArcItemPage : ArcPage
{
    private ArcCatalogItem? _item;
    private ArcDoc? _doc;

    /// <summary>Catalog slug, for example "button", "share-sheet" or "site-header".</summary>
    public abstract string ItemSlug { get; }

    public ArcCatalogItem Item =>
        _item ??= ArcCatalog.Get(ItemSlug)
                  ?? throw new InvalidOperationException($"Unknown catalog item '{ItemSlug}'");

    /// <summary>The Chinese documentation payload; null until the item's Docs/&lt;slug&gt;.json is authored.</summary>
    public ArcDoc? Doc => _doc ??= ArcDocStore.Get(ItemSlug);

    public override string Title => Item.Title;

    public override string Group => Item.Category;

    public override string? Description => Item.Description;

    public override int Order => 10000 + ArcCatalog.OrderOf(ItemSlug);

    /// <summary>Preview state keys this page actually implements. Must match the document's <c>variants[].key</c>
    /// exactly; the audit reports a missing state as an error and an extra state as a warning.</summary>
    public virtual IReadOnlyList<string> ImplementedStates => Array.Empty<string>();

    // The five members below are the pre-documentation English fields. They are kept so the 166 item pages
    // authored against the old layout keep compiling during the migration; the Chinese documentation layout
    // reads Docs/<slug>.json instead. They will be removed once every page has migrated.

    /// <summary>Legacy English guidance; superseded by the document's why-to-use section.</summary>
    public virtual IReadOnlyList<string>? WhenToUse => null;

    /// <summary>Legacy English guidance; superseded by the document's why-not-to-use section.</summary>
    public virtual IReadOnlyList<string>? WhenNotToUse => null;

    /// <summary>Legacy accessibility notes; not rendered by the Chinese layout.</summary>
    public virtual IReadOnlyList<string>? KeyboardAndAccessibility => null;

    /// <summary>Legacy motion notes; not rendered by the Chinese layout.</summary>
    public virtual IReadOnlyList<string>? MotionNotes => null;

    /// <summary>Legacy implementation note; the Chinese layout keeps a one-line port note in the footer.</summary>
    public virtual string? ImplementationNotes => null;

    /// <summary>Wraps the demo on the muted canvas so surfaces read clearly. Blocks usually want false.</summary>
    public virtual bool DemoOnCanvas => true;

    /// <summary>Builds the completeness preview: all variants, sizes and states in one composition.</summary>
    protected abstract Control BuildDemo(ArcDemoContext context);

    /// <summary>Public entry point used by the gallery documentation layout.</summary>
    public virtual Control BuildPreview(ArcDemoContext context) => BuildDemo(context);

    /// <summary>
    /// Legacy page build, kept so the screenshot harness can still render a page outside the documentation
    /// shell during the migration. The gallery itself renders <see cref="BuildPreview"/> inside its layout.
    /// </summary>
    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Spacing = 24,
        Children =
        {
            new TextBlock { Text = Item.Title, Classes = { "arc-title-sm" } },
            new TextBlock
            {
                Text = Item.Description,
                Classes = { "arc-body", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 800
            },
            BuildPreview(context)
        }
    };
}
