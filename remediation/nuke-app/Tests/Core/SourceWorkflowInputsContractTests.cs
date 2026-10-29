using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class WorkflowInputsFixture:IPinnedArtifactReader,IDisposable
{
    public string Mode{get;}
    public CancellationTokenSource Cancellation{get;}=new();
    public SourceWorkflowInputs Inputs{get;}
    public byte[] RecipeBytes{get;}=[0xff,0x01,0x02];
    public byte[] LockBytes{get;}=[0x00,0xfd];
    public List<(StoredArtifact Pin,CancellationToken Token)> Reads{get;}=[];
    public Exception Sentinel{get;}=new PinnedArtifactException("ARTIFACT_PIN_HASH");
    public WorkflowInputsFixture(string mode)
    {
        Mode=mode;Inputs=new(new("recipes/nested.recipe.gz",new('a',64),3,"application/gzip"),new("locks/recipe.lock.json",new('b',64),2,"application/json"));
        if(mode=="options")Inputs=Inputs with{Release="release-011",SourceLimit=3,AcquisitionProfile=AcquisitionCompatibilityProfile.GenericRehydrate};
        if(mode=="cancelled")Cancellation.Cancel();
    }
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken cancellationToken=default)
    {
        Reads.Add((pin,cancellationToken));
        if((Mode=="recipe-failure"&&Reads.Count==1)||(Mode=="lock-failure"&&Reads.Count==2))throw Sentinel;
        return Task.FromResult<ReadOnlyMemory<byte>>(pin.RelativePath==Inputs.Recipe.RelativePath?RecipeBytes:LockBytes);
    }
    public async Task AssertContract(ISourceWorkflowInputsReader provider)
    {
        RehydrationPlanRequest? result=null;Exception? error=null;
        try{result=await provider.ReadAsync(Inputs,Cancellation.Token);}catch(Exception e){error=e;}
        if(Mode=="cancelled")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(Mode.EndsWith("failure",StringComparison.Ordinal))Assert.Same(Sentinel,error);
        else{
            Assert.Null(error);Assert.NotNull(result);Assert.Equal(Inputs.Recipe.RelativePath,result.Recipe.RelativePath);Assert.Equal(Inputs.Recipe.MediaType,result.Recipe.MediaType);
            Assert.Equal(RecipeBytes,result.Recipe.Bytes.ToArray());Assert.Equal(Inputs.RecipeLock.RelativePath,result.Lock.RelativePath);Assert.Equal(Inputs.RecipeLock.MediaType,result.Lock.MediaType);
            Assert.Equal(LockBytes,result.Lock.Bytes.ToArray());Assert.Equal(Inputs.Release,result.ExpectedRelease);Assert.Equal(Inputs.SourceLimit,result.Limit);
            Assert.Equal(Inputs.AcquisitionProfile,result.Profile);Assert.True(result.Resume);Assert.False(result.AllowFileFixtures);
        }
        Assert.Equal(Mode=="cancelled"?0:Mode=="recipe-failure"?1:2,Reads.Count);
        if(Reads.Count>0){Assert.Same(Inputs.Recipe,Reads[0].Pin);Assert.Equal(Cancellation.Token,Reads[0].Token);}
        if(Reads.Count>1){Assert.Same(Inputs.RecipeLock,Reads[1].Pin);Assert.Equal(Cancellation.Token,Reads[1].Token);}
    }
    public void Dispose()=>Cancellation.Dispose();
}
public sealed class ScriptedWorkflowInputsReader(WorkflowInputsFixture fixture,string mutation=""):ISourceWorkflowInputsReader
{
    public async Task<RehydrationPlanRequest> ReadAsync(SourceWorkflowInputs inputs,CancellationToken cancellationToken=default)
    {
        if(mutation!="cancel")cancellationToken.ThrowIfCancellationRequested();
        var recipe=await fixture.ReadAsync(mutation=="pin"?inputs.Recipe with{Length=19}:inputs.Recipe,mutation=="token"?CancellationToken.None:cancellationToken);
        var recipeLock=await fixture.ReadAsync(inputs.RecipeLock,cancellationToken);
        if(mutation=="duplicate")await fixture.ReadAsync(inputs.Recipe,cancellationToken);
        return new(new(mutation=="recipe-path"?"nested.recipe.gz":inputs.Recipe.RelativePath,mutation=="recipe-bytes"?recipeLock:recipe,mutation=="recipe-media"?"application/json":inputs.Recipe.MediaType),
            new(mutation=="lock-path"?"recipe.lock.json":inputs.RecipeLock.RelativePath,mutation=="lock-bytes"?recipe:recipeLock,mutation=="lock-media"?"application/gzip":inputs.RecipeLock.MediaType),
            mutation=="release"?null:inputs.Release,mutation=="limit"?null:inputs.SourceLimit,Resume:mutation!="resume",AllowFileFixtures:mutation=="fixtures",
            Profile:mutation=="profile"?AcquisitionCompatibilityProfile.CanonicalRestore:inputs.AcquisitionProfile);
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourceWorkflowInputsMockTests
{
    [Theory][InlineData("normal")][InlineData("options")][InlineData("recipe-failure")][InlineData("lock-failure")][InlineData("cancelled")]
    public async Task CorrectPinnedReadContractPasses(string mode){using var f=new WorkflowInputsFixture(mode);await f.AssertContract(new ScriptedWorkflowInputsReader(f));}
    [Theory][InlineData("pin")][InlineData("token")][InlineData("duplicate")][InlineData("recipe-path")][InlineData("recipe-bytes")][InlineData("recipe-media")][InlineData("lock-path")][InlineData("lock-bytes")][InlineData("lock-media")][InlineData("resume")][InlineData("fixtures")]
    public async Task EveryFileReadAndArtifactFieldRejectsMutation(string mutation){using var f=new WorkflowInputsFixture("normal");Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedWorkflowInputsReader(f,mutation))));}
    [Theory][InlineData("release")][InlineData("limit")][InlineData("profile")]
    public async Task EveryPlanOptionRejectsMutation(string mutation){using var f=new WorkflowInputsFixture("options");Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedWorkflowInputsReader(f,mutation))));}
    [Fact]public async Task CancelledAdmissionReadsNothing(){using var f=new WorkflowInputsFixture("cancelled");Assert.NotNull(await Record.ExceptionAsync(()=>f.AssertContract(new ScriptedWorkflowInputsReader(f,"cancel"))));}
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesExtractWorkflowWireMockTests
{
    public static SourcesExtractWorkflowFile Fixture()=>new(new(new("recipes/full.recipe.gz",new('a',64),31,"application/gzip"),new("locks/exact.lock.json",new('b',64),71,"application/json"),"release-011",3,AcquisitionCompatibilityProfile.GenericRehydrate),
        new("inventory root","raw root","generation store"),new("sources/inventory.json",new('c',64),37,"application/json"),"native-generation","release-011",new("prior/index.json",new('d',64),19,"application/json"),new("java/manifest.json",new('e',64),23),2);
    [Fact]public void WireRoundTripPreservesSharedPinsRolesAndOptions()
    {var input=Fixture();var parsed=JsonSerializer.Deserialize<SourcesExtractWorkflowFile>(JsonSerializer.SerializeToUtf8Bytes(input));Assert.NotNull(parsed);
        Assert.Equal(input.Sources,parsed.Sources);Assert.Equal(input.Stores,parsed.Stores);Assert.Equal(input.InventoryManifest,parsed.InventoryManifest);
        Assert.Equal(input.GenerationRoot,parsed.GenerationRoot);Assert.Equal(input.ProfileId,parsed.ProfileId);Assert.Equal(input.TrustedPriorIndex,parsed.TrustedPriorIndex);Assert.Equal(input.Java,parsed.Java);Assert.Equal(input.CacheSourceLimit,parsed.CacheSourceLimit);}
    [Theory][InlineData("Producer",false)][InlineData("Projection",false)][InlineData("Producer",true)][InlineData("Projection",true)]
    public void EditableProducerOrProjectionIsRejected(string member,bool nested)
    {var wire=JsonSerializer.SerializeToNode(Fixture())!.AsObject();var target=nested?wire["Sources"]!.AsObject():wire;target[member]=new JsonObject();
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<SourcesExtractWorkflowFile>(wire.ToJsonString()));}
}
