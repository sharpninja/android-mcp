using System.Text.Json;

namespace AndroidMcp.BuildTool.Core;

public sealed record StrictJsonDiagnostic(string Code, string Path, string? DecodedPropertyName, string Message, bool IsFatal, long CanonicalRank = 0);

// Value owns a cloned DOM when syntax parsing succeeds, including ambiguity
// and semantic-fatal cases. Raw numeric lexemes are never rewritten.
public sealed record StrictJsonInspection(JsonElement? Value, IReadOnlyList<StrictJsonDiagnostic> Diagnostics)
{
    public bool HasFatalErrors => Diagnostics.Any(x => x.IsFatal);
    public bool HasDuplicateProperties => Diagnostics.Any(x => x.Code == CanonicalJsonErrors.DuplicateProperty);
    // Same detected instances, ordered by the original canonical validation
    // traversal: own names first, scalar-sorted object values, array index order.
    public IReadOnlyList<StrictJsonDiagnostic> CanonicalDiagnostics => Diagnostics.OrderBy(x => x.CanonicalRank).ToArray();
}

public interface IStrictJsonInspector
{
    StrictJsonInspection Inspect(ReadOnlyMemory<byte> utf8);
    StrictJsonInspection Inspect(JsonElement value);
}
