using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Demo;

/// <summary>
/// The shared documentation layout for one catalog item: the same sections in the same order on every page,
/// matching the structure of uiarc.dev's component documentation (title, description, when to use, demo,
/// accessibility, motion). Consistency here is deliberate: someone moving from the React docs should find
/// the same information in the same place.
/// </summary>
public static class ArcDocLayout
{
    public static Control Build(ArcItemPage page, Control demo)
    {
        var item = page.Item;
        var root = new StackPanel { Spacing = 0 };

        // Breadcrumb: section / category, with the tier beside it.
        var breadcrumb = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        breadcrumb.Children.Add(new TextBlock
        {
            Text = $"{item.KindLabel} / {item.Category}",
            Classes = { "arc-caption" }
        });
        breadcrumb.Children.Add(new ArcBadge
        {
            Content = item.IsPro ? "Pro" : "Free",
            Tone = item.IsPro ? ArcBadgeTone.Accent : ArcBadgeTone.Neutral
        });
        root.Children.Add(breadcrumb);

        // Title row with the link to the official documentation.
        var titleRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        titleRow.Children.Add(new TextBlock
        {
            Text = page.Title,
            Classes = { "arc-title-sm" },
            VerticalAlignment = VerticalAlignment.Center
        });
        var docs = new ArcButton
        {
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children =
                {
                    new ArcIcon { Icon = "external-link", Size = 14 },
                    new TextBlock { Text = "uiarc.dev", FontSize = 13 }
                }
            }
        };
        docs.Click += async (_, _) =>
        {
            try
            {
                if (TopLevel.GetTopLevel(docs) is { } topLevel && !string.IsNullOrWhiteSpace(item.Docs))
                {
                    await topLevel.Launcher.LaunchUriAsync(new Uri(item.Docs));
                }
            }
            catch
            {
                // Opening a browser is best effort; the docs URL is also printed in the footer note.
            }
        };
        Grid.SetColumn(docs, 1);
        titleRow.Children.Add(docs);
        root.Children.Add(titleRow);

        if (!string.IsNullOrWhiteSpace(page.Description))
        {
            root.Children.Add(new TextBlock
            {
                Text = page.Description,
                Classes = { "arc-body", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 800,
                Margin = new Thickness(0, 10, 0, 0)
            });
        }

        AddBulletSection(root, "When to use", page.WhenToUse, "arc-body-sm");
        AddBulletSection(root, "When not to use", page.WhenNotToUse, "arc-body-sm");

        // Preview section.
        var preview = new StackPanel { Spacing = 12, Margin = new Thickness(0, 26, 0, 0) };
        preview.Children.Add(SectionLabel("Preview"));
        preview.Children.Add(page.DemoOnCanvas
            ? DemoUi.Canvas(demo)
            : demo);
        root.Children.Add(preview);

        AddBulletSection(root, "Keyboard and accessibility", page.KeyboardAndAccessibility, "arc-body-sm");
        AddBulletSection(root, "Motion", page.MotionNotes, "arc-body-sm");

        if (!string.IsNullOrWhiteSpace(page.ImplementationNotes))
        {
            var notes = new StackPanel { Spacing = 6, Margin = new Thickness(0, 22, 0, 0) };
            notes.Children.Add(SectionLabel("Implementation notes"));
            notes.Children.Add(new TextBlock
            {
                Text = page.ImplementationNotes,
                Classes = { "arc-body-sm", "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 800
            });
            root.Children.Add(notes);
        }

        root.Children.Add(new TextBlock
        {
            Text = item.IsPro
                ? $"Recreated independently from Arc's public documentation: {item.Docs}. Pro source was not used."
                : $"Recreated from Arc's public documentation: {item.Docs}.",
            Classes = { "arc-caption" },
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 800,
            Margin = new Thickness(0, 22, 0, 0)
        });

        return root;
    }

    private static void AddBulletSection(StackPanel root, string title, IReadOnlyList<string>? bullets, string textClass)
    {
        if (bullets is null || bullets.Count == 0)
        {
            return;
        }

        var section = new StackPanel { Spacing = 7, Margin = new Thickness(0, 22, 0, 0) };
        section.Children.Add(SectionLabel(title));
        foreach (var bullet in bullets)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(new TextBlock
            {
                Text = "•",
                Classes = { "arc-body-sm", "arc-muted" },
                Margin = new Thickness(0, 0, 8, 0)
            });
            var text = new TextBlock
            {
                Text = bullet,
                Classes = { textClass, "arc-secondary" },
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxWidth = 800
            };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            section.Children.Add(row);
        }

        root.Children.Add(section);
    }

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        Classes = { "arc-label" }
    };
}


