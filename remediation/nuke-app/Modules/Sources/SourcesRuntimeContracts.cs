using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources;

// The selected manifest is admitted locally before any source output is opened.
// Loading cannot fetch, build, install, or fall back to a system parser.
public sealed record SourceJavaAdmission(string ManifestPath, string? ExpectedManifestSha256 = null, long? ExpectedManifestLength = null);
public interface IJavaSourceRuntime : IDisposable
{
    ISourceExtractor Extractor { get; }
}
public interface IJavaSourceRuntimeLoader
{
    // Validates selected manifest, pinned runtime/grammar identities, safe native
    // path, bytes/hash, RID, required exports and ABI. Owns cleanup on failure.
    IJavaSourceRuntime Open(SourceJavaAdmission admission);
}

// The source factory is the one provider/concern composition boundary. Alias
// routing remains exclusively inside ISourcesExtractionDispatcher.
public interface ISourcesRuntimeFactory
{
    ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? java = null);
}
public interface ISourcesRuntime : IDisposable
{
    // Access after disposal throws ObjectDisposedException. Disposal is idempotent.
    ISourcesExtractionDispatcher Dispatcher { get; }
    SourcesRuntimeConcerns Concerns { get; }
}
public sealed record SourcesRuntimeConcerns(
    IPermissionExpressionSourceFacts PermissionExpressions,
    IPermissionReferenceSourceFacts PermissionReference,
    IAnnotationTargetSourceFacts AnnotationTargets,
    IStructuralTypeHeaderSourceFacts TypeHeaders,
    ICallbackSourceFacts Callbacks,
    IFunctionalTaxonomySourceFacts Taxonomy,
    IInterfaceApplicabilitySourceFacts Applicability,
    ISoongSourceFacts Soong,
    IApexManifestSourceFacts Apex,
    INativeAbiSourceFacts NativeAbi,
    IPublishedInterfaceHashSourceFacts PublishedHashes,
    ISourceHtmlDecoder Html);
