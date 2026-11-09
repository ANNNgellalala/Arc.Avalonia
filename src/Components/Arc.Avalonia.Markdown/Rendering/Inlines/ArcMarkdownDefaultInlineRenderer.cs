using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Markdown.Controls;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;

using MdInline = Markdig.Syntax.Inlines.Inline;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Default CommonMark/GFM inline renderer. Literals, inline code, line breaks, emphasis/strong/
/// strikethrough/inserted/marked/sub-superscript, links, autolinks, task markers, images and HTML
/// entities are appended as <see cref="Run"/>/<see cref="Span"/>/<see cref="InlineUIContainer"/>
/// entries inside the host text block. Links go through <see cref="ArcMarkdownRenderContext.LinkClickSink"/>
/// so the host can mark a click handled; images load asynchronously and degrade to a placeholder.
/// </summary>
internal static class ArcMarkdownDefaultInlineRenderer
{
    private const double ImageMaxWidth = 480;
    private const double ImageMaxHeight = 320;

    /// <summary>Renders the children of <paramref name="container"/>, optionally skipping the first one.</summary>
    internal static void RenderChildren(
        ContainerInline? container,
        InlineCollection target,
        ArcMarkdownRenderContext context,
        bool skipFirst = false)
    {
        if (container is null)
        {
            return;
        }

        var first = true;
        for (var inline = container.FirstChild; inline is not null; inline = inline.NextSibling)
        {
            if (skipFirst && first)
            {
                first = false;
                continue;
            }

            first = false;
            RenderOne(inline, target, context);
        }
    }

    /// <summary>Renders one inline node. Failures degrade to a plain run and a warning; nothing escapes.</summary>
    internal static void RenderOne(MdInline inline, InlineCollection target, ArcMarkdownRenderContext context)
    {
        try
        {
            RenderOneCore(inline, target, context);
        }
        catch (Exception ex)
        {
            context.Warn(
                "ARC-MD-INLINE-002",
                $"行内渲染失败，已回退纯文本：{ex.Message}",
                inline.GetType().Name);
            var text = inline.ToString();
            if (!string.IsNullOrEmpty(text))
            {
                target.Add(new Run(text));
            }
        }
    }

    private static void RenderOneCore(MdInline inline, InlineCollection target, ArcMarkdownRenderContext context)
    {
        switch (inline)
        {
            case LiteralInline literal:
                target.Add(new Run(literal.Content.ToString()));
                return;

            case CodeInline code:
                ContextAddInlineCode(target, code.Content, context);
                return;

            case LineBreakInline lineBreak:
                // CommonMark: a soft break is a space, a hard break (two spaces or a backslash) is <br/>.
                target.Add(lineBreak.IsHard ? new LineBreak() : new Run(" "));
                return;

            case HtmlEntityInline entity:
                target.Add(new Run(entity.Transcoded.ToString()));
                return;

            case TaskList taskList:
                target.Add(new Run(taskList.Checked ? "☑ " : "☐ ")
                {
                    Foreground = context.Style.TextSecondary,
                });
                return;

            case EmphasisInline emphasis:
                RenderEmphasis(emphasis, target, context);
                return;

            case LinkInline link:
                RenderLink(link, target, context);
                return;

            case AutolinkInline autolink:
                RenderAutolink(autolink, target, context);
                return;

            case HtmlInline html:
                // HTML is disabled at parse time; if Markdig still emits an inline tag, show it as text.
                target.Add(new Run(html.Tag)
                {
                    FontFamily = context.Style.MonoFont,
                    Foreground = context.Style.TextMuted,
                });
                return;

            case ContainerInline container:
                RenderChildren(container, target, context);
                return;

            default:
                var text = inline.ToString();
                context.Warn(
                    "ARC-MD-INLINE-001",
                    $"未知行内节点 {inline.GetType().Name}，已按文本回退",
                    inline.GetType().Name);
                if (!string.IsNullOrEmpty(text))
                {
                    target.Add(new Run(text) { Foreground = context.Style.TextSecondary });
                }

                return;
        }
    }

