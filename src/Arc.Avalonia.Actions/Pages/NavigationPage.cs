using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>Tabs, breadcrumb, pagination and the accordion: moving through views and content.</summary>
public sealed class NavigationPage : ArcPage
{
    public override string Title => "Navigation";

    public override string Group => "Actions";

    public override string? Description =>
        "Moving between peer views, showing where a page sits in a hierarchy, paging a long collection, " +
        "and revealing answers in place.";

    public override int Order => 202;

    public override Control Build(ArcDemoContext context) => new StackPanel
    {
        Children =
        {
            ActionDemos.Section(
                "Tabs",
                "Peer views of the same object; the selection pill glides between triggers and the panel swaps in place.",
                TabsDemo()),
            ActionDemos.Section(
                "Breadcrumb",
                "Where the page sits in a hierarchy. The last crumb is the current page and is never a link.",
                BreadcrumbDemo()),
            ActionDemos.Section(
                "Pagination",
                "Long collections, five pages at a time with the current page in the accent and the ends always reachable.",
                PaginationDemo()),
            ActionDemos.Section(
                "Accordion",
                "Questions and answers revealed in place, one row at a time, with height and opacity moving together.",
                AccordionDemo())
        }
    };

    private static Control TabsDemo()
    {
        var tabs = new ArcTabs
        {
            Label = "Project sections",
            SelectedIndex = 1,
            Items = new[]
            {
                new ArcTabItem
                {
                    Value = "overview",
                    Header = "Overview",
                    Content = ActionDemos.Body("Twelve projects, four reviewers, and a release on Friday.")
                },
                new ArcTabItem
                {
                    Value = "activity",
                    Header = "Activity",
                    Content = ActionDemos.Body("Maya merged the checkout branch, then requested a review on the pricing page.")
                },
                new ArcTabItem
                {
                    Value = "settings",
                    Header = "Settings",
                    Icon = "settings",
                    Content = ActionDemos.Body("Notifications go to the channel, not to email. Only owners can archive.")
                }
            }
        };

        return new Border
        {
            Padding = new Thickness(20),
            CornerRadius = new CornerRadius(26),
            Background = DemoUi.Brush(ArcTokens.Surface),
            BorderBrush = DemoUi.Brush(ArcTokens.Border),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = tabs
        };
    }

    private static Control BreadcrumbDemo() => new ArcBreadcrumb
    {
        Items = new[]
        {
            new ArcBreadcrumbItem("Workspace"),
            new ArcBreadcrumbItem("Settings"),
            new ArcBreadcrumbItem("Billing"),
            new ArcBreadcrumbItem("Invoices")
        }
    };

    private static Control PaginationDemo() => new ArcPagination
    {
        Page = 4,
        PageCount = 12
    };

    private static Control AccordionDemo() => new ArcAccordion
    {
        Label = "Billing questions",
        DefaultOpen = 0,
        Items = new[]
        {
            new ArcAccordionItem
            {
                Title = "How is usage counted?",
                Content = "Every request counts once, including retries. Cached responses are free, and the " +
                          "dashboard totals them separately so a spike is easy to explain."
            },
            new ArcAccordionItem
            {
                Title = "Can I change plan mid-cycle?",
                Content = "Yes. The difference is prorated to the day, and the next invoice shows both lines."
            },
            new ArcAccordionItem
            {
                Title = "Where do invoices go?",
                Content = "Billing contacts receive the invoice and the receipt, and owners can download both."
            }
        }
    };
}
