using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Arc.Avalonia.Controls;
using Arc.Avalonia.Motion;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.Content.Controls;

/// <summary>The value emitted after every edit: clean HTML, Markdown, plain text and an empty flag.</summary>
public sealed record ArcRichTextValue(string Html, string Markdown, string Text, bool Empty)
{
    public override string ToString() => Markdown;
}

/// <summary>
/// A lightweight Markdown-marker editor built on Avalonia's TextBox: inline markers, block prefixes,
/// a selection toolbar, a slash command menu and a styled live preview. It intentionally does not
/// pretend to be a WYSIWYG contentEditable engine; the document is always the Markdown in the box.
/// </summary>
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcRichTextEditor : TemplatedControl
{
    public static readonly StyledProperty<string> HtmlProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(Html), string.Empty);

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(Markdown), string.Empty);

    public static readonly StyledProperty<string> DefaultHtmlProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(DefaultHtml), string.Empty);

    public static readonly StyledProperty<string> DefaultMarkdownProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(DefaultMarkdown), string.Empty);

    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(Placeholder), "Start writing");

    public static readonly StyledProperty<string> BlockHintProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(BlockHint), "Type / for blocks");

    public static readonly StyledProperty<bool> ReadOnlyProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, bool>(nameof(ReadOnly));

    public static readonly StyledProperty<bool> AutoFocusProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, bool>(nameof(AutoFocus));

    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ArcRichTextEditor, string>(nameof(Label), "Editor");

    private sealed record SlashCommand(string Title, string Description, string Icon, string Prefix, bool Code, string[] Keywords);

    private static readonly SlashCommand[] Commands =
    {
        new("Heading 1", "Large section title", "heading", "# ", false, new[] { "h1", "title", "heading" }),
        new("Heading 2", "Medium section title", "heading", "## ", false, new[] { "h2", "title", "heading" }),
        new("Bulleted list", "An unordered list", "list", "- ", false, new[] { "bullet", "list", "unordered" }),
        new("Numbered list", "An ordered list", "list-ordered", "1. ", false, new[] { "number", "ordered", "list" }),
        new("Quote", "Set text apart", "quote", "> ", false, new[] { "quote", "blockquote" }),
        new("Code block", "Preformatted code", "code", "```\n\n```", true, new[] { "code", "pre", "fence" }),
        new("Divider", "A horizontal rule", "minus", "\n---\n", false, new[] { "divider", "rule", "separator" })
    };

    private readonly List<ArcButton> _toolbarButtons = new();
    private readonly List<Border> _slashRows = new();
    private readonly List<SlashCommand> _visibleCommands = new();

    private TextBox? _textBox;
    private Border? _editorFrame;
    private Panel? _overlay;
    private Border? _toolbar;
    private Border? _slashMenu;
    private TextBox? _linkInput;
    private ContentControl? _previewHost;
    private Panel? _previewPanel;
    private ColumnDefinition? _previewColumn;
    private Border? _previewDivider;
    private TextBlock? _countText;
    private TextBlock? _blockHintText;
    private ArcButton? _previewButton;
    private bool _syncing;
    private bool _loadedInitial;
    private bool _previewVisible = true;
    private bool _linkMode;
    private bool _slashOpen;
    private int _slashStart;
    private int _slashIndex;
    private string _source = string.Empty;
    private int _savedStart;
    private int _savedLength;

    static ArcRichTextEditor()
    {
        HtmlProperty.Changed.AddClassHandler<ArcRichTextEditor>((editor, _) => editor.OnHtmlChanged());
        MarkdownProperty.Changed.AddClassHandler<ArcRichTextEditor>((editor, _) => editor.OnMarkdownChanged());
        ReadOnlyProperty.Changed.AddClassHandler<ArcRichTextEditor>((editor, _) => editor.UpdateReadOnly());
        PlaceholderProperty.Changed.AddClassHandler<ArcRichTextEditor>((editor, _) => editor.UpdatePlaceholder());
        LabelProperty.Changed.AddClassHandler<ArcRichTextEditor>((editor, _) =>
        {
            AutomationProperties.SetName(editor, editor.Label);
            if (editor._textBox is not null)
            {
                AutomationProperties.SetName(editor._textBox, editor.Label);
            }
        });
    }

    public ArcRichTextEditor()
    {
        Focusable = false;
    }

    /// <summary>Controlled HTML output (the documented <c>value</c> equivalent); setting it loads the document.</summary>
    public string Html
    {
        get => GetValue(HtmlProperty);
        set => SetValue(HtmlProperty, value);
    }

    /// <summary>Markdown source and output. Setting it replaces the document as one edit.</summary>
    public string Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Initial HTML when neither <see cref="Html"/> nor <see cref="Markdown"/> is set.</summary>
    public string DefaultHtml
    {
        get => GetValue(DefaultHtmlProperty);
        set => SetValue(DefaultHtmlProperty, value);
    }

    /// <summary>Initial Markdown when no HTML is supplied.</summary>
    public string DefaultMarkdown
    {
        get => GetValue(DefaultMarkdownProperty);
        set => SetValue(DefaultMarkdownProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>The hint on an empty line while the caret is in it.</summary>
    public string BlockHint
    {
        get => GetValue(BlockHintProperty);
        set => SetValue(BlockHintProperty, value);
    }

    public bool ReadOnly
    {
        get => GetValue(ReadOnlyProperty);
        set => SetValue(ReadOnlyProperty, value);
    }

    public bool AutoFocus
    {
        get => GetValue(AutoFocusProperty);
        set => SetValue(AutoFocusProperty, value);
    }

    /// <summary>Accessible name of the editing surface.</summary>
    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Raised after each edit with the clean HTML, Markdown, plain text and empty flag.</summary>
    public static readonly RoutedEvent<ArcRichTextChangedRoutedEventArgs> TextChangedEvent =
        RoutedEvent.Register<ArcRichTextEditor, ArcRichTextChangedRoutedEventArgs>("TextChanged", RoutingStrategies.Bubble);

    /// <summary>Bubbling companion to <see cref="TextChanged"/>.</summary>
    public event EventHandler<ArcRichTextChangedRoutedEventArgs>? TextChangedRouted
    {
        add => AddHandler(TextChangedEvent, value);
        remove => RemoveHandler(TextChangedEvent, value);
    }

    public event EventHandler<ArcRichTextValue>? TextChanged;

    public void FocusEditor()
    {
        _textBox?.Focus();
        if (_textBox is not null)
        {
            _textBox.CaretIndex = _textBox.Text?.Length ?? 0;
        }
    }

    public void SetMarkdown(string markdown)
    {
        LoadSource(markdown ?? string.Empty, focus: false);
    }

    public void SetHtml(string html)
    {
        LoadSource(ArcMarkdown.FromHtml(html), focus: false);
    }

    public void Clear()
    {
        LoadSource(string.Empty, focus: false);
    }

    public void Undo() => _textBox?.Undo();

    public void Redo() => _textBox?.Redo();

    /// <summary>Selects a phrase so a demo can show the floating selection toolbar.</summary>
    internal void SelectForDemo(string phrase)
    {
        if (_textBox is null)
        {
            return;
        }

        var index = _source.IndexOf(phrase, StringComparison.Ordinal);
        if (index < 0)
        {
            return;
        }

        _textBox.Focus();
        ArcContentUi.TintSelection(_textBox);
        _textBox.SelectionStart = index;
        _textBox.SelectionEnd = index + phrase.Length;
        _textBox.CaretIndex = index + phrase.Length;
        _savedStart = index;
        _savedLength = phrase.Length;
        UpdateSelectionToolbar();
        Dispatcher.UIThread.Post(PositionToolbar, DispatcherPriority.Loaded);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        AutomationProperties.SetName(this, Label);
        _host = e.NameScope.Find<Panel>("PART_Host");
        BuildShell();
    }

    private Panel? _host;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (AutoFocus)
        {
            Dispatcher.UIThread.Post(FocusEditor, DispatcherPriority.Loaded);
        }
    }

    private void OnHtmlChanged()
    {
        if (_syncing || !_loadedInitial || _textBox is null)
        {
            return;
        }

        var current = ArcMarkdown.ToHtml(_source);
        if (!string.Equals(current, Html, StringComparison.Ordinal))
        {
            LoadSource(ArcMarkdown.FromHtml(Html), focus: false);
        }
    }

    private void OnMarkdownChanged()
    {
        if (_syncing || !_loadedInitial || _textBox is null)
        {
            return;
        }

        if (!string.Equals(Markdown, _source, StringComparison.Ordinal))
        {
            LoadSource(Markdown, focus: false);
        }
    }

    private void BuildShell()
    {
        if (_host is null)
        {
            return;
        }

        if (!_loadedInitial)
        {
            _loadedInitial = true;
            _source = !string.IsNullOrWhiteSpace(Html)
                ? ArcMarkdown.FromHtml(Html)
                : !string.IsNullOrWhiteSpace(Markdown)
                    ? Markdown
                    : !string.IsNullOrWhiteSpace(DefaultHtml)
                        ? ArcMarkdown.FromHtml(DefaultHtml)
                        : DefaultMarkdown ?? string.Empty;
        }

        _host.Children.Clear();

        var shell = new Border
        {
            Background = ArcContentUi.Brush(ArcTokens.Surface),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusPanel),
            ClipToBounds = true
        };
        shell.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,Auto")
        };

        root.Children.Add(BuildHeader());

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,1,310"),
            ClipToBounds = false
        };
        _previewColumn = body.ColumnDefinitions[2];
        _previewDivider = BuildPreviewDivider();
        Grid.SetColumn(_previewDivider, 1);
        body.Children.Add(_previewDivider);
        body.Children.Add(BuildEditorArea());
        _previewPanel = BuildPreviewPanel();
        Grid.SetColumn(_previewPanel, 2);
        body.Children.Add(_previewPanel);

        Grid.SetRow(body, 1);
        root.Children.Add(body);

        var footer = BuildFooter();
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        _overlay = new Canvas { ClipToBounds = false, IsHitTestVisible = true };
        Grid.SetRow(_overlay, 1);
        Grid.SetColumnSpan(_overlay, 3);
        _overlay.Children.Add(BuildToolbar());
        _overlay.Children.Add(BuildSlashMenu());
        root.Children.Add(_overlay);

        shell.Child = root;
        _host.Children.Add(shell);

        if (_textBox is not null)
        {
            _syncing = true;
            _textBox.Text = _source;
            _syncing = false;
        }

        SyncOutputs(false);
        UpdatePreview(force: true);
        UpdateCounts();
        UpdateReadOnly();
        UpdatePreviewVisibility();
        UpdateBlockHint();
        UpdateSelectionToolbar();
    }

    private Control BuildHeader()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(16, 10, 12, 10)
        };

        var mode = ArcContentUi.Row(7,
            ArcContentUi.Icon("pencil", 14, ArcTokens.TextSecondary),
            ArcContentUi.Text("Markdown", "arc-label"));
        grid.Children.Add(mode);

        var right = ArcContentUi.Row(10);
        _countText = ArcContentUi.Caption(string.Empty);
        right.Children.Add(_countText);

        _previewButton = new ArcButton
        {
            Content = ArcContentUi.Row(6,
                ArcContentUi.Icon("eye", 14, ArcTokens.TextSecondary),
                ArcContentUi.Text("Preview", "arc-label")),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Padding = new Thickness(10, 0)
        };
        AutomationProperties.SetName(_previewButton, "Toggle preview");
        ToolTip.SetTip(_previewButton, "Show or hide the formatted preview");
        _previewButton.Click += (_, _) =>
        {
            _previewVisible = !_previewVisible;
            UpdatePreviewVisibility();
        };
        right.Children.Add(_previewButton);

        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    private Control BuildEditorArea()
    {
        _editorFrame = new Border
        {
            Margin = new Thickness(12, 0, 12, 12),
            Padding = new Thickness(14, 12),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            BorderThickness = new Thickness(2),
            Background = Brushes.Transparent
        };
        _editorFrame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);

        var stack = new StackPanel { Spacing = 6 };
        _textBox = new TextBox
        {
            Text = _source,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            MinHeight = 238,
            FontSize = ArcResources.Double(ArcTokens.TextBase),
            FontFamily = ArcResources.Font(ArcTokens.FontBody),
            VerticalContentAlignment = VerticalAlignment.Top,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = Placeholder,
            IsReadOnly = ReadOnly,
            ClearSelectionOnLostFocus = false,
            FocusAdorner = null,
            SelectionBrush = ArcContentUi.Brush(ArcTokens.AccentSubtle),
            SelectionForegroundBrush = ArcContentUi.Brush(ArcTokens.Foreground),
            CaretBrush = ArcContentUi.Brush(ArcTokens.AccentStrong)
        };
        ArcContentUi.BareTextBox(_textBox);
        AutomationProperties.SetName(_textBox, Label);
        AutomationProperties.SetHelpText(_textBox,
            "Markdown editor. Select text for formatting, type slash on an empty line for blocks, or press Ctrl+K for a link.");
        _textBox.Paint(TextBox.ForegroundProperty, ArcTokens.Foreground);
        _textBox.Paint(TextBox.PlaceholderForegroundProperty, ArcTokens.TextMuted);
        _textBox.TextChanged += OnEditorTextChanged;
        _textBox.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBox.SelectionStartProperty ||
                args.Property == TextBox.SelectionEndProperty ||
                args.Property == TextBox.CaretIndexProperty)
            {
                OnSelectionChanged();
            }
        };
        _textBox.GotFocus += (_, _) =>
        {
            if (_editorFrame is not null)
            {
                _editorFrame.Paint(Border.BorderBrushProperty, ArcTokens.FocusOutline);
            }
        };
        _textBox.LostFocus += (_, _) =>
        {
            if (_editorFrame is not null)
            {
                _editorFrame.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
            }
        };
        _textBox.KeyDown += OnEditorKeyDown;
        stack.Children.Add(_textBox);

        _blockHintText = ArcContentUi.Caption(BlockHint);
        _blockHintText.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        stack.Children.Add(_blockHintText);

        _editorFrame.Child = stack;
        return _editorFrame;
    }

    private Border BuildPreviewDivider()
    {
        var divider = new Border { Width = 1, Background = Brushes.Transparent };
        divider.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
        return divider;
    }

    private Panel BuildPreviewPanel()
    {
        var panel = new Panel();
        var frame = new Border
        {
            Background = Brushes.Transparent,
            Padding = new Thickness(18, 16, 18, 12),
            ClipToBounds = true
        };
        frame.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);

        var stack = new StackPanel { Spacing = 10 };
        _previewHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        var scroller = new ScrollViewer
        {
            Content = _previewHost,
            MaxHeight = 320,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        stack.Children.Add(ArcContentUi.Text("Preview", "arc-caption"));
        stack.Children.Add(scroller);
        frame.Child = stack;
        panel.Children.Add(frame);
        return panel;
    }

    private Control BuildFooter()
    {
        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(16, 8, 16, 9)
        };
        footer.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var hints = ArcContentUi.Row(10);
        var slash = new Border
        {
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusPill),
            Padding = new Thickness(7, 1),
            Child = ArcContentUi.Text("/", "arc-mono")
        };
        slash.Paint(Border.BackgroundProperty, ArcTokens.SurfaceMuted);
        slash.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
        slash.BorderThickness = new Thickness(1);
        hints.Children.Add(slash);
        hints.Children.Add(ArcContentUi.Caption(BlockHint));
        hints.Children.Add(ArcContentUi.Caption("Ctrl+B bold  Ctrl+K link"));
        grid.Children.Add(hints);

        var count = ArcContentUi.Caption(string.Empty);
        count.Name = "FooterCount";
        AutomationProperties.SetName(count, "Document length");
        Grid.SetColumn(count, 1);
        grid.Children.Add(count);
        footer.Child = grid;
        _footerCount = count;
        return footer;
    }

    private TextBlock? _footerCount;

    private Border BuildToolbar()
    {
        _toolbar = new Border
        {
            Background = ArcContentUi.Brush(ArcTokens.SurfaceRaised),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            Padding = new Thickness(4),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            IsVisible = false,
            ClipToBounds = false
        };
        _toolbar.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        Canvas.SetLeft(_toolbar, 16);
        Canvas.SetTop(_toolbar, 16);
        _toolbar.KeyDown += OnToolbarKeyDown;
        return _toolbar;
    }

    private void RebuildToolbar()
    {
        if (_toolbar is null)
        {
            return;
        }

        _toolbarButtons.Clear();
        if (_linkMode)
        {
            var linkRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center
            };
            _linkInput = ArcContentUi.Input("Paste a link", 34);
            _linkInput.Width = 210;
            _linkInput.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    ApplyLink();
                    args.Handled = true;
                }
                else if (args.Key == Key.Escape)
                {
                    CloseLinkMode();
                    args.Handled = true;
                }
            };
            linkRow.Children.Add(_linkInput);

            var apply = new ArcButton
            {
                Content = "Apply",
                Size = ArcButtonSize.Sm,
                Padding = new Thickness(12, 0)
            };
            apply.Click += (_, _) => ApplyLink();
            linkRow.Children.Add(apply);

            var close = new ArcButton
            {
                Content = ArcContentUi.Icon("x", 14, ArcTokens.TextSecondary),
                Variant = ArcButtonVariant.Ghost,
                Size = ArcButtonSize.Sm,
                Compact = true,
                Width = 32
            };
            AutomationProperties.SetName(close, "Close link field");
            close.Click += (_, _) => CloseLinkMode();
            linkRow.Children.Add(close);
            _toolbar.Child = linkRow;
            return;
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center
        };

        buttons.Children.Add(ToolbarButton("bold", "Bold", "Ctrl+B", () => WrapSelection("**", "**"), "**"));
        buttons.Children.Add(ToolbarButton("italic", "Italic", "Ctrl+I", () => WrapSelection("*", "*"), "*"));
        buttons.Children.Add(ToolbarButton("minus", "Strikethrough", "Ctrl+Shift+X", () => WrapSelection("~~", "~~"), "~~"));
        buttons.Children.Add(ToolbarButton("code", "Inline code", "Ctrl+E", () => WrapSelection("`", "`"), "`"));
        buttons.Children.Add(ToolbarButton("link", "Link", "Ctrl+K", OpenLinkMode, null));
        buttons.Children.Add(ToolbarButton("heading", "Heading", "", ToggleHeading, "# "));
        buttons.Children.Add(ToolbarButton("list", "Bulleted list", "", ToggleBullet, "- "));
        _toolbar.Child = buttons;
    }

    private ArcButton ToolbarButton(string icon, string label, string shortcut, Action action, string? activeMarker)
    {
        var brushToken = activeMarker is not null && IsMarkActive(activeMarker) ? ArcTokens.AccentStrong : ArcTokens.TextSecondary;
        var button = new ArcButton
        {
            Content = ArcContentUi.Icon(icon, 15, brushToken),
            Variant = ArcButtonVariant.Ghost,
            Size = ArcButtonSize.Sm,
            Compact = true,
            Width = 32,
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(0)
        };
        AutomationProperties.SetName(button, string.IsNullOrEmpty(shortcut) ? label : $"{label}, {shortcut}");
        ToolTip.SetTip(button, string.IsNullOrEmpty(shortcut) ? label : $"{label} ({shortcut})");
        button.Click += (_, _) => action();
        _toolbarButtons.Add(button);
        return button;
    }

    private Border BuildSlashMenu()
    {
        _slashMenu = new Border
        {
            Width = 292,
            MaxHeight = 292,
            Background = ArcContentUi.Brush(ArcTokens.SurfaceRaised),
            BorderThickness = new Thickness(1),
            CornerRadius = ArcContentUi.Radius(ArcTokens.RadiusControl),
            BoxShadow = ArcResources.Shadows(ArcTokens.ShadowFloating),
            IsVisible = false,
            ClipToBounds = true
        };
        _slashMenu.Paint(Border.BorderBrushProperty, ArcTokens.Border);
        Canvas.SetLeft(_slashMenu, 16);
        Canvas.SetTop(_slashMenu, 16);
        return _slashMenu;
    }

    private void RebuildSlashMenu()
    {
        if (_slashMenu is null)
        {
            return;
        }

        _slashRows.Clear();
        _visibleCommands.Clear();
        var query = SlashQuery();
        foreach (var command in Commands)
        {
            if (query.Length > 0 &&
                !command.Title.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !command.Description.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                !command.Keywords.Any(keyword => keyword.Contains(query, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _visibleCommands.Add(command);
        }

        _slashIndex = Math.Clamp(_slashIndex, 0, Math.Max(0, _visibleCommands.Count - 1));
        var stack = new StackPanel { Spacing = 1, Margin = new Thickness(4) };
        for (var i = 0; i < _visibleCommands.Count; i++)
        {
            var command = _visibleCommands[i];
            var index = i;
            var row = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 6),
                Background = i == _slashIndex ? ArcContentUi.Brush(ArcTokens.SurfaceMuted) : Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*,Auto") };
            var icon = ArcContentUi.Icon(command.Icon, 15, i == _slashIndex ? ArcTokens.AccentStrong : ArcTokens.TextSecondary);
            grid.Children.Add(icon);
            var text = new StackPanel { Spacing = 0 };
            text.Children.Add(ArcContentUi.Text(command.Title, "arc-label"));
            var description = ArcContentUi.Caption(command.Description);
            text.Children.Add(description);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            if (i == _slashIndex)
            {
                var enter = ArcContentUi.Caption("Enter");
                Grid.SetColumn(enter, 2);
                grid.Children.Add(enter);
            }

            row.Child = grid;
            AutomationProperties.SetName(row, $"{command.Title}. {command.Description}");
            row.PointerPressed += (_, args) =>
            {
                ChooseSlash(index);
                args.Handled = true;
            };
            _slashRows.Add(row);
            stack.Children.Add(row);
        }

        if (_visibleCommands.Count == 0)
        {
            var empty = ArcContentUi.Caption("No blocks match");
            empty.Margin = new Thickness(10, 8);
            stack.Children.Add(empty);
        }

        _slashMenu.Child = stack;
        AutomationProperties.SetName(_slashMenu, "Block commands");
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_syncing || _textBox is null)
        {
            return;
        }

        _source = _textBox.Text ?? string.Empty;
        SyncOutputs(true);
        UpdatePreview(force: !_previewVisible);
        UpdateCounts();
        UpdateBlockHint();
        UpdateSlashFromText();
        UpdateSelectionToolbar();
    }

    private void SyncOutputs(bool raise)
    {
        var html = ArcMarkdown.ToHtml(_source);
        _syncing = true;
        SetCurrentValue(MarkdownProperty, _source);
        SetCurrentValue(HtmlProperty, html);
        _syncing = false;

        if (raise)
        {
            var value = new ArcRichTextValue(html, _source, _source, string.IsNullOrWhiteSpace(_source));
        TextChanged?.Invoke(this, value);
        RaiseEvent(new ArcRichTextChangedRoutedEventArgs(TextChangedEvent, value));
        }
    }

    private void LoadSource(string source, bool focus)
    {
        _source = source ?? string.Empty;
        if (_textBox is not null)
        {
            _syncing = true;
            _textBox.Text = _source;
            _syncing = false;
        }

        _slashOpen = false;
        if (_slashMenu is not null)
        {
            _slashMenu.IsVisible = false;
        }

        SyncOutputs(false);
        UpdatePreview(force: true);
        UpdateCounts();
        UpdateBlockHint();
        UpdateSelectionToolbar();
        if (focus)
        {
            FocusEditor();
        }
    }

    private void UpdatePreview(bool force = false)
    {
        if (_previewHost is null || (!_previewVisible && !force))
        {
            return;
        }

        _previewHost.Content = ArcMarkdown.BuildPreview(_source);
    }

    private void UpdatePreviewVisibility()
    {
        if (_previewColumn is null || _previewPanel is null || _previewDivider is null || _previewButton is null)
        {
            return;
        }

        _previewColumn.Width = _previewVisible ? new GridLength(310, GridUnitType.Pixel) : new GridLength(0);
        _previewPanel.IsVisible = _previewVisible;
        _previewDivider.IsVisible = _previewVisible;
        _previewButton.Variant = _previewVisible ? ArcButtonVariant.Secondary : ArcButtonVariant.Ghost;
        if (_previewVisible)
        {
            UpdatePreview(force: true);
        }
    }

    private void UpdateCounts()
    {
        var words = _source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var chars = _source.Length;
        var text = $"{words} {ArcContentUi.Plural(words, "word", "words")}";
        if (_countText is not null)
        {
            _countText.Text = text;
        }

        if (_footerCount is not null)
        {
            _footerCount.Text = $"{chars} {ArcContentUi.Plural(chars, "character", "characters")}";
        }
    }

    private void UpdateBlockHint()
    {
        if (_blockHintText is null)
        {
            return;
        }

        _blockHintText.IsVisible = string.IsNullOrEmpty(_source);
    }

    private void UpdateReadOnly()
    {
        if (_textBox is null)
        {
            return;
        }

        _textBox.IsReadOnly = ReadOnly;
        if (ReadOnly)
        {
            _toolbar!.IsVisible = false;
            _slashMenu!.IsVisible = false;
        }
    }

    private void UpdatePlaceholder()
    {
        if (_textBox is not null)
        {
            _textBox.PlaceholderText = Placeholder;
        }
    }

    private void OnSelectionChanged()
    {
        if (_textBox is null)
        {
            return;
        }

        _savedStart = Math.Min(_textBox.SelectionStart, _textBox.SelectionEnd);
        _savedLength = Math.Abs(_textBox.SelectionEnd - _textBox.SelectionStart);
        UpdateSelectionToolbar();
    }

    private void UpdateSelectionToolbar()
    {
        if (_toolbar is null || _textBox is null || ReadOnly || _savedLength <= 0)
        {
            if (_toolbar is not null)
            {
                _toolbar.IsVisible = false;
            }

            return;
        }

        if (_toolbar.Child is null || _toolbarButtons.Count == 0 && !_linkMode)
        {
            RebuildToolbar();
        }

        _toolbar.IsVisible = true;
        if (!_linkMode)
        {
            RebuildToolbar();
        }

        Dispatcher.UIThread.Post(PositionToolbar, DispatcherPriority.Loaded);
    }

    private void PositionToolbar()
    {
        if (_toolbar is null || _overlay is null || !_toolbar.IsVisible)
        {
            return;
        }

        var point = CaretPoint(_savedStart);
        if (point is null)
        {
            Canvas.SetLeft(_toolbar, 16);
            Canvas.SetTop(_toolbar, 12);
            return;
        }

        _toolbar.Measure(Size.Infinity);
        var width = Math.Max(180, _toolbar.DesiredSize.Width);
        var height = Math.Max(32, _toolbar.DesiredSize.Height);
        var overlayWidth = _overlay.Bounds.Width > 0 ? _overlay.Bounds.Width : Bounds.Width;
        var left = Math.Clamp(point.Value.X - width / 2, 8, Math.Max(8, overlayWidth - width - 8));
        var top = point.Value.Y - height - 10;
        if (top < 4)
        {
            top = point.Value.Y + 26;
        }

        Canvas.SetLeft(_toolbar, left);
        Canvas.SetTop(_toolbar, top);
    }

    private Point? CaretPoint(int position)
    {
        if (_textBox is null || _overlay is null)
        {
            return null;
        }

        var presenter = _textBox.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault();
        var layout = presenter?.TextLayout;
        if (presenter is null || layout is null)
        {
            return null;
        }

        var rect = layout.HitTestTextPosition(Math.Clamp(position, 0, Math.Max(0, _source.Length)));
        var point = presenter.TranslatePoint(new Point(rect.X, rect.Y), _overlay);
        return point;
    }

    private void OnToolbarKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_linkMode || e.Key == Key.Escape)
        {
            if (e.Key == Key.Escape)
            {
                CloseLinkMode();
                FocusEditor();
                e.Handled = true;
                return;
            }
        }

        if (_toolbarButtons.Count == 0)
        {
            return;
        }

        var focused = _toolbarButtons.IndexOf(_toolbarButtons.FirstOrDefault(button => button.IsFocused)!);
        if (focused < 0)
        {
            focused = 0;
        }

        if (e.Key == Key.Right)
        {
            _toolbarButtons[(focused + 1) % _toolbarButtons.Count].Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            _toolbarButtons[(focused - 1 + _toolbarButtons.Count) % _toolbarButtons.Count].Focus();
            e.Handled = true;
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (_slashOpen && _textBox is not null)
        {
            switch (e.Key)
            {
                case Key.Down:
                    _slashIndex = Math.Min(_slashIndex + 1, Math.Max(0, _visibleCommands.Count - 1));
                    RebuildSlashMenu();
                    e.Handled = true;
                    return;
                case Key.Up:
                    _slashIndex = Math.Max(_slashIndex - 1, 0);
                    RebuildSlashMenu();
                    e.Handled = true;
                    return;
                case Key.Enter:
                case Key.Tab:
                    ChooseSlash(_slashIndex);
                    e.Handled = true;
                    return;
                case Key.Escape:
                    _slashOpen = false;
                    _slashMenu!.IsVisible = false;
                    e.Handled = true;
                    return;
            }
        }

        if (ReadOnly)
        {
            return;
        }

        if (e.Key == Key.F10 && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && _toolbar is { IsVisible: true })
        {
            _toolbarButtons.FirstOrDefault()?.Focus();
            e.Handled = true;
            return;
        }

        var command = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (command)
        {
            switch (e.Key)
            {
                case Key.B:
                    WrapSelection("**", "**");
                    e.Handled = true;
                    return;
                case Key.I:
                    WrapSelection("*", "*");
                    e.Handled = true;
                    return;
                case Key.X when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                    WrapSelection("~~", "~~");
                    e.Handled = true;
                    return;
                case Key.E:
                    WrapSelection("`", "`");
                    e.Handled = true;
                    return;
                case Key.K:
                    OpenLinkMode();
                    e.Handled = true;
                    return;
                case Key.U:
                    e.Handled = true;
                    return;
            }
        }

        if (e.Key == Key.Tab && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            InsertAtSelection("  ");
            e.Handled = true;
        }
    }

    private void WrapSelection(string open, string close)
    {
        if (_textBox is null || ReadOnly)
        {
            return;
        }

        var text = _textBox.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(_textBox.SelectionStart, _textBox.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(_textBox.SelectionStart, _textBox.SelectionEnd), start, text.Length);

        if (start == end)
        {
            var next = text.Insert(start, open + close);
            LoadSource(next, focus: true);
            _textBox.CaretIndex = start + open.Length;
            _textBox.SelectionStart = _textBox.SelectionEnd = _textBox.CaretIndex;
            return;
        }

        var selected = text[start..end];
        var surrounding = start >= open.Length && end + close.Length <= text.Length &&
                          text.Substring(start - open.Length, open.Length) == open &&
                          text.Substring(end, close.Length) == close;
        var inside = selected.Length > open.Length + close.Length &&
                     selected.StartsWith(open, StringComparison.Ordinal) &&
                     selected.EndsWith(close, StringComparison.Ordinal);

        string nextText;
        int nextStart;
        int nextEnd;
        if (surrounding)
        {
            nextText = text.Remove(end, close.Length).Remove(start - open.Length, open.Length);
            nextStart = start - open.Length;
            nextEnd = end - open.Length;
        }
        else if (inside)
        {
            nextText = text.Remove(end - close.Length, close.Length).Remove(start, open.Length);
            nextStart = start;
            nextEnd = end - open.Length - close.Length;
        }
        else
        {
            nextText = text.Insert(end, close).Insert(start, open);
            nextStart = start + open.Length;
            nextEnd = end + open.Length;
        }

        ApplyText(nextText, nextStart, nextEnd);
    }

    private void ToggleHeading()
    {
        ToggleLinePrefix("# ");
    }

    private void ToggleBullet()
    {
        ToggleLinePrefix("- ");
    }

    private void ToggleLinePrefix(string prefix)
    {
        if (_textBox is null)
        {
            return;
        }

        var text = (_textBox.Text ?? string.Empty).Replace("\r\n", "\n");
        var start = Math.Min(_textBox.SelectionStart, _textBox.SelectionEnd);
        var end = Math.Max(_textBox.SelectionStart, _textBox.SelectionEnd);
        var lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
        if (start == 0)
        {
            lineStart = 0;
        }

        var lineEnd = text.IndexOf('\n', end);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        var block = text[lineStart..lineEnd];
        var allPrefixed = block.Split('\n').All(line =>
            line.StartsWith(prefix, StringComparison.Ordinal) ||
            line.StartsWith("  " + prefix, StringComparison.Ordinal));
        var replacement = string.Join('\n', block.Split('\n').Select(line =>
        {
            if (allPrefixed)
            {
                return line.StartsWith(prefix, StringComparison.Ordinal)
                    ? line[prefix.Length..]
                    : line.StartsWith("  " + prefix, StringComparison.Ordinal)
                        ? line[(prefix.Length + 2)..]
                        : line;
            }

            return prefix + line;
        }));

        var nextText = text[..lineStart] + replacement + text[lineEnd..];
        ApplyText(nextText, lineStart + (allPrefixed ? 0 : prefix.Length),
            lineStart + replacement.Length);
    }

    private void InsertAtSelection(string insert)
    {
        if (_textBox is null)
        {
            return;
        }

        var text = _textBox.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(_textBox.SelectionStart, _textBox.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(_textBox.SelectionStart, _textBox.SelectionEnd), start, text.Length);
        ApplyText(text.Remove(start, end - start).Insert(start, insert), start + insert.Length, start + insert.Length);
    }

    private void ApplyText(string text, int selectionStart, int selectionEnd)
    {
        if (_textBox is null)
        {
            return;
        }

        _textBox.Text = text;
        _textBox.SelectionStart = Math.Clamp(selectionStart, 0, text.Length);
        _textBox.SelectionEnd = Math.Clamp(selectionEnd, 0, text.Length);
        _textBox.Focus();
    }

    private bool IsMarkActive(string marker)
    {
        if (_textBox is null)
        {
            return false;
        }

        var text = _textBox.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(_textBox.SelectionStart, _textBox.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(_textBox.SelectionStart, _textBox.SelectionEnd), start, text.Length);
        if (start >= marker.Length && end + marker.Length <= text.Length)
        {
            return text.Substring(start - marker.Length, marker.Length) == marker &&
                   text.Substring(end, marker.Length) == marker;
        }

        return false;
    }

    private void OpenLinkMode()
    {
        if (ReadOnly)
        {
            return;
        }

        _savedStart = Math.Min(_textBox?.SelectionStart ?? 0, _textBox?.SelectionEnd ?? 0);
        _savedLength = Math.Abs((_textBox?.SelectionEnd ?? 0) - (_textBox?.SelectionStart ?? 0));
        _linkMode = true;
        if (_toolbar is not null)
        {
            _toolbar.IsVisible = true;
        }

        RebuildToolbar();
        PositionToolbar();
        Dispatcher.UIThread.Post(() =>
        {
            _linkInput?.Focus();
            _linkInput?.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private void CloseLinkMode()
    {
        _linkMode = false;
        _linkInput = null;
        RebuildToolbar();
        UpdateSelectionToolbar();
        FocusEditor();
    }

    private void ApplyLink()
    {
        var url = ArcMarkdown.NormalizeUrl(_linkInput?.Text);
        if (_textBox is null)
        {
            return;
        }

        var text = _textBox.Text ?? string.Empty;
        var start = Math.Clamp(_savedStart, 0, text.Length);
        var end = Math.Clamp(start + _savedLength, start, text.Length);
        var label = start == end ? "link" : text[start..end];
        var link = $"[{label}]({url})";
        var next = text.Remove(start, end - start).Insert(start, link);
        _linkMode = false;
        _linkInput = null;
        ApplyText(next, start + 1, start + 1 + label.Length);
        UpdateSelectionToolbar();
    }

    private string SlashQuery()
    {
        if (_textBox is null || !_slashOpen)
        {
            return string.Empty;
        }

        var text = _textBox.Text ?? string.Empty;
        var caret = Math.Clamp(_textBox.CaretIndex, 0, text.Length);
        if (caret < _slashStart + 1)
        {
            return string.Empty;
        }

        var query = text[(_slashStart + 1)..caret];
        return query.Contains('\n') ? string.Empty : query;
    }

    private void UpdateSlashFromText()
    {
        if (_textBox is null || ReadOnly)
        {
            return;
        }

        var text = _textBox.Text ?? string.Empty;
        var caret = Math.Clamp(_textBox.CaretIndex, 0, text.Length);
        var lineStart = text.LastIndexOf('\n', Math.Max(0, caret - 1)) + 1;
        if (caret == 0)
        {
            lineStart = 0;
        }

        var line = text[lineStart..caret];
        var slash = line.LastIndexOf('/');
        var shouldOpen = slash >= 0 && line[..slash].Trim().Length == 0 && caret - (lineStart + slash) <= 24;
        if (shouldOpen)
        {
            _slashStart = lineStart + slash;
            _slashOpen = true;
            _slashIndex = 0;
            RebuildSlashMenu();
            _slashMenu!.IsVisible = true;
            Dispatcher.UIThread.Post(PositionSlashMenu, DispatcherPriority.Loaded);
        }
        else if (_slashOpen)
        {
            // Keep the menu while the user types a query straight after the slash. Anything else closes it.
            if (caret < _slashStart || text.Length <= _slashStart || text[_slashStart] != '/')
            {
                _slashOpen = false;
                _slashMenu!.IsVisible = false;
            }
            else
            {
                RebuildSlashMenu();
            }
        }
    }

    private void PositionSlashMenu()
    {
        if (_slashMenu is null || _overlay is null || !_slashMenu.IsVisible)
        {
            return;
        }

        var point = CaretPoint(_slashStart);
        if (point is null)
        {
            Canvas.SetLeft(_slashMenu, 20);
            Canvas.SetTop(_slashMenu, 160);
            return;
        }

        _slashMenu.Measure(Size.Infinity);
        var width = _slashMenu.Width;
        var height = Math.Max(80, _slashMenu.DesiredSize.Height);
        var overlayWidth = _overlay.Bounds.Width > 0 ? _overlay.Bounds.Width : Bounds.Width;
        var overlayHeight = _overlay.Bounds.Height > 0 ? _overlay.Bounds.Height : Bounds.Height;
        var top = point.Value.Y + 26;
        if (overlayHeight > 0 && top + height > overlayHeight - 8)
        {
            top = Math.Max(8, point.Value.Y - height - 10);
        }

        Canvas.SetLeft(_slashMenu, Math.Clamp(point.Value.X, 8, Math.Max(8, overlayWidth - width - 8)));
        Canvas.SetTop(_slashMenu, top);
    }

    private void ChooseSlash(int index)
    {
        if (index < 0 || index >= _visibleCommands.Count || _textBox is null)
        {
            return;
        }

        var command = _visibleCommands[index];
        var text = _textBox.Text ?? string.Empty;
        var caret = Math.Clamp(_textBox.CaretIndex, 0, text.Length);
        var lineStart = text.LastIndexOf('\n', Math.Max(0, _slashStart - 1)) + 1;
        if (_slashStart == 0)
        {
            lineStart = 0;
        }

        var prefix = command.Prefix;
        var next = text[..lineStart] + prefix + text[caret..];
        _slashOpen = false;
        _slashMenu!.IsVisible = false;

        var caretPosition = lineStart + prefix.Length;
        if (command.Code)
        {
            caretPosition = lineStart + prefix.IndexOf('\n') + 1;
        }
        else if (prefix.EndsWith('\n'))
        {
            caretPosition = lineStart + prefix.Length;
        }

        ApplyText(next, caretPosition, caretPosition);
    }
}




