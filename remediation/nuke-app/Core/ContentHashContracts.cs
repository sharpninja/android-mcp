namespace AndroidMcp.BuildTool.Core;

/// <summary>SHA256 for our artifacts; SHA1 is exposed only for named immutable upstream identities.</summary>
public interface IContentHash
{
    string Sha256(ReadOnlySpan<byte> bytes);
    string AidlSha1Legacy(ReadOnlySpan<byte> bytes);
    string GitBlobSha1Legacy(ReadOnlySpan<byte> payload);
}
