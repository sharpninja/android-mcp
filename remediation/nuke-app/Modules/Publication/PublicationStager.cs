using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

// Copies immutable bundles only. No Git, network, provider, subprocess, delete,
// overwrite or source mutation exists in this service.
public sealed class PublicationStager(IArtifactStore source,IArtifactStore destination,
    IArtifactLeaseProvider leases,IClock clock,IPublicationChecker checker,ICanonicalJson canonical):IPublicationStager
{
    public async Task<PublicationStageResult> StageAsync(StageRequest request,CancellationToken cancellationToken=default)
    {
        var started=clock.Now;var outputs=new List<StoredArtifact>();
        PublicationStageResult Result(bool committed,StoredArtifact? manifest,params PublicationIssue[] issues)=>
            new(request.GenerationId,committed,manifest,outputs.ToArray(),issues,started,clock.Now);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if(!PublicationNames.Generation(request.GenerationId))return Result(false,null,new PublicationIssue("UNSAFE_GENERATION",request.GenerationId));
            var check=checker.Check(request.Policy,request.Files.Select(x=>new PublicationFile(x.DestinationPath,x.Length)).ToArray());
            if(!check.IsValid)return Result(false,null,check.Issues.ToArray());
            foreach(var entry in request.Files)
            {
                if(!PublicationNames.Relative(entry.SourcePath))return Result(false,null,new PublicationIssue("UNSAFE_PATH",entry.SourcePath));
                if(!PublicationNames.Pin(entry.Sha256))return Result(false,null,new PublicationIssue("INVALID_PIN",entry.DestinationPath));
                if(string.IsNullOrWhiteSpace(entry.MediaType))return Result(false,null,new PublicationIssue("INVALID_MEDIA_TYPE",entry.DestinationPath));
            }
            await using var lease=await leases.AcquireAsync("publication/"+request.GenerationId,cancellationToken);
            var prefix="generations/"+request.GenerationId+"/";
            var manifestPath=prefix+"manifest.json";
            var ordered=request.Files.OrderBy(x=>x.DestinationPath,StringComparer.Ordinal).ToArray();
            var manifestBytes=canonical.Serialize(JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema_version=1,generation=request.GenerationId,policy_sha256=ContentHash.Sha256(request.Policy.Span),
                files=ordered.Select(x=>new{path=x.DestinationPath,sha256=x.Sha256,length=x.Length,media_type=x.MediaType}).ToArray()
            }));
            var material=new List<(StageEntry Entry,string Output,byte[] Bytes,bool Existing)>();
            // All source checks precede every destination mutation.
            foreach(var entry in ordered)
            {
                var bytes=await ReadOptional(source,entry.SourcePath,cancellationToken);
                if(bytes is null)return Result(false,null,new PublicationIssue("SOURCE_MISSING",entry.SourcePath));
                if(!Matches(entry,bytes.Value))return Result(false,null,new PublicationIssue("SOURCE_PIN_MISMATCH",entry.SourcePath));
                material.Add((entry,prefix+"files/"+entry.DestinationPath,bytes.Value.ToArray(),false));
            }
            for(var i=0;i<material.Count;i++)
            {
                var item=material[i];var old=await ReadOptional(destination,item.Output,cancellationToken);
                if(old is not null && !Matches(item.Entry,old.Value))return Result(false,null,new PublicationIssue("DESTINATION_CONFLICT",item.Entry.DestinationPath));
                material[i]=(item.Entry,item.Output,item.Bytes,old is not null);
            }
            var existingManifest=await ReadOptional(destination,manifestPath,cancellationToken);
            if(existingManifest is not null && !existingManifest.Value.Span.SequenceEqual(manifestBytes))
                return Result(false,null,new PublicationIssue("GENERATION_CONFLICT",manifestPath));
            if(existingManifest is not null && material.Any(x=>!x.Existing))
                return Result(false,null,new PublicationIssue("INCOMPLETE_COMMITTED_GENERATION",manifestPath));
            foreach(var item in material)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stored=item.Existing ? new StoredArtifact(item.Output,item.Entry.Sha256,item.Entry.Length,item.Entry.MediaType) :
                    await destination.WriteAsync(new(item.Output,item.Bytes,item.Entry.MediaType),cancellationToken);
                if(stored.RelativePath!=item.Output || stored.Sha256!=item.Entry.Sha256 || stored.Length!=item.Entry.Length || stored.MediaType!=item.Entry.MediaType)
                    return Result(false,null,new PublicationIssue("DESTINATION_PIN_MISMATCH",item.Output));
                outputs.Add(stored);
            }
            // Detect noncooperating external mutation before sealing the generation.
            foreach(var item in material)
            {
                var input=await ReadOptional(source,item.Entry.SourcePath,cancellationToken);
                if(input is null || !Matches(item.Entry,input.Value))return Result(false,null,new PublicationIssue("SOURCE_PIN_MISMATCH",item.Entry.SourcePath));
                var output=await ReadOptional(destination,item.Output,cancellationToken);
                if(output is null || !Matches(item.Entry,output.Value))return Result(false,null,new PublicationIssue("DESTINATION_PIN_MISMATCH",item.Output));
            }
            cancellationToken.ThrowIfCancellationRequested();
            var marker=existingManifest is not null ? new StoredArtifact(manifestPath,ContentHash.Sha256(manifestBytes),manifestBytes.Length,"application/json") :
                await destination.WriteAsync(new(manifestPath,manifestBytes,"application/json"),cancellationToken);
            if(marker.RelativePath!=manifestPath || marker.Sha256!=ContentHash.Sha256(manifestBytes) || marker.Length!=manifestBytes.Length)
                return Result(false,null,new PublicationIssue("MANIFEST_PIN_MISMATCH",manifestPath));
            return Result(true,marker);
        }
        catch(OperationCanceledException){return Result(false,null,new PublicationIssue("CANCELLED",""));}
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or ArgumentException)
        {return Result(false,null,new PublicationIssue("STAGE_IO_ERROR",""));}
    }
    private static bool Matches(StageEntry entry,ReadOnlyMemory<byte> bytes)=>entry.Length==bytes.Length && ContentHash.Sha256(bytes.Span)==entry.Sha256;
    private static async Task<ReadOnlyMemory<byte>?> ReadOptional(IArtifactStore store,string path,CancellationToken token)
    {
        try{return await store.ReadAsync(path,token);}
        catch(FileNotFoundException){return null;}
        catch(DirectoryNotFoundException){return null;}
    }
}
