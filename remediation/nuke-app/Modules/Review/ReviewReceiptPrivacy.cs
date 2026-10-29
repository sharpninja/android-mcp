using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewReceiptPrivacyCounts(long Events,long InvalidJsonLines,IReadOnlyDictionary<string,long> PrivacyCategories);
public sealed record ReviewReceiptPrivacyObservation(string File,string Status,long? Bytes,string? Sha256,ReviewReceiptPrivacyCounts? Counts);

/// <summary>Original receipt privacy counters only. No value, match, raw payload or trust authority escapes.</summary>
public sealed class ReviewReceiptPrivacyClassifier(ICanonicalJson canonical)
{
    // Finite original ASCII case-insensitive literals include Python's four
    // Unicode equivalents. This is pattern data, not a second regex engine.
    private const string Signature="[sSſ][iIİı][gG][nN][aA][tT][uU][rR][eE]";
    private const string Key="(?:[xX]-[aA][mM][zZ]-"+Signature+"|[xX]-[gG][oO][oO][gG]-"+Signature+"|[sSſ][iIİı][gG]|"+Signature+"|[tT][oO][kKK][eE][nN])";
    private const string RawPart="[^\\s\\u001c-\\u001f\"<>]+";
    private static readonly Regex Signed=new("[hH][tT][tT][pP][sSſ]?://"+RawPart+"[?&]"+Key+"="+RawPart,RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
    private static readonly Regex Bearer=new(@"Bearer[\s\u001c-\u001f]+(?:ghp_|sk-ant-)[A-Za-z0-9_-]{20,}",RegexOptions.CultureInvariant|RegexOptions.NonBacktracking);
    private static readonly string[] RuntimeFields=["plugins","slash_commands","terminal_slash_commands","skills","agents","messaging_socket_path"];

    // Small caller-owned input convenience. The selected-file path below never
    // allocates a whole artifact and uses the incremental TextReader overload.
    public async Task<ReviewReceiptPrivacyCounts> ClassifyAsync(ReadOnlyMemory<byte> input,CancellationToken token=default)
    {
        string text;
        try{text=ReviewPrepareService.Utf8.GetString(input.Span);}
        catch(DecoderFallbackException){throw InvalidUtf8();}
        using var reader=new StringReader(text);return await ClassifyAsync(reader,token);
    }
    public async Task<ReviewReceiptPrivacyCounts> ClassifyAsync(TextReader input,CancellationToken token=default)
    {
        long events=0,invalid=0;var counts=new Dictionary<string,long>(StringComparer.Ordinal);
        void Add(string name){counts.TryGetValue(name,out var count);counts[name]=checked(count+1);}
        static bool Is(JsonElement value,string key,string text)=>value.TryGetProperty(key,out var property)&&property.ValueKind==JsonValueKind.String&&property.GetString()==text;
        void Walk(JsonElement value)
        {
            if(value.ValueKind==JsonValueKind.Array){foreach(var item in value.EnumerateArray())Walk(item);return;}
            if(value.ValueKind!=JsonValueKind.Object)return;
            if(Is(value,"type","thinking")||Is(value,"type","redacted_thinking"))Add("hidden_reasoning_block");
            if(Is(value,"type","rate_limit_event"))Add("account_quota_event");
            if(Is(value,"subtype","init"))foreach(var field in RuntimeFields)if(value.TryGetProperty(field,out _))Add("unrelated_runtime_configuration_field");
            foreach(var property in value.EnumerateObject())Walk(property.Value);
        }
        while(true)
        {
            string? line;
            try{line=await input.ReadLineAsync(token);}
            catch(DecoderFallbackException){throw InvalidUtf8();}
            if(line is null)break;events=checked(events+1);
            if(Signed.IsMatch(line))Add("signed_transfer_url");
            if(Bearer.IsMatch(line))Add("bearer_secret");
            try
            {
                var admitted=canonical.Serialize(ReviewPrepareService.Utf8.GetBytes(line));
                using var json=JsonDocument.Parse(admitted);Walk(json.RootElement);
            }
            catch(Exception error)when(error is CanonicalJsonException or JsonException or DecoderFallbackException)
            {invalid=checked(invalid+1);}
        }
        return new(events,invalid,counts);
    }
    private static ReviewException InvalidUtf8()=>new("INVALID_REVIEW_RECEIPT","Selected receipt is not strict UTF8; no payload is exported.");
}

/// <summary>One explicitly selected admitted native file. No scan, store write,
/// whole-file array, provider, publication, acceptance or official audit capability.</summary>
public sealed class ReviewReceiptPrivacyObserver(IStreamingArtifactReader source,ReviewReceiptPrivacyClassifier classifier,Func<Stream,string> sha256)
{
    public async Task<ReviewReceiptPrivacyObservation> ObserveAsync(StoredArtifact pin,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        ReviewPrepareService.Require(pin is not null&&ArtifactPaths.IsRelative(pin.RelativePath),"INVALID_PATH","Select one relative receipt.");
        ReviewPrepareService.PublicPayload(pin!.RelativePath,"");
        var name=Path.GetFileName(pin.RelativePath);
        ReviewPrepareService.Require(pin.RelativePath.StartsWith("reviews/",StringComparison.Ordinal)&&name.EndsWith(".jsonl",StringComparison.Ordinal)&&
            (name.Contains("request",StringComparison.Ordinal)||name.Contains("response",StringComparison.Ordinal)),"INVALID_PATH","Select an explicit original request/response JSONL receipt.");
        try
        {
            await using var session=await source.OpenReadAsync(pin.RelativePath,token);
            var before=session.Opened;
            ReviewPrepareService.Require(before.RelativePath==pin.RelativePath&&before.Native.Kind==NativeArtifactKind.RegularFile,"REVIEW_RECEIPT_SCOPE_BLOCKED",pin.RelativePath);
            if(before.Native.Length!=pin.Length)throw new PinnedArtifactException("ARTIFACT_PIN_LENGTH");
            if(!session.Content.CanSeek)throw new PlatformNotSupportedException("Selected native receipt stream must support same-handle rewind.");
            // The shared synchronous hash has no mid-hash cancellation hook.
            // Owner cancellation is observed immediately before and after it;
            // no replacement hashing logic or elapsed/size cap is introduced.
            token.ThrowIfCancellationRequested();var digest=sha256(session.Content);token.ThrowIfCancellationRequested();
            if(digest!=pin.Sha256)throw new PinnedArtifactException("ARTIFACT_PIN_HASH");
            session.Content.Seek(0,SeekOrigin.Begin);
            using var reader=new StreamReader(session.Content,ReviewPrepareService.Utf8,detectEncodingFromByteOrderMarks:false,bufferSize:8192,leaveOpen:true);
            var counts=await classifier.ClassifyAsync(reader,token);
            var after=await session.ObserveCurrentPathAsync(token);
            if(before!=after)return new(pin.RelativePath,"changed_during_scan_not_sealed",null,null,null);
            var status=counts.PrivacyCategories.Count>0?"original_preserved; publication filtering required":"no flagged structured fields; still subject to final scope review";
            return new(pin.RelativePath,status,pin.Length,digest,counts);
        }
        catch(FileNotFoundException){throw new ReviewException("REVIEW_RECEIPT_MISSING",pin.RelativePath);}
    }
}