    private static void ContextAddInlineCode(InlineCollection target, string content, ArcMarkdownRenderContext context)
    {
        target.Add(new Run(content)
        {
            FontFamily = context.Style.MonoFont,
            Background = context.Style.InlineCodeBackground,
        });
    }

    private static void RenderEmphasis(
        EmphasisInline emphasis,
        InlineCollection target,
        ArcMarkdownRenderContext context)
    {
        var span = new Span();
        switch (emphasis.DelimiterChar)
        {
            case '~' when emphasis.DelimiterCount >= 2:
                span.TextDecorations = TextDecorations.Strikethrough;
                break;
            case '~':
                span.BaselineAlignment = BaselineAlignment.Subscript;
                break;
            case '^':
                span.BaselineAlignment = BaselineAlignment.Superscript;
                break;
            case '=':
                // GFM "marked" text; a quiet token wash keeps it readable in both themes.
                span.Background = context.Style.SurfaceMuted;
                break;
            case '+':
                span.TextDecorations = TextDecorations.Underline;
                break;
            default:
                if (emphasis.DelimiterCount >= 2)
                {
                    // Arc type is 400/500 only: strong text uses 500, never faux bold 700.
                    span.FontWeight = FontWeight.Medium;
                }

                if (emphasis.DelimiterCount % 2 == 1)
                {
                    span.FontStyle = FontStyle.Italic;
                }

                break;
        }

        RenderChildren(emphasis, span.Inlines, context);
        target.Add(span);
    }

    private static void RenderLink(LinkInline link, InlineCollection target, ArcMarkdownRenderContext context)
    {
        var url = link.Url ?? string.Empty;
        if (link.IsImage)
        {
            RenderImage(link, url, context, target);
            return;
        }

        var label = GetInlineText(link);
        if (string.IsNullOrWhiteSpace(label))
        {
            label = url;
        }

        if (context.Sanitized.IsBlocked(link))
        {
            target.Add(CreateMutedSpan(label, context));
            return;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            target.Add(CreateMutedSpan(label, context));
            return;
        }

        AddHyperlink(target, url, label, link.Title, context, link);
    }

    private static void RenderAutolink(
        AutolinkInline autolink,
        InlineCollection target,
        ArcMarkdownRenderContext context)
    {
        var display = autolink.Url ?? string.Empty;
        if (string.IsNullOrEmpty(display))
        {
            return;
        }

        if (context.Sanitized.IsBlocked(autolink))
        {
            target.Add(CreateMutedSpan(display, context));
            return;
        }

        // Markdig keeps the bare address for email autolinks; the handler needs the mailto scheme.
        var url = autolink.IsEmail ? "mailto:" + display : display;
        AddHyperlink(target, url, display, title: null, context, label: null);
    }

    private static void AddHyperlink(
        InlineCollection target,
        string url,
        string displayText,
        string? title,
        ArcMarkdownRenderContext context,
        LinkInline? label)
    {
        var style = context.Style;
        var content = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            Foreground = style.Accent,
            TextDecorations = TextDecorations.Underline,
        };

        if (label is not null)
        {
            // Keep emphasis inside the link label, but never nest the link itself again.
            RenderChildren(label, content.Inlines!, context);
        }

        if (content.Inlines is not { Count: > 0 })
        {
            content.Inlines!.Add(new Run(displayText));
        }

        var button = new HyperlinkButton
        {
            Content = content,
            // A little vertical padding grows the touch target of an inline link without breaking the line box.
            Padding = new Thickness(2, 2, 2, 2),
            MinHeight = 24,
            Background = null,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(button, displayText);
        AutomationProperties.SetHelpText(button, url);
        if (!string.IsNullOrEmpty(title))
        {
            ToolTip.SetTip(button, title);
        }

        // Swiping over a document must not open a link: the guard cancels clicks after an 8 px drag.
        var tapGuard = new ArcMarkdownTapGuard();
        tapGuard.Attach(button);

        // Token-only hover/focus feedback: the framework accent colour never leaks into Arc surfaces.
        button.PointerEntered += (_, _) => content.Opacity = 0.82;
        button.PointerExited += (_, _) => content.Opacity = 1;
        button.GotFocus += (_, _) => content.FontWeight = FontWeight.Medium;
        button.LostFocus += (_, _) => content.ClearValue(TextBlock.FontWeightProperty);

        button.Click += async (_, _) =>
        {
            if (tapGuard.IsCancelled)
            {
                return;
            }

            var click = new ArcMarkdownLinkClick(url, displayText);
            try
            {
                context.LinkClickSink?.Invoke(click);
            }
            catch (Exception ex)
            {
                context.Warn("ARC-MD-LINK-002", $"链接点击回调失败：{ex.Message}", nameof(LinkInline));
            }

            if (click.Handled)
            {
                return;
            }

            try
            {
                await context.Options.LinkHandler.OpenAsync(url).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                context.Warn("ARC-MD-LINK-001", $"打开链接失败：{ex.Message}", nameof(LinkInline));
            }
        };

        target.Add(new InlineUIContainer(button));
    }

