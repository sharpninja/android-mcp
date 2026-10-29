using System.Collections.Frozen;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;

namespace AndroidMcp.BuildTool.Sources;

public sealed class LoadedSourceProducerAssemblySource : ISourceProducerAssemblySource
{
    public SourceProducerAssemblyObservation Capture()
    {
        var assembly = typeof(SourcesRuntimeFactory).Assembly;
        return new(assembly.Location, assembly.ManifestModule.ModuleVersionId, File.ReadAllBytes(assembly.Location),
            RuntimeInformation.FrameworkDescription + "|" + RuntimeInformation.RuntimeIdentifier);
    }
}

public sealed class SourceProducerIdentityBuilder(ISourceProducerAssemblySource? assemblySource = null,
    IJavaSourceRuntimeLoader? javaLoader = null) : ISourceProducerIdentityBuilder
{
    private readonly ISourceProducerAssemblySource assemblies = assemblySource ?? new LoadedSourceProducerAssemblySource();
    private static SourceExtractException Failure(string code, string message, Exception? inner = null) =>
        new("SOURCE_PRODUCER_" + code, message, inner);

    public SourceProducerIdentityAdmission Create(SourceJavaAdmission? java = null)
    {
        var source = assemblies.Capture();
        if (string.IsNullOrWhiteSpace(source.RuntimeIdentity)) throw Failure("RUNTIME", "Loaded runtime identity is absent");
        TestGateBinaryIdentity observed;
        try { observed = TestGateManagedPeIdentity.Observe(source.AssemblyPath, source.AssemblyBytes); }
        catch (TestGateFactsException error) { throw Failure("ASSEMBLY", "Observed Source assembly is not supported managed PE", error); }
        if (source.LoadedModuleId == Guid.Empty || observed.ModuleId != source.LoadedModuleId)
            throw Failure("ASSEMBLY", "Source assembly bytes do not match the loaded module identity");
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["native/assembly"] = observed.Sha256, ["native/context"] = observed.Sha256, ["native/parser"] = observed.Sha256 };
        var parser = observed.Sha256; SourceJavaAdmission? admitted = null;
        if (java is not null)
        {
            if (javaLoader is null) throw Failure("JAVA_IDENTITY", "Selected Java identity requires its explicit validated loader");
            using var runtime = javaLoader.Open(java);
            if (runtime is not IJavaSourceProducerIdentityRuntime evaluated)
                throw Failure("JAVA_IDENTITY", "Java runtime does not expose its evaluated identity");
            var identity = evaluated.Identity;
            if (identity.ManifestPath != Path.GetFullPath(java.ManifestPath) ||
                java.ExpectedManifestSha256 is { } sha && sha != identity.ManifestSha256 ||
                java.ExpectedManifestLength is { } length && length != identity.ManifestLength)
                throw Failure("JAVA_IDENTITY", "Java identity does not correlate to the exact selected manifest admission");
            // The sole Java loader owns JSON, native path/hash/export/ABI and
            // version admission. This builder only binds its evaluated facts.
            parser = identity.NativeSha256;
            files["native/parser"] = parser; files["native/java-manifest"] = identity.ManifestSha256;
            admitted = new(identity.ManifestPath, identity.ManifestSha256, identity.ManifestLength);
        }
        return new(new("csharp-native-source-generation-v1", files.ToFrozenDictionary(StringComparer.Ordinal),
            source.RuntimeIdentity, parser, observed.Sha256), admitted);
    }
}
