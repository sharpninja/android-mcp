using System.Globalization;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

/// <summary>Offline bounded split/reassembly. All IO, hashing, immutable writes,
/// native admission and leases remain supplied by the shared authorities.</summary>
public sealed class ReleasePartsCodec(IStreamingArtifactReader reader,ICreateNewStreamingArtifactStore store,
    IPinnedArtifactReader controlPins,IStrictJsonInspector json,ICanonicalJson canonical,
    IArtifactLeaseProvider leases):IReleasePartsCodec
{
    public const long MaximumPartBytes=2_147_483_647;
    public const int MaximumParts=1000;
    private const string Format="android-mcp-release-parts-v1";
    private const int BufferBytes=65536;

    public async Task<ReleaseSplitResult> SplitAsync(ReleaseSplitRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(request);
        var sourceSegments=ArtifactPaths.Segments(request.SourcePath);ArtifactPaths.Segments(request.GenerationPath);
        if(!Pin(request.ExpectedSha256))throw Fail("WHOLE_PIN","Require trusted whole-file SHA256");
        if(request.PartBytes is <1 or >MaximumPartBytes)throw Fail("PART_SIZE","Every part must be strictly below2GiB");
        await using var lease=await leases.AcquireAsync("release-codec:"+request.GenerationPath,cancellationToken);
        await RequireAbsent(request.GenerationPath,cancellationToken);
        await using var source=await OpenRegular(request.SourcePath,"SOURCE_KIND","Require a sealed regular artifact, no symlink",cancellationToken);
        var length=source.Opened.Native.Length;
        if(length<0)throw Fail("SOURCE_SIZE","Invalid source size");
        var count=length==0?0:(length-1)/request.PartBytes+1;
        if(count>MaximumParts)throw Fail("PART_COUNT","More than1000parts requires an explicitly partitioned release plan");
        var retainedPath=Join(request.GenerationPath,"retained-whole.unverified");
        var retained=await store.WriteNewStreamAsync(retainedPath,"application/octet-stream",
            (output,token)=>CopyExact(source.Content,output,length,token),cancellationToken);
        RequireReceipt(retained,retainedPath,length);
        await Unchanged(source,cancellationToken);
        if(retained.Sha256!=request.ExpectedSha256)
            throw Fail("WHOLE_PIN","Input differs from trusted sealed hash; uncommitted bytes retained, no manifest");
        var parts=new List<StoredArtifact>();var rows=new List<ReleasePart>();
        await using(var whole=await OpenRegular(retained.RelativePath,"WHOLE_KIND","Require a retained regular whole",cancellationToken)){
            for(var index=1;index<=count;index++){
                cancellationToken.ThrowIfCancellationRequested();
                var bytes=Math.Min(request.PartBytes,length-(index-1)*request.PartBytes);
                var name=PartName(index);var path=Join(request.GenerationPath,name);
                var part=await store.WriteNewStreamAsync(path,"application/octet-stream",
                    (output,token)=>CopyCount(whole.Content,output,bytes,token),cancellationToken);
                RequireReceipt(part,path,bytes);parts.Add(part);rows.Add(new(name,bytes,part.Sha256));
            }
            await RequireEnd(whole.Content,cancellationToken);await Unchanged(whole,cancellationToken);
        }
        await Unchanged(source,cancellationToken);cancellationToken.ThrowIfCancellationRequested();
        var manifest=new ReleasePartsManifest(Format,sourceSegments[^1],length,retained.Sha256,request.PartBytes,rows.ToArray());
        var manifestPath=Join(request.GenerationPath,"manifest.json");
        var data=canonical.Serialize(JsonSerializer.SerializeToElement(manifest));
        var commit=await store.WriteNewAsync(new Artifact(manifestPath,data,"application/json"),cancellationToken);
        RequireReceipt(commit,manifestPath,data.Length,ContentHash.Sha256(data));
        return new(manifest,commit,parts.ToArray(),retained);
    }

    public async Task<ReleaseReassembleResult> ReassembleAsync(ReleaseReassembleRequest request,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(request);
        var manifestSegments=ArtifactPaths.Segments(request.ManifestPath);ArtifactPaths.Segments(request.GenerationPath);
        ArtifactPaths.Segments(request.DestinationPath);
        if(!Pin(request.ExpectedManifestSha256))throw Fail("MANIFEST_PIN","Manifest hash mismatch");
        var observed=await reader.InspectAsync(request.ManifestPath,cancellationToken)
            ??throw Fail("MANIFEST_KIND","Require a regular manifest");
        ReadOnlyMemory<byte> original;
        try{original=await controlPins.ReadAsync(new(request.ManifestPath,request.ExpectedManifestSha256,
            observed.Native.Length,"application/json"),cancellationToken);}
        catch(PinnedArtifactException error) when(error.Code is "ARTIFACT_PIN_HASH" or "ARTIFACT_PIN_LENGTH" or "ARTIFACT_PIN_METADATA"){
            throw Fail("MANIFEST_PIN","Manifest hash mismatch");
        }
        cancellationToken.ThrowIfCancellationRequested();
        var inventory=ParseManifest(original);
        await RequireAbsent(request.DestinationPath,cancellationToken);
        await using var lease=await leases.AcquireAsync("release-codec:"+request.GenerationPath,cancellationToken);
        await RequireAbsent(request.GenerationPath,cancellationToken);
        var parent=string.Join('/',manifestSegments.Take(manifestSegments.Length-1));
        var inputs=new List<(StoredArtifact Pin,ArtifactReadSnapshot Snapshot,StoredArtifact Verified)>();
        // All part and whole-pin preflight happens before final destination
        // creation. A later failure may retain only uncommitted whole bytes.
        foreach(var row in inventory.Parts){
            var path=Join(parent,row.Name);
            await using var part=await OpenRegular(path,"PART_KIND","Missing/invalid regular part",cancellationToken);
            if(part.Opened.Native.Length!=row.Bytes)throw Fail("PART_KIND","Missing/invalid regular part size");
            // The shared strict store computes the digest over an owned,
            // bounded retained copy. Never hash an unbounded/growing source
            // until EOF, or add a second incremental hash implementation.
            var verifiedPath=Join(request.GenerationPath,"input-parts/"+row.Name+".unverified");
            var verified=await store.WriteNewStreamAsync(verifiedPath,"application/octet-stream",
                (output,token)=>CopyExact(part.Content,output,row.Bytes,token),cancellationToken);
            RequireReceipt(verified,verifiedPath,row.Bytes);
            if(verified.Sha256!=row.Sha256)throw Fail("PART_PIN","Part hash mismatch");
            await Unchanged(part,cancellationToken);
            inputs.Add((new(path,row.Sha256,row.Bytes,"application/octet-stream"),part.Opened,verified));
        }
        var retainedPath=Join(request.GenerationPath,"retained-whole.unverified");
        var retained=await store.WriteNewStreamAsync(retainedPath,"application/octet-stream",async(output,token)=>{
            foreach(var input in inputs){
                await using var part=await OpenRegular(input.Verified.RelativePath,"PART_KIND","Missing/invalid retained regular part",token);
                if(part.Opened.Native.Length!=input.Verified.Length)throw Fail("SOURCE_CHANGED","Input changed before retained copy");
                await CopyExact(part.Content,output,input.Verified.Length,token);await Unchanged(part,token);
            }
        },cancellationToken);
        RequireReceipt(retained,retainedPath,inventory.Bytes,inventory.Sha256);
        await CheckSnapshots(inputs,cancellationToken);
        await using var whole=await OpenRegular(retained.RelativePath,"WHOLE_KIND","Require a retained regular whole",cancellationToken);
        if(whole.Opened.Native.Length!=retained.Length)throw Fail("SOURCE_CHANGED","Input changed before final copy");
        var artifact=await store.WriteNewStreamAsync(request.DestinationPath,"application/octet-stream",
            (output,token)=>CopyExact(whole.Content,output,retained.Length,token),cancellationToken);
        RequireReceipt(artifact,request.DestinationPath,inventory.Bytes,inventory.Sha256);
        await Unchanged(whole,cancellationToken);await CheckSnapshots(inputs,cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var markerPath=Join(request.GenerationPath,"success.json");
        var markerBytes=canonical.Serialize(JsonSerializer.SerializeToElement(new{
            format="android-mcp-release-reassembly-commit-v1",artifact=artifact.RelativePath,
            bytes=artifact.Length,sha256=artifact.Sha256,manifest_sha256=request.ExpectedManifestSha256
        }));
        var marker=await store.WriteNewAsync(new Artifact(markerPath,markerBytes,"application/json"),cancellationToken);
        RequireReceipt(marker,markerPath,markerBytes.Length,ContentHash.Sha256(markerBytes));
        return new(artifact,marker,retained);
    }

    private ReleasePartsManifest ParseManifest(ReadOnlyMemory<byte> bytes)
    {
        var inspection=json.Inspect(bytes);
        if(inspection.HasFatalErrors||inspection.Value is not {} m||m.ValueKind!=JsonValueKind.Object)
            throw Fail("MANIFEST_JSON","Invalid or ambiguous manifest JSON");
        if(!m.TryGetProperty("format",out var format)||format.ValueKind!=JsonValueKind.String||format.GetString()!=Format)
            throw Fail("MANIFEST_FORMAT","Unknown manifest format");
        if(!m.TryGetProperty("parts",out var parts)||parts.ValueKind!=JsonValueKind.Array||parts.GetArrayLength()>MaximumParts)
            throw Fail("PART_INVENTORY","Invalid part inventory");
        var rows=new List<ReleasePart>();long total=0;
        foreach(var row in parts.EnumerateArray()){
            var expected=PartName(rows.Count+1);
            if(row.ValueKind!=JsonValueKind.Object||!row.TryGetProperty("name",out var name)||
                name.ValueKind!=JsonValueKind.String||name.GetString()!=expected)
                throw Fail("PART_PATH","Invalid/duplicate part path or order");
            if(!row.TryGetProperty("bytes",out var size)||size.ValueKind!=JsonValueKind.Number||
                !size.TryGetInt64(out var length)||length is <1 or >MaximumPartBytes)
                throw Fail("PART_SIZE","Invalid part size");
            if(!row.TryGetProperty("sha256",out var hash)||hash.ValueKind!=JsonValueKind.String||!Pin(hash.GetString()))
                throw Fail("PART_PIN","Invalid part hash");
            total=checked(total+length);rows.Add(new(expected,length,hash.GetString()!));
        }
        if(!m.TryGetProperty("bytes",out var wholeSize)||wholeSize.ValueKind!=JsonValueKind.Number||
            !wholeSize.TryGetInt64(out var wholeLength)||wholeLength!=total||
            !m.TryGetProperty("sha256",out var wholeHash)||wholeHash.ValueKind!=JsonValueKind.String||!Pin(wholeHash.GetString()))
            throw Fail("WHOLE_PIN","Invalid whole-file identity");
        // Legacy reassembly ignores writer-only artifact_name and part_bytes.
        return new(Format,"",wholeLength,wholeHash.GetString()!,0,rows.ToArray());
    }

    private async Task<IArtifactReadSession> OpenRegular(string path,string code,string message,CancellationToken token)
    {
        try{return await reader.OpenReadAsync(path,token);}
        catch(Exception error) when(error is IOException or ArgumentException or UnauthorizedAccessException){throw Fail(code,message);}
    }
    private async Task RequireAbsent(string path,CancellationToken token)
    {
        try{if(await reader.InspectAsync(path,token) is not null)throw new IOException("Never overwrite an existing artifact or generation");}
        catch(IOException error){throw new IOException("Never overwrite an existing artifact or unavailable generation",error);}
    }
    private async Task CheckSnapshots(IEnumerable<(StoredArtifact Pin,ArtifactReadSnapshot Snapshot,StoredArtifact Verified)> inputs,CancellationToken token)
    {
        foreach(var input in inputs){
            var current=await reader.InspectAsync(input.Pin.RelativePath,token);
            if(current!=input.Snapshot)throw Fail("SOURCE_CHANGED","Input changed before commit");
        }
    }
    private static async Task Unchanged(IArtifactReadSession session,CancellationToken token)
    {
        if(await session.ObserveCurrentPathAsync(token)!=session.Opened)throw Fail("SOURCE_CHANGED","Input changed");
    }
    private static async Task CopyExact(Stream input,Stream output,long count,CancellationToken token)
    {await CopyCount(input,output,count,token);await RequireEnd(input,token);}
    private static async Task CopyCount(Stream input,Stream output,long count,CancellationToken token)
    {
        var buffer=new byte[BufferBytes];
        while(count>0){
            token.ThrowIfCancellationRequested();var read=await input.ReadAsync(buffer.AsMemory(0,(int)Math.Min(count,buffer.Length)),token);
            if(read==0)throw Fail("SOURCE_SIZE","Input changed or ended during read");
            await output.WriteAsync(buffer.AsMemory(0,read),token);count-=read;
        }
    }
    private static async Task RequireEnd(Stream input,CancellationToken token)
    {if(await input.ReadAsync(new byte[1],token)!=0)throw Fail("SOURCE_SIZE","Input changed or grew during read");}
    private static void RequireReceipt(StoredArtifact receipt,string path,long bytes,string? digest=null)
    {
        if(receipt.RelativePath!=path||receipt.Length!=bytes||!Pin(receipt.Sha256)||digest is not null&&receipt.Sha256!=digest)
            throw Fail("STORED_PIN","Whole artifact mismatch or invalid shared store receipt");
    }
    private static bool Pin(string? value)=>value is not null&&PublicationNames.Pin(value);
    private static string Join(string parent,string name)=>parent.Length==0?name:parent+"/"+name;
    private static string PartName(long index)=>"artifact.part-"+index.ToString("D4",CultureInfo.InvariantCulture);
    private static ReleasePartsException Fail(string code,string message)=>new(code,message);
}
