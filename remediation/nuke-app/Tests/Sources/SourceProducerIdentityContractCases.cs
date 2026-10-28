using System.Collections.Frozen;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

public sealed class SourceProducerIdentityFixture
{
    public required string Id { get; init; }
    public SourceProducerAssemblyObservation Assembly { get; private set; } = null!;
    public SourceJavaAdmission? Java { get; private set; }
    public JavaSourceProducerIdentity JavaIdentity { get; private set; } = null!;
    public SourceProducerIdentityAdmission Expected { get; private set; } = null!;
    public Exception? Failure { get; private set; }
    public Exception Sentinel { get; } = new IOException("Literal upstream identity admission failure");
    public bool Early => Id is "bad-mvid" or "bad-pe" or "empty-runtime" or "capture-fault";
    public int WantedOpens => Java is null || Early ? 0 : 1;
    public int WantedDisposals => WantedOpens == 1 && Id != "java-open-fault" ? 1 : 0;
    public int WantedIdentityReads => WantedDisposals == 1 && Id != "java-unavailable" ? 1 : 0;
    public static SourceProducerIdentityFixture Create(string id)
    {
        var fixture = new SourceProducerIdentityFixture { Id = id };
        var assembly = typeof(SourcesRuntimeFactory).Assembly;
        var bytes = File.ReadAllBytes(assembly.Location); var sha = ContentHash.Sha256(bytes);
        fixture.Assembly = new(assembly.Location, assembly.ManifestModule.ModuleVersionId, bytes,
            RuntimeInformation.FrameworkDescription + "|" + RuntimeInformation.RuntimeIdentifier);
        fixture.JavaIdentity = new("/fixtures/java/native-source-lock.json", new('b', 64), 79, "libandroidmcp_sources.so", new('a', 64), 123,
            "linux-x64", 14, "0.25.2", "6e0618704ad758ba2ea5822faa80bcd36fbeba3d", "695d1530e59804279d9c739335b73b882d9f1caf", "0.23.5", "94703d5a6bed02b98e438d7cad1136c01a60ba2c");
        if (id.StartsWith("java", StringComparison.Ordinal)) fixture.Java = new(fixture.JavaIdentity.ManifestPath);
        if (id == "java-pinned") fixture.Java = new(fixture.JavaIdentity.ManifestPath, fixture.JavaIdentity.ManifestSha256, fixture.JavaIdentity.ManifestLength);
        if (id == "java-wrong-path") fixture.JavaIdentity = fixture.JavaIdentity with { ManifestPath = "/fixtures/other/native-source-lock.json" };
        if (id == "java-wrong-pin") fixture.Java = new(fixture.Java!.ManifestPath, new('c', 64), fixture.JavaIdentity.ManifestLength);
        if (id == "bad-mvid") fixture.Assembly = fixture.Assembly with { LoadedModuleId = Guid.Empty };
        if (id == "bad-pe") fixture.Assembly = fixture.Assembly with { AssemblyBytes = new byte[] { 0xff } };
        if (id == "empty-runtime") fixture.Assembly = fixture.Assembly with { RuntimeIdentity = "" };
        fixture.Failure = id switch
        {
            "bad-mvid" or "bad-pe" => new SourceExtractException("SOURCE_PRODUCER_ASSEMBLY", "Managed identity mismatch"),
            "empty-runtime" => new SourceExtractException("SOURCE_PRODUCER_RUNTIME", "Runtime identity absent"),
            "java-unavailable" or "java-wrong-path" or "java-wrong-pin" => new SourceExtractException("SOURCE_PRODUCER_JAVA_IDENTITY", "Admitted Java identity differs"),
            "capture-fault" or "java-open-fault" or "java-identity-fault" => fixture.Sentinel, _ => null
        };
        var files = new Dictionary<string, string>(StringComparer.Ordinal) { ["native/assembly"] = sha, ["native/context"] = sha,
            ["native/parser"] = fixture.Java is null ? sha : fixture.JavaIdentity.NativeSha256 };
        if (fixture.Java is not null) files["native/java-manifest"] = fixture.JavaIdentity.ManifestSha256;
        fixture.Expected = new(new("csharp-native-source-generation-v1", files.ToFrozenDictionary(StringComparer.Ordinal),
            fixture.Assembly.RuntimeIdentity, fixture.Java is null ? sha : fixture.JavaIdentity.NativeSha256, sha),
            fixture.Java is null ? null : new(fixture.JavaIdentity.ManifestPath, fixture.JavaIdentity.ManifestSha256, fixture.JavaIdentity.ManifestLength));
        return fixture;
    }
}

