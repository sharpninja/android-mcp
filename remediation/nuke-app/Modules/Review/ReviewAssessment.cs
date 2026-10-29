using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Modules.Review;

/// <summary>Only visible provider events are exported. Missing terminal evidence never authorizes retry.</summary>
public sealed class ReviewAssessService(Func<ReadOnlyMemory<byte>, string> sha256)
{
    private static readonly HashSet<string> HiddenTypes = new(StringComparer.Ordinal)
    { "thinking", "redacted_thinking", "thinking_delta", "signature_delta" };

    public ReviewAssessment Assess(ReadOnlyMemory<byte> stream, ReviewTerminal terminal, string marker, ReadOnlyMemory<byte> diagnostics = default)
    {
        var models = new HashSet<string>(StringComparer.Ordinal);
        var efforts = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<JsonObject>();
        var visible = new List<string>();
        JsonObject? result = null;
        var hidden = false;
        var malformed = false;
        string? startupReason = null;
        foreach (var line in ReviewPrepareService.Utf8.GetString(stream.Span).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonNode? node;
            try { node = JsonNode.Parse(line); }
            catch (JsonException)
            {
                malformed = true;
                // Visibility is unknown for a broken JSON fragment. Keep raw bytes private.
                var bytes = ReviewPrepareService.Utf8.GetBytes(line);
                visible.Add(Encoding.UTF8.GetString(ReviewPrepareService.Json(new { type = "unparsed-visible-line",
                    utf8Bytes = bytes.Length, sha256 = sha256(bytes), reason = "Unparseable fragment; original retained in private raw capture." })));
                continue;
            }
            if (node is not JsonObject entry) { malformed = true; visible.Add(line); continue; }
            RemoveHidden(entry, ref hidden);
            var type = String(entry, "type");
            if (type == "unparsed-visible-line") malformed = true;
            if (type == "runtime-failure")
            {
                var priorReason = String(entry, "reason");
                if (priorReason.StartsWith("authentication:", StringComparison.Ordinal) || priorReason.StartsWith("capacity:", StringComparison.Ordinal) ||
                    priorReason.StartsWith("capability:", StringComparison.Ordinal)) startupReason = priorReason;
            }
            if (type == "assistant" && entry["message"] is JsonObject message)
            {
                Add(message, "model", models); Add(message, "effort", efforts);
            }
            if (type == "system") { Add(entry, "model", models); Add(entry, "effort", efforts); }
            if (type == "result")
            {
                result = entry;
                if (entry["modelUsage"] is JsonObject usage)
                    foreach (var model in usage) models.Add(model.Key);
                Add(entry, "model", models); Add(entry, "effort", efforts);
            }
            visible.Add(entry.ToJsonString());
            entries.Add(entry);
        }
        var text = VisibleText(entries);
        var report = string.Join("\n\n", text);
        if (diagnostics.Length > 0)
        {
            var privateText = ReviewPrepareService.Utf8.GetString(diagnostics.Span);
            foreach (var (category, terms) in new[]
            {
                ("capability", new[] { "unknown model", "invalid model", "unsupported effort", "invalid effort", "not supported for this model" }),
                ("authentication", new[] { "authentication", "permission denied", "invalid api key", "unauthorized", "api key required" }),
                ("capacity", new[] { "rate_limit", "rate limit", "capacity", "quota", "usage credits", "usage_credits", "billing", "overloaded" })
            })
            {
                var match = terms.FirstOrDefault(x => privateText.Contains(x, StringComparison.OrdinalIgnoreCase));
                if (match is not null) { startupReason = category + ": " + match; break; }
            }
            visible.Add(Encoding.UTF8.GetString(ReviewPrepareService.Json(new { type = "runtime-failure", reason = startupReason ?? "unclassified",
                utf8Bytes = diagnostics.Length, diagnosticsSha256 = sha256(diagnostics), rawRetainedPrivately = true })));
        }
        ReviewAssessment State(string status, string? reason = null) => new(status, report, reason,
            models.Order(StringComparer.Ordinal).ToArray(), efforts.Order(StringComparer.Ordinal).ToArray(), hidden, visible);
        if (!terminal.KnownTerminal || terminal.Cancelled) return State("outcome-uncertain", "No authoritative terminal outcome.");
        if (result is null && startupReason is not null) return State("paused-provider", startupReason);
        if (result is null || malformed) return State("outcome-uncertain", "Missing terminal event or malformed event stream.");
        var reason = String(result, "terminal_reason") + " " + result["api_error"]?.ToJsonString();
        var error = Bool(result, "is_error");
        var statusCode = result["api_error_status"]?.ToString();
        if (statusCode is "401" or "403" or "429" ||
            new[] { "auth", "permission", "rate_limit", "billing", "quota", "refill_breaker", "usage_credits", "capacity", "overloaded", "unsupported effort", "unknown model" }
                .Any(x => reason.Contains(x, StringComparison.OrdinalIgnoreCase)) ||
            Nonempty(result["permission_denials"]) || (error && Nonempty(result["safety_stops"])))
            return State("paused-provider", reason.Trim());
        if (reason.Contains("context", StringComparison.OrdinalIgnoreCase) || reason.Contains("prompt_too_long", StringComparison.OrdinalIgnoreCase))
            return State("context", reason.Trim());
        if (error || terminal.ExitCode != 0) return State("outcome-uncertain", "Unhandled terminal error: " + reason.Trim());
        var stop = String(result, "stop_reason");
        var end = terminal.ExitCode == 0 && !error && stop == "end_turn";
        var verdict = FinalObject(text, marker);
        if (end && verdict is not null && Bool(verdict, "report_complete"))
        {
            if (verdict.ContainsKey("OverallVerdict"))
            {
                var accepted = String(verdict, "OverallVerdict") == "AGREE" &&
                    Number(verdict, "accuracy") >= 98 && Number(verdict, "completeness") >= 98 &&
                    Zero(verdict["FAIL"]) && Zero(verdict["UNKNOWN"]);
                return State(accepted ? "accepted" : "rejected");
            }
            return State("complete");
        }
        if (end && report.Trim().TrimEnd('`').Trim().EndsWith(marker, StringComparison.Ordinal)) return State("complete");
        return State("continue", stop.Length == 0 ? "Incomplete terminal response" : stop);
    }

