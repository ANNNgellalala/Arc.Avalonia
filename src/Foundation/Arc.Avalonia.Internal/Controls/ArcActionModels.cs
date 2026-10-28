using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;

namespace Arc.Avalonia.Internal;

/// <summary>
/// One row in an Arc menu. Mirrors Arc's documented item shape:
/// <c>{ label, icon?, shortcut?, destructive?, disabled?, checked?, separatorBefore? }</c>.
/// </summary>
public sealed class ArcMenuItem
{
    public ArcMenuItem()
    {
    }

    public ArcMenuItem(string label, Action? action = null, string? icon = null)
    {
        Label = label;
        Action = action;
        Icon = icon;
    }

    /// <summary>Visible text and, by default, the accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>Lucide icon name drawn at 16px before the label.</summary>
    public string? Icon { get; set; }

    /// <summary>Optional keyboard hint shown at the trailing edge, such as "⌘D".</summary>
    public string? Shortcut { get; set; }

    /// <summary>Destructive items read in the danger tone and sit last.</summary>
    public bool IsDestructive { get; set; }

    /// <summary>Disabled items are dimmed and skipped by the keyboard.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Shows a check mark before the label; used by the context menu.</summary>
    public bool IsChecked { get; set; }

    /// <summary>Draws a hairline separator above this item, grouping the list.</summary>
    public bool SeparatorBefore { get; set; }

    /// <summary>Runs when the item is chosen.</summary>
    public Action? Action { get; set; }
}

/// <summary>A trailing menu attached to a button group: label plus its items.</summary>
public sealed class ArcMenuDefinition
{
    public ArcMenuDefinition()
    {
    }

    public ArcMenuDefinition(string label, params ArcMenuItem[] items)
    {
        Label = label;
        Items = items.ToList();
    }

    /// <summary>Names the chevron segment, such as "More actions".</summary>
    public string Label { get; set; } = "More actions";

    public IList<ArcMenuItem> Items { get; set; } = new List<ArcMenuItem>();
}

/// <summary>One segment of an <see cref="ArcButtonGroup"/>.</summary>
public sealed class ArcButtonGroupItem
{
    public ArcButtonGroupItem()
    {
    }

    public ArcButtonGroupItem(string label, Action? action = null, string? icon = null)
    {
        Label = label;
        Action = action;
        Icon = icon;
    }

    /// <summary>Visible text and the accessible name.</summary>
    public string? Label { get; set; }

    /// <summary>Lucide icon name drawn at 16px before the label.</summary>
    public string? Icon { get; set; }

    /// <summary>Shows only the icon; the label moves to the accessible name and tooltip.</summary>
    public bool IconOnly { get; set; }

    /// <summary>Replaces the label with a live value, such as a zoom level.</summary>
    public object? Content { get; set; }

    public bool IsDisabled { get; set; }

    public Action? Action { get; set; }
}

public enum ArcActionSwapTone
{
    Neutral,
    Accent,
    Success,
    Warning,
    Quiet
}

/// <summary>One state of an <see cref="ArcActionSwap"/>.</summary>
public sealed class ArcActionSwapState
{
    public ArcActionSwapState()
    {
    }

    public ArcActionSwapState(string id, string label, string? icon = null, ArcActionSwapTone tone = ArcActionSwapTone.Neutral)
    {
        Id = id;
        Label = label;
        Icon = icon;
        Tone = tone;
    }

    /// <summary>Stable id used by <c>Value</c> and the value-changed callback.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Visible label and accessible name.</summary>
    public string Label { get; set; } = string.Empty;

    public string? Icon { get; set; }

    public ArcActionSwapTone Tone { get; set; } = ArcActionSwapTone.Neutral;

    /// <summary>Shown while the action runs on the way into this state.</summary>
    public string? PendingLabel { get; set; }

    /// <summary>Milliseconds to hold this state before advancing on its own.</summary>
    public int HoldFor { get; set; }

    /// <summary>Runs on press. Return a task to show the working phase.</summary>
    public Func<Task>? Action { get; set; }
}

/// <summary>One panel of an <see cref="ArcTabs"/>.</summary>
public sealed class ArcTabItem
{
    public ArcTabItem()
    {
    }

    public ArcTabItem(string value, string header, object? content = null)
    {
        Value = value;
        Header = header;
        Content = content;
    }

    /// <summary>Stable id matched by <c>SelectedValue</c>.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Visible tab text.</summary>
    public string Header { get; set; } = string.Empty;

    public string? Icon { get; set; }

    /// <summary>The panel content: a control, a string, or any object for a template.</summary>
    public object? Content { get; set; }

    public bool IsDisabled { get; set; }
}

/// <summary>One crumb of an <see cref="ArcBreadcrumb"/>.</summary>
public sealed class ArcBreadcrumbItem
{
    public ArcBreadcrumbItem()
    {
    }

    public ArcBreadcrumbItem(string label, Action? action = null)
    {
        Label = label;
        Action = action;
    }

    public string Label { get; set; } = string.Empty;

    /// <summary>Runs when the crumb is followed. The last crumb is always the current page.</summary>
    public Action? Action { get; set; }
}

/// <summary>One row of an <see cref="ArcAccordion"/>.</summary>
public sealed class ArcAccordionItem
{
    public ArcAccordionItem()
    {
    }

    public ArcAccordionItem(string title, object? content = null)
    {
        Title = title;
        Content = content;
    }

    /// <summary>The trigger label.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The answer, revealed in place.</summary>
    public object? Content { get; set; }
}

/// <summary>Shorthand for a plain text panel, used by the demo pages.</summary>
internal static class ArcText
{
    public static TextBlock Body(string text, double maxWidth = 620) => new()
    {
        Text = text,
        Classes = { "arc-body-sm" },
        TextWrapping = TextWrapping.Wrap,
        MaxWidth = maxWidth
    };
}
