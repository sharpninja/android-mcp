using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Core;

/// <summary>One read through the admitted store; domain codecs consume only the verified bytes.</summary>
public sealed class PinnedArtifactReader(IArtifactStore store):IPinnedArtifactReader
{
    public async Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(callerPin is null||callerPin.Length<0||callerPin.Sha256 is null||!Regex.IsMatch(callerPin.Sha256,"^[a-f0-9]{64}$"))
            throw new PinnedArtifactException("ARTIFACT_PIN_METADATA");
        var bytes=await store.ReadAsync(callerPin.RelativePath,cancellationToken);
        if(bytes.Length!=callerPin.Length)throw new PinnedArtifactException("ARTIFACT_PIN_LENGTH");
        if(ContentHash.Sha256(bytes.Span)!=callerPin.Sha256)throw new PinnedArtifactException("ARTIFACT_PIN_HASH");
        return bytes;
    }
}
