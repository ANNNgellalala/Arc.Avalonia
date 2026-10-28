using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

internal enum ArcMarkdownBlockKind
{
    Paragraph,
    Heading,
    Bullet,
    Numbered,
    Quote,
    Code,
    Rule
}

internal sealed record ArcMarkdownBlock(
    ArcMarkdownBlockKind Kind,
    string Text,
    int Level = 0,
    string? Language = null,
    int Number = 0);

/// <summary>
/// A deliberately small Markdown subset: the blocks and inline marks the Arc editor documents.
/// It is a renderer for preview and clean HTML output, not a general-purpose CommonMark engine.
/// </summary>
internal static class ArcMarkdown
{
    private static readonly Regex HtmlBlockBreak =
        new(@"</(?:p|div|h[1-6]|li|ul|ol|blockquote|pre|table|tr)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlHeading =
        new(@"<h([1-6])[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HtmlTag =
        new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex HtmlLink =
        new(@"<a\b[^>]*\bhref\s*=\s*[""']([^""']+)[""'][^>]*>(.*?)</a\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ArcMarkdownBlock> Parse(string? markdown)
    {
        var source = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = source.Split('\n');
        var blocks = new List<ArcMarkdownBlock>();
        var paragraph = new List<string>();

        void FlushParagraph()
        {
            if (paragraph.Count == 0)
            {
                return;
            }

            var text = string.Join(" ", paragraph.Select(line => line.Trim())).Trim();
            if (text.Length > 0)
            {
                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Paragraph, text));
            }

            paragraph.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                FlushParagraph();
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                var language = trimmed[3..].Trim();
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    if (code.Length > 0)
                    {
                        code.Append('\n');
                    }

                    code.Append(lines[i]);
                    i++;
                }

                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Code, code.ToString(), Language: language));
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                FlushParagraph();
                var quote = new List<string>();
                while (i < lines.Length && lines[i].TrimStart().StartsWith('>'))
                {
                    quote.Add(lines[i].TrimStart()[1..].TrimStart());
                    i++;
                }

