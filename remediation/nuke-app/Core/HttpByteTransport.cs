using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Core;

/// <summary>The sole network byte boundary. No redirect, mutable fallback or auto decompression is configured.</summary>
public sealed class HttpByteTransport:IByteTransport
{
    private readonly HttpClient client;private readonly IClock? clock;private readonly ITransportDelay? delay;
    private readonly HttpByteTransportProfile profile;private readonly TimeProvider timeProvider;
    public HttpByteTransport(HttpClient client,IClock? clock=null,ITransportDelay? delay=null)
        :this(client,clock,delay,HttpByteTransportProfile.Default,TimeProvider.System){}
    public HttpByteTransport(HttpClient client,IClock? clock,ITransportDelay? delay,HttpByteTransportProfile profile,TimeProvider timeProvider)
    {
        if(!Enum.IsDefined(profile))throw new ArgumentOutOfRangeException(nameof(profile));
        this.client=client??throw new ArgumentNullException(nameof(client));this.clock=clock;this.delay=delay;
        this.profile=profile;this.timeProvider=timeProvider??throw new ArgumentNullException(nameof(timeProvider));
    }
    public static HttpByteTransport Create(IClock? clock=null,ITransportDelay? delay=null)=>new(new HttpClient(new HttpClientHandler
    {AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None}),clock,delay);
    public static HttpByteTransport Create(HttpByteTransportProfile profile,IClock? clock=null,ITransportDelay? delay=null,TimeProvider? timeProvider=null)
    {
        if(!Enum.IsDefined(profile))throw new ArgumentOutOfRangeException(nameof(profile));
        var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.None});
        if(profile!=HttpByteTransportProfile.Default)client.Timeout=Timeout.InfiniteTimeSpan;
        return new(client,clock,delay,profile,timeProvider??TimeProvider.System);
    }

    public async Task<TransportResponse> SendAsync(PinnedRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        var policy=request.Retry??TransportRetryPolicy.SingleAttempt;
        if(policy.Attempts is <1 or >3 || policy.MinimumDelay<TimeSpan.Zero)throw new ArgumentException("Invalid retry profile.");
        for(var attempt=0;attempt<policy.Attempts;attempt++)
        {
            if(policy.MinimumDelay>TimeSpan.Zero)await Wait(policy.MinimumDelay,cancellationToken);
            using var message=new HttpRequestMessage(new HttpMethod(request.Method),request.Url);
            message.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
            if(profile==HttpByteTransportProfile.DocumentationBootstrap)message.Headers.UserAgent.ParseAdd("Android-MCP-local-documentation-build");
            if(request.Range is { } range)message.Headers.Range=new RangeHeaderValue(range.Offset,checked(range.Offset+range.Length-1));
            if(request.IfMatch is not null)message.Headers.TryAddWithoutValidation("If-Match",request.IfMatch);
            using var response=await Blocking(token=>client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,token),cancellationToken);
            if(response.StatusCode==HttpStatusCode.TooManyRequests&&attempt+1<policy.Attempts)
            {
                var pause=TimeSpan.FromSeconds(60*(attempt+1));
                if(response.Headers.TryGetValues("Retry-After",out var values))
                {
                    var value=values.Single();TimeSpan headerPause;
                    if(double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var seconds)&&double.IsFinite(seconds))headerPause=TimeSpan.FromSeconds(Math.Max(0,seconds));
                    else if(DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out var date))headerPause=date-(clock?.Now??DateTimeOffset.UtcNow);
                    else throw new TransportException("RETRY_AFTER","Invalid Retry-After header.");
                    if(headerPause>pause)pause=headerPause;
                }
                await Wait(pause,cancellationToken);continue;
            }
            var status=(int)response.StatusCode;
            if(status is >=300 and <=399)
            {
                if(request.ToolTransfer is not { } authorization||response.Headers.Location is not { } location)throw new TransportException("REDIRECT","Pinned request redirect was not admitted.");
                var target=location.IsAbsoluteUri?location:new Uri(request.Url,location);
                if(target.Scheme!=Uri.UriSchemeHttps||target.UserInfo.Length!=0||authorization.AllowedRedirects?.Contains(target)!=true)throw new TransportException("REDIRECT","Tool redirect target was not admitted.");
                var remaining=authorization.AllowedRedirects.Where(x=>x!=target).ToArray();
                var redirected=await SendAsync(request with{Url=target,ToolTransfer=authorization with{AllowedRedirects=remaining}},cancellationToken);
                return redirected with{RequestChain=new[]{request.Url}.Concat(redirected.RequestChain??new[]{target}).ToArray()};
            }
            if(status is 401 or 403)throw new TransportException("AUTH_PAUSE","Pinned source access denied; acquisition paused.");
            if(status!=(request.Range is null?200:206))throw new TransportException("HTTP_STATUS","Pinned source returned an unexpected status.");
            var url=response.RequestMessage?.RequestUri??request.Url;
            if(url!=request.Url)throw new TransportException("REDIRECT","Pinned request URL changed.");
            var encoding=response.Content.Headers.ContentEncoding.Count==0?"identity":string.Join(",",response.Content.Headers.ContentEncoding);
            if(encoding!="identity")throw new TransportException("ENCODING","Pinned bytes require identity content encoding.");
            var tag=response.Headers.ETag?.ToString();
            if(request.ExpectedETag is not null&&tag!=request.ExpectedETag)throw new TransportException("ETAG","Pinned archive ETag changed.");
            var contentRange=response.Content.Headers.ContentRange?.ToString();
            if(request.Range is { } requested)
            {
                var parsed=response.Content.Headers.ContentRange;
                if(parsed is null||parsed.Unit!="bytes"||parsed.From!=requested.Offset||parsed.To!=checked(requested.Offset+requested.Length-1)||parsed.Length is null||parsed.Length<=parsed.To||request.ExpectedTotalLength is not null&&parsed.Length!=request.ExpectedTotalLength)
                    throw new TransportException("CONTENT_RANGE","Response range differs from pinned request.");
            }
            var declared=response.Content.Headers.ContentLength;
            if(declared>request.MaxBytes)throw new TransportException("BODY_LIMIT","Pinned response exceeds byte bound.");
            await using var input=await Blocking(token=>response.Content.ReadAsStreamAsync(token),cancellationToken);
            using var output=new MemoryStream();var buffer=new byte[64*1024];
            while(true){var count=await Blocking(token=>input.ReadAsync(buffer.AsMemory(),token).AsTask(),cancellationToken);if(count==0)break;if(output.Length+count>request.MaxBytes)throw new TransportException("BODY_LIMIT","Pinned response exceeds byte bound.");await output.WriteAsync(buffer.AsMemory(0,count),cancellationToken);}
            var bytes=output.ToArray();
            if(declared is not null&&bytes.LongLength!=declared)throw new TransportException("BODY_LENGTH","Response differs from declared length.");
            if(request.ExpectedLength is not null&&bytes.LongLength!=request.ExpectedLength)throw new TransportException("BODY_LENGTH","Response differs from pinned length.");
            if(request.Range is { } expected&&bytes.Length!=expected.Length)throw new TransportException("BODY_LENGTH","Response differs from requested range length.");
            if(request.ExpectedSha256 is not null&&ContentHash.Sha256(bytes)!=request.ExpectedSha256)throw new TransportException("BODY_HASH","Pinned response hash mismatch.");
            return new(url,status,bytes,tag,contentRange,encoding,declared,[request.Url]);
        }
        throw new TransportException("HTTP_STATUS","Pinned source retry exhausted.");
    }
    private async Task<T> Blocking<T>(Func<CancellationToken,Task<T>> operation,CancellationToken cancellationToken)
    {
        if(profile==HttpByteTransportProfile.Default)return await operation(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var budget=TimeSpan.FromSeconds(profile==HttpByteTransportProfile.SourceSnapshotFetch?45:60);
        using var timeout=new CancellationTokenSource(budget,timeProvider);
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeout.Token);
        try{return await operation(linked.Token);}
        catch(OperationCanceledException)when(timeout.IsCancellationRequested&&!cancellationToken.IsCancellationRequested)
        {throw new TransportException("TIMEOUT","Pinned source blocking operation exceeded its configured timeout.");}
    }
    private Task Wait(TimeSpan duration,CancellationToken cancellationToken)=>delay?.DelayAsync(duration,cancellationToken)??Task.Delay(duration,cancellationToken);
    private static void Validate(PinnedRequest request)
    {
        if(!request.Url.IsAbsoluteUri||request.Url.Scheme!=Uri.UriSchemeHttps||request.Url.UserInfo.Length!=0)throw new TransportException("URL","Only credential-free HTTPS source URLs are accepted.");
        if(request.Method!="GET"||request.ContentEncoding!="identity"||request.MaxBytes<0)throw new TransportException("REQUEST","Invalid bounded source request.");
        if(request.ToolTransfer is null&&request.MaxBytes>64L*1024*1024)throw new TransportException("REQUEST","Invalid bounded source request.");
        if(request.ToolTransfer is { } tool)
        {
            if(!Regex.IsMatch(tool.ArtifactSha256,"^[a-f0-9]{64}$")||tool.ExactArtifactBytes<0||tool.MaxDownloadBytes<tool.ExactArtifactBytes||tool.MaxDownloadBytes>512L*1024*1024||request.Range is not null||request.PartialMemberSha256 is not null||!Enum.IsDefined(tool.Kind)||
                tool.AllowedRedirects?.Any(x=>!x.IsAbsoluteUri||x.Scheme!=Uri.UriSchemeHttps||x.UserInfo.Length!=0)==true)
                throw new TransportException("TOOL_PIN","Invalid explicit tool transfer authorization.");
            if(tool.Kind==ToolTransferKind.Artifact&&(request.ExpectedSha256!=tool.ArtifactSha256||request.ExpectedLength!=tool.ExactArtifactBytes||request.MaxBytes!=tool.ExactArtifactBytes)||
                tool.Kind==ToolTransferKind.PublishedChecksum&&(request.ExpectedSha256 is not null||request.ExpectedLength is not null||request.MaxBytes is <=0 or >4096))throw new TransportException("TOOL_PIN","Tool request differs from its exact admitted pin.");
        }
        if(request.ExpectedSha256 is not null&&!Regex.IsMatch(request.ExpectedSha256,"^[a-f0-9]{64}$"))throw new TransportException("PIN","Invalid SHA256 pin.");
        var partialPin=request.Range is not null&&request.PartialMemberSha256 is not null&&Regex.IsMatch(request.PartialMemberSha256,"^[a-f0-9]{64}$");
        if(request.ExpectedSha256 is null&&request.ExpectedETag is null&&!partialPin&&request.ToolTransfer?.Kind!=ToolTransferKind.PublishedChecksum&&(!Regex.IsMatch(request.Revision,"^[a-f0-9]{40}$")||!request.Url.AbsolutePath.Split('/').Contains(request.Revision)))throw new TransportException("PIN","Source requires a hash, member pin for bounded metadata, validator or exact immutable commit URL.");
        if(request.Range is { } range&&(range.Offset<0||range.Length<=0||range.Length>8*1024*1024||range.Offset>long.MaxValue-range.Length))throw new TransportException("RANGE_LIMIT","Invalid bounded archive range.");
        if(request.ExpectedLength<0)throw new TransportException("PIN","Negative source length.");
    }
}
