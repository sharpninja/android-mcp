using System.Globalization;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSourceBatchPacingProbe:ISourceBatchPacingProbe
{
    public async Task<SourceBatchPacingObservation> RunAsync(SourceBatchPacingCase test)
    {
        var request=SourceBatchPacingCases.Request(test);
        var store=new MemoryAcquisitionStore();var bundle=new MemoryAcquisitionStore();
        if(test.Mode is "mixed" or "all-reused")store.Files.Add("raw/a",new("raw/a",SourceBatchCases.Abc,"application/octet-stream"));
        if(test.Mode=="all-reused")store.Files.Add("raw/b",new("raw/b",SourceBatchCases.Abc,"application/octet-stream"));
        using var cancellation=new CancellationTokenSource();
        var effects=new PacingEffects(test,cancellation);
        var service=new SourceBatch(new PacingTransport(request,test,effects),new PacingStore(store,request,effects),
            bundle,new ScriptedLeaseProvider(),SourceBatchCases.Canonical,test.DelaySeconds,effects);
        var originals=Snapshot(store);
        SourceBatchResult? result=null;string? error=null;
        try
        {
            if(test.Mode=="replay")
            {
                _=await service.RunAsync(request,cancellation.Token);
                originals=Snapshot(store);effects.Reset();
            }
            if(test.Mode=="cancel-before")cancellation.Cancel();
            result=await service.RunAsync(request,cancellation.Token);
        }
        catch(TransportException problem){error=problem.Code;}
        catch(OperationCanceledException){error="CANCELLED";}
        catch(PacingDelayFailure){error="DELAY_FAILURE";}
        return new(error,store.Files.ContainsKey(request.ReceiptPath),result?.Reused??false,effects.Events.ToArray(),
            originals.All(entry=>store.Files.TryGetValue(entry.Key,out var value)&&value.Bytes.Span.SequenceEqual(entry.Value)),
            effects.TokenMatches,result?.Receipt.RequestSha256);
    }

    private static Dictionary<string,byte[]> Snapshot(MemoryAcquisitionStore store)
        =>store.Files.ToDictionary(entry=>entry.Key,entry=>entry.Value.Bytes.ToArray(),StringComparer.Ordinal);
    private sealed class PacingDelayFailure:IOException { }
    private sealed class PacingEffects(SourceBatchPacingCase test,CancellationTokenSource source):ITransportDelay
    {
        public List<string> Events{get;}=[];
        public bool? TokenMatches{get;private set;}
        public Task DelayAsync(TimeSpan duration,CancellationToken token=default)
        {
            TokenMatches=(TokenMatches??true)&&token==source.Token;
            Events.Add("delay:"+duration.TotalSeconds.ToString("G",CultureInfo.InvariantCulture));
            if(test.Mode=="cancel-delay")source.Cancel();
            token.ThrowIfCancellationRequested();
            if(test.Mode=="delay-fault")throw new PacingDelayFailure();
            return Task.CompletedTask;
        }
        public void Reset(){Events.Clear();TokenMatches=null;}
    }
    private sealed class PacingStore(MemoryAcquisitionStore inner,SourceBatchRequest request,PacingEffects effects):IArtifactStore
    {
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)=>inner.ReadAsync(path,token);
        public async Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)
        {
            var stored=await inner.WriteAsync(artifact,token);
            effects.Events.Add(artifact.RelativePath==request.ReceiptPath?"seal":"write:"+artifact.RelativePath);
            return stored;
        }
    }
    private sealed class PacingTransport(SourceBatchRequest request,SourceBatchPacingCase test,PacingEffects effects):IByteTransport
    {
        public Task<TransportResponse> SendAsync(PinnedRequest transfer,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            var pin=Assert.Single(request.Projection.PhysicalSources,source=>source.Pin.Url==transfer.Url).Pin;
            Assert.Equal(pin.Sha256,transfer.ExpectedSha256);Assert.Equal(pin.SizeBytes,transfer.ExpectedLength);
            Assert.Equal(pin.Revision,transfer.Revision);
            effects.Events.Add("fetch:"+pin.LocalPath);
            if(test.Mode=="fail-second"&&pin.LocalPath=="raw/b")throw new TransportException("SCRIPTED_HTTP","Controlled transfer failure");
            return Task.FromResult(new TransportResponse(transfer.Url,200,SourceBatchCases.Abc));
        }
    }
}

[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class SourceBatchPacingRealTests
{
    [Theory,MemberData(nameof(SourceBatchPacingCases.All),MemberType=typeof(SourceBatchPacingCases))]
    public Task SameCasesAndOrderedEffectsBindTheExistingBatch(SourceBatchPacingCase test)
        =>SourceBatchPacingCases.AssertCase(test,new NativeSourceBatchPacingProbe());
}
