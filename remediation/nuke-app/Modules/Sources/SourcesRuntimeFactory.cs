using System.Collections.Frozen;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>The sole managed source-provider and pure-concern composition.</summary>
public sealed class SourcesRuntimeFactory : ISourcesRuntimeFactory
{
    private readonly IJavaSourceRuntimeLoader? javaLoader;
    private sealed record ProviderRegistration(ISourceExtractor Provider, bool CacheProducer);
    private readonly IReadOnlyList<ProviderRegistration> managedProviders;
    private readonly SourcesRuntimeConcerns concerns;

    public SourcesRuntimeFactory(IJavaSourceRuntimeLoader? javaLoader = null, IPermissionReferenceSourceFacts? permissionReference = null,
        ISdkAnnotationArchiveSourceFacts? annotationArchive = null)
    {
        this.javaLoader = javaLoader;
        var canonical = new CanonicalJson();
        var facts = new SourceFactProducer(canonical);
        var typeFacts = new SourceTypeFacts(new JvmSourceLinker(), canonical);
        var html = new SourceHtmlDecoder();
        var permission = permissionReference ?? new PermissionReferenceSourceFacts(html);
        var inspector = new StrictJsonInspector();
        concerns = new(new PermissionExpressionSourceFacts(), permission, typeFacts, typeFacts, facts, facts, facts,
            new SoongSourceFacts(new GlobMatcher()), new ApexManifestSourceFacts(inspector), new NativeAbiSourceFacts(inspector),
            new PublishedInterfaceHashSourceFacts(), html);
        // These are actual providers. Alias/profile decisions exist once in the
        // dispatcher. Pure concern services above do not become declarations.
        managedProviders = Array.AsReadOnly<ProviderRegistration>(
        [
            new(new AidlSourceExtractor(Hash), false), new(new SignatureSourceExtractor(Hash), false),
            new(new ManifestSourceExtractor(), false), new(new HidlSourceExtractor(), false), new(new SdkApiIndexExtractor(), true),
            new(new PermissionReferenceAdapter(permission), false),
            new(new AnnotationArchiveAdapter(annotationArchive ?? new SdkAnnotationArchiveSourceFacts(typeFacts, new SourceXmlFormatter())), false)
        ]);
    }

    public ISourcesRuntime Create(SourceExtractionProfile profile, SourceJavaAdmission? java = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (java is not null && javaLoader is null)
            throw new InvalidOperationException("Java admission loader is required for a selected native manifest");
        IJavaSourceRuntime? resource = null;
        try
        {
            var providers = managedProviders.ToList();
            if (java is not null)
            {
                resource = javaLoader!.Open(java) ?? throw new InvalidOperationException("Java admission loader returned no owned runtime");
                if (resource.Extractor.Kind != "java-source") throw new ArgumentException("Admitted Java provider kind must be java-source", nameof(java));
                providers.Add(new(resource.Extractor, true));
            }
            var dispatcher = new SourcesExtractionDispatcher(providers.Select(registration => registration.Provider), profile);
            var cacheKinds = providers.Where(registration => registration.CacheProducer).Select(registration => registration.Provider.Kind).ToFrozenSet(StringComparer.Ordinal);
            return new Runtime(dispatcher, concerns, resource, cacheKinds);
        }
        catch
        {
            resource?.Dispose();
            throw;
        }
    }

    private static string Hash(ReadOnlyMemory<byte> bytes) => ContentHash.Sha256(bytes.Span);

    private sealed class PermissionReferenceAdapter(IPermissionReferenceSourceFacts permission) : ISourceExtractor
    {
        public string Kind => "permission-reference";
        public ExtractionResult Extract(SourceDocument source) => new([], [], StructuralParseStatus.Clean,
            new JsonObject { ["permission_reference"] = permission.Parse(source.Bytes).DeepClone() });
    }
    private sealed class AnnotationArchiveAdapter(ISdkAnnotationArchiveSourceFacts archive) : IBinarySourceExtractor
    {
        public string Kind => "sdk-annotations";
        public ExtractionResult Extract(SourceDocument source)
        {
            var report = archive.Parse(source.Bytes);
            var status = report["status"]?.GetValue<string>() switch
            { "parsed" => StructuralParseStatus.Clean, "partial" => StructuralParseStatus.Partial, _ => throw new FormatException("Unrecognized SDK annotation archive status") };
            var issues = report["issues"]!.AsArray().Select(issue => new SourceIssue(issue!["code"]!.GetValue<string>(), issue["reason"]!.GetValue<string>(), null, null)).ToArray();
            return new([], issues, status, new JsonObject { ["annotation_archive"] = report.DeepClone() });
        }
    }
    private sealed class Runtime(ISourcesExtractionDispatcher dispatcher, SourcesRuntimeConcerns concerns, IJavaSourceRuntime? java, FrozenSet<string> cacheKinds) : ISourcesCacheRuntime
    {
        private int disposed;
        public ISourcesExtractionDispatcher Dispatcher { get { Check(); return dispatcher; } }
        public SourcesRuntimeConcerns Concerns { get { Check(); return concerns; } }
        public IReadOnlySet<string> CacheProducerKinds { get { Check(); return cacheKinds; } }
        private void Check()
        {
            if (Volatile.Read(ref disposed) != 0) throw new ObjectDisposedException(nameof(Runtime));
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) java?.Dispose();
        }
    }
}
