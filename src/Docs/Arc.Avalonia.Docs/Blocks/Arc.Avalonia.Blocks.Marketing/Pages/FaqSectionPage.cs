using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Inputs.Controls;
using Arc.Avalonia.Theme;
using Mk = Arc.Avalonia.Blocks.Marketing.MarketingKit;

namespace Arc.Avalonia.Blocks.Marketing.Pages;

/// <summary>
/// FAQ section: a topic filter and a search field over an accordion, with one answer open at a time. The
/// reference also offers a topic rail and a searchable list that highlights matches; the specimen folds both
/// into one specimen that filters in place and reports the match count. Answers are sample copy.
/// </summary>
public sealed class FaqSectionPage : ArcItemPage
{
    public override string ItemSlug => "faq-section";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-open", "variant-topic-filter", "variant-search", "state-empty"
    };

    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "Answering the questions buyers ask before a trial or a purchase.",
        "Pricing and docs pages where a searchable list beats a long scroll.",
        "Splitting questions into topics when the FAQ covers several areas."
    };

    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Each row is a button: Enter or Space opens it, and the open row keeps focus.",
        "Only one answer is open at a time, so the list never becomes a wall of text.",
        "The search field filters as you type and the match count is written out beside it."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Height and opacity move together on the smooth spring and the chevron turns with the row.",
        "Reduced motion opens rows without travel."
    };

    public override string? ImplementationNotes =>
        "Filtering is local: the topic chips and the search field rebuild the list from sample data, and no question is fetched. The accordion is the shared ArcAccordion control, so single-open behaviour, keyboard movement and the reduced-motion branch come from the component. The reference's match highlighting inside answers is not reproduced.";

    private sealed record Question(string Topic, string Title, string Answer);

    private static readonly Question[] Questions =
    {
        new("Billing", "Can I change plans later?",
            "Yes. Upgrades apply immediately and are prorated; downgrades take effect at the end of the current period, so nothing is lost mid-month."),
        new("Billing", "How does seat pricing work?",
            "Every active member counts as a seat. Invited guests and read-only viewers are free, and removing a seat credits the unused time to the next invoice."),
        new("Security", "Where is my data stored?",
            "Data lives in the EU (Frankfurt) or the US (Virginia), chosen when the workspace is created. Backups stay in the same region and are encrypted at rest."),
        new("Security", "Do you support SSO and SCIM?",
            "SAML SSO and SCIM provisioning are available on the Team plan and above, with just-in-time role mapping for Okta, Entra ID and Google."),
        new("API", "Is there an API rate limit?",
            "The public API allows 600 requests a minute per workspace and returns the remaining budget in the rate-limit headers. Bulk exports run on a separate queue.")
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var accordionHost = new ContentControl();
        var count = Mk.Caption(string.Empty);

        var query = string.Empty;
        var topic = "All";

        void Rebuild()
        {
            var matches = Questions.Where(question =>
                (topic == "All" || string.Equals(question.Topic, topic, StringComparison.Ordinal)) &&
                (string.IsNullOrWhiteSpace(query) ||
                 question.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 question.Answer.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

            count.Text = matches.Count == 1 ? "1 question" : $"{matches.Count} questions";

            if (matches.Count == 0)
            {
                accordionHost.Content = new Border
                {
                    Background = Mk.Brush(ArcTokens.SurfaceMuted),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(24),
                    Child = Mk.Stack(4,
                        Mk.Text("No questions match", "arc-body arc-medium"),
                        Mk.Body("Try another topic or a shorter search."))
                };
                return;
            }

            accordionHost.Content = new ArcAccordion
            {
                Items = matches.Select(question => new ArcAccordionItem(question.Title, question.Answer)).ToList(),
                DefaultOpen = 0,
                Label = "Frequently asked questions"
            };
        }

        var chips = Mk.Chips(
            new[] { ("All", "All"), ("Billing", "Billing"), ("Security", "Security"), ("API", "API") },
            "All",
            value =>
            {
                topic = value;
                Rebuild();
            });

        var search = new ArcSearchField { Placeholder = "Search questions", Width = 280 };
        search.ValueChanged += (_, _) =>
        {
            query = (search.Value ?? string.Empty).Trim();
            Rebuild();
        };

        Rebuild();

        var controls = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        controls.Children.Add(chips);
        Grid.SetColumn(search, 1);
        search.HorizontalAlignment = HorizontalAlignment.Right;
        search.Margin = new Thickness(16, 0, 16, 0);
        controls.Children.Add(search);
        Grid.SetColumn(count, 2);
        count.VerticalAlignment = VerticalAlignment.Center;
        controls.Children.Add(count);

        // The empty result is only reachable by filtering; a static copy keeps it visible in the preview.
        var emptySpecimen = new Border
        {
            Background = Mk.Brush(ArcTokens.SurfaceMuted),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(24),
            Child = Mk.Stack(4,
                Mk.Text("No questions match", "arc-body arc-medium"),
                Mk.Body("Try another topic or a shorter search."))
        };

        return Mk.Stack(16,
            Mk.Surface(new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 18,
                Margin = new Thickness(24, 22, 24, 20),
                Children =
                {
                    Mk.Stack(4,
                        Mk.Heading("Questions, answered", 520),
                        Mk.Body("Everything teams ask before they move their planning into Arc.")),
                    controls,
                    accordionHost
                }
            }, padding: new Thickness(0)),
            Mk.Caption("No matches for the current topic or query"),
            emptySpecimen);
    }
}

