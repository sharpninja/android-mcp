using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record BootstrapCompletionObservation(string? Error,bool Committed,bool ReceiptRetained,string Printed,
    bool DownstreamReached,bool SameReceipt,IReadOnlyList<string> Inputs,bool PropertyPinned,int LockReads,IReadOnlyList<string> Trace);
public static class BootstrapCompletionCases
{
    public static readonly string[] Names=["GOOD","RENDER-FAIL","INPUT-MISSING","PROPERTY-CHANGED","SOURCE-CHANGED","OUTPUT-EXISTS","RECEIPT-CHANGED"];
    public static IEnumerable<object[]> Cases=>Names.Select(id=>new object[]{id});
    public static BootstrapCompletionObservation Correct(string id)
    {
        var committed=id is "GOOD" or "RENDER-FAIL" or "RECEIPT-CHANGED";var reached=id is "GOOD" or "RENDER-FAIL";
        var error=id switch{"GOOD"=>null,"RENDER-FAIL"=>"render","INPUT-MISSING"=>"INPUT_MISSING","PROPERTY-CHANGED"=>"INPUT_PIN_MISMATCH","SOURCE-CHANGED"=>"INPUT_CHANGED","OUTPUT-EXISTS"=>"OUTPUT_EXISTS",_=>"ARTIFACT_PIN_HASH"};
        var trace=new List<string>{"complete"};if(committed)trace.Add("commit");if(reached){trace.Add("print");trace.Add("downstream");}
        return new(error,committed,id is "GOOD" or "RENDER-FAIL" or "SOURCE-CHANGED" or "RECEIPT-CHANGED",reached?BootstrapReportCases.Summary:"",reached,id=="GOOD",
            ["toolchain-lock.json",BootstrapCliCases.PropertiesPath],true,id=="SOURCE-CHANGED"||committed?2:1,trace);
    }
    public static async Task AssertContract(string id,Func<string,Task<BootstrapCompletionObservation>> probe)
    {
        var expected=Correct(id);var actual=await probe(id);
        Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.Committed,actual.Committed);Assert.Equal(expected.ReceiptRetained,actual.ReceiptRetained);
        Assert.Equal(expected.Printed,actual.Printed);Assert.Equal(expected.DownstreamReached,actual.DownstreamReached);Assert.Equal(expected.SameReceipt,actual.SameReceipt);
        Assert.Equal(expected.Inputs,actual.Inputs);Assert.True(actual.PropertyPinned);Assert.Equal(expected.LockReads,actual.LockReads);Assert.Equal(expected.Trace,actual.Trace);
    }
    public static BootstrapCompletionObservation Wrong(string id,string mutation)
    {
        var x=Correct(id);return mutation switch{"error"=>x with{Error="wrong"},"commit"=>x with{Committed=!x.Committed},"retained"=>x with{ReceiptRetained=!x.ReceiptRetained},
            "printed"=>x with{Printed="wrong"},"downstream"=>x with{DownstreamReached=!x.DownstreamReached},"same"=>x with{SameReceipt=!x.SameReceipt},
            "inputs"=>x with{Inputs=[]},"pin"=>x with{PropertyPinned=false},"reads"=>x with{LockReads=99},_=>x with{Trace=x.Trace.Reverse().Append("wrong").ToArray()}};
    }
    public static IEnumerable<object[]> Faults=>Names.SelectMany(id=>new[]{"error","commit","retained","printed","downstream","same","inputs","pin","reads","trace"}.Select(m=>new object[]{id,m}));
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapCompletionMockTests
{
    [Theory,MemberData(nameof(BootstrapCompletionCases.Cases),MemberType=typeof(BootstrapCompletionCases))]
    public Task OriginalReceiptCommitBeforePrintAndDownstreamMocksPass(string id)=>BootstrapCompletionCases.AssertContract(id,x=>Task.FromResult(BootstrapCompletionCases.Correct(x)));
    [Theory,MemberData(nameof(BootstrapCompletionCases.Faults),MemberType=typeof(BootstrapCompletionCases))]
    public async Task EveryEarlyCommitPinReadPrintAndRetentionFaultIsRejected(string id,string mutation)=>Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapCompletionCases.AssertContract(id,x=>Task.FromResult(BootstrapCompletionCases.Wrong(x,mutation)))));
}
