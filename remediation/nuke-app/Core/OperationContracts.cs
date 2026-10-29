namespace AndroidMcp.BuildTool.Core;

public enum Category { Sources, Catalog, Design, Validate, Review, Publish }
public enum Operation
{
    None, Plan, Seed, Fetch, Inventory, Extract, Provenance, NativeParser,
    Build, Project, Resolve, Export, Migrate, Rehydrate, ApplyOverrides,
    Architecture, Assets, Storyboards, Factor, Reconstruct, Manifest, Package,
    All, GradleCompatibility, Toolchain, Contracts, Catalog, Design, Tests,
    MockGate, Reproducibility, Deliverables, Prepare, Launch, Recover, Assess,
    Check, Stage, Sync, BrowserQa, BuildFromSources, GradleProjection, Split, Reassemble, Snapshot
}

public sealed record OperationKey(Category Category, Operation Operation);
public sealed record OperationRequest(string Category, string Operation, IReadOnlyDictionary<string, string> Parameters);
public sealed record PlannedStep(string Id, IReadOnlyList<string> DependsOn);
public sealed record PlanError(string Code, string Parameter, string Message);
public sealed record OperationPlan(OperationKey Key, IReadOnlyList<PlannedStep> Steps);
public sealed record PlanningResult(OperationPlan? Plan, IReadOnlyList<PlanError> Errors)
{
    public bool IsValid => Plan is not null && Errors.Count == 0;
}
public interface IOperationPlanner
{
    PlanningResult Plan(OperationRequest request);
    IReadOnlyList<string> ObservedEffects { get; }
}

public sealed record Artifact(string RelativePath, ReadOnlyMemory<byte> Bytes, string MediaType);
public sealed record SourceContext(string Repository, string Revision, string SnapshotId, string? PlatformVersion);
public sealed record SourceDocument(string RelativePath, ReadOnlyMemory<byte> Bytes, SourceContext Context);
public sealed record SourceIssue(string Code, string Reason, int? StartByte, int? EndByte);
public enum StructuralParseStatus { Clean, Partial, Unsupported, Malformed }
public sealed record ExtractionResult(
    IReadOnlyList<System.Text.Json.Nodes.JsonObject> Records,
    IReadOnlyList<SourceIssue> Issues,
    StructuralParseStatus StructuralStatus,
    System.Text.Json.Nodes.JsonObject? Metadata = null);
public interface ISourceExtractor
{
    string Kind { get; }
    ExtractionResult Extract(SourceDocument source);
}
