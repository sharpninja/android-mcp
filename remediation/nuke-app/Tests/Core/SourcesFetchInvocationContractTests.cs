using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Execution;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class SourcesFetchInvocationCase
{
    public string Id{get;}
    public SourceBatchResult Input{get;}
    public SourcesFetchInvocationCounts? Expected{get;}
    public string? Error{get;}
    public SourcesFetchInvocationCase(string id)
    {
        Id=id;
        var selected=id=="empty"?Array.Empty<int>():id is "partial" or "sealed-partial"?new[]{0}:new[]{0,1};
        var pins=new[]{SourceBatchCases.Physical(0,SourceBatchCases.Pin("raw/a"),[0,1]),SourceBatchCases.Physical(1,SourceBatchCases.Pin("raw/b"),[2])};
        // Three logical rows, two physical paths: aliases must never inflate invocation counts.
        var rows=new[]{SourceBatchCases.Row(0,0),SourceBatchCases.Row(1,0),SourceBatchCases.Row(2,1)};
        var request=SourceBatchCases.Request(pins,rows,selected);
        var sources=selected.Select(index=>new SourceBatchSource(index,new(new("raw/"+(index==0?"a":"b"),SourceBatchCases.AbcSha,3,"application/octet-stream"),
            id=="all-existing"||id=="mixed"&&index==1?"reused":id=="unknown-status"&&index==1?"unrecognized-outcome":"downloaded",new Uri("https://example.invalid/pinned"),"revision",null))).ToArray();
        var selectedRows=rows.Count(row=>selected.Contains(row.PhysicalSourceIndex));
        var receipt=new SourceBatchReceipt(request.GenerationId,"request-identity",request.Projection,SourceBatchCachePolicy.PreserveAliases,
            3,selectedRows,selectedRows,2,selected.Length,selected.Length==2,false,sources,[],sources.Select(item=>item.Source.Artifact).ToArray());
        Input=new(receipt,new("sources/acquisition.json",new('a',64),137,"application/json"),id is "sealed-full" or "sealed-partial");
        Expected=id switch{
            "all-downloaded"=>new(2,0,2,2,true),"mixed"=>new(1,1,2,2,true),"all-existing" or "sealed-full"=>new(0,2,2,2,true),
            "partial"=>new(1,0,1,2,false),"sealed-partial"=>new(0,1,1,2,false),"empty"=>new(0,0,0,2,false),_=>null};
        if(id=="unknown-status")Error="Unknown source acquisition outcome";
    }
    public void AssertContract(ISourcesFetchInvocationProjection provider)
    {
        var before=SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(Input));
        SourcesFetchInvocationCounts? result=null;Exception? error=null;
        try{result=provider.Project(Input);}catch(Exception failure){error=failure;}
        if(Error is not null){var actual=Assert.IsType<InvalidOperationException>(error);Assert.Equal(Error,actual.Message);}
        else{Assert.Null(error);Assert.Equal(Expected,result);}
        Assert.Equal(before,SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(Input)));
    }
}
// Independent prescribed responses. The native projection binds these same cases/assertions after this mock gate.
public sealed class ScriptedSourcesFetchInvocationProjection(SourcesFetchInvocationCase fixture,string mutation=""):ISourcesFetchInvocationProjection
{
    public SourcesFetchInvocationCounts Project(SourceBatchResult acquisition)
    {
        Assert.Same(fixture.Input,acquisition);
        if(fixture.Error is not null&&mutation!="ignore-status")throw new InvalidOperationException(fixture.Error);
        var value=fixture.Expected??new(0,0,2,2,true);
        if(mutation=="receipt"){
            var sources=(SourceBatchSource[])acquisition.Receipt.Sources;
            sources[0]=sources[0] with{Source=sources[0].Source with{Status="changed"}};
        }
        return mutation switch{
            "downloaded"=>value with{Downloaded=value.Downloaded+1},"reused"=>value with{Reused=value.Reused+1},
            "selected"=>value with{SelectedPaths=acquisition.Receipt.SelectedRows},"total"=>value with{TotalPaths=acquisition.Receipt.TotalRows},
            "complete"=>value with{Complete=!value.Complete},
            "replay-original-status"=>value with{Downloaded=1,Reused=0},_=>value};
    }
}
[Trait("Gate","Mock")][Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchInvocationMockTests
{
    public static IEnumerable<object[]> Cases()=>new[]{"all-downloaded","mixed","all-existing","sealed-full","partial","sealed-partial","empty","unknown-status"}.Select(id=>new object[]{id});
    [Theory][MemberData(nameof(Cases))]public void CorrectPhysicalInvocationContractPasses(string id)
    {var test=new SourcesFetchInvocationCase(id);test.AssertContract(new ScriptedSourcesFetchInvocationProjection(test));}
    [Theory][MemberData(nameof(Cases))]public void EveryCounterScopeAndReplayClaimRejectsMutation(string id)
    {
        var mutations=id=="unknown-status"?new[]{"ignore-status"}:id=="empty"?new[]{"downloaded","reused","total","complete"}:new[]{"downloaded","reused","selected","total","complete"};
        if(id.StartsWith("sealed-",StringComparison.Ordinal))mutations=mutations.Append("replay-original-status").ToArray();
        foreach(var mutation in mutations){var test=new SourcesFetchInvocationCase(id);Assert.NotNull(Record.Exception(()=>test.AssertContract(new ScriptedSourcesFetchInvocationProjection(test,mutation))));}
    }
    [Fact]public void ProjectionCannotRewriteReceiptHistory()
    {var test=new SourcesFetchInvocationCase("mixed");Assert.NotNull(Record.Exception(()=>test.AssertContract(new ScriptedSourcesFetchInvocationProjection(test,"receipt"))));}
}
