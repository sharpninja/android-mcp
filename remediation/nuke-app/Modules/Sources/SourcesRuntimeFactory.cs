using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool.Sources;

/// <summary>The sole managed source-provider and pure-concern composition.</summary>
public sealed class SourcesRuntimeFactory : ISourcesRuntimeFactory
{
    private readonly IJavaSourceRuntimeLoader? javaLoader;
    private readonly IReadOnlyList<ISourceExtractor> managedProviders;
    private readonly SourcesRuntimeConcerns concerns;

    public SourcesRuntimeFactory(IJavaSourceRuntimeLoader? javaLoader = null, IPermissionReferenceSourceFacts? permissionReference = null)
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
        managedProviders = Array.AsReadOnly<ISourceExtractor>(
        [
            new AidlSourceExtractor(Hash), new SignatureSourceExtractor(Hash),
            new ManifestSourceExtractor(), new HidlSourceExtractor(), new SdkApiIndexExtractor(),
            new PermissionReferenceAdapter(permission)
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
                providers.Add(resource.Extractor);
            }
            var dispatcher = new SourcesExtractionDispatcher(providers, profile);
            return new Runtime(dispatcher, concerns, resource);
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
    private sealed class Runtime(ISourcesExtractionDispatcher dispatcher, SourcesRuntimeConcerns concerns, IJavaSourceRuntime? java) : ISourcesRuntime
    {
        private int disposed;
        public ISourcesExtractionDispatcher Dispatcher { get { Check(); return dispatcher; } }
        public SourcesRuntimeConcerns Concerns { get { Check(); return concerns; } }
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
