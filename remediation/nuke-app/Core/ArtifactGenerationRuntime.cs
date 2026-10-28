namespace AndroidMcp.BuildTool.Core;

public sealed class ArtifactGenerationException(string code,string message,Exception? inner=null):IOException(message,inner)
{public string Code {get;}=code;}

/// <summary>One immutable generation protocol. Domain recipes and manifest formats are dependencies.</summary>
public sealed class ArtifactGenerationRuntime(IArtifactStore source,IArtifactStore output,IArtifactLeaseProvider leases,
    IArtifactReceiptVerifier receipts,IArtifactGenerationManifestFactory manifests,string admittedRootLeaseScope):IArtifactGenerationRuntime
{
    public async Task<ArtifactGenerationResult> ExecuteAsync(string generationId,ArtifactGenerationRecipe recipe,CancellationToken cancellationToken=default)
    {
        if(ReferenceEquals(source,output))throw new ArtifactGenerationException("SOURCE_OUTPUT_STORE","Source and output bindings must be separate");
        ArgumentException.ThrowIfNullOrWhiteSpace(generationId);ArgumentException.ThrowIfNullOrWhiteSpace(admittedRootLeaseScope);
        cancellationToken.ThrowIfCancellationRequested();
        await using var lease=await leases.AcquireAsync(admittedRootLeaseScope,cancellationToken);
        var specs=recipe.Sources.ToArray();
        if(specs.Any(s=>!ArtifactPaths.IsRelative(s.RelativePath))||specs.GroupBy(s=>s.RelativePath,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))
            throw new ArtifactGenerationException("INPUT_PATH","Source paths must be safe and unique");
        var inputs=new List<Artifact>();var missing=new List<string>();
        foreach(var spec in specs)
        {
            try{inputs.Add(new(spec.RelativePath,(await source.ReadAsync(spec.RelativePath,cancellationToken)).ToArray(),spec.MediaType));}
            catch(FileNotFoundException){missing.Add(spec.RelativePath);}catch(DirectoryNotFoundException){missing.Add(spec.RelativePath);}
            catch(IOException error){throw new ArtifactGenerationException("INPUT_READ_FAILED","Could not read admitted source: "+spec.RelativePath,error);}
        }
        if(missing.Count>0)throw new ArtifactGenerationException("INPUT_MISSING","Missing admitted sources: "+string.Join(", ",missing));
        for(var index=0;index<specs.Length;index++)
            if(specs[index].ExpectedIdentity is {} expected&&!receipts.Matches(inputs[index],expected))
                throw new ArtifactGenerationException("INPUT_PIN_MISMATCH","Source differs from admitted catalog: "+specs[index].RelativePath);
        var admitted=inputs.Select(receipts.Describe).ToArray();ArtifactGenerationPreparation preparation;
        try{preparation=recipe.Prepare(inputs);}
        catch(OperationCanceledException){throw;}
        catch(Exception error){throw new ArtifactGenerationException("PROJECTION_FAILED","Generation preparation failed before output writes",error);}
        var paths=preparation.OutputPaths.ToArray();ValidatePaths(paths);
        var occupied=new List<string>();
        foreach(var path in paths.Append(manifests.RelativePath).Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try{await output.ReadAsync(path,cancellationToken);occupied.Add(path);}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
            catch(ArgumentException error){throw new ArtifactGenerationException("UNSAFE_OUTPUT","Shared store rejected planned output: "+path,error);}
            catch(IOException error){throw new ArtifactGenerationException("OUTPUT_PREFLIGHT_FAILED","Output preflight failed: "+path,error);}
        }
        if(occupied.Count>0)throw new ArtifactGenerationException("OUTPUT_EXISTS","Generation is not fresh; existing files retained: "+string.Join(", ",occupied));
        cancellationToken.ThrowIfCancellationRequested();IReadOnlyList<Artifact> materialized;
        try{materialized=await preparation.MaterializeAsync(cancellationToken);}
        catch(OperationCanceledException){throw;}
        catch(Exception error){throw new ArtifactGenerationException("PROJECTION_FAILED","Generation materialization failed before final output writes",error);}
        var artifacts=materialized.OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();ValidatePaths(artifacts.Select(a=>a.RelativePath).ToArray());
        if(!paths.Order(StringComparer.Ordinal).SequenceEqual(artifacts.Select(a=>a.RelativePath),StringComparer.Ordinal))
            throw new ArtifactGenerationException("OUTPUT_PLAN_MISMATCH","Materialized output set differs from its preflighted plan");
        var written=new List<StoredArtifact>();
        foreach(var artifact in artifacts)
        {
            cancellationToken.ThrowIfCancellationRequested();StoredArtifact stored;
            try{stored=await output.WriteAsync(artifact,cancellationToken);}
            catch(IOException error){throw new ArtifactGenerationException("WRITE_FAILED","Output write failed; uncommitted files retained: "+artifact.RelativePath,error);}
            VerifyStored(artifact,stored);written.Add(stored);
        }
        var changed=new List<string>();
        foreach(var input in inputs)
        {
            try{var current=await source.ReadAsync(input.RelativePath,cancellationToken);if(!current.Span.SequenceEqual(input.Bytes.Span))changed.Add(input.RelativePath);}
            catch(IOException error){throw new ArtifactGenerationException("INPUT_CHANGED","Source became unreadable before commit: "+input.RelativePath,error);}
        }
        if(changed.Count>0)throw new ArtifactGenerationException("INPUT_CHANGED","Source changed; outputs remain uncommitted: "+string.Join(", ",changed));
        cancellationToken.ThrowIfCancellationRequested();
        var manifest=manifests.Create(generationId,admitted,written);
        if(manifest.RelativePath!=manifests.RelativePath)throw new ArtifactGenerationException("MANIFEST_PATH_MISMATCH","Manifest factory changed its preflighted path");
        StoredArtifact commit;
        try{commit=await output.WriteAsync(manifest,cancellationToken);}
        catch(IOException error){throw new ArtifactGenerationException("WRITE_FAILED","Generation manifest did not commit; files retained",error);}
        VerifyStored(manifest,commit);return new(generationId,admitted,written.ToArray(),commit);
    }
    private void ValidatePaths(IReadOnlyList<string> paths)
    {
        if(paths.GroupBy(p=>p,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new ArtifactGenerationException("DUPLICATE_OUTPUT","Planned output paths collide");
        if(paths.Any(p=>string.Equals(p,manifests.RelativePath,StringComparison.OrdinalIgnoreCase)))throw new ArtifactGenerationException("RESERVED_OUTPUT","Recipes cannot supply the generation manifest");
    }
    private void VerifyStored(Artifact artifact,StoredArtifact stored)
    {if(!receipts.Matches(artifact,stored))throw new ArtifactGenerationException("STORED_RECEIPT_MISMATCH","Stored receipt differs from requested bytes: "+artifact.RelativePath);}
}
