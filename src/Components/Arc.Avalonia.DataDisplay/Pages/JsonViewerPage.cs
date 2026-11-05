using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// JSON viewer's completeness preview: a webhook payload open two levels deep with search highlighting, the
/// same payload with the path strip switched off, and a viewer paging a long array three items at a time.
/// All three are static and the payload is fixed.
/// </summary>
public sealed class JsonViewerPage : ArcItemPage
{
    private const string Payload = """
        {
          "event": "invoice.paid",
          "created_at": "2026-09-22T08:41:07Z",
          "livemode": true,
          "attempt": 3,
          "data": {
            "amount": 4200,
            "currency": "usd",
            "customer": {
              "id": "cus_N4x",
              "name": "Maya Chen",
              "email": "maya@acme.com",
              "plan": "scale"
            },
            "lines": [
              { "description": "Seat", "quantity": 12, "unit_amount": 300 },
              { "description": "Storage", "quantity": 1, "unit_amount": 200 },
              { "description": "Support", "quantity": 1, "unit_amount": 400 },
              { "description": "Audit log", "quantity": 1, "unit_amount": 200 },
              { "description": "Exports", "quantity": 2, "unit_amount": 50 }
            ]
          }
        }
        """;

    public override string ItemSlug => "json-viewer";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-payload", "state-search", "state-no-path", "variant-paged"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Card("展开两层并搜索", "DefaultExpandDepth=2，Query=\"maya\" 会自动展开命中的分支并高亮匹配行。",
            query: "maya", showPath: true, pageSize: 50));

        panel.Children.Add(Card("无路径条", "ShowPath=false：收起底部当前路径，树本身与复制操作仍在。",
            query: null, showPath: false, pageSize: 50));

        panel.Children.Add(Card("数组分页", "PageSize=3：lines 数组一次只列三条，底部按钮翻到下一页。",
            query: null, showPath: true, pageSize: 3));

        return panel;
    }

    private static Control Card(string title, string note, string? query, bool showPath, int pageSize)
    {
        var viewer = new ArcJsonViewer
        {
            Data = Payload,
            RootName = "event",
            DefaultExpandDepth = 2,
            Query = query,
            PageSize = pageSize,
            ShowPath = showPath,
            ScrollHeight = 240,
            Label = "Webhook payload",
            Width = 700
        };

        return DemoUi.Card(title, note, viewer);
    }
}
