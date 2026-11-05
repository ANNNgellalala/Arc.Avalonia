using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.Actions.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.Actions.Pages;

/// <summary>
/// Confirm morph's completeness preview: the resting pill, the question in both danger and neutral tone, the
/// pending spinner and the done (with Undo) and error (with Retry) results, all held open. Declared state
/// keys match Docs/confirm-morph.json.
/// </summary>
public sealed class ConfirmMorphPage : ArcItemPage
{
    public override string ItemSlug => "confirm-morph";

    /// <summary>The resting pill is a muted surface, so it reads best on the page surface rather than the canvas.</summary>
    public override bool DemoOnCanvas => false;

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "state-idle", "state-confirming", "tone-neutral", "state-pending", "state-done", "state-error"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 22, HorizontalAlignment = HorizontalAlignment.Left };

        var idle = new ArcConfirmMorph
        {
            Label = "Delete project",
            Icon = "trash-2",
            Prompt = "Delete 3 files?"
        };

        var confirming = new ArcConfirmMorph
        {
            Label = "Revoke access",
            Icon = "ban",
            Prompt = "Revoke Maya's access?",
            ConfirmLabel = "Revoke",
            State = ArcConfirmMorphState.Confirming,
            ConfirmTimeout = 0,
            CancelOnOutsidePress = false
        };

        var pending = new ArcConfirmMorph
        {
            Label = "Delete project",
            Icon = "trash-2",
            PendingLabel = "Deleting",
            State = ArcConfirmMorphState.Pending
        };

        var done = new ArcConfirmMorph
        {
            Label = "Archive board",
            Icon = "archive",
            DoneLabel = "Archived",
            State = ArcConfirmMorphState.Done,
            ResultTimeout = 0,
            Undo = () => Task.CompletedTask
        };

        var error = new ArcConfirmMorph
        {
            Label = "Delete project",
            Icon = "trash-2",
            ErrorLabel = "Could not delete",
            RetryLabel = "Retry",
            State = ArcConfirmMorphState.Error,
            ResultTimeout = 0
        };

        var neutral = new ArcConfirmMorph
        {
            Label = "Archive board",
            Icon = "archive",
            Prompt = "Archive this board?",
            ConfirmLabel = "Archive",
            Tone = ArcConfirmTone.Neutral,
            State = ArcConfirmMorphState.Confirming,
            ConfirmTimeout = 0,
            CancelOnOutsidePress = false
        };

        panel.Children.Add(Row("状态", idle, confirming, pending, done, error));
        panel.Children.Add(Row("语气", neutral));

        return panel;
    }

    private static Control Row(string label, params Control[] children)
    {
        var row = new StackPanel { Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, Classes = { "arc-caption" } });
        var rowPanel = new WrapPanel();
        foreach (var child in children)
        {
            child.Margin = new Thickness(0, 0, 14, 14);
            rowPanel.Children.Add(child);
        }

        row.Children.Add(rowPanel);
        return row;
    }
}
