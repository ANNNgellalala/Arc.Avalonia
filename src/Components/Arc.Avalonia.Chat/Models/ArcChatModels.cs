namespace Arc.Avalonia.Chat;

/// <summary>A file staged in the composer or attached to a message.</summary>
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

/// <summary>Auto-scroll policy, mapped onto <c>PuraChatAutoScrollMode</c>.</summary>
public enum ArcChatAutoScrollMode
{
    /// <summary>Never scroll automatically.</summary>
    Never,

    /// <summary>Follow the newest message until the reader scrolls up.</summary>
    NearBottom,

    /// <summary>Always pin to the newest message.</summary>
    Always,
}
