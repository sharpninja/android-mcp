namespace AndroidMcp.BuildTool.Core;

// The supplied artifact store owns root/path admission. This service performs no JSON parsing or new I/O.
public interface IPinnedArtifactReader
{
    Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact callerPin,CancellationToken cancellationToken=default);
}
public sealed class PinnedArtifactException(string code):InvalidOperationException(code)
{
    public string Code{get;}=code;
}
