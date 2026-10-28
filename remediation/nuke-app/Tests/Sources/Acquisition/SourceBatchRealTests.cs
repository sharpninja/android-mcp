using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourceBatchProbe:ISourceBatchProbe
{
    public async Task<SourceBatchObservation> RunAsync(SourceBatchCase test)
    {
        var inner=new MemoryAcquisitionStore();var bundle=new MemoryAcquisitionStore();var writes=new List<string>();
        foreach(var item in test.Existing)inner.Files[item.Key]=new(item.Key,item.Value,"application/octet-stream");
        foreach(var item in test.Bundle)bundle.Files[item.Key]=new(item.Key,item.Value,"application/octet-stream");
        if(test.Id.StartsWith("sealed-",StringComparison.Ordinal))
        {
            var prior=SourceBatchCases.ExpectedReceipt(test with {Complete=true,ProcessedRows=1,Sources=1,Reused=true});
            if(test.Id=="sealed-request-change")prior=prior with {RequestSha256=new string('0',64)};
            inner.Files[test.Request.ReceiptPath]=new(test.Request.ReceiptPath,SourceBatchCases.Canonical.Serialize(JsonSerializer.SerializeToElement(prior)),"application/json");
            if(test.Id!="sealed-missing-output")inner.Files["raw/a"]=new("raw/a",SourceBatchCases.Abc,"application/octet-stream");
        }
        var originals=inner.Files.ToDictionary(x=>x.Key,x=>x.Value.Bytes.ToArray());
        var originalBundle=bundle.Files.ToDictionary(x=>x.Key,x=>x.Value.Bytes.ToArray());
        var transport=new BoundaryTransport(test);
        var service=new SourceBatch(transport,new ObservedStore(inner,writes),bundle,new ScriptedLeaseProvider(),SourceBatchCases.Canonical);
        SourceBatchResult? result=null;string? error=null;
        try{result=await service.RunAsync(test.Request);}
        catch(TransportException problem){error=problem.Code;}
        var retained=originals.All(x=>inner.Files.TryGetValue(x.Key,out var value)&&value.Bytes.Span.SequenceEqual(x.Value));
        var bundleRetained=originalBundle.All(x=>bundle.Files.TryGetValue(x.Key,out var value)&&value.Bytes.Span.SequenceEqual(x.Value));
        var last=error is null ? writes.Count==0||writes[^1]==test.Request.ReceiptPath : !writes.Contains(test.Request.ReceiptPath);
        return new(result,error,transport.Calls,writes.Count,retained,bundleRetained,last,result?.Receipt.Sources.Select(x=>x.Source.Artifact.RelativePath).ToArray()??[]);
    }
    private sealed class ObservedStore(IArtifactStore inner,List<string> writes):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string relativePath,CancellationToken cancellationToken=default)=>inner.ReadAsync(relativePath,cancellationToken);
        public async Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default){var result=await inner.WriteAsync(artifact,cancellationToken);writes.Add(artifact.RelativePath);return result;}
    }
    private sealed class BoundaryTransport(SourceBatchCase test):IByteTransport
    {
        public int Calls{get;private set;}
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {
            Calls++;
            var pins=test.Request.Projection.PhysicalSources.Where(x=>test.Request.Projection.SelectedPhysicalSourceIndices.Contains(x.PhysicalSourceIndex)).Select(x=>x.Pin).ToArray();
            Assert.Contains(pins,p=>p.Url==request.Url&&p.Sha256==request.ExpectedSha256&&p.SizeBytes==request.ExpectedLength&&p.Revision==request.Revision);
            Assert.Equal(test.Request.Projection.Profile==AcquisitionCompatibilityProfile.CanonicalRestore?TransportRetryPolicy.CanonicalRestore:TransportRetryPolicy.SingleAttempt,request.Retry);
            Assert.Null(request.Range);Assert.Null(request.ToolTransfer);
            if(test.Id=="second-transfer-failure"&&Calls==2)throw new TransportException("SCRIPTED_HTTP","Original fixture transfer failure.");
            return Task.FromResult(new TransportResponse(request.Url,200,SourceBatchCases.Abc));
        }
    }
}
[Trait("Gate","Real")]
public sealed class SourceBatchRealTests
{
    [Theory,MemberData(nameof(SourceBatchCases.All),MemberType=typeof(SourceBatchCases))]
    public Task SameRowsPinsCountersRetentionAndReceiptAssertions(SourceBatchCase test)=>SourceBatchCases.AssertCase(test,new NativeSourceBatchProbe());
}
