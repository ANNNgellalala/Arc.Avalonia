using Markdig;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using PuraMarkdown.Core;

namespace Arc.Avalonia.Markdown;

/// <summary>
/// GFM autolinks only start after whitespace or at the start of a line, so a bare URL glued to
/// Chinese/full-width punctuation (<c>下载页：https://example.com</c>) stays plain text. This
/// extension adds a second inline parser for bare <c>http://</c> / <c>https://</c> URLs that also
/// accepts a CJK or other non-whitespace left boundary and emits a Markdig
/// <see cref="AutolinkInline"/> (the same node type the core <c>&lt;…&gt;</c> autolink parser uses,
/// which Arc already renders through its autolink renderer).
/// </summary>
internal static class BareUrlAutolinks
{
    /// <summary>
    /// The extension instance to register (for example through
    /// <see cref="PuraSyntaxBuilder.UseMarkdownExtension"/>). It is stateless, so the same instance
    /// is safe to reuse across pipelines.
    /// </summary>
    internal static IMarkdownExtension Extension { get; } = new BareUrlAutolinkExtension();

    /// <summary>
    /// Registers the extension on a PuraMarkdown syntax builder. Call it after
    /// <see cref="PuraSyntaxBuilder.UseGfm"/>: the GFM autolink parser then keeps priority for
    /// line-start/whitespace URLs and this parser only supplies the CJK-boundary cases.
    /// </summary>
    /// <param name="builder">Syntax builder to register on.</param>
    /// <returns>The same builder, for chaining.</returns>
    internal static PuraSyntaxBuilder UseBareUrlAutolinks(this PuraSyntaxBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseMarkdownExtension(Extension);
    }
}

/// <summary>Markdig extension that registers <see cref="BareUrlAutolinkParser"/>.</summary>
internal sealed class BareUrlAutolinkExtension : IMarkdownExtension
{
    /// <inheritdoc />
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        // Appended after the GFM autolink parser (UseAutoLinks) so the whitespace/line-start
        // behaviour handled there is untouched; this parser only adds the extra boundaries.
        pipeline.InlineParsers.AddIfNotAlready(new BareUrlAutolinkParser());
    }

    /// <inheritdoc />
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        // AutolinkInline already has HTML and Arc renderers; nothing to register per renderer.
    }
}

/// <summary>
/// Inline parser for bare <c>http://</c> / <c>https://</c> URLs. Unlike the GFM parser it does not
/// require whitespace before the URL, and it terminates the URL at CJK punctuation, at whitespace,
/// and at the CommonMark inline delimiters <c>&lt; &gt; " ' ` * [ ]</c>, then trims trailing
/// punctuation and unbalanced closing brackets.
/// </summary>
internal sealed class BareUrlAutolinkParser : InlineParser
{
    private const int HttpSchemeLength = 7;

    /// <summary>Initializes a new instance of the <see cref="BareUrlAutolinkParser"/> class.</summary>
    public BareUrlAutolinkParser()
    {
        OpeningCharacters = ['h', 'H'];
    }

    /// <inheritdoc />
    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        ArgumentNullException.ThrowIfNull(processor);
        var text = slice.Text;
        var start = slice.Start;
        var end = slice.End;
        var schemeLength = MatchScheme(text, start, end);
        if (schemeLength == 0 || !IsLeftBoundary(text, start) || IsLinkContext(processor))
        {
            return false;
        }

        // The scheme alone is not a URL: require at least one following character.
        var contentStart = start + schemeLength;
        if (contentStart > end)
        {
            return false;
        }

        var last = contentStart;
        while (last <= end && !IsUrlTerminator(text[last]))
        {
            last++;
        }

        // last now points one past the candidate; trim to an inclusive end.
        last--;
        if (last < contentStart)
        {
            return false;
        }

        last = TrimTrailing(text, start, contentStart, last);
        if (last < contentStart)
        {
            return false;
        }

        // Mirror Markdig's own autolink parser: an emphasis delimiter still open around the URL
        // (for example `_见https://example.com/a_`) must not be swallowed by the URL.
        var pendingEmphasis = GetPendingEmphasis(processor);
        while (last >= contentStart && IsPendingEmphasisChar(pendingEmphasis, text[last]))
        {
            last--;
        }

        last = TrimTrailing(text, start, contentStart, last);
        if (last < contentStart || !LooksLikeUrl(text, contentStart, last))
        {
            return false;
        }

