using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class ConfigurationRecipeEnvironment
{
    public Artifact Properties{get;}=new("gradle/wrapper/gradle-wrapper.properties","admitted wrapper input\n"u8.ToArray(),"application/octet-stream");
    public Artifact Existing{get;}=new("toolchain-lock.json","{}"u8.ToArray(),"application/json");
    public Artifact Extra{get;}=new("tooling/nuke-design-stages.json","descriptor"u8.ToArray(),"application/json");
    public Artifact Output{get;}=new("generated/output.txt","materialized"u8.ToArray(),"text/plain");
    public ArtifactReceiptVerifier Receipts{get;}=new(new NativeContentHash());
    public List<string> Trace{get;}=[];
    public CancellationTokenSource Cancellation{get;}=new();
    public IReadOnlyList<Artifact> Configuration=>[Existing,Extra,Properties];
    public ArtifactGenerationRecipe Inner=>new([new(Existing.RelativePath,Existing.MediaType)],inputs=>{
        Trace.Add("prepare");Assert.Single(inputs);Assert.Same(Existing,inputs[0]);
        return new([Output.RelativePath],token=>{Assert.Equal(Cancellation.Token,token);Trace.Add("materialize");return Task.FromResult<IReadOnlyList<Artifact>>([Output]);},
            token=>{Assert.Equal(Cancellation.Token,token);Trace.Add("verify");return Task.CompletedTask;});});
}
public static class ConfigurationRecipeCases
{
    public static async Task AssertContract(ConfigurationRecipeEnvironment e,Func<ArtifactGenerationRecipe,IReadOnlyList<Artifact>,string?,ArtifactGenerationRecipe> bind)
    {
        var recipe=bind(e.Inner,e.Configuration,null);Assert.Empty(e.Trace);
        Assert.Equal(new[]{e.Existing,e.Extra,e.Properties}.Select(artifact=>new ArtifactSourceSpec(artifact.RelativePath,artifact.MediaType,e.Receipts.Describe(artifact))),recipe.Sources);
        var prepared=recipe.Prepare(e.Configuration);Assert.Equal(new[]{"prepare"},e.Trace);Assert.Equal(new[]{e.Output.RelativePath},prepared.OutputPaths);Assert.NotNull(prepared.VerifyBeforeCommitAsync);
        Assert.Equal(new[]{e.Output},await prepared.MaterializeAsync(e.Cancellation.Token));await prepared.VerifyBeforeCommitAsync(e.Cancellation.Token);
        Assert.Equal(new[]{"prepare","materialize","verify"},e.Trace);
    }
    public static void AssertCollision(string scenario,Func<ArtifactGenerationRecipe,IReadOnlyList<Artifact>,string?,ArtifactGenerationRecipe> bind)
    {
        var e=new ConfigurationRecipeEnvironment();var policy=scenario=="source-policy"?e.Existing.RelativePath:e.Properties.RelativePath;
        var inner=scenario=="output-policy"?new ArtifactGenerationRecipe(e.Inner.Sources,_=>new([policy],_=>Task.FromResult<IReadOnlyList<Artifact>>([]))):e.Inner;
        var error=Record.Exception(()=>bind(inner,e.Configuration,policy).Prepare(e.Configuration));
        Assert.Equal(scenario=="source-policy"?"Package policy cannot replace a declared documentation source.":"Package policy is configuration and cannot be a published output.",Assert.IsType<InvalidOperationException>(error).Message);
    }
    public static readonly string[] Faults=["missing-input","missing-pin","wrong-pin","wrong-media","configuration-output","wrong-inner-input","lost-output","lost-recheck","eager-materialize"];
    public static ArtifactGenerationRecipe Mock(ConfigurationRecipeEnvironment e,string mutation="")
    {
        var inner=e.Inner;var specs=e.Configuration.Select(artifact=>new ArtifactSourceSpec(artifact.RelativePath,mutation=="wrong-media"&&artifact==e.Properties?"wrong":artifact.MediaType,
            mutation=="missing-pin"&&artifact==e.Properties?null:mutation=="wrong-pin"&&artifact==e.Properties?e.Receipts.Describe(e.Extra):e.Receipts.Describe(artifact))).ToArray();
        if(mutation=="missing-input")specs=specs.SkipLast(1).ToArray();
        if(mutation=="eager-materialize")e.Trace.Add("materialize");
        return new(specs,all=>{
            var prepared=inner.Prepare(mutation=="wrong-inner-input"?all:all.Where(artifact=>artifact.RelativePath==e.Existing.RelativePath).ToArray());
            return prepared with{OutputPaths=mutation=="configuration-output"?prepared.OutputPaths.Append(e.Properties.RelativePath).ToArray():prepared.OutputPaths,
                MaterializeAsync=mutation=="lost-output"?_=>Task.FromResult<IReadOnlyList<Artifact>>([]):prepared.MaterializeAsync,
                VerifyBeforeCommitAsync=mutation=="lost-recheck"?null:prepared.VerifyBeforeCommitAsync};
        });
    }
}
public sealed record ConfigurationRuntimeObservation(string? Error,bool ManifestCommitted,bool OutputRetained,int PropertiesReads,bool CapturedBytesUnchanged);
public static class ConfigurationRuntimeCases
{
    public static readonly string[] Scenarios=["GOOD","BEFORE-ADMISSION","BEFORE-COMMIT"];
    public static ConfigurationRuntimeObservation Correct(string scenario)=>new(scenario=="GOOD"?null:scenario=="BEFORE-ADMISSION"?"INPUT_PIN_MISMATCH":"INPUT_CHANGED",scenario=="GOOD",scenario!="BEFORE-ADMISSION",scenario=="BEFORE-ADMISSION"?1:2,true);
    public static async Task AssertContract(string scenario,Func<string,Task<ConfigurationRuntimeObservation>> probe)
    {
        var expected=Correct(scenario);var actual=await probe(scenario);
        Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.ManifestCommitted,actual.ManifestCommitted);Assert.Equal(expected.OutputRetained,actual.OutputRetained);Assert.Equal(expected.PropertiesReads,actual.PropertiesReads);Assert.True(actual.CapturedBytesUnchanged);
    }
    public static ConfigurationRuntimeObservation Wrong(string scenario,string mutation)
    {
        var value=Correct(scenario);return mutation switch{"error"=>value with{Error="WRONG"},"manifest"=>value with{ManifestCommitted=!value.ManifestCommitted},"output"=>value with{OutputRetained=!value.OutputRetained},"reads"=>value with{PropertiesReads=0},_=>value with{CapturedBytesUnchanged=false}};
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationConfigurationRecipeMockTests
{
    [Fact]public Task CorrectCapturedPropertiesContractPasses(){var e=new ConfigurationRecipeEnvironment();return ConfigurationRecipeCases.AssertContract(e,(_,_,_)=>ConfigurationRecipeCases.Mock(e));}
    [Theory,MemberData(nameof(Faults))]
    public async Task EachInputPinOutputAndRecheckFaultIsDiscriminating(string mutation)
    {var e=new ConfigurationRecipeEnvironment();Assert.NotNull(await Record.ExceptionAsync(()=>ConfigurationRecipeCases.AssertContract(e,(_,_,_)=>ConfigurationRecipeCases.Mock(e,mutation))));}
    public static IEnumerable<object[]> Faults=>ConfigurationRecipeCases.Faults.Select(value=>new object[]{value});
    [Theory,InlineData("source-policy"),InlineData("output-policy")]
    public void BothExistingPolicyCollisionsFailBeforePublication(string scenario)
        =>ConfigurationRecipeCases.AssertCollision(scenario,(inner,config,policy)=>{
            if(scenario=="source-policy")throw new InvalidOperationException("Package policy cannot replace a declared documentation source.");
            return new(inner.Sources,_=>throw new InvalidOperationException("Package policy is configuration and cannot be a published output."));});
    [Theory,InlineData("source-policy"),InlineData("output-policy")]
    public void EachPolicyCollisionCannotBeSilentlyAccepted(string scenario)=>Assert.NotNull(Record.Exception(()=>ConfigurationRecipeCases.AssertCollision(scenario,(inner,_,_)=>inner)));
    public static IEnumerable<object[]> RuntimeCases=>ConfigurationRuntimeCases.Scenarios.Select(value=>new object[]{value});
    public static IEnumerable<object[]> RuntimeFaults=>ConfigurationRuntimeCases.Scenarios.SelectMany(scenario=>new[]{"error","manifest","output","reads","captured-bytes"}.Select(mutation=>new object[]{scenario,mutation}));
    [Theory,MemberData(nameof(RuntimeCases))]
    public Task CorrectPropertiesAdmissionAndCommitMockPasses(string scenario)=>ConfigurationRuntimeCases.AssertContract(scenario,value=>Task.FromResult(ConfigurationRuntimeCases.Correct(value)));
    [Theory,MemberData(nameof(RuntimeFaults))]
    public async Task EveryPropertiesAdmissionAndCommitFaultIsRejected(string scenario,string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>ConfigurationRuntimeCases.AssertContract(scenario,value=>Task.FromResult(ConfigurationRuntimeCases.Wrong(value,mutation)))));
}
