using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class PackageSupportEnvironment
{
    public DocumentationRendererProfile Profile{get;}=RendererProfileCases.Original().Expected;
    public IReadOnlyList<ArtifactSourceSpec> Expected{get;}
    public IReadOnlyList<Artifact> Support{get;}
    public ArtifactGenerationPreparation Child{get;}
    public List<string> Trace{get;}=[];
    public PackageSupportEnvironment(string scenario)
    {
        if(scenario=="PROFILE-CHANGE")Profile=Profile with{Downloads=Profile.Downloads.Select(pin=>pin.LocalPath=="gradlew"?pin with{Sha256=ContentHash.Sha256(new byte[]{3,4,5}),SizeBytes=3}:pin).ToArray()};
        var project=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/design-c4");
        var paths=new[]{"build.gradle.kts","tooling/nuke-design-stages.json","settings.gradle.kts","gradle.properties","gradle/wrapper/gradle-wrapper.properties"};
        Expected=Array.AsReadOnly(paths.Select(path=>new ArtifactSourceSpec(path,"application/octet-stream"))
            .Concat(Profile.Downloads.Where(pin=>!pin.LocalPath.StartsWith(".tools/",StringComparison.Ordinal))
                .Select(pin=>new ArtifactSourceSpec(pin.LocalPath,"application/octet-stream",new(pin.LocalPath,pin.Sha256,pin.SizeBytes!.Value,"application/octet-stream")))).ToArray());
        Support=Array.AsReadOnly(Expected.Select(spec=>new Artifact(spec.RelativePath,scenario=="PROFILE-CHANGE"&&spec.RelativePath=="gradlew"?new byte[]{3,4,5}:File.ReadAllBytes(ArtifactPaths.Resolve(project,spec.RelativePath)),spec.MediaType)).ToArray());
        if(scenario=="MISSING")Support=Array.AsReadOnly(Support.Skip(1).ToArray());
        if(scenario=="EXTRA")Support=Array.AsReadOnly(Support.Append(new Artifact("unexpected",new byte[]{1},"application/octet-stream")).ToArray());
        if(scenario=="PIN")Support=Array.AsReadOnly(Support.Select(item=>item.RelativePath=="gradlew"?item with{Bytes=new byte[]{1}}:item).ToArray());
        Child=new(scenario=="COLLISION"?new[]{"gradlew"}:new[]{"build-tool/Build.cs"},token=>
        {token.ThrowIfCancellationRequested();Trace.Add("child");if(scenario=="CHILD-FAIL")throw new IOException("child failed");return Task.FromResult<IReadOnlyList<Artifact>>([new("build-tool/Build.cs",new byte[]{1,2},"application/octet-stream")]);},
            scenario=="NO-HOOK"?null:token=>{token.ThrowIfCancellationRequested();Trace.Add("verify");return Task.CompletedTask;});
    }
}
public static class PackageSupportCases
{
    public static IEnumerable<object[]> Cases=>new[]{"FULL","PROFILE-CHANGE","MISSING","EXTRA","PIN","COLLISION","NO-HOOK","CHILD-FAIL","CANCEL"}.Select(value=>new object[]{value});
    public static async Task AssertContract(string scenario,PackageSupportEnvironment e,IDocumentationPackageSupport provider)
    {
        var described=provider.Describe(e.Profile);Assert.Equal(e.Expected,described);
        var list=Assert.IsAssignableFrom<IList<ArtifactSourceSpec>>(described);Assert.Throws<NotSupportedException>(()=>list[0]=list[0]);
        ArtifactGenerationPreparation? prepared=null;var error=Record.Exception(()=>prepared=provider.Append(e.Profile,e.Child,e.Support));
        if(scenario is "MISSING" or "EXTRA" or "PIN" or "COLLISION" or "NO-HOOK")
        {Assert.IsType<DocumentationBuildPayloadException>(error);Assert.Null(prepared);Assert.Empty(e.Trace);return;}
        Assert.Null(error);Assert.NotNull(prepared);Assert.Equal(e.Child.OutputPaths.Concat(e.Support.Select(item=>item.RelativePath)),prepared.OutputPaths);
        using var cancellation=new CancellationTokenSource();if(scenario=="CANCEL")cancellation.Cancel();
        IReadOnlyList<Artifact>? outputs=null;var materializeError=await Record.ExceptionAsync(async()=>outputs=await prepared.MaterializeAsync(cancellation.Token));
        if(scenario is "CANCEL" or "CHILD-FAIL")
        {if(scenario=="CANCEL")Assert.IsAssignableFrom<OperationCanceledException>(materializeError);else Assert.IsType<IOException>(materializeError);Assert.Null(outputs);Assert.Equal(scenario=="CANCEL"?[]:new[]{"child"},e.Trace);return;}
        Assert.Null(materializeError);Assert.NotNull(outputs);Assert.Equal(9,outputs.Count);
        Assert.Equal(new byte[]{1,2},outputs[0].Bytes.ToArray());
        foreach(var item in e.Support)Assert.Equal(item,outputs.Single(output=>output.RelativePath==item.RelativePath));
        Assert.Same(e.Child.VerifyBeforeCommitAsync,prepared.VerifyBeforeCommitAsync);
        await prepared.VerifyBeforeCommitAsync!(CancellationToken.None);Assert.Equal(new[]{"child","verify"},e.Trace);
    }
}
public sealed class ScriptedPackageSupport(PackageSupportEnvironment e,string scenario,string mutation=""):IDocumentationPackageSupport
{
    public IReadOnlyList<ArtifactSourceSpec> Describe(DocumentationRendererProfile profile)=>mutation=="describe"?Array.AsReadOnly(e.Expected.Skip(1).ToArray()):e.Expected;
    public ArtifactGenerationPreparation Append(DocumentationRendererProfile profile,ArtifactGenerationPreparation child,IReadOnlyList<Artifact> captured)
    {
        if(scenario is "MISSING" or "EXTRA" or "PIN" or "COLLISION" or "NO-HOOK"){
            if(mutation!="admit")throw new DocumentationBuildPayloadException("BUILD_SUPPORT");
        }
        return new(child.OutputPaths.Concat(captured.Select(item=>item.RelativePath)).ToArray(),async token=>
        {
            var result=await child.MaterializeAsync(token);
            return result.Concat(mutation=="omit"?captured.Skip(1):mutation=="bytes"?captured.Select((item,index)=>index==0?item with{Bytes=new byte[]{99}}:item):captured).ToArray();
        },mutation=="hook"?null:child.VerifyBeforeCommitAsync);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationPackageSupportMockTests
{
    [Theory,MemberData(nameof(PackageSupportCases.Cases),MemberType=typeof(PackageSupportCases))]
    public Task CorrectDeclaredSupportAndCompositionCasesPass(string scenario)
    {var e=new PackageSupportEnvironment(scenario);return PackageSupportCases.AssertContract(scenario,e,new ScriptedPackageSupport(e,scenario));}
    [Theory]
    [InlineData("FULL","describe")][InlineData("FULL","omit")][InlineData("FULL","hook")][InlineData("FULL","bytes")][InlineData("PROFILE-CHANGE","describe")]
    [InlineData("MISSING","admit")][InlineData("EXTRA","admit")][InlineData("PIN","admit")][InlineData("COLLISION","admit")][InlineData("NO-HOOK","admit")]
    public async Task EveryDeclaredBoundaryAndOutputOmissionIsRejected(string scenario,string mutation)
    {var e=new PackageSupportEnvironment(scenario);Assert.NotNull(await Record.ExceptionAsync(()=>PackageSupportCases.AssertContract(scenario,e,new ScriptedPackageSupport(e,scenario,mutation))));}
}
