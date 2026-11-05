using System;
using System.Collections.Generic;
using System.Linq;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Serialises approval requests across every run in the workspace: only <see cref="Current"/> is shown, the
/// rest queue behind it. The host decides policy and performs the call; this queue only keeps ordering,
/// de-duplication and the submitting/expired display state.
/// </summary>
public sealed class ArcApprovalQueue
{
    private readonly List<ArcApprovalRequest> _pending = new();
    private readonly HashSet<string> _ids = new(StringComparer.Ordinal);
    private readonly HashSet<string> _submitting = new(StringComparer.Ordinal);

    /// <summary>Raised whenever the queue shape changes.</summary>
    public event EventHandler? Changed;

    /// <summary>The request currently shown, or null when the queue is empty.</summary>
    public ArcApprovalRequest? Current => _pending.Count == 0 ? null : _pending[0];

    /// <summary>Every request waiting, the current one first.</summary>
    public IReadOnlyList<ArcApprovalRequest> Pending => _pending;

    /// <summary>Number of queued requests.</summary>
    public int Count => _pending.Count;

    /// <summary>Enqueues a request; duplicate request ids are ignored so a retried host event cannot
    /// show the same sheet twice.</summary>
    public bool Enqueue(ArcApprovalRequest request)
    {
        if (!_ids.Add(request.RequestId))
        {
            return false;
        }

        _pending.Add(request);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Marks the current request as submitted (waiting for the host to confirm).</summary>
    public void MarkSubmitting(string requestId)
    {
        if (_ids.Contains(requestId) && _submitting.Add(requestId))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>True while a decision has been sent and the host has not yet confirmed it.</summary>
    public bool IsSubmitting(string requestId) => _submitting.Contains(requestId);

    /// <summary>Removes a request after the host resolved it.</summary>
    public bool Resolve(string requestId)
    {
        if (!_ids.Remove(requestId))
        {
            return false;
        }

        _submitting.Remove(requestId);
        _pending.RemoveAll(item => string.Equals(item.RequestId, requestId, StringComparison.Ordinal));
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Removes a request because it expired.</summary>
    public bool Expire(string requestId) => Resolve(requestId);

    /// <summary>Drops every pending request for one run (for example when the run was stopped).</summary>
    public int ExpireRun(string runId)
    {
        var removed = _pending.RemoveAll(item =>
        {
            if (!string.Equals(item.RunId, runId, StringComparison.Ordinal))
            {
                return false;
            }

            _ids.Remove(item.RequestId);
            _submitting.Remove(item.RequestId);
            return true;
        });

        if (removed > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return removed;
    }

    /// <summary>Clears the queue.</summary>
    public void Clear()
    {
        _pending.Clear();
        _ids.Clear();
        _submitting.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The cross-run "waiting on you" queue: approvals, questions, failures, routine results and suggestions.
/// Entries are de-duplicated by id and sorted blocking-first, then newest-first.
/// </summary>
public sealed class ArcAttentionQueue
{
    private readonly Dictionary<string, ArcAttentionEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>Raised whenever an entry is added, updated, resolved or expired.</summary>
    public event EventHandler? Changed;

    /// <summary>Entries sorted by attention level (blocking first) and then by creation time (newest first).</summary>
    public IReadOnlyList<ArcAttentionEntry> Entries => _entries.Values
        .OrderByDescending(entry => entry.Level)
        .ThenByDescending(entry => entry.CreatedAt)
        .ToArray();

    /// <summary>Number of blocking entries.</summary>
    public int BlockingCount => _entries.Values.Count(entry => entry.Level == ArcAttentionLevel.Blocking);

    /// <summary>Number of entries still open.</summary>
    public int OpenCount => _entries.Values.Count(entry =>
        entry.State is ArcAttentionState.New or ArcAttentionState.Seen or ArcAttentionState.InProgress);

    /// <summary>Adds or replaces an entry by id (idempotent for repeated host events).</summary>
    public void Upsert(ArcAttentionEntry entry)
    {
        _entries[entry.EntryId] = entry;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Marks an entry resolved.</summary>
    public bool Resolve(string entryId)
    {
        if (!_entries.TryGetValue(entryId, out var entry))
        {
            return false;
        }

        entry.State = ArcAttentionState.Resolved;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Marks an entry seen.</summary>
    public bool MarkSeen(string entryId)
    {
        if (!_entries.TryGetValue(entryId, out var entry))
        {
            return false;
        }

        entry.State = ArcAttentionState.Seen;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Expires every entry owned by one run (for example when the run was stopped).</summary>
    public int ExpireRun(string runId)
    {
        var affected = 0;
        foreach (var entry in _entries.Values.Where(entry => string.Equals(entry.RunId, runId, StringComparison.Ordinal)))
        {
            if (entry.State == ArcAttentionState.Expired)
            {
                continue;
            }

            entry.State = ArcAttentionState.Expired;
            affected++;
        }

        if (affected > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return affected;
    }

    /// <summary>Removes every entry.</summary>
    public void Clear()
    {
        _entries.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// The artifacts one or more runs produced. The host supplies display text/preview payloads; this index only
/// keeps identity, ordering and the current selection.
/// </summary>
public sealed class ArcArtifactIndex
{
    private readonly List<ArcArtifact> _items = new();
    private readonly Dictionary<string, ArcArtifact> _index = new(StringComparer.Ordinal);

    /// <summary>Raised when an artifact is added or the selection changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Artifacts in publication order.</summary>
    public IReadOnlyList<ArcArtifact> Items => _items;

    /// <summary>The selected artifact, if any.</summary>
    public ArcArtifact? Selected { get; private set; }

    /// <summary>Adds an artifact; duplicate ids are ignored.</summary>
    public bool Add(ArcArtifact artifact)
    {
        if (!_index.TryAdd(artifact.ArtifactId, artifact))
        {
            return false;
        }

        _items.Add(artifact);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Finds an artifact by id.</summary>
    public ArcArtifact? Get(string artifactId) =>
        _index.TryGetValue(artifactId, out var artifact) ? artifact : null;

    /// <summary>Selects an artifact (null clears the selection).</summary>
    public void Select(ArcArtifact? artifact)
    {
        Selected = artifact;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Removes every artifact.</summary>
    public void Clear()
    {
        _items.Clear();
        _index.Clear();
        Selected = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
