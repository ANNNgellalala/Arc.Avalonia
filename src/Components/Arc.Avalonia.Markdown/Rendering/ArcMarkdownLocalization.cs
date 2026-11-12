namespace Arc.Avalonia.Markdown;

/// <summary>Built-in Chinese localizer; hosts can replace it through <see cref="ArcMarkdownOptions.Localizer"/>.</summary>
public sealed class ArcMarkdownLocalizer : IArcMarkdownLocalizer
{
    private readonly IReadOnlyDictionary<string, string> _copy;

    /// <summary>Creates a localizer from a key/value dictionary.</summary>
    public ArcMarkdownLocalizer(IReadOnlyDictionary<string, string> copy) => _copy = copy;

    /// <summary>Default Chinese copy used by the gallery and the docs platform.</summary>
    public static ArcMarkdownLocalizer Default { get; } = new(new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ArcMarkdownStringKeys.Copy] = "复制",
        [ArcMarkdownStringKeys.Copied] = "已复制",
        [ArcMarkdownStringKeys.CopyFailed] = "复制失败",
        [ArcMarkdownStringKeys.CopyCode] = "复制代码",
        [ArcMarkdownStringKeys.CodeLanguageFallback] = "代码",
        [ArcMarkdownStringKeys.CodeTruncated] = "…[代码已截断]",
        [ArcMarkdownStringKeys.Image] = "图片",
        [ArcMarkdownStringKeys.ImageLoading] = "正在加载图片…",
        [ArcMarkdownStringKeys.ImageBlocked] = "图片 {0} 已被安全策略阻止",
        [ArcMarkdownStringKeys.ImageLoadFailed] = "图片加载失败",
        [ArcMarkdownStringKeys.UnsupportedNode] = "暂不支持的 Markdown 节点：{0}",
        [ArcMarkdownStringKeys.EmptyDocument] = "暂无内容",
        [ArcMarkdownStringKeys.AgentComponentError] = "组件解析失败",
        [ArcMarkdownStringKeys.AgentUnsupported] = "不支持的 Agent 组件：{0}",
        [ArcMarkdownStringKeys.AgentChoice] = "请选择",
        [ArcMarkdownStringKeys.AgentConfirm] = "请确认",
        [ArcMarkdownStringKeys.Submit] = "提交",
        [ArcMarkdownStringKeys.Cancel] = "取消",
        [ArcMarkdownStringKeys.Confirm] = "确认",
        [ArcMarkdownStringKeys.LinkMenu] = "链接操作",
        [ArcMarkdownStringKeys.CopyLink] = "复制链接",
        [ArcMarkdownStringKeys.LinkCopied] = "链接已复制",
        [ArcMarkdownStringKeys.OpenInBrowser] = "在浏览器中打开",
        [ArcMarkdownStringKeys.OpenLinkFailed] = "打开链接失败",
        [ArcMarkdownStringKeys.OpenLinkUnavailable] = "该链接无法在浏览器中打开",
    });

    /// <inheritdoc />
    public string Get(string key, params object?[] arguments)
    {
        var template = _copy.TryGetValue(key, out var value) ? value : key;
        return arguments.Length == 0 ? template : string.Format(template, arguments);
    }
}

/// <summary>English copy for hosts that do not want the Chinese defaults.</summary>
public sealed class ArcMarkdownEnglishLocalizer : IArcMarkdownLocalizer
{
    /// <summary>Shared instance.</summary>
    public static ArcMarkdownEnglishLocalizer Instance { get; } = new();

    private ArcMarkdownEnglishLocalizer()
    {
    }

    /// <inheritdoc />
    public string Get(string key, params object?[] arguments)
    {
        var template = key switch
        {
            ArcMarkdownStringKeys.Copy => "Copy",
            ArcMarkdownStringKeys.Copied => "Copied",
            ArcMarkdownStringKeys.CopyFailed => "Copy failed",
            ArcMarkdownStringKeys.CopyCode => "Copy code",
            ArcMarkdownStringKeys.CodeLanguageFallback => "Code",
            ArcMarkdownStringKeys.CodeTruncated => "…[truncated]",
            ArcMarkdownStringKeys.Image => "Image",
            ArcMarkdownStringKeys.ImageLoading => "Loading image…",
            ArcMarkdownStringKeys.ImageBlocked => "Image {0} blocked by policy",
            ArcMarkdownStringKeys.ImageLoadFailed => "Image failed to load",
            ArcMarkdownStringKeys.UnsupportedNode => "Unsupported Markdown node: {0}",
            ArcMarkdownStringKeys.EmptyDocument => "No content",
            ArcMarkdownStringKeys.AgentComponentError => "Component failed to parse",
            ArcMarkdownStringKeys.AgentUnsupported => "Unsupported agent component: {0}",
            ArcMarkdownStringKeys.AgentChoice => "Choose",
            ArcMarkdownStringKeys.AgentConfirm => "Confirm",
            ArcMarkdownStringKeys.Submit => "Submit",
            ArcMarkdownStringKeys.Cancel => "Cancel",
            ArcMarkdownStringKeys.Confirm => "Confirm",
            ArcMarkdownStringKeys.LinkMenu => "Link actions",
            ArcMarkdownStringKeys.CopyLink => "Copy link",
            ArcMarkdownStringKeys.LinkCopied => "Link copied",
            ArcMarkdownStringKeys.OpenInBrowser => "Open in browser",
            ArcMarkdownStringKeys.OpenLinkFailed => "Could not open link",
            ArcMarkdownStringKeys.OpenLinkUnavailable => "This link cannot be opened in a browser",
            _ => key,
        };

        return arguments.Length == 0 ? template : string.Format(template, arguments);
    }
}
