namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// Ready for SAME-case activation after the sole Publication typed facts and
// existing SnapshotMetadataProofReader binding are imported. No new engine.
public sealed class NativeMetadataPlatformCallerProbe:IMetadataPlatformCallerProbe
{
    public async Task<MetadataPlatformCallerObservation> RunAsync(MetadataPlatformCallerCase c)
    {
        var platforms=new List<string>();
        var caller=await new NativeMetadataCallerProbe(c.Input,platforms.Add).RunAsync(c.Caller);
        return new(caller,platforms.ToArray());
    }
}
