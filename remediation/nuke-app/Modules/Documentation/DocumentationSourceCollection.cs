using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Read-only typed observations over existing role-bound stores. Paths come only from the admitted planner.</summary>
public sealed class DocumentationSourceCollection:IDocumentationSourceCollection
{
    private const string ReceiptPath="validation/model-sources.json";
    private readonly IArtifactStore project,catalog;
    private readonly IArtifactReceiptVerifier receipts;
    private readonly ICanonicalJson canonical;
    private readonly IReadOnlyList<DocumentationModelSourceSpec> sources;
    private readonly StoredArtifact admittedPins;
    public DocumentationSourceCollection(IArtifactStore projectStore,IArtifactStore catalogStore,Artifact admittedSourcePins,
        IDocumentationModelSourcePlanner planner,IArtifactReceiptVerifier receiptVerifier,ICanonicalJson canonicalJson)
    {
        project=projectStore;catalog=catalogStore;receipts=receiptVerifier;canonical=canonicalJson;
        // The proven planner is the only pin-to-route authority. No caller supplies a route override.
        sources=Array.AsReadOnly(planner.Plan(admittedSourcePins).Sources.ToArray());
        if(sources.Any(source=>!Enum.IsDefined(source.Role)||!ArtifactPaths.IsRelative(source.RelativePath))||sources.Select(source=>source.ArtifactId).Distinct(StringComparer.Ordinal).Count()!=sources.Count)
            throw new DocumentationSourceCollectionException("MODEL_SOURCE_SNAPSHOT",[]);
        admittedPins=receipts.Describe(admittedSourcePins);
    }
    public async Task<DocumentationModelSourceSnapshot> CaptureAsync(CancellationToken cancellationToken=default)
    {
        var observed=await ReadAllAsync(true,cancellationToken);
        return new(observed,CreateReceipt(observed));
    }
    public async Task VerifyUnchangedAsync(DocumentationModelSourceSnapshot snapshot,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();ValidateSnapshot(snapshot);
        var current=await ReadAllAsync(false,cancellationToken);
        var changed=snapshot.Sources.Zip(current).Where(pair=>pair.First.Identity!=pair.Second.Identity).Select(pair=>pair.First.Observation.Source.ArtifactId).ToArray();
        if(changed.Length>0)throw new DocumentationSourceCollectionException("MODEL_SOURCE_CHANGED",changed);
    }
    private async Task<IReadOnlyList<DocumentationCapturedSource>> ReadAllAsync(bool capture,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var observations=new List<DocumentationCapturedSource>();var missing=new List<string>();var mismatches=new List<string>();
        foreach(var source in sources)
        {
            token.ThrowIfCancellationRequested();Artifact? content=null;
            try
            {
                var store=source.Role==DocumentationInputRole.CanonicalContractOptional?catalog:project;
                content=new(source.RelativePath,(await store.ReadAsync(source.RelativePath,token)).ToArray(),source.MediaType);
            }
            catch(FileNotFoundException){if(source.Role!=DocumentationInputRole.CanonicalContractOptional)missing.Add(source.ArtifactId);}
            catch(DirectoryNotFoundException){if(source.Role!=DocumentationInputRole.CanonicalContractOptional)missing.Add(source.ArtifactId);}
            catch(IOException error){throw new DocumentationSourceCollectionException(capture?"MODEL_SOURCE_READ":"MODEL_SOURCE_CHANGED",[source.ArtifactId],error);}
            var identity=content is null?null:receipts.Describe(content);
            if(capture&&content is not null&&identity is not null)
            {
                var expected=source.ExpectedSha256;
                var isPins=source.Role==DocumentationInputRole.ProjectRequired&&source.RelativePath==admittedPins.RelativePath;
                if(expected is not null&&identity.Sha256!=expected||isPins&&!receipts.Matches(content,admittedPins))mismatches.Add(source.ArtifactId);
            }
            observations.Add(new(new(source,content),identity));
        }
        if(capture&&missing.Count>0)throw new DocumentationSourceCollectionException("MODEL_SOURCE_MISSING",missing);
        if(capture&&mismatches.Count>0)throw new DocumentationSourceCollectionException("MODEL_SOURCE_PIN_MISMATCH",mismatches);
        return observations.AsReadOnly();
    }
    private void ValidateSnapshot(DocumentationModelSourceSnapshot snapshot)
    {
        if(snapshot.Sources.Count!=sources.Count)throw new DocumentationSourceCollectionException("MODEL_SOURCE_SNAPSHOT",[]);
        for(var index=0;index<sources.Count;index++)
        {
            var source=sources[index];var row=snapshot.Sources[index];var content=row.Observation.Content;
            if(row.Observation.Source!=source||content is null&&(row.Identity is not null||source.Role!=DocumentationInputRole.CanonicalContractOptional)||
                content is not null&&(content.RelativePath!=source.RelativePath||content.MediaType!=source.MediaType||row.Identity is null||!receipts.Matches(content,row.Identity)))
                throw new DocumentationSourceCollectionException("MODEL_SOURCE_SNAPSHOT",[source.ArtifactId]);
        }
        var expected=CreateReceipt(snapshot.Sources);
        if(!receipts.Matches(snapshot.Receipt,receipts.Describe(expected)))throw new DocumentationSourceCollectionException("MODEL_SOURCE_SNAPSHOT",[]);
    }
    private Artifact CreateReceipt(IReadOnlyList<DocumentationCapturedSource> observations)
    {
        var value=JsonSerializer.SerializeToElement(new
        {
            schema_version="1.0.0",kind="android-mcp-model-sources",sources=observations.Select(row=>new
            {
                artifact_id=row.Observation.Source.ArtifactId,role=row.Observation.Source.Role.ToString(),contract_id=row.Observation.Source.ContractId?.ToString(),
                relative_path=row.Observation.Source.RelativePath,media_type=row.Observation.Source.MediaType,present=row.Observation.Content is not null,
                sha256=row.Identity?.Sha256,size_bytes=row.Identity?.Length
            }).ToArray()
        });
        return new(ReceiptPath,canonical.Serialize(value),"application/json");
    }
}
