using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Execution;

public sealed class SourcesFetchInvocationProjection:ISourcesFetchInvocationProjection
{
    public SourcesFetchInvocationCounts Project(SourceBatchResult acquisition)
    {
        var receipt=acquisition.Receipt;var downloaded=0;var reused=0;
        if(acquisition.Reused)
        {
            // RunAsync has reverified every selected raw output. Original action
            // statuses remain in the immutable receipt and are not this run's downloads.
            reused=receipt.SelectedPhysicalSources;
        }
        else
        {
            foreach(var source in receipt.Sources)
                switch(source.Source.Status)
                {
                    case "downloaded":downloaded++;break;
                    case "reused":reused++;break;
                    default:throw new InvalidOperationException("Unknown source acquisition outcome");
                }
        }
        return new(downloaded,reused,receipt.SelectedPhysicalSources,receipt.TotalPhysicalSources,receipt.Complete);
    }
}
