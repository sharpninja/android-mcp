using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Modules.Review.ReviewNormalization;
namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewReceiptInput(string LogicalPath,StoredArtifact Artifact);
public sealed record ReviewReceiptRound(string Directory,StoredArtifact Request,StoredArtifact Manifest,StoredArtifact Visible,
    IReadOnlyList<ReviewReceiptInput> Inputs,StoredArtifact? Completion,IReadOnlyList<StoredArtifact> DerivedReports);
public sealed record ReviewReceiptCollectRequest(IReadOnlyList<ReviewReceiptRound> Rounds,string OutputPath);
public sealed record ReviewReceiptCollection(StoredArtifact Audit,IReadOnlyList<StoredArtifact> Evidence,int Rounds);

/// <summary>Local original audit_receipts.py integrity/metadata collector. Never official audit,
/// provider capability, PreparedReview or acceptance authority. No process/MCP/provider dependency.</summary>
public sealed class ReviewReceiptCollector(IPinnedArtifactReader pins,IArtifactStore store,ICanonicalJson canonical,IClock clock,ReviewAssessService assessor)
{
    internal static IReadOnlyList<string> ReportPriority {get;}=System.Array.AsReadOnly(new[]{"review-report.json","review-report.reassembled.json","review-report.parsed.json","review-report.repaired.json"});
    private static readonly JsonSerializerOptions ControlOptions=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectRequiredConstructorParameters=true,MaxDepth=64};
    public async Task<ReviewReceiptCollection> CollectRequestAsync(StoredArtifact requestPin,CancellationToken token=default)
        =>await Guard(async()=>
        {
            var control=await ReadAsync(pins,canonical,requestPin,token);
            var request=JsonSerializer.Deserialize<ReviewReceiptCollectRequest>(control.ToJsonString(),ControlOptions);
            Need(request is not null&&request.Rounds is not null&&request.Rounds.All(x=>x is not null&&x.Inputs is not null&&x.DerivedReports is not null),"INVALID_REVIEW_RECEIPT");
            return await CollectAsync(request,requestPin,token);
        });
    internal Task<ReviewReceiptCollection> CollectObservedAsync(ReviewReceiptCollectRequest request,CancellationToken token=default)
        =>Guard(async()=>
        {
            try{await store.ReadAsync(request.OutputPath,token);throw new ReviewException("OUTPUT_CONFLICT",request.OutputPath);}
            catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
            return await CollectAsync(request,null,token);
        });
    private static async Task<ReviewReceiptCollection> Guard(Func<Task<ReviewReceiptCollection>> action)
    {
        try
        {
            return await action();
        }
        catch(Exception error)when(error is JsonException or CanonicalJsonException or NotSupportedException or DecoderFallbackException)
        {throw new ReviewException("INVALID_REVIEW_RECEIPT","Local receipt schema is invalid.");}
        catch(FileNotFoundException){throw new ReviewException("REVIEW_RECEIPT_MISSING","A selected local receipt is missing.");}
        catch(DirectoryNotFoundException){throw new ReviewException("REVIEW_RECEIPT_MISSING","A selected local receipt directory is missing.");}
    }
    private JsonObject Object(ReadOnlyMemory<byte> bytes)
        =>JsonSerializer.Deserialize<JsonObject>(canonical.Serialize(bytes))??throw new ReviewException("INVALID_REVIEW_RECEIPT","Expected a receipt object.");
    internal JsonObject VerifyOriginalRequest(ReadOnlyMemory<byte> bytes)
    {
        var lines=ReviewPrepareService.Utf8.GetString(bytes.Span);var newline=lines.IndexOf('\n');
        var original=Object(ReviewPrepareService.Utf8.GetBytes(newline<0?lines:lines[..newline]));
        Need(original["prompt"] is JsonValue prompt&&prompt.TryGetValue<string>(out _),"INVALID_REVIEW_RECEIPT");
        ReviewPrepareService.PublicPayload("",Text(original,"prompt"));
        Need(Text(original,"prompt_sha256")==ContentHash.Sha256(ReviewPrepareService.Utf8.GetBytes(Text(original,"prompt"))),"REQUEST_PROMPT_HASH_MISMATCH");
        return original;
    }
    internal IReadOnlyList<(string Path,string Hash)> VerifyOriginalManifest(JsonObject original,ReadOnlyMemory<byte> bytes)
    {
        Need(Text(original,"manifest_sha256")==ContentHash.Sha256(bytes.Span),"MANIFEST_HASH_MISMATCH");
        var files=Array(Object(bytes),"files","INVALID_REVIEW_RECEIPT");var result=new List<(string,string)>();
        foreach(var item in files)
        {
            Need(item is JsonObject,"INVALID_REVIEW_RECEIPT");var logical=((JsonObject)item!).ContainsKey("file")?Text(item,"file"):Text(item,"path");
            Need(ArtifactPaths.IsRelative(logical),"INVALID_PATH");
            try{ReviewPrepareService.PublicPayload(logical,"");}
            catch(ReviewException error)when(error.Code=="PRIVATE_PAYLOAD"){throw new ReviewException(error.Code,"Blocked review receipt input path: "+logical);}
            result.Add((logical,Text(item,"sha256")));
        }
        return result;
    }
    private async Task<ReviewReceiptCollection> CollectAsync(ReviewReceiptCollectRequest request,StoredArtifact? requestPin,CancellationToken token)
    {
        Need(ArtifactPaths.IsRelative(request.OutputPath),"INVALID_PATH");ReviewPrepareService.PublicPayload(request.OutputPath,"");
        var evidence=new List<StoredArtifact>();if(requestPin is not null)evidence.Add(requestPin);var rows=new List<JsonObject>();
        async Task<ReadOnlyMemory<byte>> Read(StoredArtifact pin,string? expected=null)
        {
            Need(pin is not null&&ArtifactPaths.IsRelative(pin.RelativePath),"INVALID_PATH");
            if(expected is not null)Need(pin.RelativePath==expected,"INVALID_REVIEW_RECEIPT");
            var bytes=await ReadBytesAsync(pins,pin,token);evidence.Add(pin);return bytes;
        }
        foreach(var round in request.Rounds.OrderBy(x=>x.Directory,StringComparer.Ordinal))
        {
            Need(ArtifactPaths.IsRelative(round.Directory),"INVALID_PATH");ReviewPrepareService.PublicPayload(round.Directory,"");
            var requestBytes=await Read(round.Request,round.Directory+"/request.jsonl");
            var original=VerifyOriginalRequest(requestBytes);
            var manifestBytes=await Read(round.Manifest,round.Directory+"/input-manifest.json");
            var files=VerifyOriginalManifest(original,manifestBytes);
            foreach(var item in files)
            {
                var logical=item.Path;
                var selected=round.Inputs.Where(x=>x.LogicalPath==logical).ToArray();Need(selected.Length==1,"INVALID_REVIEW_RECEIPT");
                var body=await Read(selected[0].Artifact,round.Directory+"/input/"+logical);
                Need(item.Hash==ContentHash.Sha256(body.Span),"FROZEN_INPUT_HASH_MISMATCH");
            }
            var visibleBytes=await Read(round.Visible,round.Directory+"/response.visible.jsonl");
            var visibleText=ReviewPrepareService.Utf8.GetString(visibleBytes.Span);var eventLines=visibleText.Split('\n');
            var eventCount=eventLines.Length-(eventLines[^1].Length==0?1:0);var events=new List<JsonObject>();
            for(var index=0;index<eventCount;index++)events.Add(Object(ReviewPrepareService.Utf8.GetBytes(eventLines[index])));
            Need(!assessor.Assess(visibleBytes,new(false,0),"unused-local-collector-marker").ContainsHiddenReasoning,"HIDDEN_REASONING_PRESENT");
            JsonObject? completion=null;
            if(round.Completion is not null)
            {
                completion=Object(await Read(round.Completion,round.Directory+"/completion.json"));
                if(completion.Count>0)Need(Text(completion,"visible_response_sha256")==ContentHash.Sha256(visibleBytes.Span),"VISIBLE_RESPONSE_HASH_MISMATCH");
            }
            var init=events.FirstOrDefault(x=>Text(x,"subtype")=="init");StoredArtifact? report=null;
            foreach(var filename in ReportPriority)
            {report=round.DerivedReports.FirstOrDefault(x=>x.RelativePath==round.Directory+"/"+filename);if(report is not null)break;}
            var complete=false;
            if(report is not null)
            {var derived=Object(await Read(report));complete=derived["report_complete"] is JsonValue value&&value.TryGetValue<bool>(out var flag)&&flag;}
            Need(original.ContainsKey("requested_model")&&original.ContainsKey("requested_effort"),"INVALID_REVIEW_RECEIPT");
            rows.Add(new JsonObject{["round"]=Path.GetFileName(round.Directory),["input_files"]=files.Count,["input_hashes_match"]=true,["request_hashes_match"]=true,["hidden_reasoning_excluded"]=true,
                ["returned_model"]=init?["model"]?.DeepClone(),["returned_tools"]=init?["tools"]?.DeepClone(),["requested_model"]=original["requested_model"]?.DeepClone(),["requested_effort"]=original["requested_effort"]?.DeepClone(),
                ["finished"]=completion is not null,["exit_code"]=completion?["exit_code"]?.DeepClone(),["complete_derived_report"]=complete,["parsed_report"]=report?.RelativePath});
        }
        var output=canonical.Serialize(JsonSerializer.SerializeToElement(new{audited_at=clock.Now.ToString("O"),rounds=rows,scope="Local original receipt integrity/metadata only; no official audit or acceptance authority."}));
        var artifact=await store.WriteAsync(new(request.OutputPath,output,"application/json"),token);
        return new(artifact,evidence,rows.Count);
    }
}
