using Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Demo;

/// <summary>
/// Base class for a single catalog item page (a component, special component or block). Every page built
/// from this base renders the same documentation structure: breadcrumb, title with tier, one-line
/// description, when-to-use guidance, the demo, accessibility and motion notes, and a link to the official
/// markdown. That consistency is what keeps the port approachable for people who know the React docs.
/// </summary>
public abstract class ArcItemPage : ArcPage
{
    private ArcCatalogItem? _item;

    /// <summary>Catalog slug, for example "button", "share-sheet" or "site-header".</summary>
    public abstract string ItemSlug { get; }

    public ArcCatalogItem Item =>
        _item ??= ArcCatalog.Get(ItemSlug)
                  ?? throw new InvalidOperationException($"Unknown catalog item '{ItemSlug}'");

    public override string Title => Item.Title;

    public override string Group => Item.Category;

    public override string? Description => Item.Description;

    public override int Order => 10000 + ArcCatalog.OrderOf(ItemSlug);

    /// <summary>Bullets shown under "When to use"; fill from the public markdown where available.</summary>
    public virtual IReadOnlyList<string>? WhenToUse => null;

    /// <summary>Optional "When not to use" guidance.</summary>
    public virtual IReadOnlyList<string>? WhenNotToUse => null;

    /// <summary>Keyboard and accessibility notes for this item.</summary>
    public virtual IReadOnlyList<string>? KeyboardAndAccessibility => null;

    /// <summary>Motion notes for this item.</summary>
    public virtual IReadOnlyList<string>? MotionNotes => null;

    /// <summary>Anything deliberately simplified versus the reference implementation.</summary>
    public virtual string? ImplementationNotes => null;

    /// <summary>Wraps the demo on the muted canvas so surfaces read clearly. Blocks usually want false.</summary>
    public virtual bool DemoOnCanvas => true;

    /// <summary>Builds the interactive demo for this item.</summary>
    protected abstract Control BuildDemo(ArcDemoContext context);

    public sealed override Control Build(ArcDemoContext context) =>
        ArcDocLayout.Build(this, BuildDemo(context));
}


