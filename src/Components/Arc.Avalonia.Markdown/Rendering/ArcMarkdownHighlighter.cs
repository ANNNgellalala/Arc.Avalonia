namespace Arc.Avalonia.Markdown;

/// <summary>
/// Built-in lightweight syntax colouring for fenced code. No third-party highlighter is used: a
/// hand written tokenizer covers the languages that show up in AI answers (C family, JSON, shell,
/// PowerShell, Python, SQL, markup, CSS, YAML, diff and Markdown) and falls back to plain text for
/// anything else. Spans are ordered by start offset and never overlap.
/// </summary>
public sealed class ArcMarkdownHighlighter : IArcMarkdownCodeHighlighter
{
    private static readonly Dictionary<string, string> Aliases = BuildAliases();

    private static readonly HashSet<string> CSharpKeywords = Words(
        "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while add alias and ascending async await by descending dynamic equals file from get global group init into join let managed nameof nint not notnull nuint on or orderby partial record remove required scoped select set unmanaged value var when where with yield");

    private static readonly HashSet<string> CSharpTypes = Words(
        "bool byte char decimal double float int long object sbyte short string uint ulong ushort void nint nuint");

    private static readonly HashSet<string> JavaScriptKeywords = Words(
        "await break case catch class const continue debugger default delete do else enum export extends false finally for function if implements import in instanceof interface let new null package private protected public return static super switch this throw true try typeof var void while with yield async of get set from as target meta");

    private static readonly HashSet<string> JavaScriptTypes = Words(
        "Array Boolean Date Error Function JSON Map Math Number Object Promise Proxy Reflect RegExp Set String Symbol WeakMap WeakSet BigInt Intl console window document globalThis undefined NaN Infinity");

    private static readonly HashSet<string> TypeScriptKeywords = Words(
        "abstract any as asserts async await break case catch class const continue declare default delete do else enum export extends false finally for from function if implements import in infer instanceof interface is keyof let module namespace never new null of override package private protected public readonly require return satisfies set static super switch target this throw true try type typeof unique unknown var void while with yield");

    private static readonly HashSet<string> TypeScriptTypes = Words(
        "any bigint boolean never number object string symbol undefined unknown void Array Date Error Function Map Promise ReadonlyArray Record Partial Required Readonly Set WeakMap WeakSet JSON Math console window document globalThis NaN Infinity Record Pick Omit");

    private static readonly HashSet<string> PythonKeywords = Words(
        "False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield match case self type");

    private static readonly HashSet<string> PythonTypes = Words(
        "int float complex bool str bytes bytearray list tuple dict set frozenset range object type Exception BaseException ValueError TypeError KeyError IndexError RuntimeError StopIteration AttributeError NameError OSError IOError ZeroDivisionError NotImplemented Ellipsis");

    private static readonly HashSet<string> JsonKeywords = Words("true false null");

    private static readonly HashSet<string> SqlKeywords = Words(
        "ADD ALL ALTER AND ANY AS ASC BETWEEN BY CASE CAST CHECK COLUMN CONSTRAINT CREATE CROSS CURSOR DATABASE DEFAULT DELETE DESC DISTINCT DROP ELSE END EXISTS FOREIGN FROM FULL FUNCTION GRANT GROUP HAVING IF IN INDEX INNER INSERT INTERSECT INTO IS JOIN KEY LEFT LIKE LIMIT MERGE NATURAL NOT NULL OFFSET ON OR ORDER OUTER OVER PARTITION PRIMARY PROCEDURE RECURSIVE REFERENCES RETURNING REVOKE RIGHT ROLLBACK ROW ROWS SELECT SET TABLE THEN TO TRANSACTION TRIGGER UNION UNIQUE UPDATE USING VALUES VIEW WHEN WHERE WITH");

    private static readonly HashSet<string> SqlFunctions = Words(
        "AVG COUNT COALESCE CONCAT CONVERT CURRENT_TIMESTAMP DATEADD DATEDIFF EXTRACT GREATEST IFNULL LAG LEAD LEAST LENGTH LOWER MAX MIN NOW NULLIF ROW_NUMBER ROUND SUBSTRING SUM TRIM UPPER");

    private static readonly HashSet<string> SqlTypes = Words(
        "BIGINT BIT BLOB BOOLEAN BOOL CHAR CHARACTER CLOB DATE DATETIME DECIMAL DOUBLE FLOAT INT INTEGER INTERVAL JSON MONEY NCHAR NUMERIC NVARCHAR REAL SERIAL SMALLINT TEXT TIME TIMESTAMP TINYINT UUID VARBINARY VARCHAR");

    private static readonly HashSet<string> ShellKeywords = Words(
        "if then else elif fi for while until do done case esac in function select time coproc return exit break continue shift local export readonly declare typeset unset set source alias unalias eval exec trap wait kill true false test");

    private static readonly HashSet<string> PowerShellKeywords = Words(
        "begin break catch class continue data define do dynamicparam else elseif end enum exit filter finally for foreach from function hidden if in param process return static switch throw trap try until using var while workflow parallel sequence");

    private static readonly HashSet<string> PowerShellOperators = Words(
        "and as band bnot bor bxor contains div eq f ge gt in is isnot join le like lt match mod ne not notcontains notin notlike notmatch or replace shl shr split xor");

    private static readonly HashSet<string> YamlKeywords = Words(
        "true false yes no on off null");

    private static readonly Dictionary<string, ArcMarkdownTokenKind> ShellPending = new(StringComparer.Ordinal)
    {
        ["function"] = ArcMarkdownTokenKind.Function,
    };

    private static readonly Dictionary<string, ArcMarkdownTokenKind> PythonPending = new(StringComparer.Ordinal)
    {
        ["def"] = ArcMarkdownTokenKind.Function,
        ["class"] = ArcMarkdownTokenKind.Type,
    };

    private static readonly Dictionary<string, ArcMarkdownTokenKind> PowerShellPending = new(StringComparer.Ordinal)
    {
        ["function"] = ArcMarkdownTokenKind.Function,
        ["filter"] = ArcMarkdownTokenKind.Function,
        ["class"] = ArcMarkdownTokenKind.Type,
        ["enum"] = ArcMarkdownTokenKind.Type,
    };

    /// <summary>Shared default highlighter.</summary>
    public static ArcMarkdownHighlighter Default { get; } = new();

    /// <summary>Canonical language names understood by the built-in tokenizer.</summary>
    public static IReadOnlyCollection<string> Languages { get; } = new[]
    {
        "csharp", "json", "javascript", "typescript", "tsx", "jsx", "xml", "html", "xaml",
        "css", "bash", "powershell", "python", "sql", "yaml", "diff", "markdown",
    };

    /// <inheritdoc />
    public bool CanHighlight(string? language) => Resolve(language) is not null;

    /// <inheritdoc />
    public IReadOnlyList<ArcMarkdownHighlightSpan> Highlight(string code, string? language)
    {
        var canonical = Resolve(language);
        if (string.IsNullOrEmpty(code) || canonical is null)
        {
            return Array.Empty<ArcMarkdownHighlightSpan>();
        }

        var spans = new List<ArcMarkdownHighlightSpan>(128);
        try
        {
            switch (canonical)
            {
                case "csharp":
                    LexCLike(code, spans, CSharpSpec);
                    break;
                case "javascript":
                    LexCLike(code, spans, JavaScriptSpec);
                    break;
                case "typescript":
                    LexCLike(code, spans, TypeScriptSpec);
                    break;
                case "tsx":
                case "jsx":
                    LexCLike(code, spans, JsxSpec);
                    break;
                case "json":
                    LexJson(code, spans);
                    break;
                case "xml":
                case "html":
                case "xaml":
                    LexMarkup(code, spans);
                    break;
                case "css":
                    LexCss(code, spans);
                    break;
                case "bash":
                    LexShell(code, spans);
                    break;
                case "powershell":
                    LexPowerShell(code, spans);
                    break;
                case "python":
                    LexCLike(code, spans, PythonSpec);
                    break;
                case "sql":
                    LexSql(code, spans);
                    break;
                case "yaml":
                    LexYaml(code, spans);
                    break;
                case "diff":
                    LexDiff(code, spans);
                    break;
                case "markdown":
                    LexMarkdown(code, spans);
                    break;
            }
        }
        catch
        {
            // Never let a tokenizer bug surface as a renderer failure; callers fall back to plain text.
            return Array.Empty<ArcMarkdownHighlightSpan>();
        }

        return Normalize(spans, code.Length);
    }

