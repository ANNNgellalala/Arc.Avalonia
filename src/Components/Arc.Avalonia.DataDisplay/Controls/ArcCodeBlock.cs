using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Arc.Avalonia.Theme;

namespace Arc.Avalonia.DataDisplay.Controls;

/// <summary>
/// A source surface with filename, language label, line numbers, a lightweight syntax tint, copy,
/// and an optional Show all collapse for long files.
/// </summary>
[TemplatePart("PART_Host", typeof(Panel))]
public class ArcCodeBlock : TemplatedControl
{
    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<ArcCodeBlock, string>(nameof(Code), string.Empty);

    public static readonly StyledProperty<string?> FilenameProperty =
        AvaloniaProperty.Register<ArcCodeBlock, string?>(nameof(Filename));

    public static readonly StyledProperty<string> LanguageProperty =
        AvaloniaProperty.Register<ArcCodeBlock, string>(nameof(Language), "tsx");

    public static readonly StyledProperty<int> MaxLinesProperty =
        AvaloniaProperty.Register<ArcCodeBlock, int>(nameof(MaxLines));

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "import", "from", "export", "function", "const", "let", "var", "return", "if", "else", "for",
        "while", "new", "class", "public", "private", "async", "await", "true", "false", "null",
        "undefined", "this", "interface", "type", "extends", "implements", "default", "in", "of",
        "typeof", "as", "void", "number", "string", "boolean", "throw", "try", "catch", "finally",
        "static", "readonly", "enum", "namespace", "use", "package", "func", "struct"
    };

    private Panel? _host;
    private bool _expanded;
    private bool _copied;
    private DispatcherTimer? _copyTimer;

    static ArcCodeBlock()
    {
        CodeProperty.Changed.AddClassHandler<ArcCodeBlock>((block, _) => block.Rebuild());
        FilenameProperty.Changed.AddClassHandler<ArcCodeBlock>((block, _) => block.Rebuild());
        LanguageProperty.Changed.AddClassHandler<ArcCodeBlock>((block, _) => block.Rebuild());
        MaxLinesProperty.Changed.AddClassHandler<ArcCodeBlock>((block, _) => block.Rebuild());
    }

    /// <summary>Source shown and copied.</summary>
    public string Code
    {
        get => GetValue(CodeProperty);
        set => SetValue(CodeProperty, value);
    }

    /// <summary>File name in the header; defaults to "Source code".</summary>
    public string? Filename
    {
        get => GetValue(FilenameProperty);
        set => SetValue(FilenameProperty, value);
    }

    /// <summary>Header label and highlighter mode.</summary>
    public string Language
    {
        get => GetValue(LanguageProperty);
        set => SetValue(LanguageProperty, value);
    }

    /// <summary>Collapses longer sources to this many lines with a Show all toggle.</summary>
    public int MaxLines
    {
        get => GetValue(MaxLinesProperty);
        set => SetValue(MaxLinesProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _host = e.NameScope.Find<Panel>("PART_Host");
        _expanded = false;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _copyTimer?.Stop();
        _copyTimer = null;
    }

    private void Rebuild()
    {
        if (_host is null)
        {
            return;
        }

        _host.Children.Clear();

        var lines = (Code ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        var max = MaxLines;
        var collapsed = max > 0 && lines.Length > max && !_expanded;
        var shown = collapsed ? lines.Take(max).ToArray() : lines;
        var shownCode = string.Join("\n", shown);

        var shell = new Border
        {
            CornerRadius = FeedbackUi.Radius(ArcTokens.RadiusPanel),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };
        shell.Paint(Border.BackgroundProperty, ArcTokens.Surface);
        shell.Paint(Border.BorderBrushProperty, ArcTokens.Border);

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var header = new Grid { Margin = new Thickness(18, 10, 10, 10) };
        header.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var name = FeedbackUi.Text(string.IsNullOrWhiteSpace(Filename) ? "Source code" : Filename!, "arc-caption");
        name.FontFamily = FeedbackUi.MonoFamily;
        name.FontSize = 12;
        name.Paint(TextBlock.ForegroundProperty, ArcTokens.TextSecondary);
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.VerticalAlignment = VerticalAlignment.Center;
        header.Children.Add(name);

        var language = FeedbackUi.Text(Language.ToLowerInvariant());
        language.FontFamily = FeedbackUi.MonoFamily;
        language.FontSize = 11;
        language.Margin = new Thickness(0, 0, 8, 0);
        var badge = FeedbackUi.Pill(language, ArcTokens.SurfaceMuted, ArcTokens.TextMuted, 8, 2);
        Grid.SetColumn(badge, 1);
        header.Children.Add(badge);

        var copy = new ArcIconButton
        {
            Icon = _copied ? "check" : "copy",
            IconSize = 15,
            BoxSize = 28,
            Label = _copied ? "Copied" : "Copy code",
            Muted = !_copied
        };
        Grid.SetColumn(copy, 2);
        copy.Click += (_, _) => CopyCode();
        header.Children.Add(copy);
        grid.Children.Add(header);

        var divider = FeedbackUi.Divider();
        Grid.SetRow(divider, 1);
        grid.Children.Add(divider);

        var body = new Grid { Margin = new Thickness(0) };
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        body.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        body.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));

        var numbers = new TextBlock
        {
            Text = string.Join("\n", Enumerable.Range(1, shown.Length)),
            FontFamily = FeedbackUi.MonoFamily,
            FontSize = 12.5,
            LineHeight = 20,
            TextAlignment = TextAlignment.Right,
            TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(16, 12, 12, 14),
            MinWidth = 26
        };
        numbers.FontFeatures = FeedbackUi.Tabular;
        numbers.Paint(TextBlock.ForegroundProperty, ArcTokens.TextMuted);
        body.Children.Add(numbers);

        var separator = new Border { Width = 1, Margin = new Thickness(0, 12, 0, 12) };
        separator.Paint(Border.BackgroundProperty, ArcTokens.BorderSubtle);
        Grid.SetColumn(separator, 1);
        body.Children.Add(separator);

        var code = BuildCode(shownCode);
        code.Margin = new Thickness(14, 12, 18, 14);
        Grid.SetColumn(code, 2);
        body.Children.Add(code);

        var scroller = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = true
        };
        Grid.SetRow(scroller, 2);
        grid.Children.Add(scroller);

        var footerRow = 3;
        if (collapsed)
        {
            var toggleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            toggleRow.Children.Add(FeedbackUi.Icon("chevron-down", 14, ArcTokens.TextSecondary));
            toggleRow.Children.Add(FeedbackUi.Text($"Show all {lines.Length} lines", "arc-caption"));

            var toggle = new ArcFlatButton
            {
                Content = toggleRow,
                Margin = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            AutomationProperties.SetName(toggle, $"Show all {lines.Length} lines");
            toggle.Click += (_, _) =>
            {
                _expanded = true;
                Rebuild();
            };

            var footer = new Border
            {
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(4, 0),
                Child = toggle
            };
            footer.Paint(Border.BorderBrushProperty, ArcTokens.BorderSubtle);
            Grid.SetRow(footer, footerRow);
            grid.Children.Add(footer);
        }

        shell.Child = grid;
        _host.Children.Add(shell);

        AutomationProperties.SetName(this, $"{(!string.IsNullOrWhiteSpace(Filename) ? Filename : "Source code")} source code");
    }

    private TextBlock BuildCode(string source)
    {
        var block = new TextBlock
        {
            FontFamily = FeedbackUi.MonoFamily,
            FontSize = 12.5,
            LineHeight = 20,
            TextWrapping = TextWrapping.NoWrap
        };

        foreach (var (text, token) in Tokenize(source))
        {
            FeedbackUi.AddRun(block, text, token);
        }

        return block;
    }

    /// <summary>A tiny regex-free scan that tints comments, strings, numbers, and keywords.</summary>
    private static IEnumerable<(string Text, string? Token)> Tokenize(string source)
    {
        var result = new List<(string, string?)>();
        var plain = new StringBuilder();
        var index = 0;

        void Flush()
        {
            if (plain.Length > 0)
            {
                result.Add((plain.ToString(), null));
                plain.Clear();
            }
        }

        while (index < source.Length)
        {
            var current = source[index];

            if ((current == '/' && index + 1 < source.Length && source[index + 1] == '/') ||
                (current == '#' && (index == 0 || source[index - 1] == '\n')))
            {
                Flush();
                var end = source.IndexOf('\n', index);
                if (end < 0)
                {
                    end = source.Length;
                }

                result.Add((source[index..end], ArcTokens.TextMuted));
                index = end;
                continue;
            }

            if (current == '"' || current == '\'' || current == '`')
            {
                Flush();
                var quote = current;
                var end = index + 1;
                while (end < source.Length)
                {
                    if (source[end] == '\\')
                    {
                        end += 2;
                        continue;
                    }

                    if (source[end] == quote)
                    {
                        end++;
                        break;
                    }

                    end++;
                }

                result.Add((source[index..Math.Min(end, source.Length)], ArcTokens.Success));
                index = end;
                continue;
            }

            if (char.IsDigit(current))
            {
                Flush();
                var end = index;
                while (end < source.Length && (char.IsDigit(source[end]) || source[end] == '.' || source[end] == '_'))
                {
                    end++;
                }

                result.Add((source[index..end], ArcTokens.Series2));
                index = end;
                continue;
            }

            if (char.IsLetter(current) || current == '_' || current == '$')
            {
                Flush();
                var end = index;
                while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_' || source[end] == '$'))
                {
                    end++;
                }

                var word = source[index..end];
                if (Keywords.Contains(word))
                {
                    result.Add((word, ArcTokens.AccentStrong));
                }
                else
                {
                    result.Add((word, null));
                }

                index = end;
                continue;
            }

            plain.Append(current);
            index++;
        }

        Flush();
        return result;
    }

    private async void CopyCode()
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is not null)
            {
                await clipboard.SetTextAsync(Code ?? string.Empty);
            }
        }
        catch
        {
            // Clipboard access can be denied; the button still reports nothing rather than failing.
        }

        _copied = true;
        Rebuild();
        _copyTimer?.Stop();
        _copyTimer = new DispatcherTimer(TimeSpan.FromSeconds(1.6), DispatcherPriority.Background, (_, _) =>
        {
            _copied = false;
            _copyTimer?.Stop();
            _copyTimer = null;
            Rebuild();
        });
        _copyTimer.Start();
    }
}
