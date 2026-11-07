using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Arc.Avalonia.Controls;

/// <summary>
/// Keeps an inline overlay owner (morph select, menu hosted in the page, expanding search, popover, ...)
/// painted above the content that follows it.
/// </summary>
/// <remarks>
/// Several Arc components expand an overlay inside their own template instead of opening a popup window, so
/// that documentation screenshots capture the open state and the morph animation stays in place. That means
/// the expanded surface is an ordinary child of the surrounding layout panels: paint order is decided by the
/// ancestor panels, and a surface that overflows its host is drawn underneath the siblings that come after it.
/// While the overlay is open <see cref="SetRaised"/> raises the owner branch through every panel up to the
/// nearest scroll boundary or top level, so the expanded list stays on top; passing <c>false</c> puts the
/// recorded z-indexes back when the overlay closes.
/// </remarks>
/// <remarks>
/// Every raise gets a fresh, increasing z-index, so when several overlays are open at once (the docs preview
/// opens three selects, then the reader opens one more above them) the most recently opened one is painted on
/// top instead of tying with the previous branch at the same z-index. The state is counted per panel child, so
/// owners sharing a branch restore correctly in any close order.
/// </remarks>
public static class ArcOverlayOrder
{
    /// <summary>The first z-index handed to a raised branch; every later overlay gets one more.</summary>
    private const int BaseZIndex = 1000;

    private static readonly ConditionalWeakTable<Panel, Dictionary<Control, Entry>> Panels = new();
    private static readonly ConditionalWeakTable<Control, List<Key>> Owners = new();

    private static int _sequence = BaseZIndex;
    private static int _openOwners;

    /// <summary>
    /// Raises (or restores) <paramref name="owner"/> and its ancestor panels above their later siblings.
    /// Call it with <c>true</c> when an inline overlay opens and with <c>false</c> once the last one closes.
    /// </summary>
    /// <param name="owner">The templated control whose template hosts the inline overlay.</param>
    /// <param name="raised">Whether the inline overlay is currently open.</param>
    public static void SetRaised(Control owner, bool raised)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (raised)
        {
            Raise(owner);
        }
        else
        {
            Restore(owner);
        }
    }

    private static void Raise(Control owner)
    {
        if (Owners.TryGetValue(owner, out _))
        {
            return;
        }

        var zIndex = ++_sequence;
        var keys = new List<Key>();
        Visual? current = owner;
        while (current is Control control)
        {
            var parent = control.GetVisualParent();
            if (parent is null or ScrollViewer or TopLevel)
            {
                // Single-child (or window chrome) boundary: raising further would reorder the page against
                // the app shell instead of only against the content that follows the overlay owner.
                break;
            }

            if (parent is Panel windowRoot && windowRoot.GetVisualParent() is TopLevel)
            {
                // The owner is inside the window template itself; never reorder the window content host.
                break;
            }

            if (parent is Panel panel && panel.Children.Contains(control))
            {
                var children = Panels.GetOrCreateValue(panel);
                if (!children.TryGetValue(control, out var entry))
                {
                    entry = new Entry(control.ZIndex);
                    children[control] = entry;
                }

                entry.Raised.Add(zIndex);
                control.ZIndex = zIndex;
                keys.Add(new Key(panel, control, zIndex));
            }

            current = parent;
        }

        Owners.Add(owner, keys);
        _openOwners++;
    }

    private static void Restore(Control owner)
    {
        if (!Owners.TryGetValue(owner, out var keys))
        {
            return;
        }

        Owners.Remove(owner);
        for (var index = keys.Count - 1; index >= 0; index--)
        {
            var key = keys[index];
            if (!Panels.TryGetValue(key.Panel, out var children) ||
                !children.TryGetValue(key.Child, out var entry))
            {
                continue;
            }

            entry.Raised.Remove(key.ZIndex);
            if (entry.Raised.Count > 0)
            {
                // Another still-open overlay shares this branch: fall back to the newest remaining raise.
                key.Child.ZIndex = entry.Raised[^1];
                continue;
            }

            children.Remove(key.Child);
            if (key.Panel.Children.Contains(key.Child))
            {
                key.Child.ZIndex = entry.Original;
            }
        }

        if (--_openOwners <= 0)
        {
            // No inline overlay is open any more; restart the stacking range.
            _openOwners = 0;
            _sequence = BaseZIndex;
        }
    }

    private sealed class Entry
    {
        public Entry(int original) => Original = original;

        public int Original { get; }

        /// <summary>Z-indexes of the currently open owners, in raise order (ascending).</summary>
        public List<int> Raised { get; } = new();
    }

    private readonly record struct Key(Panel Panel, Control Child, int ZIndex);
}
