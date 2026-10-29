using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

// SAME 12 assertion cases, real shared store/reader/lease and actual Linux
// kernel composition. Fault/identity controls decorate those same authorities;
// fixtures and failure bytes are retained, with no Move/delete or Python.
public sealed class NativeReleaseCodecProbe:IReleaseCodecProbe
{
    public async Task<ReleaseCodecObservation> RunAsync(ReleaseCodecCase test)
    {
        Assert.True(OperatingSystem.IsLinux(),"This native fixture uses the proven Linux kernel, not Windows.");
        var root=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"android-mcp-codec-"+Guid.NewGuid().ToString("N"))).FullName;
        var payload=test.Mode=="empty"?Array.Empty<byte>():ReleaseCodecCases.Payload;
        var sourcePath=Path.Combine(root,"sealed.bin");await File.WriteAllBytesAsync(sourcePath,payload);
        var before=await File.ReadAllBytesAsync(sourcePath);var calls=new NativeArtifactCalls(new NativeArtifactKernel());
        var opener=new ArtifactReadOpener(calls,new NativeArtifactIdentityReader(calls));
        var storage=new FileArtifactStore(root,value=>ContentHash.Sha256(value.Span),null,opener);
        var reader=new ObservedReader(storage){ChangeSourceIdentity=test.Mode=="identity-change"};
        var writer=new ObservedWriter(storage);var strict=new StrictJsonInspector();
        var codec=new ReleasePartsCodec(reader,writer,new PinnedArtifactReader(storage),strict,new CanonicalJson(),
            new FileArtifactLeaseProvider(Path.Combine(root,"leases")));
        var errors=new List<Exception>();ReleaseSplitResult? split=null;ReleaseReassembleResult? assembled=null;
        var destination="new.bin";var fullDestination=Path.Combine(root,destination);
        byte[]? existingBefore=null;var retryRejected=false;
        using var cancellation=new CancellationTokenSource();if(test.Mode=="cancel-before")cancellation.Cancel();
        try{
            split=await codec.SplitAsync(new("sealed.bin","parts",test.Mode=="wrong-whole"?new string('0',64):ContentHash.Sha256(payload),
                test.Mode=="two-gib"?2_147_483_648L:377),cancellation.Token);
        }catch(Exception error){errors.Add(error);}
        if(split is not null){
            var manifestPath=split.ManifestArtifact.RelativePath;var manifestSha=split.ManifestArtifact.Sha256;
            if(test.Mode=="existing"){
                await storage.WriteNewAsync(new(destination,ReleaseCodecCases.Keep,"application/octet-stream"));
                existingBefore=await File.ReadAllBytesAsync(fullDestination);
            }
            if(test.Mode is "tampered-part" or "symlink-part"){
                Directory.CreateDirectory(Path.Combine(root,"altered"));
                for(var index=0;index<split.Parts.Count;index++){
                    var row=split.Manifest.Parts[index];var original=Path.Combine(root,split.Parts[index].RelativePath);
                    var changed="altered/"+row.Name;
                    if(test.Mode=="symlink-part"&&index==0)File.CreateSymbolicLink(Path.Combine(root,changed),original);
                    else{
                        var bytes=await File.ReadAllBytesAsync(original);
                        if(test.Mode=="tampered-part"&&index==1)Array.Fill(bytes,(byte)'X');
                        await storage.WriteNewAsync(new(changed,bytes,"application/octet-stream"));
                    }
                }
                var raw=await storage.ReadAsync(manifestPath);
                var alternate=await storage.WriteNewAsync(new("altered/manifest.json",raw,"application/json"));
                manifestPath=alternate.RelativePath;manifestSha=alternate.Sha256;
            }
            if(test.Mode=="tampered-manifest-path"){
                var originalBytes=await storage.ReadAsync(manifestPath);
                var node=JsonNode.Parse(originalBytes.Span)!.AsObject();
                node["parts"]![0]!["name"]="../outside";
                var raw=JsonSerializer.SerializeToUtf8Bytes(node);
                var alternate=await storage.WriteNewAsync(new("altered/manifest.json",raw,"application/json"));
                try{await codec.ReassembleAsync(new(alternate.RelativePath,"assembly",destination,manifestSha));}
                catch(Exception error){errors.Add(error);}
                manifestPath=alternate.RelativePath;manifestSha=alternate.Sha256;
            }
            writer.Fault=test.Mode;
            try{assembled=await codec.ReassembleAsync(new(manifestPath,"assembly",destination,manifestSha));}
            catch(Exception error){errors.Add(error);}
            if(test.Mode is "final-copy-failure" or "marker-failure"){
                writer.Fault=null;
                try{await codec.ReassembleAsync(new(manifestPath,"assembly",destination,manifestSha));}
                catch(IOException){retryRejected=true;}
            }
        }
        var output=File.Exists(fullDestination)?await File.ReadAllBytesAsync(fullDestination):null;
        var manifestFile=Path.Combine(root,"parts/manifest.json");
        var retainedFile=Path.Combine(root,"assembly/retained-whole.unverified");
        var marker=Path.Combine(root,"assembly/success.json");
        return new ReleaseCodecObservation(before,await File.ReadAllBytesAsync(sourcePath),output,assembled?.Artifact.Length,
            split?.Manifest.Parts??Array.Empty<ReleasePart>(),errors,File.Exists(manifestFile),File.Exists(fullDestination),
            Directory.Exists(Path.Combine(root,"parts")),reader.SourcePayloadReadBytes,existingBefore,
            existingBefore is null?null:await File.ReadAllBytesAsync(fullDestination),
            File.Exists(manifestFile)?await File.ReadAllBytesAsync(manifestFile):null,File.Exists(marker),writer.Order.ToArray(),
            File.Exists(retainedFile)?await File.ReadAllBytesAsync(retainedFile):null,retryRejected,
            test.Mode=="final-copy-failure"?output:null);
    }

    private sealed class ObservedWriter(ICreateNewStreamingArtifactStore inner):ICreateNewStreamingArtifactStore
    {
        public string? Fault{get;set;}
        public List<string> Order{get;}=[];
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default)=>inner.ReadAsync(path,token);
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default)=>inner.WriteAsync(artifact,token);
        public Task<StoredArtifact> WriteStreamAsync(string path,string type,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)=>
            inner.WriteStreamAsync(path,type,producer,token);
        public Task<StoredArtifact> WriteNewAsync(Artifact artifact,CancellationToken token=default)
        {
            if(artifact.RelativePath=="assembly/success.json"){
                if(Fault=="marker-failure")throw new IOException("marker-fixture");
                Order.Add("success-marker");
            }else Order.Add("manifest");
            return inner.WriteNewAsync(artifact,token);
        }
        public Task<StoredArtifact> WriteNewStreamAsync(string path,string type,Func<Stream,CancellationToken,Task> producer,CancellationToken token=default)
        {
            Order.Add(path=="new.bin"?"final":path.EndsWith("retained-whole.unverified",StringComparison.Ordinal)?"retained-whole":"part");
            if(path=="new.bin"&&Fault=="final-copy-failure")
                return inner.WriteNewStreamAsync(path,type,async(output,cancel)=>{
                    await output.WriteAsync(ReleaseCodecCases.Payload.AsMemory(0,123),cancel);throw new IOException("copy-fixture");
                },token);
            return inner.WriteNewStreamAsync(path,type,producer,token);
        }
    }
    private sealed class ObservedReader(IStreamingArtifactReader inner):IStreamingArtifactReader
    {
        public bool ChangeSourceIdentity{get;set;}
        public long SourcePayloadReadBytes{get;private set;}
        public Task<ArtifactReadSnapshot?> InspectAsync(string path,CancellationToken token=default)=>inner.InspectAsync(path,token);
        public async Task<IArtifactReadSession> OpenReadAsync(string path,CancellationToken token=default)
        {
            var session=await inner.OpenReadAsync(path,token);
            return path=="sealed.bin"?new Session(session,this):session;
        }
        private sealed class Session:IArtifactReadSession
        {
            private readonly IArtifactReadSession inner;
            private readonly ObservedReader owner;
            public Session(IArtifactReadSession inner,ObservedReader owner){this.inner=inner;this.owner=owner;Content=new CountedStream(inner.Content,bytes=>owner.SourcePayloadReadBytes+=bytes);}
            public ArtifactReadSnapshot Opened=>inner.Opened;
            public Stream Content{get;}
            public async Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken token=default)
            {
                var actual=await inner.ObserveCurrentPathAsync(token);
                return owner.ChangeSourceIdentity?actual with{Native=actual.Native with{FileIdLow=actual.Native.FileIdLow^1UL}}:actual;
            }
            public ValueTask DisposeAsync()=>inner.DisposeAsync();
        }
        private sealed class CountedStream(Stream inner,Action<int> count):Stream
        {
            public override bool CanRead=>inner.CanRead;public override bool CanSeek=>inner.CanSeek;public override bool CanWrite=>false;
            public override long Length=>inner.Length;public override long Position{get=>inner.Position;set=>inner.Position=value;}
            public override int Read(byte[] buffer,int offset,int length){var read=inner.Read(buffer,offset,length);count(read);return read;}
            public override int Read(Span<byte> buffer){var read=inner.Read(buffer);count(read);return read;}
            public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default){var read=await inner.ReadAsync(buffer,token);count(read);return read;}
            public override async Task<int> ReadAsync(byte[] buffer,int offset,int length,CancellationToken token){var read=await inner.ReadAsync(buffer,offset,length,token);count(read);return read;}
            public override long Seek(long offset,SeekOrigin origin)=>inner.Seek(offset,origin);
            public override void Flush()=>throw new NotSupportedException();
            public override void SetLength(long value)=>throw new NotSupportedException();
            public override void Write(byte[] buffer,int offset,int length)=>throw new NotSupportedException();
            protected override void Dispose(bool disposing) { }
        }
    }
}

[Trait("Gate","Real"),Trait("Platform","Linux"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseCodecRealTests
{
    [Theory,MemberData(nameof(ReleaseCodecCases.Cases),MemberType=typeof(ReleaseCodecCases))]
    public Task SameOriginalAndCorrectedRetentionCasesUseProductionCodec(ReleaseCodecCase test)=>
        ReleaseCodecCases.AssertCase(test,new NativeReleaseCodecProbe());
}
