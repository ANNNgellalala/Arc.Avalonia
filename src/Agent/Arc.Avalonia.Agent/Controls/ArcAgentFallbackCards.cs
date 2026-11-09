using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Degradation cards for a <c>pura-agent</c> fence that cannot become an interactive component: the loading
/// placeholder while the fence is still streaming, the error card for malformed or invalid JSON, and the
/// unsupported card for an unknown component type. All three are plain Arc surfaces (no template needed) so
/// they render in any host and never throw.
/// </summary>
internal static class ArcAgentFallbackCards
{
    /// <summary>Non-interactive placeholder for an unclosed fence or the unstable streaming tail.</summary>
    internal static Control Loading(ArcMarkdownRenderContext context)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(new ArcSpinner { Size = 14, Stroke = context.Style.TextMuted });
        row.Children.Add(new TextBlock
        {
            Text = "组件加载中…",
            FontSize = context.Style.SmallSize,
            Foreground = context.Style.TextMuted,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        var surface = Surface(context, row);
        AutomationProperties.SetName(surface, "Agent component loading");
        AutomationProperties.SetLiveSetting(surface, AutomationLiveSetting.Polite);
        return surface;
    }

    /// <summary>Error card: reason (with the field path when known) plus the raw JSON in a collapsible block.</summary>
    internal static Control Error(ArcMarkdownRenderContext context, string title, string? detail, string raw)
        => Build(context, title, detail, raw, context.Style.Danger, "triangle-alert");

    /// <summary>Unsupported-type card: the type name plus the raw JSON.</summary>
    internal static Control Unsupported(ArcMarkdownRenderContext context, string type, string raw)
        => Build(
            context,
            context.Options.Localizer.Get(ArcMarkdownStringKeys.AgentUnsupported, type),
            "该组件类型尚未注册渲染器，已按原始 JSON 展示。",
            raw,
            context.Style.TextMuted,
            "circle-help");

    private static Control Build(
        ArcMarkdownRenderContext context,
        string title,
        string? detail,
        string raw,
        IBrush accent,
        string icon)
    {
        var stack = new StackPanel { Spacing = 8 };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(new ArcIcon
        {
            Icon = icon,
            Size = 15,
            Foreground = accent,
            VerticalAlignment = VerticalAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = context.Style.BodySize,
            FontWeight = FontWeight.SemiBold,
            Foreground = accent,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(detail))
        {
            stack.Children.Add(new TextBlock
            {
                Text = detail,
                FontSize = context.Style.SmallSize,
                Foreground = context.Style.TextSecondary,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            stack.Children.Add(RawJson(context, raw));
        }

        var surface = Surface(context, stack);
        AutomationProperties.SetName(surface, $"{title}. {detail}".Trim());
        AutomationProperties.SetLiveSetting(surface, AutomationLiveSetting.Polite);
        return surface;
    }

    private static Control RawJson(ArcMarkdownRenderContext context, string raw)
    {
        var code = new TextBlock
        {
            Text = raw,
            FontFamily = context.Style.MonoFont,
            FontSize = Math.Max(10, context.Style.SmallSize - 1),
            Foreground = context.Style.Foreground,
            TextWrapping = TextWrapping.NoWrap
        };

        var scroll = new ScrollViewer
        {
            Content = code,
            MaxHeight = 180,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            IsScrollChainingEnabled = false,
            AllowAutoHide = true
        };

        var header = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(new TextBlock
        {
            Text = "原始 JSON",
            FontSize = context.Style.CaptionSize,
            Foreground = context.Style.TextMuted,
            VerticalAlignment = VerticalAlignment.Center
        });

        if (context.Options.Clipboard is { } clipboard)
        {
            var copy = new ArcButton
            {
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Content = context.Options.Localizer.Get(ArcMarkdownStringKeys.Copy)
            };
            AutomationProperties.SetName(copy, "Copy the component JSON");
            copy.Click += (_, _) => _ = CopyAsync(clipboard, raw);
            header.Children.Add(copy);
        }

        return new StackPanel
        {
            Spacing = 4,
            Children =
            {
                header,
                new Expander
                {
                    Header = new TextBlock
                    {
                        Text = "查看载荷",
                        FontSize = context.Style.CaptionSize,
                        Foreground = context.Style.TextSecondary
                    },
                    Content = scroll,
                    IsExpanded = false
                }
            }
        };
    }

    private static async Task CopyAsync(IArcMarkdownClipboard clipboard, string text)
    {
        try
        {
            await clipboard.SetTextAsync(text).ConfigureAwait(true);
        }
        catch
        {
            // Copy is best effort: a host clipboard may be unavailable in headless environments.
        }
    }

    private static Border Surface(ArcMarkdownRenderContext context, Control child) => new()
    {
        Background = context.Style.SurfaceMuted,
        BorderBrush = context.Style.Border,
        BorderThickness = new Thickness(1),
        CornerRadius = context.Style.PanelRadius,
        Padding = new Thickness(14),
        Margin = new Thickness(0, 0, 0, context.Style.BlockSpacing),
        Child = child
    };
}
