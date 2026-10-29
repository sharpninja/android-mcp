using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
namespace AndroidMcp.BuildTool;
public sealed partial class Build
{
    private async Task AssessLocalReviewReceipts()
    {
        var project=AdmitWorkflowRoot(Environment.CurrentDirectory,ProjectRoot!);
        var output=AdmitWorkflowRoot(project,OutputRoot!);
        if(StringComparer.Ordinal.Equals(project,output))throw new ReviewException("INVALID_REVIEW_OUTPUT","Select a separate audit output root; source originals are retained.");
        var hashes=new NativeContentHash();string Hash(ReadOnlyMemory<byte> bytes)=>hashes.Sha256(bytes.Span);
        var source=new FileArtifactStore(project,Hash);var destination=new FileArtifactStore(output,Hash);
        var collector=new ReviewReceiptCollector(new PinnedArtifactReader(source),destination,new CanonicalJson(),new SystemClock(),new ReviewAssessService(Hash));
        ReviewReceiptCollection result;
        try{result=await new ReviewReceiptAuditWorkflow(new NativeArtifactWorkspace(project),source,collector).AssessAsync();}
        catch(ReviewException error){throw new InvalidOperationException(error.Code+": "+error.Message,error);}
        Console.WriteLine(ReviewPrepareService.Utf8.GetString((await destination.ReadAsync(result.Audit.RelativePath)).Span));
    }
}
