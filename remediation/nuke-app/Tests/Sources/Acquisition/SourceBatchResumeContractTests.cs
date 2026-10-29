using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SourceBatchResumeCase(string Id,string? Error,int Reads,bool AdmittedThenRaced=false);
public sealed record SourceBatchResumeObservation(SourceBatchResult? Result,Exception? Error,IReadOnlyList<string> Reads,
    int Http,int Writes,bool OriginalsRetained,bool TokenForwarded,bool AdmissionVerified);
public interface ISourceBatchResumeProbe
{
    Task<SourceBatchResumeObservation> RunAsync(SourceBatchResumeFixture fixture,CancellationToken cancellationToken);
}
public sealed class SourceBatchResumeFixture
{
    public required SourceBatchResumeCase Case{get;init;}
    public required SourceBatchResumeRequest Request{get;init;}
    public required SourceBatchResult Expected{get;init;}
    public required IReadOnlyDictionary<string,byte[]> Initial{get;init;}
    public IReadOnlyList<string> ExpectedReads=>Enumerable.Range(0,Case.Reads).Select(index=>index==0?Request.Acquisition.ReceiptPath:"raw/a").ToArray();
    public static SourceBatchResumeFixture Create(SourceBatchResumeCase test)
    {
        var request=SourceBatchCases.Request();
        var original=new Dictionary<string,byte[]>{{"raw/a",SourceBatchCases.Abc}};
        var priorCase=new SourceBatchCase("sealed-resume",request,original,new Dictionary<string,byte[]>(),null,0,0,true,1,1,0,true);
        var receipt=SourceBatchCases.ExpectedReceipt(priorCase);
        var bytes=SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(receipt));
        var pin=new StoredArtifact(request.ReceiptPath,ContentHash.Sha256(bytes),bytes.Length,"application/json");
        original[request.ReceiptPath]=bytes;
        var required=pin;
        if(test.Id=="missing")original.Remove(request.ReceiptPath);
        if(test.Id=="wrong-hash")required=pin with{Sha256=new string('0',64)};
        if(test.Id=="wrong-length")required=pin with{Length=pin.Length+1};
        if(test.Id=="wrong-path")required=pin with{RelativePath="other.json"};
        if(test.Id=="missing-pin")required=null!;
        if(test.Id=="missing-output")original.Remove("raw/a");
        if(test.Id=="tampered-output")original["raw/a"]=[0];
        if(test.Id=="changed-request")request=request with{GenerationId="different"};
        return new(){Case=test,Request=new(request,required),Expected=new(receipt,pin,true),Initial=original};
    }
    public byte[] RacedReplacement()
    {
        var source=Expected.Receipt.Sources[0];
        return SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(Expected.Receipt with{
            Sources=[source with{Source=source.Source with{Status="downloaded"}}]}));
    }
}
public static class SourceBatchResumeCases
{
    public static IEnumerable<object[]> All=>new SourceBatchResumeCase[]{
        new SourceBatchResumeCase("valid",null,3),new("missing","BATCH_REQUIRED_RECEIPT",1),
        new("race-missing","BATCH_REQUIRED_RECEIPT",1,true),new("race-replaced","BATCH_RECEIPT_PIN",1,true),
        new("wrong-hash","BATCH_RECEIPT_PIN",1),new("wrong-length","BATCH_RECEIPT_PIN",1),
        new("wrong-path","BATCH_RECEIPT_PIN",0),new("missing-pin","BATCH_RECEIPT_PIN",0),
        new("missing-output","BATCH_SEALED",2),new("tampered-output","BATCH_SEALED",2),
        new("changed-request","BATCH_IDENTITY",1),new("cancelled","OperationCanceledException",0)
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(SourceBatchResumeCase test,ISourceBatchResumeProbe probe)
    {
        var fixture=SourceBatchResumeFixture.Create(test);using var cancel=new CancellationTokenSource();if(test.Id=="cancelled")cancel.Cancel();
        var result=await probe.RunAsync(fixture,cancel.Token);
        Assert.Equal(fixture.ExpectedReads,result.Reads);Assert.Equal(0,result.Http);Assert.Equal(0,result.Writes);
        Assert.True(result.OriginalsRetained);Assert.True(result.TokenForwarded);Assert.True(result.AdmissionVerified);
        if(test.Error is not null)
        {
            Assert.Null(result.Result);
            if(test.Error=="OperationCanceledException")Assert.IsAssignableFrom<OperationCanceledException>(result.Error);
            else Assert.Equal(test.Error,Assert.IsType<TransportException>(result.Error).Code);
            return;
        }
        Assert.Null(result.Error);var actual=Assert.IsType<SourceBatchResult>(result.Result);Assert.True(actual.Reused);
        Assert.Equal(fixture.Expected.Manifest,actual.Manifest);
        Assert.Equal(SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(fixture.Expected.Receipt)),
            SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(actual.Receipt)));
    }
}
public sealed class ScriptedSourceBatchResumeProbe(string? mutation=null):ISourceBatchResumeProbe
{
    public Task<SourceBatchResumeObservation> RunAsync(SourceBatchResumeFixture fixture,CancellationToken cancellationToken)
    {
        Exception? error=fixture.Case.Error switch{null=>null,"OperationCanceledException"=>new OperationCanceledException(cancellationToken),_=>new TransportException(mutation=="error-code"?"wrong":fixture.Case.Error,"Expected resume-only rejection")};
        var result=error is null?fixture.Expected:null;
        if(mutation=="error-omitted")error=null;if(mutation=="error-type")error=new IOException();
        if(mutation=="result-added")result=fixture.Expected;
        if(mutation=="result-omitted")result=null;
        if(result is not null)result=mutation switch{
            "reuse"=>result with{Reused=false},"pin"=>result with{Manifest=result.Manifest with{Sha256="wrong"}},
            "receipt"=>result with{Receipt=result.Receipt with{GenerationId="wrong"}},_=>result};
        return Task.FromResult(new SourceBatchResumeObservation(result,error,mutation=="reads"?["wrong"]:fixture.ExpectedReads,
            mutation=="http"?1:0,mutation=="writes"?1:0,mutation!="originals",mutation!="token",mutation!="admission"));
    }
}
[Trait("Gate","Mock")]
public sealed class SourceBatchResumeMockTests
{
    [Theory,MemberData(nameof(SourceBatchResumeCases.All),MemberType=typeof(SourceBatchResumeCases))]
    public Task CorrectExplicitReadMode(SourceBatchResumeCase test)=>SourceBatchResumeCases.AssertCase(test,new ScriptedSourceBatchResumeProbe());
    [Theory,MemberData(nameof(SourceBatchResumeCases.All),MemberType=typeof(SourceBatchResumeCases))]
    public async Task MissingRaceEffectsAndResultMutationsRejected(SourceBatchResumeCase test)
    {
        var mutations=new List<string>{"reads","http","writes","originals","token","admission"};
        if(test.Error is null)mutations.AddRange(["result-omitted","error-type","reuse","pin","receipt"]);
        else{mutations.AddRange(["error-omitted","error-type","result-added"]);if(test.Error!="OperationCanceledException")mutations.Add("error-code");}
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>SourceBatchResumeCases.AssertCase(test,new ScriptedSourceBatchResumeProbe(mutation))));
    }
}