public sealed class SourceProducerIdentityRig(SourceProducerIdentityFixture fixture) : ISourceProducerAssemblySource, IJavaSourceRuntimeLoader
{
    public int Captures, IdentityReads, Disposals;
    public List<SourceJavaAdmission> Opens { get; } = [];
    public SourceProducerAssemblyObservation Capture()
    { Captures++; if (fixture.Id == "capture-fault") throw fixture.Sentinel; return fixture.Assembly; }
    public IJavaSourceRuntime Open(SourceJavaAdmission admission)
    {
        Opens.Add(admission); Assert.Equal(fixture.Java, admission);
        if (fixture.Id == "java-open-fault") throw fixture.Sentinel;
        return fixture.Id == "java-unavailable" ? new BasicRuntime(this) : new IdentityRuntime(this, fixture);
    }
    private class BasicRuntime(SourceProducerIdentityRig rig) : IJavaSourceRuntime
    {
        protected bool Disposed;
        protected SourceProducerIdentityRig Owner { get; } = rig;
        public ISourceExtractor Extractor => throw new InvalidOperationException("Identity construction must not extract");
        public void Dispose() { if (Disposed) return; Disposed = true; Owner.Disposals++; }
    }
    private sealed class IdentityRuntime(SourceProducerIdentityRig rig, SourceProducerIdentityFixture fixture) : BasicRuntime(rig), IJavaSourceProducerIdentityRuntime
    {
        public JavaSourceProducerIdentity Identity
        {
            get { ObjectDisposedException.ThrowIf(Disposed, this); Owner.IdentityReads++; if (fixture.Id == "java-identity-fault") throw fixture.Sentinel; return fixture.JavaIdentity; }
        }
    }
}

public static class SourceProducerIdentityContractCases
{
    public static readonly string[] Ids = ["managed", "java", "java-pinned", "bad-mvid", "bad-pe", "empty-runtime", "capture-fault",
        "java-unavailable", "java-open-fault", "java-identity-fault", "java-wrong-path", "java-wrong-pin"];
    public static IEnumerable<object[]> Cases => Ids.Select(id => new object[] { id });
    public static void AssertContract(string id, Func<SourceProducerIdentityFixture, SourceProducerIdentityRig, ISourceProducerIdentityBuilder> create)
    {
        var fixture = SourceProducerIdentityFixture.Create(id); var original = fixture.Assembly.AssemblyBytes.ToArray(); var rig = new SourceProducerIdentityRig(fixture);
        SourceProducerIdentityAdmission? result = null; var error = Record.Exception(() => result = create(fixture, rig).Create(fixture.Java));
        Assert.Equal(original, fixture.Assembly.AssemblyBytes.ToArray()); Assert.Equal(1, rig.Captures);
        Assert.Equal(fixture.WantedOpens, rig.Opens.Count); Assert.Equal(fixture.WantedDisposals, rig.Disposals); Assert.Equal(fixture.WantedIdentityReads, rig.IdentityReads);
        if (fixture.Failure is not null)
        {
            Assert.Null(result); Assert.NotNull(error);
            if (fixture.Failure is SourceExtractException source) Assert.Equal(source.Code, Assert.IsType<SourceExtractException>(error).Code);
            else Assert.Same(fixture.Sentinel, error);
            return;
        }
        Assert.Null(error); Assert.NotNull(result);
        Assert.Equal(fixture.Expected.Identity.ProducerId, result.Identity.ProducerId); Assert.Equal(fixture.Expected.Identity.RuntimeIdentity, result.Identity.RuntimeIdentity);
        Assert.Equal(fixture.Expected.Identity.ParserIdentity, result.Identity.ParserIdentity); Assert.Equal(fixture.Expected.Identity.ContextIdentity, result.Identity.ContextIdentity);
        Assert.Equal(fixture.Expected.Identity.ProducerFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal), result.Identity.ProducerFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(fixture.Expected.JavaAdmission, result.JavaAdmission);
    }
}
