namespace Arc.Avalonia.Markdown;

/// <summary>Who authored a chat message.</summary>
public enum ArcMarkdownChatRole
{
    /// <summary>The end user.</summary>
    User,

    /// <summary>The assistant/model.</summary>
    Assistant,

    /// <summary>A system/tool notice.</summary>
    System,
}

/// <summary>Streaming/final state of one message.</summary>
public enum ArcMarkdownChatMessageStatus
{
    /// <summary>Finished successfully.</summary>
    Complete,

    /// <summary>Still being appended by a streaming session.</summary>
    Streaming,

    /// <summary>The stream or the host reported a failure.</summary>
    Failed,

    /// <summary>The stream was interrupted on purpose; the partial body is kept.</summary>
    Aborted,
}

/// <summary>Auto-scroll policy for the chat surface.</summary>
public enum ArcMarkdownChatAutoScrollMode
{
    /// <summary>Never auto-scroll; the visitor stays where they are.</summary>
    Never,

    /// <summary>Follow new content only while the visitor is near the end (default).</summary>
    NearBottom,

    /// <summary>Always follow new content, even if the visitor scrolled up.</summary>
    Always,
}

/// <summary>
/// One chat message rendered with the full Markdown renderer. The object is the host-owned model: mutate
/// <see cref="Markdown"/>, <see cref="Status"/> or <see cref="Error"/> and <see cref="Changed"/> tells the
/// surface to update the realized row (a message outside the viewport is simply re-rendered when it scrolls
/// back in, which is what makes message-level virtualization cheap).
/// </summary>
public sealed class ArcMarkdownChatMessage
{
    private string _markdown = string.Empty;
    private ArcMarkdownChatMessageStatus _status = ArcMarkdownChatMessageStatus.Complete;
    private string? _error;

    /// <summary>Stable message id (unique within a conversation).</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("n");

    /// <summary>Author role.</summary>
    public ArcMarkdownChatRole Role { get; init; } = ArcMarkdownChatRole.Assistant;

    /// <summary>Markdown body; changing it bumps <see cref="ContentVersion"/> and raises <see cref="Changed"/>.</summary>
    public string Markdown
    {
        get => _markdown;
        set
        {
            if (string.Equals(_markdown, value, StringComparison.Ordinal))
            {
                return;
            }

            _markdown = value ?? string.Empty;
            ContentVersion++;
            UpdatedAt = DateTimeOffset.Now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Streaming/final state; changing it raises <see cref="Changed"/>.</summary>
    public ArcMarkdownChatMessageStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            UpdatedAt = DateTimeOffset.Now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Failure/abort reason shown as an error row under the body; changing it raises <see cref="Changed"/>.</summary>
    public string? Error
    {
        get => _error;
        set
        {
            if (string.Equals(_error, value, StringComparison.Ordinal))
            {
                return;
            }

            _error = value;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Last content/status change.</summary>
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.Now;

    /// <summary>Monotonic content version (increments on every <see cref="Markdown"/> change).</summary>
    public int ContentVersion { get; private set; }

    /// <summary>Host metadata (tool call ids, citations, token stats). Not rendered.</summary>
    public IDictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Raised when the body, status or error changes.</summary>
    public event EventHandler? Changed;
}
