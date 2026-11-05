using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Arc.Avalonia.DataDisplay.Controls;
using Arc.Avalonia.Demo;

namespace Arc.Avalonia.DataDisplay.Pages;

/// <summary>
/// Code block's completeness preview: a file with a name and language header, a long file collapsed to a
/// line limit with the Show all toggle, and a snippet without a file name. All three are static and the
/// sources are fixed.
/// </summary>
public sealed class CodeBlockPage : ArcItemPage
{
    private const string UploadSource = """
        import { useCallback, useState } from "react";

        type Upload = { id: string; name: string; progress: number };

        export function useUploads() {
          const [uploads, setUploads] = useState<Upload[]>([]);

          const start = useCallback((file: File) => {
            const id = crypto.randomUUID();
            setUploads((current) => [
              ...current,
              { id, name: file.name, progress: 0 },
            ]);
            return id;
          }, []);

          return { uploads, start };
        }
        """;

    private const string LongSource = """
        export class InvoiceClient {
          private readonly baseUrl: string;
          private readonly token: string;

          constructor(baseUrl: string, token: string) {
            this.baseUrl = baseUrl;
            this.token = token;
          }

          async list(cursor?: string) {
            const url = new URL("/v1/invoices", this.baseUrl);
            if (cursor) {
              url.searchParams.set("cursor", cursor);
            }

            const response = await fetch(url, {
              headers: { Authorization: `Bearer ${this.token}` },
            });

            if (!response.ok) {
              throw new Error(`Request failed: ${response.status}`);
            }

            return response.json();
          }

          async pay(id: string) {
            const response = await fetch(`${this.baseUrl}/v1/invoices/${id}/pay`, {
              method: "POST",
              headers: { Authorization: `Bearer ${this.token}` },
            });
            return response.json();
          }
        }
        """;

    private const string SmallSource = """
        var block = new ArcCodeBlock
        {
            Code = snippet,
            Filename = "use-uploads.ts",
            Language = "tsx",
            MaxLines = 12
        };
        """;

    public override string ItemSlug => "code-block";

    public override IReadOnlyList<string> ImplementedStates => new[]
    {
        "variant-filename", "variant-collapsed", "variant-no-filename"
    };

    protected override Control BuildDemo(ArcDemoContext context)
    {
        var panel = new StackPanel { Spacing = 16, HorizontalAlignment = HorizontalAlignment.Left };

        panel.Children.Add(Card("带文件名与语言", "文件名、语言标签、行号与复制按钮都在头部；短文件不出现折叠。",
            new ArcCodeBlock
            {
                Code = UploadSource,
                Filename = "use-uploads.ts",
                Language = "tsx"
            }));

        panel.Children.Add(Card("折叠长文件", "MaxLines=8：只显示前八行，底部出现 Show all，展开后不再折叠。",
            new ArcCodeBlock
            {
                Code = LongSource,
                Filename = "invoice-client.ts",
                Language = "tsx",
                MaxLines = 8
            }));

        panel.Children.Add(Card("无文件名", "Filename 留空时头部显示默认标题；语言标签仍按 Language 着色。",
            new ArcCodeBlock
            {
                Code = SmallSource,
                Language = "csharp"
            }));

        return panel;
    }

    private static Control Card(string title, string note, Control block)
    {
        block.Width = 720;
        return DemoUi.Card(title, note, block);
    }
}