    private static Span CreateMutedSpan(string text, ArcMarkdownRenderContext context)
    {
        var span = new Span { Foreground = context.Style.TextMuted };
        span.Inlines.Add(new Run(text));
        return span;
    }

    private static void RenderImage(
        LinkInline link,
        string url,
        ArcMarkdownRenderContext context,
        InlineCollection target)
    {
        var style = context.Style;
        var localizer = context.Options.Localizer;
        var alt = GetInlineText(link).Trim();
        var accessibleName = string.IsNullOrEmpty(alt)
            ? localizer.Get(ArcMarkdownStringKeys.Image)
            : alt;

        var image = new Image
        {
            MaxWidth = ImageMaxWidth,
            MaxHeight = ImageMaxHeight,
            Stretch = Stretch.Uniform,
            IsVisible = false,
        };
        AutomationProperties.SetName(image, accessibleName);

        var placeholder = new TextBlock
        {
            Text = localizer.Get(ArcMarkdownStringKeys.ImageLoading),
            Foreground = style.TextMuted,
            FontStyle = FontStyle.Italic,
            FontSize = style.SmallSize,
            FontFamily = style.BodyFont,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetName(placeholder, accessibleName);

        var panel = new StackPanel { Spacing = style.ListItemSpacing };
        panel.Children.Add(image);
        panel.Children.Add(placeholder);
        ToolTip.SetTip(panel, accessibleName);

        var blocked = context.Sanitized.IsBlocked(link);
        if (blocked || string.IsNullOrWhiteSpace(url))
        {
            placeholder.Text = blocked
                ? localizer.Get(ArcMarkdownStringKeys.ImageBlocked, url)
                : localizer.Get(ArcMarkdownStringKeys.Image, accessibleName);
            target.Add(new InlineUIContainer(panel));
            return;
        }

        target.Add(new InlineUIContainer(panel));

        // The streaming tail is re-rendered on every flush; defer the network/decode work until the
        // block is committed, so a fast token stream does not start a load per frame.
        if (context.IsTail)
        {
            return;
        }

        StartImageLoad(context, panel, url, accessibleName, image, placeholder);
    }

    private static void StartImageLoad(
        ArcMarkdownRenderContext context,
        Panel panel,
        string url,
        string accessibleName,
        Image image,
        TextBlock placeholder)
    {
        var cancellation = new CancellationTokenSource();
        panel.DetachedFromVisualTree += (_, _) =>
        {
            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Load already finished; nothing to cancel.
            }
        };

        try
        {
            context.ImageLoadingSink?.Invoke(url);
        }
        catch (Exception ex)
        {
            context.Warn("ARC-MD-IMG-002", $"图片加载通知失败：{ex.Message}", nameof(LinkInline));
        }

        _ = LoadImageAsync(context, url, accessibleName, image, placeholder, cancellation);
    }

