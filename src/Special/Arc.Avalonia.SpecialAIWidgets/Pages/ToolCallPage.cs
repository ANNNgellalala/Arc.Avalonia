using System.Collections.Generic;
using Avalonia.Controls;
using Arc.Avalonia.Demo;
using Arc.Avalonia.SpecialAIWidgets.Controls;

namespace Arc.Avalonia.SpecialAIWidgets.Pages;

/// <summary>
/// Catalog page for the tool call rows. The demo is one assistant turn with four calls in different states and
/// the GitHub search expanded so its arguments and result read at a glance.
/// </summary>
public sealed class ToolCallPage : ArcItemPage
{
    public override string ItemSlug => "tool-call";

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var group = new ArcToolCall
        {
            DefaultOpenId = "gh",
            Calls = new List<ArcToolCallData>
            {
                new()
                {
                    Id = "gh",
                    Tool = "github",
                    Title = "Searched issues in arc-ui/web",
                    RunningTitle = "Searching issues in arc-ui/web",
                    Status = ArcToolCallStatus.Success,
                    Summary = "3 results",
                    DurationSeconds = 1.2,
                    Input = new List<ArcToolArgument>
                    {
                        new() { Key = "repo", Value = "arc-ui/web" },
                        new() { Key = "query", Value = "is:open label:release-blocker" }
                    },
                    Output = new List<ArcToolListItem>
                    {
                        new() { Lead = "#1284", Title = "Checkout button stays disabled after a coupon is removed", Tag = "checkout" },
                        new() { Lead = "#1279", Title = "Command menu steals focus on open", Tag = "menu" },
                        new() { Lead = "#1268", Title = "Table row height jumps on theme swap", Tag = "table" }
                    }
                },
                new()
                {
                    Id = "sql",
                    Tool = "postgres",
                    Title = "Queried orders by plan",
                    RunningTitle = "Querying orders by plan",
                    Status = ArcToolCallStatus.Running,
                    Summary = "reading rows"
                },
                new()
                {
                    Id = "web",
                    Tool = "web",
                    Title = "Fetched the pricing page",
                    Status = ArcToolCallStatus.Error,
                    Message = "Timed out after 8s",
                    DurationSeconds = 8
                },
                new()
                {
                    Id = "slack",
                    Tool = "slack",
                    Title = "Posted a summary to #release",
                    ApprovalTitle = "Post a summary to #release",
                    Status = ArcToolCallStatus.Approval,
                    Message = "Allows the agent to post one message in this channel"
                }
            }
        };

        return group;
    }

    public override IReadOnlyList<string>? WhenToUse => new[]
    {
        "A chat or agent transcript shows which tools ran, with the arguments and results one click away.",
        "A call must be allowed or denied inline before it runs, such as posting a message or writing to a database.",
        "Debug and audit views where the exact tool input and output must be copyable."
    };


    public override IReadOnlyList<string>? KeyboardAndAccessibility => new[]
    {
        "Tab moves through the header, each call row, the Allow, Deny or Retry buttons and the copy controls inside an open row.",
        "Enter or Space on the header folds the list; on a row it opens or closes Input and Output.",
        "Each row name reads the tool, the call and the status, and every state pairs its glyph with words, so colour is never the only signal."
    };

    public override IReadOnlyList<string>? MotionNotes => new[]
    {
        "Opening a row or the list settles on the smooth spring with a short fade, and the chevron turns on the morph spring.",
        "Status glyphs trade places with a snappy scale, and a finished call settles its shimmering title to solid text.",
        "Reduced motion removes the springs and lands every panel and status change in one frame."
    };

    public override string? ImplementationNotes =>
        "Recreated from the public tool-call documentation. Rows are composed Avalonia controls, so an opened row grows in one layout step " +
        "instead of animating its height, and tool marks are neutral icon or letter plates rather than brand glyphs. " +
        "Copy buttons confirm in place; approval, retry and deny are simulated locally because the demo has no agent loop behind it.";
}
