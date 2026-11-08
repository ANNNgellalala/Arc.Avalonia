namespace Arc.Avalonia.Chat;

/// <summary>Who produced a chat message.</summary>
public enum ArcChatRole
{
    /// <summary>The local user.</summary>
    User,

    /// <summary>The assistant / AI answer.</summary>
    Assistant,

    /// <summary>System notice.</summary>
    System,
}

/// <summary>Generation state of one message.</summary>
public enum ArcChatMessageStatus
{
    /// <summary>Created but not sent/started.</summary>
    Draft,

    /// <summary>Tokens are still arriving.</summary>
    Streaming,

    /// <summary>Finished successfully.</summary>
    Complete,

    /// <summary>Generation failed; see <see cref="ArcChatMessage.Error"/>.</summary>
    Failed,

    /// <summary>Interrupted by the user; partial content is kept.</summary>
    Aborted,
}

/// <summary>Delivery receipt for human-to-human chat; orthogonal to generation state.</summary>
public enum ArcChatDeliveryState
{
    /// <summary>No receipt (AI answers, system notices).</summary>
    None,

    /// <summary>Handed to the transport.</summary>
    Sent,

    /// <summary>Delivered to the peer.</summary>
    Delivered,

    /// <summary>Read by the peer.</summary>
    Read,
}

/// <summary>Auto-scroll policy for <see cref="ArcChatView"/>.</summary>
public enum ArcChatAutoScrollMode
{
    /// <summary>Never scroll automatically.</summary>
    Never,

    /// <summary>Follow the newest message until the reader scrolls up.</summary>
    NearBottom,

    /// <summary>Always pin to the newest message.</summary>
    Always,
}

/// <summary>A person shown next to a bubble.</summary>
public sealed class ArcChatParticipant
{
    /// <summary>Stable participant id.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional avatar path or avares URI; initials are the fallback.</summary>
    public string? Avatar { get; init; }
}

/// <summary>A file attached to a message or staged in the composer.</summary>
public sealed class ArcChatAttachment
{
    /// <summary>File name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Size in bytes (0 = unknown).</summary>
    public long Size { get; init; }

    /// <summary>Optional source URL.</summary>
    public string? Url { get; init; }

    /// <summary>Whether the attachment is an image preview.</summary>
    public bool IsImage { get; init; }
}

/// <summary>The text and files handed to a send handler.</summary>
public sealed record ArcChatDraft(string Text, IReadOnlyList<ArcChatAttachment> Attachments);

/// <summary>
/// The single message model of the design system: human chat, AI answers and agent conversations all use
/// it. <see cref="Markdown"/> is rendered by <c>ArcMarkdownView</c>; delivery state and generation state are
/// separate so a read receipt never masquerades as "streaming".
/// </summary>
public sealed class ArcChatMessage
{
    private string _markdown = string.Empty;

    /// <summary>Stable message id.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("n");

    /// <summary>Author role.</summary>
    public ArcChatRole Role { get; init; } = ArcChatRole.Assistant;

    /// <summary>Author identity; null for AI/system messages.</summary>
    public ArcChatParticipant? Author { get; set; }

    /// <summary>Markdown body. Setting it bumps <see cref="ContentVersion"/> and raises <see cref="Changed"/>.</summary>
    public string Markdown
    {
        get => _markdown;
        set
        {
            if (string.Equals(_markdown, value, StringComparison.Ordinal))
            {
                return;
            }

            _markdown = value;
            ContentVersion++;
            UpdatedAt = DateTimeOffset.Now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Generation state.</summary>
    public ArcChatMessageStatus Status
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

    /// <summary>Delivery receipt (human chat only). Changing it raises <see cref="Changed"/>.</summary>
    public ArcChatDeliveryState Delivery
    {
        get => _delivery;
        set
        {
            if (_delivery == value)
            {
                return;
            }

            _delivery = value;
            UpdatedAt = DateTimeOffset.Now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Failure/abort reason. Changing it raises <see cref="Changed"/>.</summary>
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
            UpdatedAt = DateTimeOffset.Now;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Creation time.</summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

    /// <summary>Last content/status update.</summary>
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.Now;

    /// <summary>Monotonic body version.</summary>
    public int ContentVersion { get; private set; }

    /// <summary>Attachments shown under the body.</summary>
    public IList<ArcChatAttachment> Attachments { get; init; } = new List<ArcChatAttachment>();

    /// <summary>Host metadata (citations, token stats, tool ids).</summary>
    public IDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Whether tokens are still arriving.</summary>
    public bool IsStreaming => Status == ArcChatMessageStatus.Streaming;

    /// <summary>Raised when the body, status or delivery state changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Appends a streaming delta.</summary>
    public void Append(string delta)
    {
        if (!string.IsNullOrEmpty(delta))
        {
            Markdown += delta;
        }
    }

    /// <summary>Marks the message complete.</summary>
    public void Complete()
    {
        Status = ArcChatMessageStatus.Complete;
        Delivery = Role == ArcChatRole.User
            ? (Delivery == ArcChatDeliveryState.None ? ArcChatDeliveryState.Sent : Delivery)
            : Delivery;
    }

    private ArcChatMessageStatus _status = ArcChatMessageStatus.Complete;
    private ArcChatDeliveryState _delivery;
    private string? _error;
}