    /// <summary>Exact ordered visible text fragments from typed provider events. Assistant
    /// fragments, including empty strings, suppress fallback; only the last result can supply it.</summary>
    public static IReadOnlyList<string> VisibleText(IEnumerable<JsonObject> events)
    {
        var chunks = new List<string>();
        JsonObject? result = null;
        foreach (var entry in events)
        {
            if (String(entry, "type") == "assistant" && entry["message"] is JsonObject message && message["content"] is JsonArray content)
                foreach (var block in content.OfType<JsonObject>())
                    if (String(block, "type") == "text") chunks.Add(String(block, "text"));
            if (String(entry, "type") == "result") result = entry;
        }
        if (chunks.Count == 0 && result is not null && !string.IsNullOrEmpty(String(result, "result"))) chunks.Add(String(result, "result"));
        return chunks;
    }

    private static bool Zero(JsonNode? value) => value is JsonArray array ? array.Count == 0 : value?.ToString() == "0";
    private static JsonObject? FinalObject(IReadOnlyList<string> chunks, string marker)
    {
        // Progress stays in Report and every original event. Only a complete suffix can be the final verdict.
        // Joining suffixes also preserves JSON split across assistant messages without inventing separators.
        for (var start = chunks.Count - 1; start >= 0; --start)
        {
            var candidate = string.Concat(chunks.Skip(start)).Trim();
            if (candidate.EndsWith(marker, StringComparison.Ordinal)) candidate = candidate[..^marker.Length].TrimEnd();
            if (candidate.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = candidate.IndexOf('\n');
                if (firstNewline >= 0) candidate = candidate[(firstNewline + 1)..].TrimEnd().TrimEnd('`').Trim();
                else
                {
                    candidate = candidate[3..];
                    if (candidate.StartsWith("json", StringComparison.Ordinal)) candidate = candidate[4..];
                    if (candidate.EndsWith("```", StringComparison.Ordinal)) candidate = candidate[..^3];
                    candidate = candidate.Trim();
                }
            }
            try { if (JsonNode.Parse(candidate) is JsonObject result) return result; }
            catch (JsonException) { }
        }
        return null;
    }
    private static double Number(JsonObject value, string key) => value[key]?.GetValueKind() == JsonValueKind.Number &&
        double.TryParse(value[key]!.ToJsonString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n is >= 0 and <= 100 ? n : double.NaN;
    private static string String(JsonObject value, string key) => value[key] is JsonValue scalar && scalar.TryGetValue<string>(out var s) ? s : "";
    private static bool Bool(JsonObject value, string key) => value[key] is JsonValue scalar && scalar.TryGetValue<bool>(out var b) && b;
    private static bool Nonempty(JsonNode? node) => node switch { null => false, JsonArray a => a.Count > 0, JsonObject o => o.Count > 0, _ => node.ToString() is not ("" or "0" or "false") };
    private static void Add(JsonObject value, string key, HashSet<string> output)
    { var text = String(value, key); if (text.Length > 0) output.Add(text); }
    private static void RemoveHidden(JsonNode node, ref bool removed)
    {
        if (node is JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; --i)
            {
                if (array[i] is JsonObject item && HiddenTypes.Contains(String(item, "type")))
                { array.RemoveAt(i); removed = true; }
                else if (array[i] is { } child) RemoveHidden(child, ref removed);
            }
        }
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                if (key is "thinking" or "redacted_thinking") { obj.Remove(key); removed = true; }
                else if (obj[key] is JsonObject item && HiddenTypes.Contains(String(item, "type"))) { obj.Remove(key); removed = true; }
                else if (obj[key] is { } child) RemoveHidden(child, ref removed);
            }
        }
    }
}

public sealed class ReviewRecoverService(ReviewAssessService assessor)
{
    public ReviewAssessment Recover(ReviewRecoveryInput input)
    {
        if (input.ExpectedHead != input.ObservedHead) throw new ReviewException("HEAD_MISMATCH", "Recovery snapshot head changed.");
        if (input.Active || !input.KnownTerminal)
            return new("outcome-uncertain", "", "Active or uncertain calls are preserved and never relaunched.", [], [], false, []);
        return assessor.Assess(input.Response, new(true, input.ExitCode), input.Marker);
    }
}
