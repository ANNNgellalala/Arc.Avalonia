using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Markdown.Controls;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// Arc-styled fenced code surface: language label, line numbers, syntax colouring, copy action and an
/// optional collapse for very long blocks. Used by the Markdown code-fence renderer and usable standalone.
/// <para>
/// All colours, sizes and spacing come from <see cref="ArcMarkdownStyle"/>; the block re-reads the style
/// snapshot and rebuilds itself when the application theme variant changes. A highlighter that throws or
/// returns invalid spans degrades to plain text instead of failing the render.
/// </para>
/// </summary>
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcMarkdownCodeBlock : TemplatedControl
{
    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, string>(nameof(Code), string.Empty);

    public static readonly StyledProperty<string?> LanguageProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, string?>(nameof(Language));

    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, bool>(nameof(ShowLineNumbers));

    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, int>(nameof(MaxLines));

    public static readonly StyledProperty<IArcMarkdownCodeHighlighter?> HighlighterProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, IArcMarkdownCodeHighlighter?>(nameof(Highlighter));

    public static readonly StyledProperty<IArcMarkdownLocalizer?> LocalizerProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, IArcMarkdownLocalizer?>(nameof(Localizer));

    public static readonly StyledProperty<IArcMarkdownClipboard?> ClipboardProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, IArcMarkdownClipboard?>(nameof(Clipboard));

    public static readonly StyledProperty<string?> OriginalCodeProperty =
        AvaloniaProperty.Register<ArcMarkdownCodeBlock, string?>(nameof(OriginalCode));

    private Panel? _host;
    private ArcButton? _copyButton;
    private DispatcherTimer? _copyTimer;
    private bool _expanded;
    private bool _subscribedToTheme;

    static ArcMarkdownCodeBlock()
    {
        CodeProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
        LanguageProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
        ShowLineNumbersProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
        MaxLinesProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
        HighlighterProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
        LocalizerProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
        ClipboardProperty.Changed.AddClassHandler<ArcMarkdownCodeBlock>((block, _) => block.Rebuild());
    }

    /// <summary>Source shown and copied.</summary>
    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>Fence language; drives the highlighter and the header label.</summary>
    public string? Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>Shows a line-number gutter.</summary>
    public bool ShowLineNumbers
    {
        get => GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    /// <summary>Full source when <see cref="Code"/> was truncated; the copy action uses it.</summary>
    public string? OriginalCode
    {
        get => GetValue(OriginalCodeProperty);
        set => SetValue(OriginalCodeProperty, value);
    }

    /// <summary>Collapses the block to this many lines with a "show all" toggle (0 = no limit).</summary>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    /// <summary>Optional highlighter; defaults to <see cref="ArcMarkdownHighlighter.Default"/>.</summary>
    public IArcMarkdownCodeHighlighter? Highlighter
    {
        get => GetValue(HighlighterProperty);
        set => SetValue(HighlighterProperty, value);
    }

    /// <summary>Optional localizer for copy labels.</summary>
    public IArcMarkdownLocalizer? Localizer
    {
        get => GetValue(LocalizerProperty);
        set => SetValue(LocalizerProperty, value);
    }

    /// <summary>Optional clipboard; when null the block uses the TopLevel clipboard.</summary>
    public IArcMarkdownClipboard? Clipboard
    {
        get => GetValue(ClipboardProperty);
        set => SetValue(ClipboardProperty, value);
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        _expanded = false;
        Rebuild();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_subscribedToTheme || Application.Current is null)
        {
            return;
        }

        Application.Current.ActualThemeVariantChanged += OnThemeVariantChanged;
        _subscribedToTheme = true;
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_subscribedToTheme && Application.Current is not null)
        {
            Application.Current.ActualThemeVariantChanged -= OnThemeVariantChanged;
            _subscribedToTheme = false;
        }

        _copyTimer?.Stop();
        _copyTimer = null;
        _copyButton = null;
    }

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        // ArcMarkdownStyle is a token snapshot; refresh it and rebuild the code runs with the new brushes.
        Dispatcher.UIThread.Post(() =>
        {
            ArcMarkdownStyle.Current = new ArcMarkdownStyle();
            Rebuild();
        }, DispatcherPriority.Background);
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();
        _copyButton = null;

        var style = ArcMarkdownStyle.Current;
        var localizer = Localizer ?? ArcMarkdownLocalizer.Default;
        var language = string.IsNullOrWhiteSpace(Language) ? null : Language.Trim();
        var source = (Code ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = source.Split('\n');
        var collapsed = MaxLines > 0 && lines.Length > MaxLines && !_expanded;
        var shown = collapsed ? lines.Take(MaxLines).ToArray() : lines;
        var shownSource = string.Join("\n", shown);

        var header = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = language ?? localizer.Get(ArcMarkdownStringKeys.CodeLanguageFallback),
            Foreground = style.TextMuted,
            FontFamily = style.MonoFont,
            FontSize = style.CaptionSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);
        header.Children.Add(label);

        var copy = new ArcButton
        {
            Content = localizer.Get(ArcMarkdownStringKeys.Copy),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            MinHeight = 44,
            MinWidth = 44,
        };
        AutomationProperties.SetName(copy, localizer.Get(ArcMarkdownStringKeys.CopyCode));
        var copyGuard = new ArcMarkdownTapGuard();
        copyGuard.Attach(copy);
        copy.Click += (sender, args) =>
        {
            if (!copyGuard.IsCancelled)
            {
                OnCopyClick(sender, args);
            }
        };
        Grid.SetColumn(copy, 1);
        header.Children.Add(copy);
        _copyButton = copy;

        var codeText = new TextBlock
        {
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = style.MonoFont,
            FontSize = style.SmallSize,
            Foreground = style.Foreground,
        };

        var body = new Grid();
        if (ShowLineNumbers)
        {
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            var gutter = new TextBlock
            {
                Text = string.Join("\n", Enumerable.Range(1, Math.Max(shown.Length, 1))),
                TextAlignment = TextAlignment.Right,
                Foreground = style.TextMuted,
                FontFamily = style.MonoFont,
                FontSize = style.SmallSize,
                Margin = new Thickness(0, 0, 12, 0),
            };
            Grid.SetColumn(gutter, 0);
            body.Children.Add(gutter);
            Grid.SetColumn(codeText, 1);
            body.Children.Add(codeText);
        }
        else
        {
            body.Children.Add(codeText);
        }

        AppendHighlighted(codeText, shownSource, language, Highlighter ?? ArcMarkdownHighlighter.Default, style);

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(header);
        stack.Children.Add(new ScrollViewer
        {
            // Horizontal scrolling belongs to the code block alone: it never chains to the vertical page
            // scroll, and the scrollbar stays out of the way until the block is actually touched.
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsScrollChainingEnabled = false,
            AllowAutoHide = true,
            Content = body,
        });

        if (MaxLines > 0 && lines.Length > MaxLines)
        {
            var toggle = new ArcButton
            {
                Content = _expanded ? "收起" : $"展开全部（{lines.Length} 行）",
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                MinHeight = 44,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0),
            };
            AutomationProperties.SetName(toggle, _expanded ? "收起代码块" : "展开全部代码");
            var toggleGuard = new ArcMarkdownTapGuard();
            toggleGuard.Attach(toggle);
            toggle.Click += (_, _) =>
            {
                if (toggleGuard.IsCancelled)
                {
                    return;
                }

                _expanded = !_expanded;
                Rebuild();
            };
            stack.Children.Add(toggle);
        }

        _host.Children.Add(stack);
    }

    private async void OnCopyClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var localizer = Localizer ?? ArcMarkdownLocalizer.Default;
        if (sender is not ArcButton button)
        {
            return;
        }

        try
        {
            var text = Code ?? string.Empty;
            if (Clipboard is { } clipboard)
            {
                await clipboard.SetTextAsync(text);
            }
            else if (TopLevel.GetTopLevel(this)?.Clipboard is { } topLevelClipboard)
            {
                await topLevelClipboard.SetTextAsync(text);
            }

            button.Content = localizer.Get(ArcMarkdownStringKeys.Copied);
        }
        catch
        {
            button.Content = localizer.Get(ArcMarkdownStringKeys.CopyFailed);
        }

        _copyTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _copyTimer.Stop();
        _copyTimer.Tick -= OnCopyTimerTick;
        _copyTimer.Tick += OnCopyTimerTick;
        _copyTimer.Start();
    }

    private void OnCopyTimerTick(object? sender, EventArgs e)
    {
        _copyTimer?.Stop();
        if (_copyButton is not null)
        {
            _copyButton.Content = (Localizer ?? ArcMarkdownLocalizer.Default).Get(ArcMarkdownStringKeys.Copy);
        }
    }

    private static void AppendHighlighted(
        TextBlock target,
        string code,
        string? language,
        IArcMarkdownCodeHighlighter highlighter,
        ArcMarkdownStyle style)
    {
        IReadOnlyList<ArcMarkdownHighlightSpan> spans;
        try
        {
            spans = language is not null && highlighter.CanHighlight(language)
                ? highlighter.Highlight(code, language)
                : Array.Empty<ArcMarkdownHighlightSpan>();
        }
        catch
        {
            spans = Array.Empty<ArcMarkdownHighlightSpan>();
        }

        if (spans.Count == 0)
        {
            target.Text = code;
            return;
        }

        var cursor = 0;
        foreach (var span in spans)
        {
            var start = Math.Clamp(span.Start, 0, code.Length);
            var end = Math.Clamp(span.Start + Math.Max(span.Length, 0), start, code.Length);
            if (end <= start || start < cursor)
            {
                continue;
            }

            if (start > cursor)
            {
                target.Inlines!.Add(new Run(code[cursor..start]));
            }

            target.Inlines!.Add(new Run(code[start..end])
            {
                Foreground = TokenBrush(span.Kind, style),
            });
            cursor = end;
        }

        if (cursor < code.Length)
        {
            target.Inlines!.Add(new Run(code[cursor..]));
        }
    }

    private static IBrush TokenBrush(ArcMarkdownTokenKind kind, ArcMarkdownStyle style) => kind switch
    {
        ArcMarkdownTokenKind.Comment => style.TextMuted,
        ArcMarkdownTokenKind.Keyword => style.Accent,
        ArcMarkdownTokenKind.String => style.Success,
        ArcMarkdownTokenKind.Number => style.WarningBrush,
        ArcMarkdownTokenKind.Type => style.Accent,
        ArcMarkdownTokenKind.Function => style.Accent,
        ArcMarkdownTokenKind.Variable => style.WarningBrush,
        ArcMarkdownTokenKind.Tag => style.Accent,
        ArcMarkdownTokenKind.Attribute => style.WarningBrush,
        ArcMarkdownTokenKind.Addition => style.Success,
        ArcMarkdownTokenKind.Deletion => style.Danger,
        ArcMarkdownTokenKind.Operator or ArcMarkdownTokenKind.Punctuation => style.TextSecondary,
        _ => style.Foreground,
    };
}

