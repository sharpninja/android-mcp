namespace AndroidMcp.BuildTool.Core;

public sealed record SourceExtractionProfile
{
    private SourceExtractionProfile(string id) { Id = id; }
    public string Id { get; }
    public static SourceExtractionProfile RELEASE011 { get; } = new("release-011");
    public static SourceExtractionProfile CurrentCandidate { get; } = new("current-candidate");
    public static SourceExtractionProfile Resolve(string? id)
    {
        if (id is null || string.Equals(id, CurrentCandidate.Id, StringComparison.Ordinal)) return CurrentCandidate;
        if (string.Equals(id, RELEASE011.Id, StringComparison.Ordinal)) return RELEASE011;
        throw new InvalidOperationException("Unknown source extraction profile");
    }
}

public interface ISourcesExtractionDispatcher
{
    IReadOnlyList<string> SupportedKinds { get; }
    ExtractionResult Extract(SourceDocument source, string? kind, string? scope = null);
}
