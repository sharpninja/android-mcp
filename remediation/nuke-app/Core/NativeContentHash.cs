namespace AndroidMcp.BuildTool.Core;

/// <summary>Injectable adapter over the one shared byte-hash implementation.</summary>
public sealed class NativeContentHash:IContentHash
{
    public string Sha256(ReadOnlySpan<byte> bytes)=>ContentHash.Sha256(bytes);
    public string AidlSha1Legacy(ReadOnlySpan<byte> bytes)=>ContentHash.AidlSha1Legacy(bytes);
    public string GitBlobSha1Legacy(ReadOnlySpan<byte> payload)=>ContentHash.GitBlobSha1Legacy(payload);
}
