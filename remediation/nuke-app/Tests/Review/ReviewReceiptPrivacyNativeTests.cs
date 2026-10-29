using System.Globalization;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewReceiptPrivacyNativeTests
{
    [Theory,MemberData(nameof(ReviewReceiptPrivacyCases.Data),MemberType=typeof(ReviewReceiptPrivacyCases))]
    public async Task Same_original_and_native_JSON_packets_bind_to_actual_classifier(string id)
    {
        var fixture=ReviewReceiptPrivacyCases.Get(id);var retained=fixture.Bytes.ToArray();var error="";ReviewReceiptPrivacyCounts? result=null;
        try{result=await new ReviewReceiptPrivacyClassifier(new CanonicalJson()).ClassifyAsync(fixture.Bytes);}
        catch(ReviewException failure){error=failure.Code;}
        ReviewReceiptPrivacyCases.AssertContract(id,new(error,result?.Events??0,result?.InvalidJsonLines??0,PrivacyNativeFacts.Categories(result),fixture.Bytes.SequenceEqual(retained),PrivacyNativeFacts.Exposes(result)));
    }
}
public sealed class ReviewReceiptPrivacySelectionNativeTests
{
    [Theory,MemberData(nameof(ReviewReceiptPrivacySelectionCases.Data),MemberType=typeof(ReviewReceiptPrivacySelectionCases))]
    public async Task Same_selected_packets_bind_to_native_stream_store_hash_and_metadata(string id)
    {
        var root=Path.Combine(Path.GetTempPath(),"review-privacy",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var calls=new NativeArtifactCalls(new NativeArtifactKernel());
        var store=new FileArtifactStore(root,ReviewFixture.Hash,ContentHash.Sha256,new ArtifactReadOpener(calls,new NativeArtifactIdentityReader(calls)));
        var body=Encoding.UTF8.GetBytes(id=="stable-unflagged"?"{}":"{\"type\":\"thinking\",\"text\":\"PRIVATE_FIXTURE_VALUE\"}");
        var pin=await store.WriteAsync(new("reviews/round-owned/request.jsonl",body,"application/x-ndjson"));
        var original=await store.WriteAsync(new("retained/original.jsonl",body,"application/x-ndjson"));
        Assert.Equal(pin.Sha256,original.Sha256);
        if(id=="private-path")pin=pin with{RelativePath="reviews/PROFILE-MERGED.md/request.jsonl"};
        if(id=="invalid-path")pin=pin with{RelativePath="reviews/round-owned/unrelated.bin"};
        if(id=="wrong-pin-hash")pin=pin with{Sha256=new string('0',64)};
        if(id=="wrong-pin-length")pin=pin with{Length=pin.Length+1};
        var source=new PrivacySelectedReader(store,root,id);var observer=new ReviewReceiptPrivacyObserver(source,new(new CanonicalJson()),ContentHash.Sha256);
        using var token=new CancellationTokenSource();if(id=="cancellation")token.Cancel();
        var code="";ReviewReceiptPrivacyObservation? result=null;
        try{result=await observer.ObserveAsync(pin,token.Token);code=result.Status;}
        catch(ReviewException error){code=error.Code;}
        catch(PinnedArtifactException error){code=error.Code;}
        catch(OperationCanceledException){code="cancelled";}
        catch(PlatformNotSupportedException){code="unsupported";}
        var retained=(await store.ReadAsync(original.RelativePath)).Span.SequenceEqual(body);
        ReviewReceiptPrivacySelectionCases.AssertContract(id,new(code,result?.Sha256 is not null&&result.Bytes is not null,result?.Counts is not null,source.Opens,source.Observations,retained,PrivacyNativeFacts.Exposes(result)));
        if(source.Session is not null)Assert.True(source.Session.Disposed);
        if(id is "stable" or "stable-unflagged")
        {
            Assert.Equal(original.Sha256,result!.Sha256);Assert.Equal(body.LongLength,result.Bytes);
            Assert.Equal(1,result.Counts!.Events);Assert.Equal(0,result.Counts.InvalidJsonLines);
            Assert.Equal(id=="stable"?"hidden_reasoning_block=1":"",PrivacyNativeFacts.Categories(result.Counts));
        }
        if(id!="size-change")Assert.Equal(body,(await store.ReadAsync("reviews/round-owned/request.jsonl")).ToArray());
        Assert.Equal(original.Sha256,ContentHash.Sha256((await store.ReadAsync(original.RelativePath)).Span));
    }
}
internal static class PrivacyNativeFacts
{
    internal static string Categories(ReviewReceiptPrivacyCounts? result)=>result is null?"":string.Join('|',result.PrivacyCategories.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>x.Key+"="+x.Value.ToString(CultureInfo.InvariantCulture)));
    internal static bool Exposes(object? result)
    {
        var serialized=JsonSerializer.Serialize(result);
        return new[]{"PRIVATE_FIXTURE_VALUE","fixture.test","ABCDEFGHIJKLMNOPQRST"}.Any(x=>serialized.Contains(x,StringComparison.Ordinal));
    }
}
internal sealed class PrivacySelectedReader(IStreamingArtifactReader inner,string root,string id):IStreamingArtifactReader
{
    internal int Opens {get;private set;}internal int Observations {get;private set;}internal PrivacySelectedSession? Session {get;private set;}
    public Task<ArtifactReadSnapshot?> InspectAsync(string path,CancellationToken token=default)=>throw new InvalidOperationException("Selected observer must use one admitted native session.");
    public async Task<IArtifactReadSession> OpenReadAsync(string path,CancellationToken token=default)
    {
        Opens++;
        if(id=="missing-before")throw new FileNotFoundException("Synthetic selected receipt absent.",path);
        if(id=="unsupported-metadata")throw new PlatformNotSupportedException("Synthetic unsupported identity authority.");
        Session=new(await inner.OpenReadAsync(path,token),this,Path.Combine(root,"reviews/round-owned/request.jsonl"),id);return Session;
    }
    internal sealed class PrivacySelectedSession(IArtifactReadSession inner,PrivacySelectedReader owner,string physical,string id):IArtifactReadSession
    {
        internal bool Disposed {get;private set;}
        public Stream Content=>inner.Content;
        public ArtifactReadSnapshot Opened {get{owner.Observations++;return inner.Opened;}}
        public async Task<ArtifactReadSnapshot> ObserveCurrentPathAsync(CancellationToken token=default)
        {
            owner.Observations++;
            if(id=="missing-after")throw new FileNotFoundException("Synthetic missing current-path observation.",physical);
            // External mutation of test-owned live fixtures only. The exact
            // pre-mutation bytes are separately retained and hash-verified.
            if(id=="mtime-change")File.SetLastWriteTimeUtc(physical,DateTime.UnixEpoch.AddDays(3));
            if(id=="size-change")await File.AppendAllTextAsync(physical,"\n",Encoding.UTF8,token);
            var observed=await inner.ObserveCurrentPathAsync(token);
            // Controlled metadata fault over a real native open/read; this is
            // not a claim that an actual inode was replaced or a Windows run.
            if(id=="identity-change")observed=observed with{Native=observed.Native with{FileIdLow=observed.Native.FileIdLow+1}};
            return observed;
        }
        public async ValueTask DisposeAsync(){Disposed=true;await inner.DisposeAsync();}
    }
}