    // ---------------------------------------------------------------------------------------------
    // Language resolution
    // ---------------------------------------------------------------------------------------------

    private static Dictionary<string, string> BuildAliases()
        => new(StringComparer.OrdinalIgnoreCase)
        {
            ["csharp"] = "csharp", ["cs"] = "csharp", ["c#"] = "csharp", ["dotnet"] = "csharp",
            ["json"] = "json", ["jsonc"] = "json", ["json5"] = "json",
            ["javascript"] = "javascript", ["js"] = "javascript", ["mjs"] = "javascript", ["cjs"] = "javascript",
            ["javascriptreact"] = "jsx", ["jsx"] = "jsx",
            ["typescript"] = "typescript", ["ts"] = "typescript", ["mts"] = "typescript", ["cts"] = "typescript",
            ["typescriptreact"] = "tsx", ["tsx"] = "tsx",
            ["xml"] = "xml", ["svg"] = "xml", ["xsl"] = "xml",
            ["html"] = "html", ["htm"] = "html", ["xhtml"] = "html",
            ["xaml"] = "xaml", ["axaml"] = "xaml",
            ["css"] = "css", ["scss"] = "css", ["less"] = "css",
            ["bash"] = "bash", ["sh"] = "bash", ["shell"] = "bash", ["zsh"] = "bash", ["fish"] = "bash",
            ["console"] = "bash", ["terminal"] = "bash", ["shellscript"] = "bash",
            ["powershell"] = "powershell", ["ps1"] = "powershell", ["pwsh"] = "powershell", ["ps"] = "powershell",
            ["python"] = "python", ["py"] = "python", ["python3"] = "python",
            ["sql"] = "sql", ["mysql"] = "sql", ["postgresql"] = "sql", ["postgres"] = "sql",
            ["sqlite"] = "sql", ["tsql"] = "sql", ["plsql"] = "sql",
            ["yaml"] = "yaml", ["yml"] = "yaml",
            ["diff"] = "diff", ["patch"] = "diff",
            ["markdown"] = "markdown", ["md"] = "markdown", ["mdx"] = "markdown",
        };

    private static string? Resolve(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var value = language.Trim().TrimStart('.').TrimStart('{').TrimEnd('}').ToLowerInvariant();
        var separator = value.IndexOfAny([' ', '\t', ',', ';', ':']);
        if (separator > 0)
        {
            value = value[..separator];
        }

        return value.Length > 0 && Aliases.TryGetValue(value, out var canonical) ? canonical : null;
    }