                i--;
                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Quote,
                    string.Join(" ", quote).Trim()));
                continue;
            }

            var heading = Regex.Match(trimmed, @"^(#{1,6})\s+(.+)$");
            if (heading.Success)
            {
                FlushParagraph();
                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Heading,
                    heading.Groups[2].Value.Trim(), Math.Clamp(heading.Groups[1].Value.Length, 1, 3)));
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^(?:---+|\*\*\*+|___+)$"))
            {
                FlushParagraph();
                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Rule, string.Empty));
                continue;
            }

            var bullet = Regex.Match(line, @"^\s*[-*+]\s+(.+)$");
            if (bullet.Success)
            {
                FlushParagraph();
                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Bullet, bullet.Groups[1].Value.Trim()));
                continue;
            }

            var numbered = Regex.Match(line, @"^\s*(\d+)\.\s+(.+)$");
            if (numbered.Success)
            {
                FlushParagraph();
                var number = int.TryParse(numbered.Groups[1].Value, out var parsed) ? parsed : 1;
                blocks.Add(new ArcMarkdownBlock(ArcMarkdownBlockKind.Numbered, numbered.Groups[2].Value.Trim(), Number: number));
                continue;
            }

            paragraph.Add(line);
        }

        FlushParagraph();
        return blocks;
    }

    public static string ToHtml(string? markdown)
    {
        var blocks = Parse(markdown);
        var builder = new StringBuilder();
        var listKind = string.Empty;

        void CloseList()
        {
            if (listKind.Length > 0)
            {
                builder.Append("</").Append(listKind).Append('>');
                listKind = string.Empty;
            }
        }

        foreach (var block in blocks)
        {
            if (block.Kind is not (ArcMarkdownBlockKind.Bullet or ArcMarkdownBlockKind.Numbered))
            {
                CloseList();
            }

            switch (block.Kind)
            {
                case ArcMarkdownBlockKind.Heading:
                    builder.Append($"<h{block.Level}>").Append(InlineToHtml(block.Text)).Append($"</h{block.Level}>");
                    break;
                case ArcMarkdownBlockKind.Paragraph:
                    builder.Append("<p>").Append(InlineToHtml(block.Text)).Append("</p>");
                    break;
                case ArcMarkdownBlockKind.Quote:
                    builder.Append("<blockquote><p>").Append(InlineToHtml(block.Text)).Append("</p></blockquote>");
                    break;
                case ArcMarkdownBlockKind.Code:
                    builder.Append("<pre><code");
                    if (!string.IsNullOrWhiteSpace(block.Language))
                    {
                        builder.Append(" class=\"language-").Append(Escape(block.Language)).Append('"');
                    }

                    builder.Append('>').Append(Escape(block.Text)).Append("</code></pre>");
                    break;
                case ArcMarkdownBlockKind.Rule:
                    builder.Append("<hr>");
                    break;
                case ArcMarkdownBlockKind.Bullet:
                case ArcMarkdownBlockKind.Numbered:
                    var kind = block.Kind == ArcMarkdownBlockKind.Bullet ? "ul" : "ol";
                    if (listKind != kind)
                    {
                        CloseList();
                        builder.Append('<').Append(kind).Append('>');
                        listKind = kind;
                    }

                    builder.Append("<li>").Append(InlineToHtml(block.Text)).Append("</li>");
                    break;
            }
        }

        CloseList();
        return builder.ToString();
    }

    /// <summary>Converts the editor's own clean HTML back to Markdown, dropping unsupported markup.</summary>
    public static string FromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var source = html.Replace("\r\n", "\n").Replace('\r', '\n');
        source = Regex.Replace(source, @"<(script|style)\b[^>]*>.*?</\1\s*>", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        source = Regex.Replace(source, @"<pre[^>]*>\s*<code[^>]*>(.*?)</code\s*>\s*</pre>",
            match => $"\n```\n{WebUtility.HtmlDecode(match.Groups[1].Value)}\n```\n",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        source = HtmlLink.Replace(source, match =>
        {
            var href = match.Groups[1].Value;
            var label = HtmlTag.Replace(match.Groups[2].Value, string.Empty);
            return $"[{WebUtility.HtmlDecode(label)}]({href})";
        });

        source = HtmlHeading.Replace(source, match =>
        {
            var level = Math.Clamp(match.Groups[1].Value.Length, 1, 3);
            return "\n" + new string('#', level) + " ";
        });

        source = Regex.Replace(source, @"<li[^>]*>", "\n- ", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"<blockquote[^>]*>", "\n> ", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        source = HtmlBlockBreak.Replace(source, "\n");
        source = Regex.Replace(source, @"<(strong|b)\b[^>]*>", "**", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"</(strong|b)\s*>", "**", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"<(em|i)\b[^>]*>", "*", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"</(em|i)\s*>", "*", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"<(s|del|strike)\b[^>]*>", "~~", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"</(s|del|strike)\s*>", "~~", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"<code\b[^>]*>", "`", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"</code\s*>", "`", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"<hr\s*/?>", "\n---\n", RegexOptions.IgnoreCase);
        source = HtmlTag.Replace(source, string.Empty);
        source = WebUtility.HtmlDecode(source);

        var lines = source.Split('\n').Select(line => line.TrimEnd()).ToList();
        var result = new List<string>();
        var blank = false;
        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
            {
                if (!blank && result.Count > 0)
                {
                    result.Add(string.Empty);
                }

                blank = true;
                continue;
            }

            blank = false;
            result.Add(line.TrimStart());
        }

        return string.Join("\n", result).Trim();
    }

    public static Control BuildPreview(string? markdown)
    {
        var blocks = Parse(markdown);
        var stack = new StackPanel { Spacing = 10 };
        if (blocks.Count == 0)
        {
            stack.Children.Add(ArcContentUi.Caption("Nothing to preview yet."));
            return stack;
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            var previous = i > 0 ? blocks[i - 1].Kind : (ArcMarkdownBlockKind?)null;
            var loose = previous is null or ArcMarkdownBlockKind.Paragraph or ArcMarkdownBlockKind.Quote
                or ArcMarkdownBlockKind.Bullet or ArcMarkdownBlockKind.Numbered;
            var margin = block.Kind is ArcMarkdownBlockKind.Bullet or ArcMarkdownBlockKind.Numbered
                ? new Thickness(0, 0, 0, 0)
                : new Thickness(0, loose ? 0 : 6, 0, 0);

            switch (block.Kind)
            {
                case ArcMarkdownBlockKind.Heading:
                    var heading = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        FontWeight = FontWeight.Medium,
                        FontSize = block.Level switch { 1 => 28, 2 => 22, _ => 18 },
                        LineHeight = block.Level switch { 1 => 34, 2 => 28, _ => 24 },
                        Margin = new Thickness(0, i == 0 ? 0 : 6, 0, 2)
                    };
                    heading.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
                    AddInlineRuns(heading, block.Text);
                    stack.Children.Add(heading);
                    break;

                case ArcMarkdownBlockKind.Paragraph:
                    stack.Children.Add(BuildParagraph(block.Text, margin));
                    break;

                case ArcMarkdownBlockKind.Quote:
                    var quoteText = BuildParagraph(block.Text, default);
                    quoteText.FontStyle = FontStyle.Normal;
                    quoteText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
                    var quote = new Border
                    {
                        BorderThickness = new Thickness(2, 0, 0, 0),
                        Padding = new Thickness(12, 2, 0, 2),
                        Margin = new Thickness(0, 2, 0, 2),
                        Child = quoteText
                    };
                    quote.Paint(Border.BorderBrushProperty, ArcTokens.Accent);
                    stack.Children.Add(quote);
                    break;

                case ArcMarkdownBlockKind.Bullet:
                case ArcMarkdownBlockKind.Numbered:
                    var row = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("22,*,Auto"),
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    var marker = ArcContentUi.Text(block.Kind == ArcMarkdownBlockKind.Bullet ? "•" : $"{block.Number}.",
                        "arc-body-sm");
                    marker.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
                    marker.HorizontalAlignment = HorizontalAlignment.Left;
                    marker.VerticalAlignment = VerticalAlignment.Top;
                    marker.Margin = new Thickness(2, 0, 0, 0);
                    row.Children.Add(marker);
                    var item = BuildParagraph(block.Text, default);
                    Grid.SetColumn(item, 1);
                    row.Children.Add(item);
                    stack.Children.Add(row);
                    break;

                case ArcMarkdownBlockKind.Code:
                    var code = new TextBlock
                    {
                        Text = block.Text,
                        FontFamily = ArcContentUi.MonoFamily,
                        FontSize = 13,
                        LineHeight = 20,
                        TextWrapping = TextWrapping.Wrap
                    };
                    code.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
                    var codeFrame = new Border
                    {
                        CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
                        Padding = new Thickness(12, 10),
                        Margin = new Thickness(0, 4, 0, 2),
                        Child = code
                    };
                    codeFrame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
                    codeFrame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
                    codeFrame.BorderThickness = new Thickness(1);
                    stack.Children.Add(codeFrame);
                    break;

                case ArcMarkdownBlockKind.Rule:
                    var rule = ArcContentUi.Divider(ArcTokens.Border, 1);
                    rule.Margin = new Thickness(0, 8, 0, 8);
                    stack.Children.Add(rule);
                    break;
            }
        }

        return stack;
    }

    private static TextBlock BuildParagraph(string text, Thickness margin)
    {
        var block = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            LineHeight = 25,
            Margin = margin
        };
        block.Paint(TextBlock.ForegroundProperty, ArcTokens.Foreground);
        AddInlineRuns(block, text);
        return block;
    }

    public static void AddInlineRuns(TextBlock block, string text) =>
        AddInlineRuns(block, text, false, false, false, false);

    private static void AddInlineRuns(TextBlock block, string text, bool bold, bool italic, bool strike, bool link)
    {
        var index = 0;
        var plain = new StringBuilder();

        void Flush()
        {
            if (plain.Length == 0)
            {
                return;
            }

            ArcContentUi.AddRun(block, plain.ToString(),
                link ? ArcTokens.AccentStrong : null,
                bold ? FontWeight.Medium : null,
                italic ? FontStyle.Italic : null,
                strike ? TextDecorations.Strikethrough : link ? TextDecorations.Underline : null);
            plain.Clear();
        }

        while (index < text.Length)
        {
            var current = text[index];

            if (current == '\\' && index + 1 < text.Length)
            {
                plain.Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (current == '`')
            {
                var end = text.IndexOf('`', index + 1);
                if (end > index + 1)
                {
                    Flush();
                    ArcContentUi.AddRun(block, text[(index + 1)..end], ArcTokens.AccentStrong, family: ArcContentUi.MonoFamily);
                    index = end + 1;
                    continue;
                }
            }

            if (current == '[')
            {
                var close = text.IndexOf("](", index + 1, StringComparison.Ordinal);
                if (close > index)
                {
                    var urlEnd = text.IndexOf(')', close + 2);
                    if (urlEnd > close + 2)
                    {
                        var label = text[(index + 1)..close];
                        Flush();
                        AddInlineRuns(block, label, bold, italic, strike, true);
                        index = urlEnd + 1;
                        continue;
                    }
                }
            }

            if (current == '*' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end > index + 1)
                {
                    Flush();
                    AddInlineRuns(block, text[(index + 2)..end], !bold, italic, strike, link);
                    index = end + 2;
                    continue;
                }
            }

            if (current == '~' && index + 1 < text.Length && text[index + 1] == '~')
            {
                var end = text.IndexOf("~~", index + 2, StringComparison.Ordinal);
                if (end > index + 1)
                {
                    Flush();
                    AddInlineRuns(block, text[(index + 2)..end], bold, italic, !strike, link);
                    index = end + 2;
                    continue;
                }
            }

            if (current is '*' or '_')
            {
                var end = text.IndexOf(current, index + 1);
                if (end > index + 1)
                {
                    Flush();
                    AddInlineRuns(block, text[(index + 1)..end], bold, !italic, strike, link);
                    index = end + 1;
                    continue;
                }
            }

            plain.Append(current);
            index++;
        }

        Flush();
    }

    private static string InlineToHtml(string text)
    {
        var builder = new StringBuilder();
        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];
            if (current == '\\' && index + 1 < text.Length)
            {
                builder.Append(Escape(text[index + 1].ToString()));
                index += 2;
                continue;
            }

            if (current == '`')
            {
                var end = text.IndexOf('`', index + 1);
                if (end > index + 1)
                {
                    builder.Append("<code>").Append(Escape(text[(index + 1)..end])).Append("</code>");
                    index = end + 1;
                    continue;
                }
            }

            if (current == '[')
            {
                var close = text.IndexOf("](", index + 1, StringComparison.Ordinal);
                var urlEnd = close > 0 ? text.IndexOf(')', close + 2) : -1;
                if (urlEnd > close + 2)
                {
                    var url = text[(close + 2)..urlEnd];
                    builder.Append("<a href=\"").Append(Escape(NormalizeUrl(url))).Append("\">")
                        .Append(Escape(text[(index + 1)..close])).Append("</a>");
                    index = urlEnd + 1;
                    continue;
                }
            }

            if (current == '*' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end > index + 1)
                {
                    builder.Append("<strong>").Append(InlineToHtml(text[(index + 2)..end])).Append("</strong>");
                    index = end + 2;
                    continue;
                }
            }

            if (current == '~' && index + 1 < text.Length && text[index + 1] == '~')
            {
                var end = text.IndexOf("~~", index + 2, StringComparison.Ordinal);
                if (end > index + 1)
                {
                    builder.Append("<s>").Append(InlineToHtml(text[(index + 2)..end])).Append("</s>");
                    index = end + 2;
                    continue;
                }
            }

            if (current is '*' or '_')
            {
                var end = text.IndexOf(current, index + 1);
                if (end > index + 1)
                {
                    builder.Append("<em>").Append(InlineToHtml(text[(index + 1)..end])).Append("</em>");
                    index = end + 1;
                    continue;
                }
            }

            builder.Append(Escape(current.ToString()));
            index++;
        }

        return builder.ToString();
    }

    public static string NormalizeUrl(string? input)
    {
        var url = (input ?? string.Empty).Trim();
        if (url.Length == 0)
        {
            return "https://";
        }

        if (url.Contains('@') && !url.Contains('/') && !url.Contains(':'))
        {
            return "mailto:" + url;
        }

        if (!url.Contains("://", StringComparison.Ordinal) && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith('#') && !url.StartsWith('/'))
        {
            return "https://" + url;
        }

        return url;
    }

    private static string Escape(string text) => WebUtility.HtmlEncode(text);
}
