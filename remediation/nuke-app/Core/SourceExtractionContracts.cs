namespace AndroidMcp.BuildTool.Core;

public sealed record SourceExtractionProfile
{
    private SourceExtractionProfile(string id) { Id = id; }
    public string Id { get; }
    public static SourceExtractionProfile RELEASE011 { get; } = new("release-011");
    public static SourceExtractionProfile CurrentCandidate { get; } = new("current-candidate");
}

public interface ISourcesExtractionDispatcher
{
    IReadOnlyList<string> SupportedKinds { get; }
    ExtractionResult Extract(SourceDocument source, string? kind, string? scope = null);
}