    private static HashSet<string> Words(string text)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            set.Add(word);
        }

        return set;
    }

    // ---------------------------------------------------------------------------------------------
    // C family (C#, JavaScript, TypeScript, TSX/JSX, Python)
    // ---------------------------------------------------------------------------------------------

    private sealed class CLikeSpec
    {
        public required HashSet<string> Keywords { get; init; }

        public HashSet<string> Types { get; init; } = new(StringComparer.Ordinal);

        public Dictionary<string, ArcMarkdownTokenKind> PendingKinds { get; init; } = new(StringComparer.Ordinal);

        public bool BlockComments { get; init; } = true;

        public bool CSharpStrings { get; init; }

        public bool CharLiteral { get; init; }

        public bool Backtick { get; init; }

        public bool Decorators { get; init; }

        public bool Attributes { get; init; }

        public bool Preprocessor { get; init; }

        public bool HashComments { get; init; }

        public bool PythonStrings { get; init; }

        public bool Jsx { get; init; }

        public bool DollarIdentifiers { get; init; }

        public bool UpperCaseTypes { get; init; } = true;

        public bool SuffixNumbers { get; init; }
    }

    private static readonly CLikeSpec CSharpSpec = new()
    {
        Keywords = CSharpKeywords,
        Types = CSharpTypes,
        CSharpStrings = true,
        CharLiteral = true,
        Attributes = true,
        Preprocessor = true,
        SuffixNumbers = true,
    };

    private static readonly CLikeSpec JavaScriptSpec = new()
    {
        Keywords = JavaScriptKeywords,
        Types = JavaScriptTypes,
        Backtick = true,
        Decorators = true,
        DollarIdentifiers = true,
        UpperCaseTypes = false,
    };

    private static readonly CLikeSpec TypeScriptSpec = new()
    {
        Keywords = TypeScriptKeywords,
        Types = TypeScriptTypes,
        Backtick = true,
        Decorators = true,
        DollarIdentifiers = true,
        UpperCaseTypes = false,
    };

    private static readonly CLikeSpec JsxSpec = new()
    {
        Keywords = TypeScriptKeywords,
        Types = TypeScriptTypes,
        Backtick = true,
        Decorators = true,
        DollarIdentifiers = true,
        Jsx = true,
        UpperCaseTypes = false,
    };

    private static readonly CLikeSpec PythonSpec = new()
    {
        Keywords = PythonKeywords,
        Types = PythonTypes,
        PendingKinds = PythonPending,
        HashComments = true,
        PythonStrings = true,
        Decorators = true,
        BlockComments = false,
    };

    private static void LexCLike(string code, List<ArcMarkdownHighlightSpan> spans, CLikeSpec spec)
    {
        var length = code.Length;
        var i = 0;
        var pending = ArcMarkdownTokenKind.Plain;

        while (i < length)
        {
            var c = code[i];

            if (c == '/' && i + 1 < length && code[i + 1] == '/')
            {
                var end = IndexOfOrEnd(code, i + 2, '\n');
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (spec.BlockComments && c == '/' && i + 1 < length && code[i + 1] == '*')
            {
                var close = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var end = close < 0 ? length : close + 2;
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (spec.HashComments && c == '#')
            {
                var end = IndexOfOrEnd(code, i + 1, '\n');
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (spec.Preprocessor && c == '#' && IsLineStart(code, i))
            {
                var end = IndexOfOrEnd(code, i + 1, '\n');
                Add(spans, i, end - i, ArcMarkdownTokenKind.Keyword);
                i = end;
                continue;
            }

            if (spec.CSharpStrings)
            {
                if (c == '@' && i + 1 < length && code[i + 1] == '"')
                {
                    i = ScanVerbatimString(code, i + 1, spans);
                    continue;
                }

                if (c == '$' && i + 1 < length && code[i + 1] == '"')
                {
                    i = ScanQuoted(code, i + 1, '"', spans);
                    continue;
                }

                if (c == '$' && i + 2 < length && code[i + 1] == '@' && code[i + 2] == '"')
                {
                    i = ScanVerbatimString(code, i + 2, spans);
                    continue;
                }

                if (c == '@' && i + 2 < length && code[i + 1] == '$' && code[i + 2] == '"')
                {
                    i = ScanVerbatimString(code, i + 2, spans);
                    continue;
                }
            }

            if (spec.CharLiteral && c == '\'')
            {
                i = ScanCharLiteral(code, i, spans);
                continue;
            }

            if (spec.PythonStrings && (c is '"' or '\'') && i + 2 < length
                && code[i + 1] == c && code[i + 2] == c)
            {
                i = ScanTripleQuoted(code, i, c, spans);
                continue;
            }

            if (spec.PythonStrings && IsPythonStringPrefix(code, i))
            {
                var quoteIndex = i;
                while (quoteIndex < length && code[quoteIndex] is 'r' or 'R' or 'b' or 'B' or 'f' or 'F' or 'u' or 'U')
                {
                    quoteIndex++;
                }

                if (quoteIndex < length && code[quoteIndex] is '"' or '\'')
                {
                    var quote = code[quoteIndex];
                    var start = i;
                    var triple = quoteIndex + 2 < length
                                 && code[quoteIndex + 1] == quote
                                 && code[quoteIndex + 2] == quote;
                    i = triple
                        ? FindTripleQuotedEnd(code, quoteIndex, quote)
                        : FindQuotedEnd(code, quoteIndex, quote);
                    Add(spans, start, i - start, ArcMarkdownTokenKind.String);
                    continue;
                }
            }

            if (spec.Jsx && c == '<' && TryScanJsxTag(code, ref i, spans))
            {
                continue;
            }

            if (spec.Backtick && c == '`')
            {
                i = ScanTemplateString(code, i, spans);
                continue;
            }

            if (c == '"')
            {
                i = ScanQuoted(code, i, '"', spans);
                continue;
            }

            if (c == '\'' && !spec.CharLiteral && !spec.PythonStrings)
            {
                i = ScanQuoted(code, i, '\'', spans);
                continue;
            }

            if (spec.PythonStrings && c == '\'')
            {
                i = ScanQuoted(code, i, '\'', spans);
                continue;
            }

            if (spec.Decorators && c == '@' && i + 1 < length && IsIdentifierStart(code[i + 1], spec))
            {
                var start = i;
                i++;
                while (i < length && (IsIdentifierPart(code[i], spec) || code[i] == '.'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Attribute);
                continue;
            }

            if (spec.Attributes && c == '[' && i + 1 < length && IsIdentifierStart(code[i + 1], spec))
            {
                var close = FindClosingBracket(code, i);
                if (close > i)
                {
                    Add(spans, i + 1, close - i - 1, ArcMarkdownTokenKind.Attribute);
                    i = close + 1;
                    continue;
                }
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < length && char.IsDigit(code[i + 1]) && !IsIdentifierPart(i > 0 ? code[i - 1] : '\0', spec)))
            {
                i = ScanNumber(code, i, spans, spec.SuffixNumbers);
                continue;
            }

            if (IsIdentifierStart(c, spec))
            {
                var start = i;
                i++;
                while (i < length && IsIdentifierPart(code[i], spec))
                {
                    i++;
                }

                var word = code[start..i];
                var kind = ArcMarkdownTokenKind.Plain;
                if (pending != ArcMarkdownTokenKind.Plain)
                {
                    kind = pending;
                    pending = ArcMarkdownTokenKind.Plain;
                }
                else if (spec.Keywords.Contains(word))
                {
                    kind = ArcMarkdownTokenKind.Keyword;
                    if (spec.PendingKinds.TryGetValue(word, out var next))
                    {
                        pending = next;
                    }
                }
                else if (spec.Types.Contains(word))
                {
                    kind = ArcMarkdownTokenKind.Type;
                }
                else if (spec.UpperCaseTypes && word.Length > 1 && char.IsUpper(word[0]) && !IsAllUpper(word))
                {
                    kind = ArcMarkdownTokenKind.Type;
                }
                else if (NextNonWhitespace(code, i) is var nextIndex && nextIndex < length && code[nextIndex] == '(')
                {
                    kind = ArcMarkdownTokenKind.Function;
                }

                if (kind != ArcMarkdownTokenKind.Plain)
                {
                    Add(spans, start, i - start, kind);
                }

                continue;
            }

            if (IsOperatorChar(c))
            {
                var start = i;
                while (i < length && IsOperatorChar(code[i]))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Operator);
                continue;
            }

            i++;
        }
    }

    private static bool IsIdentifierStart(char c, CLikeSpec spec)
        => char.IsLetter(c) || c == '_' || c > 127 || (spec.DollarIdentifiers && c == '$');

    private static bool IsIdentifierPart(char c, CLikeSpec spec)
        => char.IsLetterOrDigit(c) || c == '_' || c > 127 || (spec.DollarIdentifiers && c == '$');

    private static bool IsAllUpper(string word)
    {
        var hasLetter = false;
        foreach (var c in word)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            hasLetter = true;
            if (!char.IsUpper(c))
            {
                return false;
            }
        }

        return hasLetter;
    }

    private static bool IsOperatorChar(char c)
        => c is '+' or '-' or '*' or '/' or '%' or '=' or '<' or '>' or '!' or '&' or '|' or '^' or '~' or '?' or ':';

    private static bool IsPythonStringPrefix(string code, int index)
    {
        var c = code[index];
        if (c is not ('r' or 'R' or 'b' or 'B' or 'f' or 'F' or 'u' or 'U'))
        {
            return false;
        }

        if (index > 0 && (char.IsLetterOrDigit(code[index - 1]) || code[index - 1] == '_'))
        {
            return false;
        }

        var next = index + 1;
        if (next < code.Length && code[next] is 'r' or 'R' or 'b' or 'B' or 'f' or 'F' or 'u' or 'U')
        {
            next++;
        }

        return next < code.Length && code[next] is '"' or '\'';
    }

    private static int ScanQuoted(string code, int start, char quote, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = FindQuotedEnd(code, start, quote);
        Add(spans, start, i - start, ArcMarkdownTokenKind.String);
        return i;
    }

    private static int FindQuotedEnd(string code, int start, char quote)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == quote)
            {
                return i + 1;
            }

            if (c == '\n')
            {
                return i;
            }

            i++;
        }

        return code.Length;
    }

    private static int ScanVerbatimString(string code, int quoteIndex, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = quoteIndex + 1;
        while (i < code.Length)
        {
            if (code[i] == '"')
            {
                if (i + 1 < code.Length && code[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                i++;
                break;
            }

            i++;
        }

        Add(spans, quoteIndex, i - quoteIndex, ArcMarkdownTokenKind.String);
        return i;
    }

    private static int ScanCharLiteral(string code, int start, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '\'')
            {
                i++;
                break;
            }

            if (c == '\n')
            {
                break;
            }

            i++;
        }

        i = Math.Min(i, code.Length);
        Add(spans, start, i - start, ArcMarkdownTokenKind.String);
        return i;
    }

    private static int ScanTripleQuoted(string code, int start, char quote, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = FindTripleQuotedEnd(code, start, quote);
        Add(spans, start, i - start, ArcMarkdownTokenKind.String);
        return i;
    }

    private static int FindTripleQuotedEnd(string code, int start, char quote)
    {
        var i = start + 3;
        while (i + 2 < code.Length)
        {
            if (code[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (code[i] == quote && code[i + 1] == quote && code[i + 2] == quote)
            {
                return i + 3;
            }

            i++;
        }

        return code.Length;
    }

    private static int ScanTemplateString(string code, int start, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '\\')
            {
                i += 2;
                continue;
            }

            if (c == '`')
            {
                i++;
                break;
            }

            i++;
        }

        i = Math.Min(i, code.Length);
        Add(spans, start, i - start, ArcMarkdownTokenKind.String);
        return i;
    }

    private static int ScanNumber(string code, int start, List<ArcMarkdownHighlightSpan> spans, bool suffix)
    {
        var i = start;
        var length = code.Length;

        if (code[i] == '0' && i + 1 < length && (code[i + 1] is 'x' or 'X'))
        {
            i += 2;
            while (i < length && (char.IsAsciiHexDigit(code[i]) || code[i] == '_'))
            {
                i++;
            }
        }
        else if (code[i] == '0' && i + 1 < length && (code[i + 1] is 'b' or 'B'))
        {
            i += 2;
            while (i < length && (code[i] is '0' or '1' or '_'))
            {
                i++;
            }
        }
        else
        {
            while (i < length && (char.IsDigit(code[i]) || code[i] == '_'))
            {
                i++;
            }

            if (i < length && code[i] == '.' && i + 1 < length && char.IsDigit(code[i + 1]))
            {
                i++;
                while (i < length && (char.IsDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                }
            }

            if (i < length && (code[i] is 'e' or 'E'))
            {
                var exponent = i + 1;
                if (exponent < length && (code[exponent] is '+' or '-'))
                {
                    exponent++;
                }

                if (exponent < length && char.IsDigit(code[exponent]))
                {
                    i = exponent;
                    while (i < length && char.IsDigit(code[i]))
                    {
                        i++;
                    }
                }
            }
        }

        if (suffix)
        {
            while (i < length && (code[i] is 'f' or 'F' or 'd' or 'D' or 'm' or 'M' or 'u' or 'U' or 'l' or 'L'))
            {
                i++;
            }
        }

        Add(spans, start, i - start, ArcMarkdownTokenKind.Number);
        return i;
    }

    private static int FindClosingBracket(string code, int start)
    {
        var depth = 0;
        for (var i = start; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '\n')
            {
                return -1;
            }

            if (c is '"' or '\'')
            {
                var quote = c;
                i++;
                while (i < code.Length && code[i] != quote && code[i] != '\n')
                {
                    if (code[i] == '\\')
                    {
                        i++;
                    }

                    i++;
                }

                continue;
            }

            if (c == '[')
            {
                depth++;
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool TryScanJsxTag(string code, ref int index, List<ArcMarkdownHighlightSpan> spans)
    {
        var start = index;
        var i = index + 1;
        if (i < code.Length && code[i] == '/')
        {
            i++;
        }

        if (i >= code.Length || !char.IsLetter(code[i]))
        {
            return false;
        }

        var nameStart = i;
        while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '.' or '-' or ':'))
        {
            i++;
        }

        Add(spans, start, i - start, ArcMarkdownTokenKind.Tag);

        while (i < code.Length && code[i] != '>')
        {
            var c = code[i];
            if (c == '\n')
            {
                return false;
            }

            if (c is '"' or '\'')
            {
                i = ScanQuoted(code, i, c, spans);
                continue;
            }

            if (c == '{')
            {
                var depth = 0;
                while (i < code.Length)
                {
                    if (code[i] == '{')
                    {
                        depth++;
                    }
                    else if (code[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            i++;
                            break;
                        }
                    }

                    i++;
                }

                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var attributeStart = i;
                i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '-' or ':'))
                {
                    i++;
                }

                Add(spans, attributeStart, i - attributeStart, ArcMarkdownTokenKind.Attribute);
                continue;
            }

            i++;
        }

        if (i < code.Length && code[i] == '>')
        {
            i++;
        }

        index = i;
        return true;
    }

    // ---------------------------------------------------------------------------------------------
    // JSON
    // ---------------------------------------------------------------------------------------------

    private static void LexJson(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = 0;
        while (i < code.Length)
        {
            var c = code[i];
            if (c == '"')
            {
                var start = i;
                i = ScanQuoted(code, i, '"', spans);
                var after = i;
                while (after < code.Length && code[after] is ' ' or '\t')
                {
                    after++;
                }

                if (after < code.Length && code[after] == ':')
                {
                    spans[^1] = spans[^1] with { Kind = ArcMarkdownTokenKind.Attribute };
                }

                continue;
            }

            if (char.IsDigit(c) || (c == '-' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
            {
                i = ScanNumber(code, i, spans, suffix: false);
                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;
                while (i < code.Length && char.IsLetter(code[i]))
                {
                    i++;
                }

                if (JsonKeywords.Contains(code[start..i]))
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Keyword);
                }

                continue;
            }

            if (IsOperatorChar(c))
            {
                var start = i;
                while (i < code.Length && IsOperatorChar(code[i]))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Operator);
                continue;
            }

            i++;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // XML / HTML / XAML
    // ---------------------------------------------------------------------------------------------

    private static void LexMarkup(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = 0;
        while (i < code.Length)
        {
            if (StartsWith(code, i, "<!--"))
            {
                var close = code.IndexOf("-->", i + 4, StringComparison.Ordinal);
                var end = close < 0 ? code.Length : close + 3;
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (StartsWith(code, i, "<![CDATA["))
            {
                var close = code.IndexOf("]]>", i + 9, StringComparison.Ordinal);
                var end = close < 0 ? code.Length : close + 3;
                Add(spans, i, end - i, ArcMarkdownTokenKind.String);
                i = end;
                continue;
            }

            var c = code[i];
            if (c == '<' && i + 1 < code.Length
                && (char.IsLetter(code[i + 1]) || code[i + 1] is '/' or '!' or '?'))
            {
                var start = i;
                i++;
                var closing = i < code.Length && code[i] == '/';
                if (closing)
                {
                    i++;
                }

                if (i < code.Length && code[i] is '!' or '?')
                {
                    var gt = code.IndexOf('>', i);
                    var end = gt < 0 ? code.Length : gt + 1;
                    Add(spans, start, end - start, ArcMarkdownTokenKind.Tag);
                    i = end;
                    continue;
                }

                var nameStart = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or ':' or '-' or '.'))
                {
                    i++;
                }

                if (i == nameStart)
                {
                    i = start + 1;
                    continue;
                }

                Add(spans, nameStart, i - nameStart, ArcMarkdownTokenKind.Tag);

                while (i < code.Length && code[i] != '>')
                {
                    var inner = code[i];
                    if (inner is '"' or '\'')
                    {
                        i = ScanQuoted(code, i, inner, spans);
                        continue;
                    }

                    if (char.IsLetter(inner) || inner is '_' or ':')
                    {
                        var attributeStart = i;
                        while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or ':' or '-' or '.'))
                        {
                            i++;
                        }

                        Add(spans, attributeStart, i - attributeStart, ArcMarkdownTokenKind.Attribute);
                        continue;
                    }

                    i++;
                }

                if (i < code.Length)
                {
                    i++;
                }

                continue;
            }

            i++;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // CSS
    // ---------------------------------------------------------------------------------------------

    private static void LexCss(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = 0;
        var depth = 0;
        while (i < code.Length)
        {
            var c = code[i];

            if (c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                var close = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var end = close < 0 ? code.Length : close + 2;
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (c is '"' or '\'')
            {
                i = ScanQuoted(code, i, c, spans);
                continue;
            }

            if (c == '@' && i + 1 < code.Length && (char.IsLetter(code[i + 1]) || code[i + 1] == '-'))
            {
                var start = i;
                i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Keyword);
                continue;
            }

            if (c == '$' && i + 1 < code.Length && (char.IsLetter(code[i + 1]) || code[i + 1] == '-'))
            {
                var start = i;
                i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Variable);
                continue;
            }

            if (c == '-' && i + 2 < code.Length && code[i + 1] == '-' && (char.IsLetter(code[i + 2]) || code[i + 2] == '-'))
            {
                var start = i;
                i += 2;
                if (i < code.Length && code[i] == '-')
                {
                    i++;
                }

                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Variable);
                continue;
            }

            if (c == '{')
            {
                depth++;
                i++;
                continue;
            }

            if (c == '}')
            {
                depth = Math.Max(0, depth - 1);
                i++;
                continue;
            }

            if (char.IsDigit(c) || (c == '.' && i + 1 < code.Length && char.IsDigit(code[i + 1])))
            {
                i = ScanCssNumber(code, i, spans);
                continue;
            }

            if (c == '.' && i + 1 < code.Length && (char.IsLetter(code[i + 1]) || code[i + 1] is '-' or '_'))
            {
                var start = i;
                i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Type);
                continue;
            }

            if (c == '#' && i + 1 < code.Length && (char.IsLetter(code[i + 1]) || code[i + 1] is '-' or '_'))
            {
                var start = i;
                i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Type);
                continue;
            }

            if ((char.IsLetter(c) || c is '-' or '_') && (i == 0 || !char.IsLetterOrDigit(code[i - 1])))
            {
                var start = i;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_' or '\\'))
                {
                    i++;
                }

                var word = code[start..i];
                var next = NextNonWhitespace(code, i);
                if (depth > 0 && next < code.Length && code[next] == ':')
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Attribute);
                }
                else if (next < code.Length && code[next] == '(')
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Function);
                }
                else if (depth == 0)
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Type);
                }

                continue;
            }

            if (c == ':' && i + 1 < code.Length && (char.IsLetter(code[i + 1]) || code[i + 1] == ':'))
            {
                var start = i;
                i++;
                if (i < code.Length && code[i] == ':')
                {
                    i++;
                }

                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Type);
                continue;
            }

            if (c == '!' && StartsWith(code, i, "!important"))
            {
                Add(spans, i, 10, ArcMarkdownTokenKind.Keyword);
                i += 10;
                continue;
            }

            if (c is '>' or '+' or '~' or '=' or '*' or '/' or '%')
            {
                Add(spans, i, 1, ArcMarkdownTokenKind.Operator);
                i++;
                continue;
            }

            i++;
        }
    }

    private static int ScanCssNumber(string code, int start, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = start;
        if (code[i] == '.')
        {
            i++;
        }

        while (i < code.Length && (char.IsDigit(code[i]) || code[i] == '.'))
        {
            i++;
        }

        while (i < code.Length && (char.IsLetter(code[i]) || code[i] == '%'))
        {
            i++;
        }

        Add(spans, start, i - start, ArcMarkdownTokenKind.Number);
        return i;
    }

    // ---------------------------------------------------------------------------------------------
    // Bash / sh / zsh
    // ---------------------------------------------------------------------------------------------

    private static void LexShell(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = 0;
        var length = code.Length;
        var commandPosition = true;

        while (i < length)
        {
            var c = code[i];

            if (c == '#' && (i == 0 || char.IsWhiteSpace(code[i - 1]) || code[i - 1] is ';' or '|' or '&'))
            {
                var end = IndexOfOrEnd(code, i, '\n');
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (c == '\'')
            {
                var start = i;
                i++;
                while (i < length && code[i] != '\'')
                {
                    i++;
                }

                i = Math.Min(i + 1, length);
                Add(spans, start, i - start, ArcMarkdownTokenKind.String);
                commandPosition = false;
                continue;
            }

            if (c == '"')
            {
                var start = i;
                i++;
                while (i < length)
                {
                    if (code[i] == '\\')
                    {
                        i += 2;
                        continue;
                    }

                    if (code[i] == '"')
                    {
                        i++;
                        break;
                    }

                    i++;
                }

                i = Math.Min(i, length);
                Add(spans, start, i - start, ArcMarkdownTokenKind.String);
                commandPosition = false;
                continue;
            }

            if (c == '$')
            {
                var start = i;
                i++;
                if (i < length && code[i] == '{')
                {
                    while (i < length && code[i] != '}')
                    {
                        i++;
                    }

                    if (i < length)
                    {
                        i++;
                    }
                }
                else if (i < length && code[i] == '(')
                {
                    var depth = 0;
                    while (i < length)
                    {
                        if (code[i] == '(')
                        {
                            depth++;
                        }
                        else if (code[i] == ')')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                i++;
                                break;
                            }
                        }

                        i++;
                    }
                }
                else if (i < length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '?' or '!' or '@' or '*' or '#' or '-' or '$'))
                {
                    while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '?' or '!' or '@' or '*' or '#' or '$'))
                    {
                        i++;
                    }
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Variable);
                continue;
            }

            if (char.IsDigit(c))
            {
                i = ScanNumber(code, i, spans, suffix: false);
                commandPosition = false;
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                }

                var word = code[start..i];
                var next = NextNonWhitespace(code, i);
                if (next < length && code[next] == '(')
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Function);
                    commandPosition = false;
                }
                else if (ShellKeywords.Contains(word))
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Keyword);
                    if (word is "if" or "then" or "do" or "else" or "elif")
                    {
                        commandPosition = true;
                    }
                }
                else if (commandPosition)
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Function);
                    commandPosition = false;
                }

                continue;
            }

            if (c == '-' && i + 1 < length && (char.IsLetter(code[i + 1]) || code[i + 1] == '-'))
            {
                var start = i;
                i++;
                while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] is '-' or '_' or '=' or '.'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Attribute);
                continue;
            }

            if (c is '|' or '&' or ';' or '>' or '<' or '=' or '!')
            {
                var start = i;
                while (i < length && (code[i] is '|' or '&' or ';' or '>' or '<' or '=' or '!'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Operator);
                if (code[start] is ';' or '|' or '&')
                {
                    commandPosition = true;
                }

                continue;
            }

            if (c == '\n')
            {
                commandPosition = true;
                i++;
                continue;
            }

            i++;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // PowerShell
    // ---------------------------------------------------------------------------------------------

    private static void LexPowerShell(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = 0;
        var length = code.Length;
        var pending = ArcMarkdownTokenKind.Plain;

        while (i < length)
        {
            var c = code[i];

            if (c == '<' && StartsWith(code, i, "<#"))
            {
                var close = code.IndexOf("#>", i + 2, StringComparison.Ordinal);
                var end = close < 0 ? length : close + 2;
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (c == '#' && (i == 0 || char.IsWhiteSpace(code[i - 1]) || code[i - 1] is ';' or '|' or '(' or '{'))
            {
                var end = IndexOfOrEnd(code, i, '\n');
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (c == '@' && i + 1 < length && (code[i + 1] is '"' or '\''))
            {
                var quote = code[i + 1];
                var start = i;
                var close = code.IndexOf("\n" + quote + "@", i + 2, StringComparison.Ordinal);
                i = close < 0 ? length : close + 3;
                Add(spans, start, i - start, ArcMarkdownTokenKind.String);
                continue;
            }

            if (c is '"' or '\'')
            {
                i = ScanPowerShellString(code, i, c, spans);
                continue;
            }

            if (c == '$')
            {
                var start = i;
                i++;
                if (i < length && code[i] == '{')
                {
                    while (i < length && code[i] != '}')
                    {
                        i++;
                    }

                    if (i < length)
                    {
                        i++;
                    }
                }
                else
                {
                    while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or ':' or '?' or '$' or '^'))
                    {
                        i++;
                    }
                }

                if (i > start + 1)
                {
                    Add(spans, start, i - start, ArcMarkdownTokenKind.Variable);
                }

                continue;
            }

            if (c == '[' && IsPowerShellType(code, i))
            {
                var close = code.IndexOf(']', i + 1);
                if (close > i)
                {
                    Add(spans, i + 1, close - i - 1, ArcMarkdownTokenKind.Type);
                    i = close + 1;
                    continue;
                }
            }

            if (char.IsDigit(c))
            {
                i = ScanNumber(code, i, spans, suffix: false);
                continue;
            }

            if (c == '-' && i + 1 < length && char.IsLetter(code[i + 1]))
            {
                var start = i;
                i++;
                while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] == '-'))
                {
                    i++;
                }

                var word = code[(start + 1)..i].ToLowerInvariant();
                Add(
                    spans,
                    start,
                    i - start,
                    PowerShellOperators.Contains(word) ? ArcMarkdownTokenKind.Operator : ArcMarkdownTokenKind.Attribute);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '-'))
                {
                    i++;
                }

                var word = code[start..i];
                var kind = ArcMarkdownTokenKind.Plain;
                if (pending != ArcMarkdownTokenKind.Plain)
                {
                    kind = pending;
                    pending = ArcMarkdownTokenKind.Plain;
                }
                else if (PowerShellKeywords.Contains(word))
                {
                    kind = ArcMarkdownTokenKind.Keyword;
                    if (PowerShellPending.TryGetValue(word, out var next))
                    {
                        pending = next;
                    }
                }
                else if (word.Contains('-') && !word.StartsWith('-'))
                {
                    kind = ArcMarkdownTokenKind.Function;
                }
                else if (next_is_call(code, i))
                {
                    kind = ArcMarkdownTokenKind.Function;
                }

                if (kind != ArcMarkdownTokenKind.Plain)
                {
                    Add(spans, start, i - start, kind);
                }

                continue;
            }

            if (c is '|' or '&' or '=' or '+' or '*' or '/' or '%' or '!' or '>' or '<' or '?' or ':')
            {
                var start = i;
                while (i < length && (code[i] is '|' or '&' or '=' or '+' or '*' or '/' or '%' or '!' or '>' or '<' or '?' or ':'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Operator);
                continue;
            }

            i++;
        }

        static bool next_is_call(string text, int index)
        {
            var next = NextNonWhitespace(text, index);
            return next < text.Length && text[next] == '(';
        }
    }

    private static bool IsPowerShellType(string code, int start)
    {
        var i = start + 1;
        if (i >= code.Length || !char.IsLetter(code[i]))
        {
            return false;
        }

        while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] is '.' or ':' or '[' or ']' or ','))
        {
            if (code[i] == ']')
            {
                return true;
            }

            i++;
        }

        return false;
    }

    private static int ScanPowerShellString(string code, int start, char quote, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = start + 1;
        while (i < code.Length)
        {
            var c = code[i];
            if (quote == '\'' && c == '\'' && i + 1 < code.Length && code[i + 1] == '\'')
            {
                i += 2;
                continue;
            }

            if (quote == '"' && c == '`')
            {
                i += 2;
                continue;
            }

            if (c == quote)
            {
                i++;
                break;
            }

            if (c == '\n')
            {
                break;
            }

            i++;
        }

        i = Math.Min(i, code.Length);
        Add(spans, start, i - start, ArcMarkdownTokenKind.String);
        return i;
    }

    // ---------------------------------------------------------------------------------------------
    // SQL
    // ---------------------------------------------------------------------------------------------

    private static void LexSql(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = 0;
        var length = code.Length;

        while (i < length)
        {
            var c = code[i];

            if (c == '-' && i + 1 < length && code[i + 1] == '-')
            {
                var end = IndexOfOrEnd(code, i + 2, '\n');
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (c == '/' && i + 1 < length && code[i + 1] == '*')
            {
                var close = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                var end = close < 0 ? length : close + 2;
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                i = end;
                continue;
            }

            if (c == '\'')
            {
                var start = i;
                i++;
                while (i < length)
                {
                    if (code[i] == '\'')
                    {
                        if (i + 1 < length && code[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }

                        i++;
                        break;
                    }

                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.String);
                continue;
            }

            if (c == '"' && i + 1 < length)
            {
                var start = i;
                i++;
                while (i < length && code[i] != '"')
                {
                    i++;
                }

                i = Math.Min(i + 1, length);
                Add(spans, start, i - start, ArcMarkdownTokenKind.Attribute);
                continue;
            }

            if (c == '[')
            {
                var close = code.IndexOf(']', i + 1);
                if (close > i)
                {
                    Add(spans, i + 1, close - i - 1, ArcMarkdownTokenKind.Attribute);
                    i = close + 1;
                    continue;
                }
            }

            if (c == '@' || c == ':')
            {
                var start = i;
                i++;
                if (i < length && code[i] == '@')
                {
                    i++;
                }

                while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Variable);
                continue;
            }

            if (char.IsDigit(c))
            {
                i = ScanNumber(code, i, spans, suffix: false);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var start = i;
                while (i < length && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '$' or '#'))
                {
                    i++;
                }

                var word = code[start..i];
                var upper = word.ToUpperInvariant();
                var next = NextNonWhitespace(code, i);
                var kind = ArcMarkdownTokenKind.Plain;
                if (next < length && code[next] == '(' && !SqlKeywords.Contains(upper))
                {
                    kind = ArcMarkdownTokenKind.Function;
                }
                else if (SqlKeywords.Contains(upper))
                {
                    kind = ArcMarkdownTokenKind.Keyword;
                }
                else if (SqlTypes.Contains(upper))
                {
                    kind = ArcMarkdownTokenKind.Type;
                }
                else if (SqlFunctions.Contains(upper))
                {
                    kind = ArcMarkdownTokenKind.Function;
                }
                else if (IsAllUpper(word) && char.IsUpper(word[0]))
                {
                    kind = ArcMarkdownTokenKind.Keyword;
                }
                else if (word.Length > 1 && char.IsUpper(word[0]) && !IsAllUpper(word))
                {
                    kind = ArcMarkdownTokenKind.Function;
                }

                if (kind != ArcMarkdownTokenKind.Plain)
                {
                    Add(spans, start, i - start, kind);
                }

                continue;
            }

            if (c is '=' or '<' or '>' or '+' or '-' or '*' or '/' or '%' or '|' or '!')
            {
                var start = i;
                while (i < length && (code[i] is '=' or '<' or '>' or '+' or '-' or '*' or '/' or '%' or '|' or '!'))
                {
                    i++;
                }

                Add(spans, start, i - start, ArcMarkdownTokenKind.Operator);
                continue;
            }

            i++;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // YAML
    // ---------------------------------------------------------------------------------------------

    private static void LexYaml(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var position = 0;
        var blockScalarIndent = -1;

        while (position < code.Length)
        {
            var lineEnd = code.IndexOf('\n', position);
            lineEnd = lineEnd < 0 ? code.Length : lineEnd;

            if (blockScalarIndent >= 0)
            {
                var indent = CountIndent(code, position, lineEnd);
                if (lineEnd == position || indent > blockScalarIndent)
                {
                    if (lineEnd > position)
                    {
                        Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.String);
                    }

                    position = lineEnd + 1;
                    continue;
                }

                blockScalarIndent = -1;
            }

            blockScalarIndent = LexYamlLine(code, position, lineEnd, spans);
            position = lineEnd + 1;
        }
    }

    private static int LexYamlLine(string code, int start, int end, List<ArcMarkdownHighlightSpan> spans)
    {
        var indent = CountIndent(code, start, end);
        var i = start + indent;
        if (i >= end)
        {
            return -1;
        }

        if ((end - i >= 3 && (code[i] == '-' || code[i] == '.')
             && code[i + 1] == code[i] && code[i + 2] == code[i]
             && (i + 3 >= end || char.IsWhiteSpace(code[i + 3]))))
        {
            Add(spans, i, 3, ArcMarkdownTokenKind.Keyword);
            i += 3;
        }
        else if (code[i] == '-' && (i + 1 >= end || char.IsWhiteSpace(code[i + 1])))
        {
            Add(spans, i, 1, ArcMarkdownTokenKind.Operator);
            i++;
            while (i < end && code[i] == ' ')
            {
                i++;
            }
        }

        if (i >= end || code[i] == '#')
        {
            if (i < end)
            {
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
            }

            return -1;
        }

        // Key detection: quoted keys or plain scalar up to a colon followed by whitespace/EOL.
        var keyStart = i;
        if (code[i] is '"' or '\'')
        {
            var quote = code[i];
            var keyEnd = i + 1;
            while (keyEnd < end && code[keyEnd] != quote)
            {
                if (code[keyEnd] == '\\')
                {
                    keyEnd++;
                }

                keyEnd++;
            }

            var after = keyEnd + 1;
            while (after < end && code[after] == ' ')
            {
                after++;
            }

            if (after < end && code[after] == ':')
            {
                Add(spans, i, Math.Min(keyEnd + 1, end) - i, ArcMarkdownTokenKind.String);
                return ScanYamlValue(code, after + 1, end, spans, indent);
            }

            return ScanYamlValue(code, i, end, spans, indent);
        }

        var colon = -1;
        for (var scan = i; scan < end; scan++)
        {
            var c = code[scan];
            if (c == '#' && scan > i && char.IsWhiteSpace(code[scan - 1]))
            {
                break;
            }

            if (c == ':' && (scan + 1 >= end || char.IsWhiteSpace(code[scan + 1])))
            {
                colon = scan;
                break;
            }
        }

        if (colon > keyStart)
        {
            var key = code[keyStart..colon].TrimEnd();
            if (key.Length > 0)
            {
                Add(spans, keyStart, key.Length, ArcMarkdownTokenKind.Attribute);
            }

            return ScanYamlValue(code, colon + 1, end, spans, indent);
        }

        return ScanYamlValue(code, i, end, spans, indent);
    }

    private static int ScanYamlValue(string code, int start, int end, List<ArcMarkdownHighlightSpan> spans, int indent)
    {
        var i = start;
        var blockScalar = false;

        while (i < end)
        {
            var c = code[i];

            if (c == '#' && (i == start || char.IsWhiteSpace(code[i - 1])))
            {
                Add(spans, i, end - i, ArcMarkdownTokenKind.Comment);
                return -1;
            }

            if (c is '"' or '\'')
            {
                var quote = c;
                var stringStart = i;
                i++;
                while (i < end && code[i] != quote)
                {
                    if (code[i] == '\\')
                    {
                        i++;
                    }

                    i++;
                }

                i = Math.Min(i + 1, end);
                Add(spans, stringStart, i - stringStart, ArcMarkdownTokenKind.String);
                continue;
            }

            if (c is '|' or '>')
            {
                Add(spans, i, 1, ArcMarkdownTokenKind.Keyword);
                blockScalar = true;
                i++;
                continue;
            }

            if (c is '&' or '*')
            {
                var anchorStart = i;
                i++;
                while (i < end && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '-'))
                {
                    i++;
                }

                Add(spans, anchorStart, i - anchorStart, ArcMarkdownTokenKind.Variable);
                continue;
            }

            if (c == '!' && i + 1 < end)
            {
                var tagStart = i;
                i++;
                while (i < end && (char.IsLetterOrDigit(code[i]) || code[i] is '!' or '-' or '_'))
                {
                    i++;
                }

                Add(spans, tagStart, i - tagStart, ArcMarkdownTokenKind.Type);
                continue;
            }

            if (char.IsDigit(c) || (c == '-' && i + 1 < end && char.IsDigit(code[i + 1])))
            {
                i = ScanNumber(code, i, spans, suffix: false);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var wordStart = i;
                while (i < end && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or '-'))
                {
                    i++;
                }

                var word = code[wordStart..i];
                if (YamlKeywords.Contains(word.ToLowerInvariant()))
                {
                    Add(spans, wordStart, i - wordStart, ArcMarkdownTokenKind.Keyword);
                }

                continue;
            }

            i++;
        }

        return blockScalar ? indent : -1;
    }

    private static int CountIndent(string code, int start, int end)
    {
        var indent = 0;
        while (start + indent < end && code[start + indent] == ' ')
        {
            indent++;
        }

        return indent;
    }

    // ---------------------------------------------------------------------------------------------
    // Diff
    // ---------------------------------------------------------------------------------------------

    private static void LexDiff(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var position = 0;
        while (position < code.Length)
        {
            var lineEnd = code.IndexOf('\n', position);
            lineEnd = lineEnd < 0 ? code.Length : lineEnd;

            if (StartsWith(code, position, "+++ ") || StartsWith(code, position, "--- "))
            {
                Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.Tag);
            }
            else if (StartsWith(code, position, "@@"))
            {
                Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.Keyword);
            }
            else if (StartsWith(code, position, "diff ")
                || StartsWith(code, position, "index ")
                || StartsWith(code, position, "new file ")
                || StartsWith(code, position, "deleted file ")
                || StartsWith(code, position, "similarity index ")
                || StartsWith(code, position, "rename ")
                || StartsWith(code, position, "Binary files "))
            {
                Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.Comment);
            }
            else if (lineEnd > position && code[position] == '+')
            {
                Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.Addition);
            }
            else if (lineEnd > position && code[position] == '-')
            {
                Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.Deletion);
            }
            else if (lineEnd > position && code[position] == '\\')
            {
                Add(spans, position, lineEnd - position, ArcMarkdownTokenKind.Comment);
            }

            position = lineEnd + 1;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Markdown
    // ---------------------------------------------------------------------------------------------

    private static void LexMarkdown(string code, List<ArcMarkdownHighlightSpan> spans)
    {
        var position = 0;
        var inFence = false;
        var fenceChar = '\0';
        var fenceCount = 0;

        while (position < code.Length)
        {
            var lineEnd = code.IndexOf('\n', position);
            lineEnd = lineEnd < 0 ? code.Length : lineEnd;
            var indent = CountIndent(code, position, lineEnd);
            var content = position + indent;

            if (content < lineEnd && TryReadMarkdownFence(code, content, lineEnd, out var character, out var count))
            {
                if (!inFence)
                {
                    inFence = true;
                    fenceChar = character;
                    fenceCount = count;
                    Add(spans, content, lineEnd - content, ArcMarkdownTokenKind.Keyword);
                    position = lineEnd + 1;
                    continue;
                }

                if (character == fenceChar && count >= fenceCount)
                {
                    inFence = false;
                    Add(spans, content, lineEnd - content, ArcMarkdownTokenKind.Keyword);
                    position = lineEnd + 1;
                    continue;
                }
            }

            if (inFence)
            {
                position = lineEnd + 1;
                continue;
            }

            if (content < lineEnd && code[content] == '>')
            {
                Add(spans, content, 1, ArcMarkdownTokenKind.Comment);
                content++;
            }

            if (content < lineEnd && code[content] == '#')
            {
                var hashes = 0;
                while (content + hashes < lineEnd && code[content + hashes] == '#')
                {
                    hashes++;
                }

                if (hashes is >= 1 and <= 6 && (content + hashes >= lineEnd || code[content + hashes] == ' '))
                {
                    Add(spans, content, hashes, ArcMarkdownTokenKind.Keyword);
                    content += hashes;
                }
            }
            else if (content < lineEnd && IsMarkdownListMarker(code, content, lineEnd))
            {
                var markerEnd = content + 1;
                while (markerEnd < lineEnd && code[markerEnd] is ' ' or '\t')
                {
                    markerEnd++;
                }

                Add(spans, content, markerEnd - content, ArcMarkdownTokenKind.Operator);
                content = markerEnd;
            }
            else if (content < lineEnd && IsThematicBreak(code, content, lineEnd))
            {
                Add(spans, content, lineEnd - content, ArcMarkdownTokenKind.Operator);
                position = lineEnd + 1;
                continue;
            }

            ScanMarkdownInline(code, content, lineEnd, spans);
            position = lineEnd + 1;
        }
    }

    private static void ScanMarkdownInline(string code, int start, int end, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = start;
        while (i < end)
        {
            var c = code[i];

            if (c == '\\' && i + 1 < end)
            {
                i += 2;
                continue;
            }

            if (c == '`')
            {
                var tickStart = i;
                var ticks = 0;
                while (i < end && code[i] == '`')
                {
                    ticks++;
                    i++;
                }

                var close = -1;
                for (var scan = i; scan < end; scan++)
                {
                    if (code[scan] != '`')
                    {
                        continue;
                    }

                    var run = 0;
                    while (scan + run < end && code[scan + run] == '`')
                    {
                        run++;
                    }

                    if (run == ticks)
                    {
                        close = scan + run;
                        break;
                    }

                    scan += run - 1;
                }

                if (close > 0)
                {
                    Add(spans, tickStart, close - tickStart, ArcMarkdownTokenKind.String);
                    i = close;
                }
                else
                {
                    i = tickStart + ticks;
                }

                continue;
            }

            if (c == '!' && i + 1 < end && code[i + 1] == '[')
            {
                i = ScanMarkdownLink(code, i + 1, end, spans, image: true);
                continue;
            }

            if (c == '[')
            {
                var next = ScanMarkdownLink(code, i, end, spans, image: false);
                if (next > i)
                {
                    i = next;
                    continue;
                }

                i++;
                continue;
            }

            if (c is '*' or '_' or '~')
            {
                var runStart = i;
                while (i < end && code[i] == c)
                {
                    i++;
                }

                if (i - runStart is 1 or 2 or 3)
                {
                    Add(spans, runStart, i - runStart, ArcMarkdownTokenKind.Operator);
                }

                continue;
            }

            if (c == '<' && i + 1 < end && (char.IsLetter(code[i + 1]) || code[i + 1] is '/' or '!'))
            {
                var close = code.IndexOf('>', i);
                if (close > i && close < end)
                {
                    AddMarkupTagSpans(code, i, close + 1, spans);
                    i = close + 1;
                    continue;
                }
            }

            if (c == '<' && i + 1 < end && (char.IsLetter(code[i + 1]) || code[i + 1] == 'h'))
            {
                i++;
                continue;
            }

            i++;
        }
    }

    private static int ScanMarkdownLink(string code, int bracketStart, int end, List<ArcMarkdownHighlightSpan> spans, bool image)
    {
        var close = -1;
        var depth = 0;
        for (var scan = bracketStart; scan < end; scan++)
        {
            if (code[scan] == '\\')
            {
                scan++;
                continue;
            }

            if (code[scan] == '[')
            {
                depth++;
            }
            else if (code[scan] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    close = scan;
                    break;
                }
            }
        }

        if (close < 0 || close + 1 >= end || code[close + 1] != '(')
        {
            return bracketStart;
        }

        var parenClose = -1;
        depth = 0;
        for (var scan = close + 1; scan < end; scan++)
        {
            if (code[scan] == '(')
            {
                depth++;
            }
            else if (code[scan] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    parenClose = scan;
                    break;
                }
            }
        }

        if (parenClose < 0)
        {
            return bracketStart;
        }

        var targetStart = close + 2;
        var targetEnd = parenClose;
        while (targetStart < targetEnd && code[targetStart] == ' ')
        {
            targetStart++;
        }

        var titleStart = -1;
        var quoteIndex = code.IndexOfAny(['"', '\''], targetStart, targetEnd - targetStart);
        if (quoteIndex > 0 && char.IsWhiteSpace(code[quoteIndex - 1]))
        {
            titleStart = quoteIndex;
        }

        var urlEnd = titleStart > 0 ? titleStart : targetEnd;
        while (urlEnd > targetStart && char.IsWhiteSpace(code[urlEnd - 1]))
        {
            urlEnd--;
        }

        if (urlEnd > targetStart)
        {
            Add(spans, targetStart, urlEnd - targetStart, ArcMarkdownTokenKind.Attribute);
        }

        if (titleStart > 0)
        {
            var titleEnd = code.IndexOf(code[titleStart], titleStart + 1);
            if (titleEnd > 0 && titleEnd < targetEnd)
            {
                Add(spans, titleStart, titleEnd - titleStart + 1, ArcMarkdownTokenKind.String);
            }
        }

        if (image)
        {
            Add(spans, bracketStart - 1, 1, ArcMarkdownTokenKind.Operator);
        }

        return parenClose + 1;
    }

    private static void AddMarkupTagSpans(string code, int start, int end, List<ArcMarkdownHighlightSpan> spans)
    {
        var i = start + 1;
        if (i < end && code[i] == '/')
        {
            i++;
        }

        var nameStart = i;
        while (i < end && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or ':' or '-'))
        {
            i++;
        }

        if (i > nameStart)
        {
            Add(spans, start, i - start, ArcMarkdownTokenKind.Tag);
        }

        while (i < end)
        {
            var c = code[i];
            if (c is '"' or '\'')
            {
                var quote = c;
                var quoteStart = i;
                i++;
                while (i < end && code[i] != quote)
                {
                    i++;
                }

                i = Math.Min(i + 1, end);
                Add(spans, quoteStart, i - quoteStart, ArcMarkdownTokenKind.String);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var attributeStart = i;
                while (i < end && (char.IsLetterOrDigit(code[i]) || code[i] is '_' or ':' or '-'))
                {
                    i++;
                }

                Add(spans, attributeStart, i - attributeStart, ArcMarkdownTokenKind.Attribute);
                continue;
            }

            i++;
        }
    }

    private static bool TryReadMarkdownFence(string code, int start, int end, out char fenceChar, out int count)
    {
        fenceChar = '\0';
        count = 0;
        if (start >= end || (code[start] != '`' && code[start] != '~'))
        {
            return false;
        }

        var c = code[start];
        var i = start;
        while (i < end && code[i] == c)
        {
            count++;
            i++;
        }

        if (count < 3)
        {
            return false;
        }

        fenceChar = c;
        return true;
    }

    private static bool IsMarkdownListMarker(string code, int start, int end)
    {
        var c = code[start];
        if (c is '-' or '*' or '+')
        {
            return start + 1 < end && code[start + 1] == ' ';
        }

        var i = start;
        while (i < end && char.IsDigit(code[i]))
        {
            i++;
        }

        return i > start && i + 1 < end && (code[i] == '.' || code[i] == ')') && code[i + 1] == ' ';
    }

    private static bool IsThematicBreak(string code, int start, int end)
    {
        var c = code[start];
        if (c is not ('-' or '*' or '_'))
        {
            return false;
        }

        var count = 0;
        for (var i = start; i < end; i++)
        {
            if (code[i] == c)
            {
                count++;
            }
            else if (code[i] != ' ')
            {
                return false;
            }
        }

        return count >= 3;
    }

    // ---------------------------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------------------------

    private static bool StartsWith(string code, int index, string value)
        => index >= 0 && index + value.Length <= code.Length
           && string.CompareOrdinal(code, index, value, 0, value.Length) == 0;

    private static bool IsLineStart(string code, int index)
    {
        if (index == 0)
        {
            return true;
        }

        for (var i = index - 1; i >= 0 && code[i] != '\n'; i--)
        {
            if (!char.IsWhiteSpace(code[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int IndexOfOrEnd(string code, int start, char value)
    {
        var index = code.IndexOf(value, start);
        return index < 0 ? code.Length : index;
    }

    private static int NextNonWhitespace(string code, int index)
    {
        while (index < code.Length && char.IsWhiteSpace(code[index]))
        {
            index++;
        }

        return index;
    }

    private static void Add(List<ArcMarkdownHighlightSpan> spans, int start, int length, ArcMarkdownTokenKind kind)
    {
        if (length <= 0 || start < 0 || kind == ArcMarkdownTokenKind.Plain)
        {
            return;
        }

        spans.Add(new ArcMarkdownHighlightSpan(start, length, kind));
    }

    private static IReadOnlyList<ArcMarkdownHighlightSpan> Normalize(List<ArcMarkdownHighlightSpan> spans, int length)
    {
        if (spans.Count == 0)
        {
            return Array.Empty<ArcMarkdownHighlightSpan>();
        }

        spans.Sort(static (left, right) => left.Start.CompareTo(right.Start));

        var result = new List<ArcMarkdownHighlightSpan>(spans.Count);
        var cursor = 0;
        foreach (var span in spans)
        {
            var start = Math.Clamp(span.Start, 0, length);
            var end = Math.Clamp(span.Start + Math.Max(span.Length, 0), start, length);
            if (end <= start || start < cursor)
            {
                continue;
            }

            if (result.Count > 0)
            {
                var last = result[^1];
                if (last.Kind == span.Kind && last.Start + last.Length == start)
                {
                    result[^1] = last with { Length = end - last.Start };
                    cursor = end;
                    continue;
                }
            }

            if (result.Count >= 8192)
            {
                break;
            }

            result.Add(new ArcMarkdownHighlightSpan(start, end - start, span.Kind));
            cursor = end;
        }

        return result;
    }
}
