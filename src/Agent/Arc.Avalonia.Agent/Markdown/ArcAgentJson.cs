using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace Arc.Avalonia.Agent;

/// <summary>
/// Strict parser for the <c>pura-agent</c> fence payload (docs/03-agent-syntax-spec, PuraMarkdown v0.3).
/// One JSON object per fence, two built-in types (<c>choice</c>, <c>confirm</c>), no third-party schema
/// dependency. Malformed payloads never throw: they become <see cref="ArcAgentPayloadResult"/> values the
/// renderer turns into degradation cards.
/// </summary>
public static class ArcAgentJson
{
    /// <summary>Fence body size guard (docs/03 §3.1: 256 KB).</summary>
    public const int MaxPayloadChars = 256 * 1024;

    /// <summary>JSON nesting guard (docs/03 §3.1: depth 32).</summary>
    public const int MaxDepth = 32;

    /// <summary>Option count guard for <c>choice</c>.</summary>
    public const int MaxOptionCount = 200;

    private const int MaxTypeChars = 64;
    private const int MaxIdChars = 128;
    private const int MaxQuestionChars = 500;
    private const int MaxChoiceDescriptionChars = 1000;
    private const int MaxConfirmDescriptionChars = 2000;
    private const int MaxOptionIdChars = 128;
    private const int MaxOptionLabelChars = 500;
    private const int MaxButtonLabelChars = 64;

    /// <summary>
    /// Parses one fence body. Whitespace, a leading BOM and trailing newlines are normalized first; arrays
    /// and scalars at the root are rejected. Unknown fields are ignored so newer models keep rendering.
    /// </summary>
    /// <param name="source">Fence body as emitted by the model.</param>
    /// <returns>The parse result; never null and never throwing.</returns>
    public static ArcAgentPayloadResult Parse(string? source)
    {
        var raw = Normalize(source);
        if (raw.Length == 0)
        {
            return ArcAgentPayloadResult.Invalid(raw, new ArcAgentPayloadError("组件载荷为空", "$"));
        }

        if (raw.Length > MaxPayloadChars)
        {
            return ArcAgentPayloadResult.Invalid(
                raw,
                new ArcAgentPayloadError($"组件载荷超过 {MaxPayloadChars} 字符上限", "$"));
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw, new JsonDocumentOptions
            {
                MaxDepth = MaxDepth,
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
        }
        catch (JsonException exception)
        {
            return ArcAgentPayloadResult.Invalid(raw, JsonSyntaxError(exception));
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ArcAgentPayloadResult.Invalid(raw, new ArcAgentPayloadError("载荷必须是单个 JSON 对象", "$"));
            }

            if (!TryReadString(root, "type", required: true, out var type, out var error)
                || string.IsNullOrWhiteSpace(type))
            {
                return ArcAgentPayloadResult.Invalid(raw, error ?? new ArcAgentPayloadError("缺少 type", "type"));
            }

            if (!IsValidTypeName(type))
            {
                return ArcAgentPayloadResult.Invalid(
                    raw,
                    new ArcAgentPayloadError("type 必须是小写字母、数字或连字符，1-64 字符", "type"));
            }

            if (!TryReadString(root, "version", required: true, out var version, out error)
                || string.IsNullOrWhiteSpace(version))
            {
                return ArcAgentPayloadResult.Invalid(raw, error ?? new ArcAgentPayloadError("缺少 version", "version"));
            }

            if (!TryReadString(root, "id", required: false, out var id, out error))
            {
                return ArcAgentPayloadResult.Invalid(raw, error!);
            }

            if (!string.IsNullOrEmpty(id) && id.Trim().Length > MaxIdChars)
            {
                return ArcAgentPayloadResult.Invalid(
                    raw,
                    new ArcAgentPayloadError($"id 超过 {MaxIdChars} 字符", "id"));
            }

            var normalizedType = type.Trim().ToLowerInvariant();
            switch (normalizedType)
            {
                case "choice":
                {
                    var (choice, choiceError) = ParseChoice(root, raw, normalizedType, version, id);
                    return choice is not null
                        ? ArcAgentPayloadResult.Parsed(choice, raw)
                        : ArcAgentPayloadResult.Invalid(raw, choiceError ?? new ArcAgentPayloadError("choice 校验失败", "choice"));
                }

                case "confirm":
                {
                    var (confirm, confirmError) = ParseConfirm(root, raw, normalizedType, version, id);
                    return confirm is not null
                        ? ArcAgentPayloadResult.Parsed(confirm, raw)
                        : ArcAgentPayloadResult.Invalid(raw, confirmError ?? new ArcAgentPayloadError("confirm 校验失败", "confirm"));
                }

                default:
                    return ArcAgentPayloadResult.Unsupported(raw, normalizedType);
            }
        }
    }