    private static async Task LoadImageAsync(
        ArcMarkdownRenderContext context,
        string url,
        string accessibleName,
        Image image,
        TextBlock placeholder,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await context.Options.ImageLoader.LoadAsync(url, cancellation.Token).ConfigureAwait(false);
            if (cancellation.IsCancellationRequested)
            {
                return;
            }

            PostToUi(context, () => ApplyImageResult(context, url, accessibleName, image, placeholder, result));
        }
        catch (OperationCanceledException)
        {
            // The control left the visual tree (streaming tail re-render); drop the result silently.
        }
        catch (Exception ex)
        {
            context.Warn("ARC-MD-IMG-001", $"图片加载异常：{ex.Message}", nameof(LinkInline));
            PostToUi(context, () =>
            {
                placeholder.Text = context.Options.Localizer.Get(ArcMarkdownStringKeys.ImageLoadFailed);
                InvokeImageLoaded(context, url, ArcMarkdownImageResult.Failed(ex.Message));
            });
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private static void ApplyImageResult(
        ArcMarkdownRenderContext context,
        string url,
        string accessibleName,
        Image image,
        TextBlock placeholder,
        ArcMarkdownImageResult result)
    {
        InvokeImageLoaded(context, url, result);

        if (result is { Success: true, Image: not null })
        {
            image.Source = result.Image;
            image.IsVisible = true;
            placeholder.IsVisible = false;
            AutomationProperties.SetName(image, accessibleName);
            return;
        }

        // The record does not carry a kind, so the documented refusal reasons map to the policy copy.
        placeholder.Text = result.Reason is "offline" or "rejected"
            ? context.Options.Localizer.Get(ArcMarkdownStringKeys.ImageBlocked, url)
            : context.Options.Localizer.Get(ArcMarkdownStringKeys.ImageLoadFailed);
    }

    private static void InvokeImageLoaded(ArcMarkdownRenderContext context, string url, ArcMarkdownImageResult result)
    {
        try
        {
            context.ImageLoadedSink?.Invoke(url, result);
        }
        catch (Exception ex)
        {
            context.Warn("ARC-MD-IMG-003", $"图片加载结果通知失败：{ex.Message}", nameof(LinkInline));
        }
    }

    private static void PostToUi(ArcMarkdownRenderContext context, Action action)
    {
        try
        {
            // A loader that completes synchronously (the offline default, cached images) stays on the UI
            // thread; applying inline avoids a frame of placeholder and keeps headless tests deterministic.
            if (context.Dispatcher.CheckAccess())
            {
                RunUiAction(context, action);
                return;
            }

            context.Dispatcher.Post(() => RunUiAction(context, action));
        }
        catch (Exception ex)
        {
            context.Warn("ARC-MD-IMG-005", $"无法回到 UI 线程回填图片：{ex.Message}", nameof(LinkInline));
        }
    }

    private static void RunUiAction(ArcMarkdownRenderContext context, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            context.Warn("ARC-MD-IMG-004", $"图片结果回填失败：{ex.Message}", nameof(LinkInline));
        }
    }

    /// <summary>Plain text of an inline container (link labels, image alt text, heading anchors).</summary>
    internal static string GetInlineText(ContainerInline? container)
    {
        if (container is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var child = container.FirstChild; child is not null; child = child.NextSibling)
        {
            AppendText(child, builder);
        }

        return builder.ToString();
    }

    /// <summary>Appends the plain text of one inline, used for copy/anchor/alt extraction.</summary>
    internal static void AppendText(MdInline inline, StringBuilder builder)
    {
        switch (inline)
        {
            case LiteralInline literal:
                builder.Append(literal.Content.ToString());
                break;
            case CodeInline code:
                builder.Append(code.Content);
                break;
            case LineBreakInline:
                builder.Append(' ');
                break;
            case HtmlEntityInline entity:
                builder.Append(entity.Transcoded.ToString());
                break;
            case TaskList taskList:
                builder.Append(taskList.Checked ? "☑ " : "☐ ");
                break;
            case AutolinkInline autolink:
                builder.Append(autolink.Url ?? string.Empty);
                break;
            case LinkInline { IsImage: true } image:
                var alt = GetInlineText(image);
                builder.Append(string.IsNullOrEmpty(alt) ? image.Url ?? string.Empty : alt);
                break;
            case LinkInline link:
                builder.Append(GetInlineText(link));
                break;
            case ContainerInline container:
                for (var child = container.FirstChild; child is not null; child = child.NextSibling)
                {
                    AppendText(child, builder);
                }

                break;
        }
    }
}
