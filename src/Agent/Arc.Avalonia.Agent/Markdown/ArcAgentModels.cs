using System.Collections.Generic;

namespace Arc.Avalonia.Agent;

/// <summary>Outcome of parsing one <c>pura-agent</c> fence payload.</summary>
public enum ArcAgentPayloadStatus
{
    /// <summary>The payload is valid and <see cref="ArcAgentPayloadResult.Component"/> is set.</summary>
    Success,

    /// <summary>The payload is malformed or fails schema validation; render the error card.</summary>
    Invalid,

    /// <summary>The payload is valid JSON with an unknown component <c>type</c>; render the unsupported card.</summary>
    Unsupported
}

/// <summary>One validation error with a JSON-pointer style path.</summary>
/// <param name="Message">Human readable reason.</param>
/// <param name="Path">Field path such as <c>options[1].label</c>; <c>$</c> for the root.</param>
public sealed record ArcAgentPayloadError(string Message, string? Path = null);

/// <summary>Base model of a parsed <c>pura-agent</c> component payload.</summary>
public abstract record ArcAgentComponentModel
{
    /// <summary>Component type, lower case (<c>choice</c> or <c>confirm</c>).</summary>
    public required string Type { get; init; }

    /// <summary>Payload schema version declared by the model (for example <c>1.0</c>).</summary>
    public required string Version { get; init; }

    /// <summary>Component instance id; generated from the payload when the model omitted it.</summary>
    public string? Id { get; init; }

    /// <summary>The question shown as the card title.</summary>
    public required string Question { get; init; }

    /// <summary>Optional supporting copy.</summary>
    public string? Description { get; init; }

    /// <summary>The raw JSON body, kept for diagnostics, error cards and interaction persistence.</summary>
    public string Raw { get; init; } = string.Empty;
}

/// <summary>One selectable option of a <c>choice</c> component.</summary>
/// <param name="Id">Stable id reported back to the host.</param>
/// <param name="Label">Visible label; rendered as plain text.</param>
/// <param name="Description">Optional secondary line.</param>
/// <param name="Disabled">Disabled options are shown but cannot be selected.</param>
public sealed record ArcAgentChoiceOption(string Id, string Label, string? Description = null, bool Disabled = false);

/// <summary>A parsed <c>choice</c> payload: single or multi select.</summary>
public sealed record ArcAgentChoiceModel : ArcAgentComponentModel
{
    /// <summary>Whether several options may be selected.</summary>
    public bool Multi { get; init; }

    /// <summary>Options in declaration order.</summary>
    public IReadOnlyList<ArcAgentChoiceOption> Options { get; init; } = [];

    /// <summary>Option ids selected before any interaction.</summary>
    public IReadOnlyList<string> DefaultSelected { get; init; } = [];

    /// <summary>Minimum number of selections for the multi select submit.</summary>
    public int Min { get; init; }

    /// <summary>Maximum number of selections for the multi select submit.</summary>
    public int? Max { get; init; }

    /// <summary>Submit button copy.</summary>
    public string SubmitLabel { get; init; } = "确认";

    /// <summary>Whether the whole component is read-only.</summary>
    public bool Disabled { get; init; }
}

/// <summary>A parsed <c>confirm</c> payload: confirm or cancel.</summary>
public sealed record ArcAgentConfirmModel : ArcAgentComponentModel
{
    /// <summary>Confirm button copy.</summary>
    public string ConfirmLabel { get; init; } = "确认";

    /// <summary>Cancel button copy.</summary>
    public string CancelLabel { get; init; } = "取消";

    /// <summary>Whether the confirm action is destructive (danger button styling).</summary>
    public bool Danger { get; init; }
}

/// <summary>
/// Result of <see cref="ArcAgentJson.Parse"/>: a component, an error card payload, or an unsupported-type
/// card payload. Parsing never throws; every failure degrades to a card.
/// </summary>
public sealed class ArcAgentPayloadResult
{
    /// <summary>Parse outcome.</summary>
    public required ArcAgentPayloadStatus Status { get; init; }

    /// <summary>Raw JSON body (normalized, never null) for error/unsupported cards.</summary>
    public required string Raw { get; init; }

    /// <summary>Parsed component; only set when <see cref="Status"/> is <see cref="ArcAgentPayloadStatus.Success"/>.</summary>
    public ArcAgentComponentModel? Component { get; init; }

    /// <summary>Declared component type when it could be read (also set for unsupported types).</summary>
    public string? Type { get; init; }

    /// <summary>First validation error; set when the status is <see cref="ArcAgentPayloadStatus.Invalid"/>.</summary>
    public ArcAgentPayloadError? Error { get; init; }

    /// <summary>True when the payload produced a usable component.</summary>
    public bool Success => Status == ArcAgentPayloadStatus.Success;

    /// <summary>Creates a success result.</summary>
    public static ArcAgentPayloadResult Parsed(ArcAgentComponentModel component, string raw) => new()
    {
        Status = ArcAgentPayloadStatus.Success,
        Component = component,
        Type = component.Type,
        Raw = raw
    };

    /// <summary>Creates an invalid result.</summary>
    public static ArcAgentPayloadResult Invalid(string raw, ArcAgentPayloadError error) => new()
    {
        Status = ArcAgentPayloadStatus.Invalid,
        Error = error,
        Raw = raw
    };

    /// <summary>Creates an unsupported-type result.</summary>
    public static ArcAgentPayloadResult Unsupported(string raw, string type) => new()
    {
        Status = ArcAgentPayloadStatus.Unsupported,
        Type = type,
        Raw = raw
    };
}
