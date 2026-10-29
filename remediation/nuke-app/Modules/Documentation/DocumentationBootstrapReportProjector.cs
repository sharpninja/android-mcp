using System.Globalization;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

// Original receipt/summary layout only. Acquisition, final source reread,
// committed publication and printing remain existing caller/runtime concerns.
public sealed class DocumentationBootstrapReportProjector(IClock clock,IArtifactReceiptVerifier receipts):IDocumentationBootstrapReportProjector
{
    private static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true,NewLine="\n"};
    public DocumentationBootstrapReport Project(DocumentationRendererProfile profile,Artifact finalLock,IReadOnlyList<ToolArtifactReceipt> successfulTools)
    {
        if(finalLock.RelativePath!="toolchain-lock.json"||finalLock.MediaType!="application/json"||successfulTools.Count!=5||profile.Downloads.Count!=5)throw Invalid();
        for(var index=0;index<successfulTools.Count;index++){
            var tool=successfulTools[index];var pin=profile.Downloads[index];
            if(tool.Status is not("downloaded" or "reused")||tool.Pin.Id!=pin.LocalPath||tool.Pin.Url!=pin.Url||tool.Pin.Version!=pin.Revision||tool.Pin.Sha256!=pin.Sha256||tool.Pin.SizeBytes!=pin.SizeBytes||
                tool.Artifact.RelativePath!=pin.LocalPath||tool.Artifact.Sha256!=pin.Sha256||tool.Artifact.Length!=pin.SizeBytes)throw Invalid();
        }
        var utc=clock.Now.ToUniversalTime();utc=utc.AddTicks(-(utc.Ticks%10));
        var created=utc.ToString(utc.Ticks%TimeSpan.TicksPerSecond==0?"yyyy-MM-dd'T'HH:mm:sszzz":"yyyy-MM-dd'T'HH:mm:ss.ffffffzzz",CultureInfo.InvariantCulture);
        var value=new{created_at=created,files=successfulTools.Select(tool=>new{file=tool.Artifact.RelativePath,url=tool.Pin.Url.AbsoluteUri,size_bytes=tool.Artifact.Length,sha256=tool.Artifact.Sha256}).ToArray(),original_lock_sha256=receipts.Describe(finalLock).Sha256};
        var receipt=new Artifact("toolchain-bootstrap-receipt.json",Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value,Pretty)+"\n"),"application/json");
        // BCL indented serialization supplies original scalar-object spacing;
        // join its four field lines for the original one-line printed object.
        var lines=JsonSerializer.Serialize(new{files=successfulTools.Count,gradle=profile.GradleVersion,plantuml=profile.PlantUmlVersion,task_local_only=true},Pretty).Split('\n');
        var summary="{"+string.Join(" ",lines.Skip(1).SkipLast(1).Select(line=>line.Trim()))+"}\n";
        return new(receipt,summary);
    }
    private static ArchitectureAssetException Invalid()=>new("BOOTSTRAP_RECEIPT","Only exact successful bootstrap tools and final lock bytes may publish the original receipt");
}
