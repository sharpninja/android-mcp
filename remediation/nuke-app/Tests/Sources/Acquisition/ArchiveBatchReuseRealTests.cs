using System.IO.Compression;
using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeArchiveBatchReuseProbe:IArchiveBatchReuseProbe
{
    public async Task<ArchiveBatchReuseObservation> RunAsync(ArchiveBatchReuseCase test)
    {
        var zip=Archive(test);var inner=new MemoryAcquisitionStore();
        if(test.Full)inner.Files["archives/source.zip"]=new("archives/source.zip",test.Change=="tampered"?new byte[]{0}:zip,"application/zip");
        var originals=inner.Files.ToDictionary(x=>x.Key,x=>x.Value.Bytes.ToArray());
        var transport=new FixtureTransport(zip);var store=new ObservedStore(inner,transport);
        SourcePin Pin(int index)
        {
            var bytes=index==0?ArchiveBatchReuseCases.First(test):ArchiveBatchReuseCases.Second(test);
            var member=index==0?(test.Full?"src/a/C.java":"data/api-versions.xml"):(test.Full?"src/b/D.java":"source.properties");
            var xml=Encoding.UTF8.GetBytes($"<repository><remotePackage path=\"platforms;android-37.2\"><archives><archive><complete><size>{zip.Length}</size></complete></archive></archives></remotePackage></repository>");
            var archive=new SourceArchivePin(new("https://example.test/archive.zip"),member,
                test.Full?ContentHash.Sha256(zip):null,test.Full?ContentHash.AidlSha1Legacy(zip):null,zip.Length,
                "\"version-one\"",test.Full?"archives/source.zip":null,RepositoryXml:xml,PlatformVersion:"37.2");
            var pin=new SourcePin(index==0?"raw/first":"raw/second",new("https://example.test/archive.zip"),"revision-one",
                ContentHash.Sha256(bytes),bytes.Length,ContentHash.GitBlobSha1Legacy(bytes),Archive:archive);
            if(index==1)pin=test.Change switch{
                "etag"=>pin with{Archive=archive with{ETag="\"version-two\""}},
                "revision"=>pin with{Revision="revision-two"},
                "profile"=>pin with{Compatibility=AcquisitionCompatibilityProfile.GenericRehydrate,Archive=archive with{Compatibility=AcquisitionCompatibilityProfile.GenericRehydrate}},
                "missing-etag"=>pin with{Archive=archive with{ETag=null}},
                "missing-platform"=>pin with{Archive=archive with{PlatformVersion=null}},
                "repository"=>pin with{Archive=archive with{RepositoryXml=Encoding.UTF8.GetBytes("<repository/>")}},
                "archive-hash"=>pin with{Archive=archive with{ArchiveSha256=new string('0',64)}},
                "source-hash"=>pin with{Sha256=new string('0',64)},
                "source-size"=>pin with{SizeBytes=bytes.Length+1},_=>pin};
            return pin;
        }
        var request=SourceBatchCases.Request(
            [SourceBatchCases.Physical(0,Pin(0)),SourceBatchCases.Physical(1,Pin(1))],
            [SourceBatchCases.Row(0,0),SourceBatchCases.Row(1,1)],[0,1]);
        using var cancellation=new CancellationTokenSource();if(test.Change=="cancel")cancellation.Cancel();
        SourceBatchResult? result=null;string? error=null;
        try{result=await new SourceBatch(transport,store,new MemoryAcquisitionStore(),new ScriptedLeaseProvider(),new CanonicalJson()).RunAsync(request,cancellation.Token);}
        catch(TransportException problem){error=problem.Code;}
        catch(OperationCanceledException){error="CANCELLED";}
        var bytesOut=new[]{"raw/first","raw/second"}.Where(inner.Files.ContainsKey).Select(x=>inner.Files[x].Bytes.ToArray()).ToArray();
        return new(error,store.FirstReads,transport.Calls,store.ArchiveReads,store.SourceWrites,
            result is not null&&inner.Files.ContainsKey(request.ReceiptPath),
            originals.All(x=>inner.Files[x.Key].Bytes.Span.SequenceEqual(x.Value)),bytesOut,transport.Pins);
    }
    private static byte[] Archive(ArchiveBatchReuseCase test)
    {
        using var output=new MemoryStream();
        using(var archive=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            void Add(string path,byte[] bytes){var entry=archive.CreateEntry(path,CompressionLevel.NoCompression);entry.LastWriteTime=new DateTimeOffset(2026,1,1,0,0,0,TimeSpan.Zero);using var stream=entry.Open();stream.Write(bytes);}
            Add(test.Full?"src/a/C.java":"data/api-versions.xml",ArchiveBatchReuseCases.First(test));
            Add(test.Full?"src/b/D.java":"source.properties",ArchiveBatchReuseCases.Second(test));
            if(!test.Full)Add("android.jar",Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("UNSELECTED_BINARY",10000))));
        }
        return output.ToArray();
    }
    private sealed class ObservedStore(MemoryAcquisitionStore inner,FixtureTransport transport):IArtifactStore
    {
        public int FirstReads{get;private set;}public int ArchiveReads{get;private set;}public int SourceWrites{get;private set;}
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
        {if(path=="archives/source.zip")ArchiveReads++;return inner.ReadAsync(path,cancellationToken);}
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
        {if(artifact.RelativePath=="raw/first")FirstReads=transport.Calls;if(artifact.RelativePath.StartsWith("raw/",StringComparison.Ordinal))SourceWrites++;return inner.WriteAsync(artifact,cancellationToken);}
    }
    private sealed class FixtureTransport(byte[] archive):IByteTransport
    {
        public int Calls{get;private set;}public bool Pins{get;private set;}=true;
        public Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
        {
            Calls++;var range=request.Range??throw new InvalidOperationException("Full archive already supplied; network download forbidden.");
            Pins&=request.Url==new Uri("https://example.test/archive.zip")&&request.ExpectedTotalLength==archive.Length&&
                request.IfMatch is "\"version-one\"" or "\"version-two\""&&request.ExpectedETag==request.IfMatch&&
                request.ContentEncoding=="identity"&&request.Retry==TransportRetryPolicy.CanonicalRestore&&
                request.Revision is "revision-one" or "revision-two"&&request.PartialMemberSha256 is {Length:64}&&request.MaxBytes==8*1024*1024;
            return Task.FromResult(new TransportResponse(request.Url,206,archive.AsMemory((int)range.Offset,range.Length),request.ExpectedETag,
                $"bytes {range.Offset}-{range.Offset+range.Length-1}/{archive.Length}"));
        }
    }
}
[Trait("Gate","Real")]
public sealed class ArchiveBatchReuseRealTests
{
    [Theory,MemberData(nameof(ArchiveBatchReuseCases.All),MemberType=typeof(ArchiveBatchReuseCases))]
    public Task SameOriginalInteractionContract(ArchiveBatchReuseCase test)=>ArchiveBatchReuseCases.AssertCase(test,new NativeArchiveBatchReuseProbe());
}
