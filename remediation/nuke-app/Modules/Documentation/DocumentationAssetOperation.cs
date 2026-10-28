using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Creates a new immutable generation through the shared store boundary.</summary>
public sealed class DocumentationAssetOperation(
    IArtifactStore source,IArtifactStore output,IArtifactLeaseProvider leases,
    IDocumentationProjector projector,IContentHash hashes,string outputRootIdentity) : IDocumentationAssetOperation
{
    private readonly IArtifactReceiptVerifier receipts=new ArtifactReceiptVerifier(hashes);
    public async Task<DocumentationGeneration> GenerateAsync(string generationId,CancellationToken cancellationToken=default)
    {
        if(ReferenceEquals(source,output))throw new DocumentationAssetException("SOURCE_OUTPUT_STORE","Canonical source and output bindings must be separate.");
        ArgumentException.ThrowIfNullOrWhiteSpace(generationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRootIdentity);
        cancellationToken.ThrowIfCancellationRequested();
        await using var lease=await leases.AcquireAsync("design-assets/"+hashes.Sha256(Encoding.UTF8.GetBytes(outputRootIdentity)),cancellationToken);

        var inputs=new List<Artifact>();var missing=new List<string>();
        foreach(var path in new[]{DocumentationAssetPaths.Blueprint,DocumentationAssetPaths.Design})
        {
            try{inputs.Add(new(path,await source.ReadAsync(path,cancellationToken),"text/markdown"));}
            catch(FileNotFoundException){missing.Add(path);}
            catch(DirectoryNotFoundException){missing.Add(path);}
            catch(IOException e){throw new DocumentationAssetException("INPUT_READ_FAILED","Could not read canonical input: "+path,e);}
        }
        if(missing.Count>0)throw new DocumentationAssetException("INPUT_MISSING","Missing canonical inputs: "+string.Join(", ",missing));
        var admittedInputs=inputs.Select(Metadata).ToArray();
        DocumentationProjection projection;
        try{projection=projector.Project(new(inputs[0],inputs[1]));}
        catch(OperationCanceledException){throw;}
        catch(Exception e){throw new DocumentationAssetException("PROJECTION_FAILED","Canonical UI projection failed before output writes.",e);}
        var artifacts=projection.Artifacts.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        if(artifacts.GroupBy(a=>a.RelativePath,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))
            throw new DocumentationAssetException("DUPLICATE_OUTPUT","Projected output paths collide.");
        if(artifacts.Any(a=>string.Equals(a.RelativePath,DocumentationAssetPaths.Manifest,StringComparison.OrdinalIgnoreCase)))
            throw new DocumentationAssetException("RESERVED_OUTPUT","The projector cannot supply the generation commit manifest.");

        var occupied=new List<string>();
        foreach(var path in artifacts.Select(a=>a.RelativePath).Append(DocumentationAssetPaths.Manifest).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try{await output.ReadAsync(path,cancellationToken);occupied.Add(path);}
            catch(FileNotFoundException){}
            catch(DirectoryNotFoundException){}
            catch(ArgumentException e){throw new DocumentationAssetException("UNSAFE_OUTPUT","Shared store rejected projected path: "+path,e);}
            catch(IOException e){throw new DocumentationAssetException("OUTPUT_PREFLIGHT_FAILED","Could not preflight projected path: "+path,e);}
        }
        if(occupied.Count>0)throw new DocumentationAssetException("OUTPUT_EXISTS","Generation is not fresh; retained existing paths: "+string.Join(", ",occupied));

        var written=new List<StoredArtifact>();
        foreach(var artifact in artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StoredArtifact stored;
            try{stored=await output.WriteAsync(artifact,cancellationToken);}
            catch(IOException e){throw new DocumentationAssetException("WRITE_FAILED","Artifact write failed; partial generation retained without commit: "+artifact.RelativePath,e);}
            VerifyStored(artifact,stored);written.Add(stored);
        }
        var changed=new List<string>();
        foreach(var input in inputs)
        {
            try{var current=await source.ReadAsync(input.RelativePath,cancellationToken);if(!current.Span.SequenceEqual(input.Bytes.Span))changed.Add(input.RelativePath);}
            catch(IOException e){throw new DocumentationAssetException("INPUT_CHANGED","Canonical input became unreadable before commit: "+input.RelativePath,e);}
        }
        if(changed.Count>0)throw new DocumentationAssetException("INPUT_CHANGED","Canonical input changed; generated files remain uncommitted: "+string.Join(", ",changed));
        cancellationToken.ThrowIfCancellationRequested();

        var manifest=new JsonObject
        {
            ["schema_version"]="1.0.0",["kind"]="android-mcp-design-generation",["generation_id"]=generationId,
            ["inputs"]=Rows(admittedInputs),["artifacts"]=Rows(written)
        };
        var commit=new Artifact(DocumentationAssetPaths.Manifest,Encoding.UTF8.GetBytes(manifest.ToJsonString()+"\n"),"application/json");
        StoredArtifact committed;
        try{committed=await output.WriteAsync(commit,cancellationToken);}
        catch(IOException e){throw new DocumentationAssetException("WRITE_FAILED","Generation manifest did not commit; retained outputs must not be treated as a completed generation.",e);}
        VerifyStored(commit,committed);
        return new(generationId,admittedInputs,written.ToArray(),committed);
    }

    private StoredArtifact Metadata(Artifact artifact)=>receipts.Describe(artifact);
    private void VerifyStored(Artifact artifact,StoredArtifact stored)
    {
        if(!receipts.Matches(artifact,stored))throw new DocumentationAssetException("STORED_RECEIPT_MISMATCH","Shared store receipt differs from requested artifact: "+artifact.RelativePath);
    }
    private static JsonArray Rows(IEnumerable<StoredArtifact> artifacts)=>new(artifacts.Select(a=>(JsonNode?)new JsonObject
    {
        ["path"]=a.RelativePath,["sha256"]=a.Sha256,["size_bytes"]=a.Length,["media_type"]=a.MediaType
    }).ToArray());
}
