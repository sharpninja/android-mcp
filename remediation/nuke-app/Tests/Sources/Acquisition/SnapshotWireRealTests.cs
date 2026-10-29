using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed class NativeSnapshotWireProbe:ISnapshotWireProbe
{
    public Task<SnapshotWireObservation> DecodeAsync(string mode,byte[] wire)
    {
        byte[] output=[];string? error=null;
        try{output=mode switch{
            "Generic"=>SourceAcquirer.DecodeGitiles(wire),
            "Restore"=>SourceAcquirer.DecodeGitiles(wire,SourceWireEncoding.SnapshotRestoreGitiles),
            "Fetch"=>SourceAcquirer.DecodeGitiles(wire,SourceWireEncoding.SnapshotFetchGitiles),
            _=>throw new InvalidOperationException("Unknown test policy.")};}
        catch(TransportException problem){error=problem.Code;}
        return Task.FromResult(new SnapshotWireObservation(mode,wire.ToArray(),output,error));
    }
}
[Trait("Gate","Real")]
public sealed class SnapshotWireRealTests
{
    [Theory,MemberData(nameof(SnapshotWireCases.All),MemberType=typeof(SnapshotWireCases))]
    public Task SameRuntimeSourcedWireCases(SnapshotWireCase test)=>SnapshotWireCases.AssertCase(test,new NativeSnapshotWireProbe());
}
