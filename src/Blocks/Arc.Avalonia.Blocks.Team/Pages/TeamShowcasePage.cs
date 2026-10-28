using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Feedback.Controls;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Blocks.Team.Pages;

/// <summary>
/// Team showcase: an image-led introduction with profiles that open in place. The gallery and spotlight
/// layouts share the same sample people; selection is immediate and the detail strip stays in the block.
/// Portraits are gradient and initial stand-ins, sized for licensed photography in a real deployment.
/// </summary>
public sealed class TeamShowcasePage : ArcItemPage
{
    public override string ItemSlug => "team-showcase";

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "About or company pages that introduce the team with portraits.",
        "Sections that need a gallery grid or a single spotlight profile from the same data."
    };

    public override IReadOnlyList<string>? WhenNotToUse => new[]
    {
        "Use team-directory for an internal, searchable people list, and testimonial-stage for customer quotes."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each person is a pressed-state button with a portrait label; the in-place detail is announced as it changes.",
        "Copy actions are labelled per person, for example \"Copy Maya's email\"."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Profiles expand in place with a short crossfade; the spotlight and gallery layouts cross-fade when switched.",
        "Reduced motion removes the offsets and uses instant swaps, matching Arc's reduced-motion branch."
    };

    public override string? ImplementationNotes =>
        "Gallery and spotlight are implemented; filmstrip and directory variants share the same card code and are " +
        "left out of the 950 px preview. Portraits use the brand gradient with initials instead of licensed " +
        "photography, and the sample people are the same ones used by team-directory. No Pro source was consulted.";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var showcase = new[] { "Maya Chen", "Tomas Novak", "Priya Raman", "Alice Moreau" }
            .Select(TeamData.Person)
            .ToList();
        var selected = showcase[0];
        var variant = "gallery";

        var stage = new Border();
        var detail = new Border();

        void RenderDetail()
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            grid.Children.Add(TeamKit.Avatar(selected.Name, ArcAvatarSize.Md, ArcAvatarStatus.Online));
            var copy = BuildDetailCopy(selected);
            copy.Margin = new Thickness(12, 0, 12, 0);
            Grid.SetColumn(copy, 1);
            grid.Children.Add(copy);
            var button = CopyFor(selected, "Copy email", ArcButtonVariant.Secondary);
            Grid.SetColumn(button, 2);
            grid.Children.Add(button);
            detail.Child = grid;
        }

        void RenderStage()
        {
            if (variant == "spotlight")
            {
                var image = TeamKit.Portrait(selected, 332, 236, 20, 56);
                var copy = new StackPanel { Spacing = 12 };
                copy.Children.Add(TeamKit.Caption("Spotlight"));
                copy.Children.Add(new TextBlock { Text = selected.Name, Classes = { "arc-subtitle" } });
                copy.Children.Add(TeamKit.Label(selected.Role));
                copy.Children.Add(TeamKit.Body(selected.Focus, 460));
                copy.Children.Add(TeamKit.Row(14,
                    TeamKit.Meta("map-pin", $"{selected.Location} · {selected.Timezone}"),
                    TeamKit.Meta("mail", selected.Email)));
                copy.Children.Add(TeamKit.Row(8,
                    CopyFor(selected, "Copy email", ArcButtonVariant.Secondary),
                    TeamKit.Btn("Open profile", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "arrow-up-right")));

                stage.Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("332,*"),
                    Children =
                    {
                        image,
                        new Border { Padding = new Thickness(22, 6, 0, 0), Child = copy }
                    }
                };
                if (stage.Child is Grid grid)
                {
                    Grid.SetColumn(grid.Children[1], 1);
                }

                return;
            }

            var cards = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            for (var index = 0; index < showcase.Count; index++)
            {
                var person = showcase[index];
                var card = TeamKit.Selectable(TeamKit.Stack(10,
                    TeamKit.Portrait(person, 220, 150, 16, 36),
                    new StackPanel
                    {
                        Spacing = 1,
                        Children =
                        {
                            new TextBlock { Text = person.Name, Classes = { "arc-label" } },
                            TeamKit.Caption(person.Role)
                        }
                    }), () =>
                {
                    selected = person;
                    RenderStage();
                    RenderDetail();
                }, person == selected, $"{person.Name}, {person.Role}", 16, new Thickness(6));

                card.Margin = new Thickness(index == 0 ? 0 : 8, 0, index == showcase.Count - 1 ? 0 : 8, 0);
                Grid.SetColumn(card, index);
                cards.Children.Add(card);
            }

            stage.Child = cards;
        }

        var variantSwitch = TeamKit.Segmented(new[] { ("gallery", "Gallery"), ("spotlight", "Spotlight") },
            "gallery", value =>
            {
                variant = value;
                RenderStage();
            }, "Layout");

        var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        heading.Children.Add(new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock { Text = "The people behind Northwind", Classes = { "arc-subtitle" } },
                TeamKit.Body("A small team in six cities, building a calmer way to run projects together.", 520)
            }
        });
        var right = TeamKit.Row(10,
            variantSwitch,
            TeamKit.Btn("We're hiring", ArcButtonVariant.Ghost, ArcButtonSize.Sm, icon: "arrow-up-right"));
        Grid.SetColumn(right, 1);
        heading.Children.Add(right);

        RenderStage();
        RenderDetail();

        var content = TeamKit.Stack(18,
            heading,
            stage,
            TeamKit.Divider(),
            detail);

        return TeamKit.Root(new Border
        {
            Background = TeamKit.Brush(ArcTokens.Surface),
            BorderBrush = TeamKit.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(26),
            ClipToBounds = true,
            Padding = new Thickness(24),
            Child = content
        });
    }

    private static Control BuildDetailCopy(TeamPerson person)
    {
        return new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock { Text = person.Focus, Classes = { "arc-body-sm" } },
                TeamKit.Caption($"{person.Location} · {person.Timezone} · joined 2023")
            }
        };
    }

    private static ArcCopyButton CopyFor(TeamPerson person, string label, ArcButtonVariant variant)
    {
        var copy = new ArcCopyButton
        {
            Value = person.Email,
            Label = label,
            CopiedLabel = "Copied",
            Variant = variant,
            Size = ArcButtonSize.Sm,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(copy, $"Copy {person.FirstName}'s email");
        return copy;
    }
}
