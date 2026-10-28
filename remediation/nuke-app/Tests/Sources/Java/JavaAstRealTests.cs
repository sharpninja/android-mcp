using System.Text.Json.Nodes;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public sealed class JavaNativeFixture : IDisposable
{
    public JavaNativeParser Parser { get; }
    public JsonObject SourceLock { get; }
    public string ManifestPath { get; }
    public JavaNativeFixture()
    {
        var manifest=Environment.GetEnvironmentVariable("ANDROIDMCP_JAVA_NATIVE_MANIFEST")??throw new InvalidDataException("Set ANDROIDMCP_JAVA_NATIVE_MANIFEST to the exact Sources/NativeParser generation manifest before native Java tests.");
        ManifestPath=Path.GetFullPath(manifest);var sourceLock=JsonNode.Parse(File.ReadAllBytes(manifest))!;SourceLock=sourceLock.AsObject();
        var artifact=sourceLock["native_artifact"]!;
        Parser=new(new(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifest))!,artifact["relative_path"]!.GetValue<string>()),artifact["sha256"]!.GetValue<string>(),sourceLock["rid"]!.GetValue<string>()));
    }
    public void Dispose()=>Parser.Dispose();
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaNativeBuilderRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Fact]
    public void FreshNativeGenerationReceiptSatisfiesIdenticalWorkflowOutputAssertions()
    {
        var sourceLock=fixture.SourceLock;var request=sourceLock["workflow_request"]!.Deserialize<JavaNativeWorkflowRequest>()!;var native=sourceLock["native_artifact"]!;
        var artifact=new StoredArtifact(native["relative_path"]!.GetValue<string>(),native["sha256"]!.GetValue<string>(),native["length"]!.GetValue<long>(),"application/x-sharedlib");
        var build=new JavaNativeBuildResult(artifact,sourceLock["compilation"]!.Deserialize<ProcessRequest>()!,sourceLock["process"]!.Deserialize<ProcessResult>()!,
            sourceLock["runtime_commit"]!.GetValue<string>(),sourceLock["grammar_commit"]!.GetValue<string>(),native["language_abi"]!.GetValue<int>());
        var bytes=File.ReadAllBytes(fixture.ManifestPath);var manifest=new StoredArtifact("native-source-lock.json",ContentHash.Sha256(bytes),bytes.LongLength,"application/json");
        JavaWorkflowCase.AssertResult(request,new(manifest,build,sourceLock));
    }
    [Fact]
    public async Task IdenticalNativeBuildContractBindsToActualPinnedCompilerAndSources()
    {
        var sourceLock=fixture.SourceLock;var original=sourceLock["workflow_request"]!.Deserialize<JavaNativeWorkflowRequest>()!;
        var output=Path.Combine(Path.GetDirectoryName(fixture.ManifestPath)!,"validation","native-build-"+Guid.NewGuid().ToString("N"));
        var sourceRoot=Path.GetFullPath(original.CacheRoot??Path.Combine(original.OutputRoot,"native-sources"));
        var hashes=sourceLock["source_files"]!.AsArray().ToDictionary(x=>x!["path"]!.GetValue<string>(),x=>x!["sha256"]!.GetValue<string>(),StringComparer.Ordinal);
        var request=new JavaNativeBuildRequest(sourceRoot,hashes,original.Compiler,Path.Combine(output,"work"),Path.Combine(output,"receipts"),"native/linux-x64/libandroidmcp_java_parser.so");
        var provider=new JavaNativeBuilder(new NativeProcessRunner(new SystemClock()),new FileArtifactStore(output,bytes=>ContentHash.Sha256(bytes.Span)));
        await JavaNativeBuildCases.AssertContract(provider,request);
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaAstRealTests(JavaNativeFixture fixture):IClassFixture<JavaNativeFixture>
{
    [Theory,MemberData(nameof(JavaContractCases.Cases),MemberType=typeof(JavaContractCases))]
    public void IdenticalAstCasesBindToPinnedNativeProductionVisitor(string id)=>JavaContractCases.AssertContract(JavaContractCases.Get(id),new JavaSourceExtractor(fixture.Parser));
}
