using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AndroidMcp.BuildTool.Modules.Review;

/// <summary>Only visible provider events are exported. Missing terminal evidence never authorizes retry.</summary>
public sealed class ReviewAssessService
{
    private static readonly HashSet<string> HiddenTypes = new(StringComparer.Ordinal)
    { "thinking", "redacted_thinking", "thinking_delta", "signature_delta" };

    public ReviewAssessment Assess(ReadOnlyMemory<byte> stream, ReviewTerminal terminal, string marker)
    {
        var models = new HashSet<string>(StringComparer.Ordinal);
        var efforts = new HashSet<string>(StringComparer.Ordinal);
        var text = new List<string>();
        var visible = new List<string>();
        JsonObject? result = null;
        var hidden = false;
        var malformed = false;
        foreach (var line in ReviewPrepareService.Utf8.GetString(stream.Span).Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            JsonNode? node;
            try { node = JsonNode.Parse(line); }
            catch (JsonException)
            {
                malformed = true;
                // Never discard an unparseable visible fragment or claim it proves completion.
                visible.Add(Encoding.UTF8.GetString(ReviewPrepareService.Json(new { type = "unparsed-visible-line", text = line })));
                continue;
            }
            if (node is not JsonObject entry) { malformed = true; visible.Add(line); continue; }
            RemoveHidden(entry, ref hidden);
            var type = String(entry, "type");
            if (type == "assistant" && entry["message"] is JsonObject message)
            {
                Add(message, "model", models); Add(message, "effort", efforts);
                if (message["content"] is JsonArray content)
                    foreach (var block in content.OfType<JsonObject>())
                        if (String(block, "type") == "text") text.Add(String(block, "text"));
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
        }
        if (text.Count == 0 && result is not null && !string.IsNullOrEmpty(String(result, "result")))
            text.Add(String(result, "result"));
        var report = string.Join("\n\n", text);
        ReviewAssessment State(string status, string? reason = null) => new(status, report, reason,
            models.Order(StringComparer.Ordinal).ToArray(), efforts.Order(StringComparer.Ordinal).ToArray(), hidden, visible);
        if (!terminal.KnownTerminal || terminal.Cancelled) return State("outcome-uncertain", "No authoritative terminal outcome.");
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
        var stop = String(result, "stop_reason");
        var end = terminal.ExitCode == 0 && !error && stop == "end_turn";
        var candidate = string.Concat(text).Trim();
        if (candidate.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = candidate.IndexOf('\n');
            if (firstNewline >= 0) candidate = candidate[(firstNewline + 1)..].TrimEnd().TrimEnd('`').Trim();
        }
        JsonObject? verdict = null;
        try { verdict = JsonNode.Parse(candidate) as JsonObject; } catch (JsonException) { }
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

    private static bool Zero(JsonNode? value) => value is JsonArray array ? array.Count == 0 : value?.ToString() == "0";
    private static double Number(JsonObject value, string key) => double.TryParse(value[key]?.ToString(),
        System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : double.NaN;
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
                if (key is "thinking" or "redacted_thinking" or "signature") { obj.Remove(key); removed = true; }
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
