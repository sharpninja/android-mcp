using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Review;

/// <summary>Original project-local direct round-* discovery over the shared tree/store authority.
/// No recursive corpus scan, process/provider/MCP capability or second receipt parser.</summary>
public sealed class ReviewReceiptAuditWorkflow(IArtifactTreeReader tree,IArtifactStore source,ReviewReceiptCollector collector)
{
    public async Task<ReviewReceiptCollection> AssessAsync(CancellationToken token=default)
    {
        try
        {
            IReadOnlyList<ArtifactTreeEntry> entries;
            try{entries=await tree.EnumerateAsync("reviews",false,token);}
            catch(FileNotFoundException){entries=[];}catch(DirectoryNotFoundException){entries=[];}
            var rounds=new List<ReviewReceiptRound>();
            foreach(var entry in entries.Where(x=>Path.GetFileName(x.RelativePath).StartsWith("round-",StringComparison.Ordinal)).OrderBy(x=>x.RelativePath,StringComparer.Ordinal))
            {
                ReviewPrepareService.Require(entry.Kind==NativeArtifactKind.Directory,"REVIEW_RECEIPT_SCOPE_BLOCKED",entry.RelativePath);
                var children=await tree.EnumerateAsync(entry.RelativePath,false,token);
                async Task<(StoredArtifact Pin,ReadOnlyMemory<byte> Bytes)> Capture(string path,string media)
                {
                    ReviewPrepareService.Require(ArtifactPaths.IsRelative(path),"INVALID_PATH",path);ReviewPrepareService.PublicPayload(path,"");
                    var bytes=await source.ReadAsync(path,token);return(new(path,ContentHash.Sha256(bytes.Span),bytes.Length,media),bytes);
                }
                async Task<(StoredArtifact Pin,ReadOnlyMemory<byte> Bytes)?> Fixed(string name,bool required)
                {
                    var path=entry.RelativePath+"/"+name;var found=children.FirstOrDefault(x=>x.RelativePath==path);
                    if(found is null){if(required)throw new ReviewException("REVIEW_RECEIPT_MISSING",path);return null;}
                    ReviewPrepareService.Require(found.Kind==NativeArtifactKind.RegularFile,"REVIEW_RECEIPT_SCOPE_BLOCKED",path);
                    return await Capture(path,name.EndsWith("jsonl",StringComparison.Ordinal)?"application/x-ndjson":"application/json");
                }
                var request=(await Fixed("request.jsonl",true))!.Value;var original=collector.VerifyOriginalRequest(request.Bytes);
                var manifest=(await Fixed("input-manifest.json",true))!.Value;
                var files=collector.VerifyOriginalManifest(original,manifest.Bytes);var inputs=new Dictionary<string,ReviewReceiptInput>(StringComparer.Ordinal);
                foreach(var file in files)
                {
                    if(inputs.ContainsKey(file.Path))continue;
                    var captured=await Capture(entry.RelativePath+"/input/"+file.Path,"application/octet-stream");
                    ReviewPrepareService.Require(captured.Pin.Sha256==file.Hash,"FROZEN_INPUT_HASH_MISMATCH",file.Path);
                    inputs.Add(file.Path,new(file.Path,captured.Pin));
                }
                var visible=(await Fixed("response.visible.jsonl",true))!.Value;
                var completion=await Fixed("completion.json",false);var reports=new List<StoredArtifact>();
                foreach(var filename in ReviewReceiptCollector.ReportPriority)
                {var report=await Fixed(filename,false);if(report is{} selected){reports.Add(selected.Pin);break;}}
                rounds.Add(new(entry.RelativePath,request.Pin,manifest.Pin,visible.Pin,inputs.Values.ToArray(),completion?.Pin,reports));
            }
            return await collector.CollectObservedAsync(new(rounds,"review-receipt-audit.json"),token);
        }
        catch(Exception error)when(error is JsonException or CanonicalJsonException or DecoderFallbackException)
        {throw new ReviewException("INVALID_REVIEW_RECEIPT","Local receipt schema is invalid.");}
        catch(FileNotFoundException error){throw new ReviewException("REVIEW_RECEIPT_MISSING",error.FileName??"Selected local receipt is missing.");}
        catch(ArgumentException error){throw new ReviewException("REVIEW_RECEIPT_SCOPE_BLOCKED",error.Message);}
    }
}
