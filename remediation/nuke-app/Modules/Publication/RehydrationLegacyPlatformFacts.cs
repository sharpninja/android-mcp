using System.Text.Json;

namespace AndroidMcp.BuildTool.Publication;

// Capture only: no truth, scalar text, number formatting or selection engine.
// Value is detached with JsonElement.Clone inside the sole PlanCore walk.
// Absent is false/Undefined/null. Explicit JSON null is true/Null/a cloned
// null element, not a missing Nullable<JsonElement>.
public sealed record RehydrationLegacyPlatformValue(
    bool Present,JsonValueKind Kind,JsonElement? Value);
public sealed record RehydrationLegacyPlatformFacts(
    RehydrationLegacyPlatformValue Row,RehydrationLegacyPlatformValue Transport);