        var url = text.Substring(start, last - start + 1);
        processor.Inline = new AutolinkInline(url)
        {
            Span = new SourceSpan(
                processor.GetSourcePosition(start, out var line, out var column),
                processor.GetSourcePosition(last)),
            Line = line,
            Column = column,
        };

        slice.Start = last + 1;
        return true;
    }

    /// <summary>Returns the scheme length when <paramref name="text"/> starts a bare http(s) URL, else 0.</summary>
    private static int MatchScheme(string text, int start, int end)
    {
        if (end - start + 1 < HttpSchemeLength + 1)
        {
            return 0;
        }

        if (!MatchAsciiIgnoreCase(text, start, "http"))
        {
            return 0;
        }

        var index = start + 4;
        if (text[index] is 's' or 'S')
        {
            index++;
        }

        if (index + 2 > end)
        {
            return 0;
        }

        return text[index] == ':' && text[index + 1] == '/' && text[index + 2] == '/'
            ? index + 3 - start
            : 0;
    }

    private static bool MatchAsciiIgnoreCase(string text, int start, string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            var c = text[start + i];
            var expected = value[i];
            if (c != expected && c != expected - ('a' - 'A'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether the character before the URL lets it start: start of line, whitespace, or any
    /// non-ASCII-word character (CJK punctuation, CJK text, ASCII punctuation such as <c>(</c>,
    /// <c>=</c> or <c>"</c>). ASCII letters/digits keep the URL inside a longer word
    /// (<c>myhttps://…</c>) so those are not treated as a boundary.
    /// </summary>
    private static bool IsLeftBoundary(string text, int start)
    {
        if (start <= 0)
        {
            return true;
        }

        var previous = text[start - 1];
        if (previous == '\0' || char.IsWhiteSpace(previous))
        {
            return true;
        }

        return previous is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'));
    }

    /// <summary>
    /// Rejects text that starts with a scheme but has no usable host, for example <c>https://</c> or
    /// <c>http://后续说明</c>: a CJK word after the scheme is prose, not a host. A <c>localhost</c>
    /// host (local preview URLs) is accepted even without a dot, and a <c>:port</c> counts as a host
    /// separator so <c>http://localhost:8080</c> still links.
    /// </summary>
    private static bool LooksLikeUrl(string text, int contentStart, int last)
    {
        var hostEnd = contentStart;
        while (hostEnd <= last && text[hostEnd] is not ('/' or '?' or '#'))
        {
            hostEnd++;
        }

        if (hostEnd == contentStart)
        {
            return false;
        }

        var hasSeparator = false;
        var hasAlphaNumeric = false;
        for (var i = contentStart; i < hostEnd; i++)
        {
            var c = text[i];
            if (c is '.' or ':')
            {
                hasSeparator = true;
            }
            else if (char.IsLetterOrDigit(c))
            {
                hasAlphaNumeric = true;
            }
        }

        return hasAlphaNumeric
            && (hasSeparator || (hostEnd - contentStart == 9
                && string.Compare(text, contentStart, "localhost", 0, 9, StringComparison.OrdinalIgnoreCase) == 0));
    }

    /// <summary>
    /// Whether the parser is currently inside link syntax. Link destinations are consumed by
    /// Markdig's link parser before inline parsing reaches them, so a URL seen here can only be
    /// label text (<c>[url text](…)</c>) or text between a not-yet-closed <c>[</c> and <c>]</c>;
    /// linking it there would produce a nested link node, which CommonMark forbids.
    /// </summary>
    private static bool IsLinkContext(InlineProcessor processor)
    {
        for (Inline? inline = processor.Inline; inline is not null; inline = inline.Parent)
        {
            if (inline is LinkInline)
            {
                return true;
            }

            if (inline is LinkDelimiterInline { IsActive: true, Type: DelimiterType.Open })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Emphasis delimiters that are still open in the current inline context.</summary>
    private static PendingEmphasisSet GetPendingEmphasis(InlineProcessor processor)
    {
        var pending = new PendingEmphasisSet();
        for (Inline? inline = processor.Inline; inline is not null; inline = inline.Parent)
        {
            if (inline is EmphasisDelimiterInline { IsActive: true } delimiter)
            {
                pending = pending.Add(delimiter.DelimiterChar);
            }
        }

        return pending;
    }

    private static bool IsPendingEmphasisChar(PendingEmphasisSet pending, char c)
        => (c == '*' && pending.Star)
            || (c == '_' && pending.Underscore)
            || (c == '~' && pending.Tilde);

    private readonly record struct PendingEmphasisSet(bool Star, bool Underscore = false, bool Tilde = false)
    {
        public PendingEmphasisSet Add(char delimiter) => delimiter switch
        {
            '*' => this with { Star = true },
            '_' => this with { Underscore = true },
            '~' => this with { Tilde = true },
            _ => this,
        };
    }

    /// <summary>
    /// Characters that end a URL while scanning. ASCII <c>. , ; : ! ?</c> are intentionally not
    /// terminators because they are legal inside URLs (<c>.com</c>, <c>:9443</c>, <c>?a=b</c>);
    /// they are trimmed afterwards when they are the last character. <c>* [ ]</c> terminate so the
    /// scanner never swallows emphasis delimiters or inline-link brackets that follow the URL.
    /// </summary>
    private static bool IsUrlTerminator(char c)
        => c == '\0'
            || char.IsWhiteSpace(c)
            || c is '<' or '>' or '"' or '\'' or '`' or '*' or '[' or ']'
            || IsCjkDelimiter(c);

    /// <summary>Chinese/full-width punctuation that always ends a URL instead of becoming part of it.</summary>
    private static bool IsCjkDelimiter(char c) => c switch
    {
        '\u3001' or '\u3002' // 、。
        or '\u3008' or '\u3009' or '\u300A' or '\u300B' // 〈〉《》
        or '\u300C' or '\u300D' or '\u300E' or '\u300F' // 「」『』
        or '\u3010' or '\u3011' or '\u3014' or '\u3015' // 【】〔〕
        or '\u3016' or '\u3017' or '\u3018' or '\u3019'
        or '\u301A' or '\u301B' or '\u301C' or '\u301D'
        or '\u301E' or '\u301F'
        or '\u2026' or '\u2014' or '\u2013' or '\u00B7' or '\u2022' // … — – · •
        or '\u2018' or '\u2019' or '\u201C' or '\u201D' // ‘’“”
        or '\uFF01' or '\uFF02' or '\uFF08' or '\uFF09' // ！＂（）
        or '\uFF0C' or '\uFF0D' or '\uFF0E' or '\uFF1A' // ，－．：
        or '\uFF1B' or '\uFF1F' or '\uFF5C' or '\uFF5E' // ；？｜～
            => true,
        _ => false,
    };

    /// <summary>Trailing characters trimmed from a scanned candidate (after the CJK delimiters).</summary>
    private static bool IsTrailingPunctuation(char c)
        => IsCjkDelimiter(c)
            || c is '.' or ',' or ';' or ':' or '!' or '?' or '…';

    /// <summary>Trims trailing punctuation and closing brackets that have no opening partner inside the URL.</summary>
    private static int TrimTrailing(string text, int start, int contentStart, int last)
    {
        while (last >= contentStart)
        {
            var c = text[last];
            if (IsTrailingPunctuation(c)
                || (IsClosingBracket(c) && IsUnbalancedClosingBracket(text, start, last, c)))
            {
                last--;
                continue;
            }

            break;
        }

        return last;
    }

    private static bool IsClosingBracket(char c)
        => c is ')' or ']' or '}' or '\uFF09' or '\u3011' or '\u300B' or '\u300D' or '\u300F'
            or '\u3015' or '\u3009';

    private static bool IsUnbalancedClosingBracket(string text, int start, int end, char closing)
    {
        var opening = OpeningBracketFor(closing);
        if (opening == '\0')
        {
            return false;
        }

        var opens = 0;
        var closes = 0;
        for (var i = start; i <= end; i++)
        {
            if (text[i] == opening)
            {
                opens++;
            }
            else if (text[i] == closing)
            {
                closes++;
            }
        }

        return closes > opens;
    }

    private static char OpeningBracketFor(char closing) => closing switch
    {
        ')' => '(',
        ']' => '[',
        '}' => '{',
        '\uFF09' => '\uFF08', // ）（
        '\u3011' => '\u3010', // 】【
        '\u300B' => '\u300A', // 》〈→《
        '\u300D' => '\u300C', // 」「
        '\u300F' => '\u300E', // 』『
        '\u3015' => '\u3014', // 〕〔
        '\u3009' => '\u3008', // 〉〈
        _ => '\0',
    };
}
