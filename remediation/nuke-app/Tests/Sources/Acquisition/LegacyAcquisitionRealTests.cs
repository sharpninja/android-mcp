using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeLegacyAcquisitionProbe:ILegacyAcquisitionProbe
{
    public async Task<LegacyAcquisitionObservation> RunAsync(LegacyAcquisitionCase test)
    {
        var store=new MemoryAcquisitionStore();var transport=new ForbiddenTransport();var files=new Dictionary<string,byte[]>();
        var inventory=0;long rangeBytes=0;string? errorType=null,errorCode=null;
        if(test.Id.Contains("snapshot",StringComparison.Ordinal)&&test.Id!="missing-snapshot-no-fallback")
            store.Files["reference.html"]=new("reference.html",test.Id=="changed-snapshot-retained"?Encoding.ASCII.GetBytes("new version"):LegacyAcquisitionCases.Snapshot,"text/html");
        var zip=LegacyAcquisitionCases.Zip(false);
        if(test.Id.Contains("source-zip",StringComparison.Ordinal))
            store.Files["sources.zip"]=new("sources.zip",test.Id=="tampered-source-zip-retained"?Encoding.ASCII.GetBytes("tampered"):zip,"application/zip");
        var originals=store.Files.ToDictionary(x=>x.Key,x=>x.Value.Bytes.ToArray());
        try
        {
            if(test.Id=="metadata-original-literals")
            {
                var source=new FixtureRangeSource(LegacyAcquisitionCases.Zip(true));
                var result=new MetadataArchiveReader().Read(source);files=result.Files.ToDictionary(x=>x.Key,x=>x.Value);
                inventory=result.Entries.Count;rangeBytes=source.Reads.Sum(x=>(long)x.Count);
            }
            else if(test.Id.Contains("source-zip",StringComparison.Ordinal))
            {
                var pin=new SourcePin("unused/member",new("https://example.test/sources.zip"),"fixture",ContentHash.Sha256(LegacyAcquisitionCases.Java),LegacyAcquisitionCases.Java.Length,
                    Archive:new(new("https://example.test/sources.zip"),"src/a/C.java",ContentHash.Sha256(zip),ContentHash.AidlSha1Legacy(zip),zip.Length,ArchiveLocalPath:"sources.zip"));
                files["src/a/C.java"]=(await new SourceArchiveAcquirer(transport,store).ReadMemberAsync(pin)).ToArray();
            }
            else
            {
                var pin=new SourcePin("restored/reference.html",new("https://example.test/mutable/reference.html"),"fixture",LegacyAcquisitionCases.SnapshotSha,LegacyAcquisitionCases.Snapshot.Length,
                    LegacyAcquisitionCases.SnapshotGit,RequiredLocalSnapshot:test.Id=="snapshot-escape"?"../elsewhere":"reference.html");
                var result=await new SourceAcquirer(transport,store).AcquireAsync(pin);files[result.Artifact.RelativePath]=(await store.ReadAsync(result.Artifact.RelativePath)).ToArray();
            }
        }
        catch(Exception problem) when(problem is TransportException or FileNotFoundException or ArgumentException)
        {errorType=problem.GetType().Name;errorCode=(problem as TransportException)?.Code;}
        return new(errorType,errorCode,files,transport.Calls,store.Writes,
            originals.All(x=>store.Files.TryGetValue(x.Key,out var value)&&value.Bytes.Span.SequenceEqual(x.Value)),rangeBytes,inventory);
    }
    private sealed class ForbiddenTransport:IByteTransport
    {
        public int Calls{get;private set;}
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {Calls++;throw new InvalidOperationException("Network is forbidden by the original offline fixture.");}
    }
}
[Trait("Gate","Real")]
public sealed class LegacyAcquisitionRealTests
{
    [Theory,MemberData(nameof(LegacyAcquisitionCases.All),MemberType=typeof(LegacyAcquisitionCases))]
    public Task SameOriginalFixturesAndAssertions(LegacyAcquisitionCase test)=>LegacyAcquisitionCases.AssertCase(test,new NativeLegacyAcquisitionProbe());
}