    /// <summary>
    /// Generates a stable component id from the payload when the model omitted <c>id</c>. The hash keeps
    /// interaction state restorable across re-renders and streaming rebuilds.
    /// </summary>
    /// <param name="raw">Normalized payload JSON.</param>
    /// <returns>A <c>pura-agent-&lt;hash&gt;</c> id.</returns>
    public static string CreateComponentId(string raw) => $"pura-agent-{Fnv1a(raw):x}";

    /// <summary>Normalizes a fence body: strips a BOM and surrounding whitespace.</summary>
    /// <param name="source">Raw fence body.</param>
    /// <returns>The normalized JSON text.</returns>
    public static string Normalize(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return string.Empty;
        }

        var text = source.Trim();
        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text[1..].Trim();
        }

        return text;
    }

    private static (ArcAgentChoiceModel? Component, ArcAgentPayloadError? Error) ParseChoice(
        JsonElement root,
        string raw,
        string type,
        string version,
        string? id)
    {
        if (!TryReadString(root, "question", required: true, out var question, out var error) || string.IsNullOrWhiteSpace(question))
        {
            return (null, error ?? new ArcAgentPayloadError("question 不能为空", "question"));
        }

        if (question!.Trim().Length > MaxQuestionChars)
        {
            return (null, new ArcAgentPayloadError($"question 超过 {MaxQuestionChars} 字符", "question"));
        }

        if (!TryReadString(root, "description", required: false, out var description, out error))
        {
            return (null, error);
        }

        if (description is { Length: > MaxChoiceDescriptionChars })
        {
            return (null, new ArcAgentPayloadError($"description 超过 {MaxChoiceDescriptionChars} 字符", "description"));
        }

        if (!TryReadBool(root, "multi", defaultValue: false, out var multi, out error)
            || !TryReadBool(root, "disabled", defaultValue: false, out var disabled, out error))
        {
            return (null, error);
        }

        if (!TryReadString(root, "submitLabel", required: false, out var submitLabel, out error))
        {
            return (null, error);
        }

        if (submitLabel is { Length: > MaxButtonLabelChars })
        {
            return (null, new ArcAgentPayloadError($"submitLabel 超过 {MaxButtonLabelChars} 字符", "submitLabel"));
        }

        if (!TryReadInt(root, "min", defaultValue: null, out var min, out error)
            || !TryReadInt(root, "max", defaultValue: null, out var max, out error))
        {
            return (null, error);
        }

        if (min is < 0)
        {
            return (null, new ArcAgentPayloadError("min 不能为负数", "min"));
        }

        if (max is not null && max < (min ?? 0))
        {
            return (null, new ArcAgentPayloadError("max 必须大于等于 min", "max"));
        }

        if (!root.TryGetProperty("options", out var optionsElement) || optionsElement.ValueKind != JsonValueKind.Array)
        {
            return (null, new ArcAgentPayloadError("缺少 options 数组", "options"));
        }

        var options = new List<ArcAgentChoiceOption>();
        var index = 0;
        foreach (var optionElement in optionsElement.EnumerateArray())
        {
            var path = $"options[{index}]";
            if (optionElement.ValueKind != JsonValueKind.Object)
            {
                return (null, new ArcAgentPayloadError("选项必须是 JSON 对象", path));
            }

            if (!TryReadString(optionElement, "id", required: true, out var optionId, out error)
                || string.IsNullOrWhiteSpace(optionId))
            {
                return (null, new ArcAgentPayloadError(error?.Message ?? "选项缺少 id", $"{path}.id"));
            }

            if (optionId!.Trim().Length > MaxOptionIdChars)
            {
                return (null, new ArcAgentPayloadError($"选项 id 超过 {MaxOptionIdChars} 字符", $"{path}.id"));
            }

            if (!TryReadString(optionElement, "label", required: true, out var label, out error)
                || string.IsNullOrWhiteSpace(label))
            {
                return (null, new ArcAgentPayloadError(error?.Message ?? "选项缺少 label", $"{path}.label"));
            }

            if (label!.Trim().Length > MaxOptionLabelChars)
            {
                return (null, new ArcAgentPayloadError($"选项 label 超过 {MaxOptionLabelChars} 字符", $"{path}.label"));
            }

            if (!TryReadString(optionElement, "description", required: false, out var optionDescription, out error))
            {
                return (null, new ArcAgentPayloadError(error!.Message, $"{path}.description"));
            }

            if (optionDescription is { Length: > MaxChoiceDescriptionChars })
            {
                return (null, new ArcAgentPayloadError(
                    $"选项 description 超过 {MaxChoiceDescriptionChars} 字符",
                    $"{path}.description"));
            }

            if (!TryReadBool(optionElement, "disabled", defaultValue: false, out var optionDisabled, out error))
            {
                return (null, new ArcAgentPayloadError(error!.Message, $"{path}.disabled"));
            }

            if (options.Exists(option => string.Equals(option.Id, optionId.Trim(), StringComparison.Ordinal)))
            {
                return (null, new ArcAgentPayloadError($"选项 id 重复：{optionId}", $"{path}.id"));
            }

            options.Add(new ArcAgentChoiceOption(optionId.Trim(), label.Trim(), optionDescription?.Trim(), optionDisabled));
            index++;
        }

        if (options.Count == 0)
        {
            return (null, new ArcAgentPayloadError("options 至少需要 1 项", "options"));
        }

        if (options.Count > MaxOptionCount)
        {
            return (null, new ArcAgentPayloadError($"options 超过 {MaxOptionCount} 项", "options"));
        }

        var defaultSelected = new List<string>();
        if (root.TryGetProperty("defaultSelected", out var defaultElement))
        {
            if (defaultElement.ValueKind != JsonValueKind.Array)
            {
                return (null, new ArcAgentPayloadError("defaultSelected 必须是数组", "defaultSelected"));
            }

            var defaultIndex = 0;
            foreach (var item in defaultElement.EnumerateArray())
            {
                var selectedId = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (selectedId is null || !options.Exists(option => option.Id == selectedId))
                {
                    return (null, new ArcAgentPayloadError(
                        $"defaultSelected 引用了不存在的选项：{selectedId ?? "(null)"}",
                        $"defaultSelected[{defaultIndex}]"));
                }

                if (!defaultSelected.Contains(selectedId!))
                {
                    defaultSelected.Add(selectedId!);
                }

                defaultIndex++;
            }

            if (!multi && defaultSelected.Count > 1)
            {
                return (null, new ArcAgentPayloadError("单选组件的 defaultSelected 最多 1 项", "defaultSelected"));
            }
        }

        if (multi && max is not null && max > options.Count)
        {
            // "Up to N" where N exceeds the available options: clamp instead of rejecting a usable payload.
            max = options.Count;
        }

        return (new ArcAgentChoiceModel
        {
            Type = type,
            Version = version,
            Id = NormalizeId(id),
            Question = question.Trim(),
            Description = description?.Trim(),
            Raw = raw,
            Multi = multi,
            Options = options,
            DefaultSelected = defaultSelected,
            Min = min ?? 0,
            Max = max,
            SubmitLabel = string.IsNullOrWhiteSpace(submitLabel) ? "确认" : submitLabel.Trim(),
            Disabled = disabled,
        }, null);
    }

    private static (ArcAgentConfirmModel? Component, ArcAgentPayloadError? Error) ParseConfirm(
        JsonElement root,
        string raw,
        string type,
        string version,
        string? id)
    {
        if (!TryReadString(root, "question", required: true, out var question, out var error) || string.IsNullOrWhiteSpace(question))
        {
            return (null, error ?? new ArcAgentPayloadError("question 不能为空", "question"));
        }

        if (question!.Trim().Length > MaxQuestionChars)
        {
            return (null, new ArcAgentPayloadError($"question 超过 {MaxQuestionChars} 字符", "question"));
        }

        if (!TryReadString(root, "description", required: false, out var description, out error))
        {
            return (null, error);
        }

        if (description is { Length: > MaxConfirmDescriptionChars })
        {
            return (null, new ArcAgentPayloadError(
                $"description 超过 {MaxConfirmDescriptionChars} 字符",
                "description"));
        }

        if (!TryReadString(root, "confirmLabel", required: false, out var confirmLabel, out error)
            || !TryReadString(root, "cancelLabel", required: false, out var cancelLabel, out error)
            || !TryReadBool(root, "danger", defaultValue: false, out var danger, out error))
        {
            return (null, error);
        }

        if (confirmLabel is { Length: > MaxButtonLabelChars })
        {
            return (null, new ArcAgentPayloadError($"confirmLabel 超过 {MaxButtonLabelChars} 字符", "confirmLabel"));
        }

        if (cancelLabel is { Length: > MaxButtonLabelChars })
        {
            return (null, new ArcAgentPayloadError($"cancelLabel 超过 {MaxButtonLabelChars} 字符", "cancelLabel"));
        }

        return (new ArcAgentConfirmModel
        {
            Type = type,
            Version = version,
            Id = NormalizeId(id),
            Question = question.Trim(),
            Description = description?.Trim(),
            Raw = raw,
            ConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "确认" : confirmLabel.Trim(),
            CancelLabel = string.IsNullOrWhiteSpace(cancelLabel) ? "取消" : cancelLabel.Trim(),
            Danger = danger,
        }, null);
    }

    private static string? NormalizeId(string? id) => string.IsNullOrWhiteSpace(id) ? null : id.Trim();

    private static bool IsValidTypeName(string? type)
    {
        if (string.IsNullOrWhiteSpace(type) || type.Length > MaxTypeChars)
        {
            return false;
        }

        foreach (var character in type)
        {
            var lower = character is >= 'A' and <= 'Z' ? (char)(character + 32) : character;
            var valid = lower is >= 'a' and <= 'z' or >= '0' and <= '9' or '-';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static ArcAgentPayloadError JsonSyntaxError(JsonException exception)
    {
        var where = exception.LineNumber is { } line
            ? string.Create(CultureInfo.InvariantCulture, $"（第 {line + 1} 行，第 {(exception.BytePositionInLine ?? 0) + 1} 列）")
            : string.Empty;
        return new ArcAgentPayloadError($"JSON 语法错误{where}：{exception.Message}", "$");
    }

    private static bool TryReadString(
        JsonElement element,
        string name,
        bool required,
        out string? value,
        out ArcAgentPayloadError? error)
    {
        value = null;
        error = null;
        if (!element.TryGetProperty(name, out var property))
        {
            if (required)
            {
                error = new ArcAgentPayloadError($"缺少字段 {name}", name);
                return false;
            }

            return true;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            error = new ArcAgentPayloadError($"字段 {name} 必须是字符串", name);
            return false;
        }

        value = property.GetString();
        return true;
    }

    private static bool TryReadBool(
        JsonElement element,
        string name,
        bool defaultValue,
        out bool value,
        out ArcAgentPayloadError? error)
    {
        value = defaultValue;
        error = null;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            error = new ArcAgentPayloadError($"字段 {name} 必须是布尔值", name);
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadInt(
        JsonElement element,
        string name,
        int? defaultValue,
        out int? value,
        out ArcAgentPayloadError? error)
    {
        value = defaultValue;
        error = null;
        if (!element.TryGetProperty(name, out var property))
        {
            return true;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var number))
        {
            error = new ArcAgentPayloadError($"字段 {name} 必须是整数", name);
            return false;
        }

        value = number;
        return true;
    }

    private static ulong Fnv1a(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 1099511628211UL;
        }

        return hash;
    }
}
