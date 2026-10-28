using System.Globalization;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation.Browser;

internal sealed class BrowserSession:IAsyncDisposable
{
    private readonly CdpClient client;private readonly IProcessSession process;private readonly string pageSession;
    private readonly CancellationTokenSource eventsLifetime=new();private readonly object loadsLock=new();
    private readonly HashSet<string> loaded=[];private readonly Dictionary<string,TaskCompletionSource> loadWaiters=[];
    private readonly List<string> external=[];private readonly Task events;private bool closing,closed;private int disposed;
    internal ProcessRequest Launch {get;}internal ProcessResult? Exit {get;private set;}internal bool Stopped {get;private set;}internal Exception? EventsError {get;private set;}
    internal bool CloseAcknowledged {get;private set;}internal bool ExpectedShutdownTransportEnd {get;private set;}
    internal string? TransportErrorCode=>(EventsError as WebSocketException)?.WebSocketErrorCode.ToString();
    internal IReadOnlyList<StoredArtifact> Protocol=>client.Transcript;
    internal IReadOnlyList<string> ExternalRequests{get{lock(external)return external.ToArray();}}
    private BrowserSession(CdpClient client,IProcessSession process,string pageSession,ProcessRequest launch)
    {this.client=client;this.process=process;this.pageSession=pageSession;Launch=launch;events=PumpEventsAsync();}
    internal static async Task<BrowserSession> OpenAsync(BrowserQaRequest request,IProcessSessionFactory processes,IArtifactStore store,IClock clock,CancellationToken token)
    {
        var profile=Path.Combine(request.OutputRoot,"profile");if(Directory.Exists(profile)&&Directory.EnumerateFileSystemEntries(profile).Any())throw new IOException("Browser profile must be fresh.");Directory.CreateDirectory(profile);
        var args=new[]{"--headless=new","--remote-debugging-address=127.0.0.1","--remote-debugging-port=0","--user-data-dir="+profile,"--disable-background-networking","--disable-component-update","--disable-extensions","--no-first-run","--no-default-browser-check","--disable-sync","about:blank"};
        var launch=new ProcessRequest(request.Browser,args,request.OutputRoot,new Dictionary<string,string>(),Path.Combine(request.OutputRoot,"process","browser"),ClearEnvironment:true);
        await store.WriteAsync(new("browser-launch.json",new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(launch)),"application/json"),token);
        var process=await processes.StartAsync(launch,token);CdpClient? client=null;BrowserSession? browser=null;
        try
        {
            await store.WriteAsync(new("browser-process.json",new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(new{pid=process.ProcessId,stdout=process.StandardOutputPath,stderr=process.StandardErrorPath})),"application/json"),token);
            var endpointFile=Path.Combine(profile,"DevToolsActivePort");string[] lines=[];
            while(true)
            {
                token.ThrowIfCancellationRequested();
                if(process.Completion.IsCompleted){var exit=await process.Completion;throw new IOException("Chromium exited before its owned CDP endpoint: "+exit.ExitCode.ToString(CultureInfo.InvariantCulture)+". Captures: "+exit.StandardErrorPath);}
                if(File.Exists(endpointFile)){lines=await File.ReadAllLinesAsync(endpointFile,token);if(lines.Length>=2&&lines[1].Length>0)break;}
                await Task.WhenAny(Task.Delay(10,token),process.Completion);
            }
            if(!int.TryParse(lines[0],NumberStyles.None,CultureInfo.InvariantCulture,out var port)||port is <=0 or >65535||!lines[1].StartsWith("/devtools/browser/",StringComparison.Ordinal))throw new IOException("Malformed owned Chromium CDP endpoint.");
            var endpoint=new Uri("ws://127.0.0.1:"+port.ToString(CultureInfo.InvariantCulture)+lines[1]);
            client=new CdpClient(await NativeCdpTransport.ConnectAsync(endpoint,token),store,clock);
            var target=(await client.CallAsync("Target.createTarget",new(){["url"]="about:blank"},cancellationToken:token))["targetId"]!.GetValue<string>();
            await client.CallAsync("Target.activateTarget",new(){["targetId"]=target},cancellationToken:token);
            var session=(await client.CallAsync("Target.attachToTarget",new(){["targetId"]=target,["flatten"]=true},cancellationToken:token))["sessionId"]!.GetValue<string>();
            browser=new BrowserSession(client,process,session,launch);
            await browser.CallAsync("Page.enable",null,token);await browser.CallAsync("Runtime.enable",null,token);
            await browser.CallAsync("Page.setLifecycleEventsEnabled",new(){["enabled"]=true},token);
            await browser.CallAsync("Fetch.enable",new(){["patterns"]=new JsonArray(new JsonObject{["urlPattern"]="*",["requestStage"]="Request"})},token);
            await browser.ViewportAsync(request.ViewportWidth,request.ViewportHeight,token);return browser;
        }
        catch{if(browser is not null)await browser.DisposeAsync();else{if(client is not null)await client.DisposeAsync();await process.StopAsync();await process.DisposeAsync();}throw;}
    }
    private async Task PumpEventsAsync()
    {
        try
        {
            await foreach(var item in client.EventsAsync(eventsLifetime.Token))
            {
                if(item.SessionId!=pageSession)continue;
                if(item.Method=="Fetch.requestPaused")
                {
                    var url=item.Parameters["request"]!["url"]!.GetValue<string>();var id=item.Parameters["requestId"]!.GetValue<string>();
                    var allowed=Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme is "file" or "data" or "blob";
                    if(!allowed){lock(external)external.Add(url);await CallAsync("Fetch.failRequest",new(){["requestId"]=id,["errorReason"]="BlockedByClient"},eventsLifetime.Token);}
                    else await CallAsync("Fetch.continueRequest",new(){["requestId"]=id},eventsLifetime.Token);
                }
                else if(item.Method=="Page.lifecycleEvent"&&item.Parameters["name"]?.GetValue<string>()=="load")
                {
                    var loader=item.Parameters["loaderId"]!.GetValue<string>();lock(loadsLock){loaded.Add(loader);if(loadWaiters.Remove(loader,out var waiter))waiter.TrySetResult();}
                }
            }
        }
        catch(OperationCanceledException)when(eventsLifetime.IsCancellationRequested){}
        catch(CdpDisconnectedException)when(closing){}
        catch(Exception error){EventsError=error;lock(loadsLock){foreach(var waiter in loadWaiters.Values)waiter.TrySetException(error);loadWaiters.Clear();}}
    }
    internal Task<JsonObject> CallAsync(string method,JsonObject? parameters,CancellationToken token)=>client.CallAsync(method,parameters,pageSession,token);
    internal Task ViewportAsync(int width,int height,CancellationToken token)=>CallAsync("Emulation.setDeviceMetricsOverride",new(){["width"]=width,["height"]=height,["deviceScaleFactor"]=1,["mobile"]=false},token);
    internal async Task NavigateAsync(string file,CancellationToken token)
    {
        var result=await CallAsync("Page.navigate",new(){["url"]=new Uri(Path.GetFullPath(file)).AbsoluteUri},token);
        if(result["errorText"] is not null)throw new IOException("Chromium navigation failed: "+result["errorText"]);
        var loader=result["loaderId"]?.GetValue<string>()??throw new IOException("Chromium navigation did not create a document loader.");Task wait;
        lock(loadsLock){if(loaded.Contains(loader))return;var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);loadWaiters.Add(loader,completion);wait=completion.Task;}
        var finished=await Task.WhenAny(wait,events).WaitAsync(token);await finished;
        if(finished==events)throw new IOException("CDP event stream ended before navigation completed.",EventsError);
    }
    internal async Task<JsonObject> EvaluateAsync(string action,JsonObject? values,CancellationToken token)
    {
        var result=await CallAsync("Runtime.evaluate",new(){["expression"]=BrowserDom.Expression(action,values),["awaitPromise"]=true,["returnByValue"]=true},token);
        if(result["exceptionDetails"] is not null)throw new IOException("Browser DOM inspection failed: "+result["exceptionDetails"]!.ToJsonString());
        return result["result"]!["value"]?.AsObject()??throw new IOException("Browser DOM inspection returned no object value.");
    }
    internal async Task KeyAsync(string key,int code,CancellationToken token)
    {foreach(var type in new[]{"keyDown","keyUp"})await CallAsync("Input.dispatchKeyEvent",new(){["type"]=type,["key"]=key,["code"]=key,["windowsVirtualKeyCode"]=code,["nativeVirtualKeyCode"]=code},token);}
    internal async Task<byte[]> ScreenshotAsync(bool full,CancellationToken token)
    {
        var options=new JsonObject{["format"]="png",["fromSurface"]=true,["captureBeyondViewport"]=full};
        if(full){var metrics=await CallAsync("Page.getLayoutMetrics",null,token);var size=metrics["cssContentSize"]??metrics["contentSize"]!;options["clip"]=new JsonObject{["x"]=0,["y"]=0,["width"]=size["width"]!.DeepClone(),["height"]=size["height"]!.DeepClone(),["scale"]=1};}
        return Convert.FromBase64String((await CallAsync("Page.captureScreenshot",options,token))["data"]!.GetValue<string>());
    }
    internal async Task CloseAsync(CancellationToken token)
    {
        if(closed)return;closing=true;
        await client.CallAsync("Browser.close",cancellationToken:token);CloseAcknowledged=true;
        Exit=await process.Completion.WaitAsync(token);await events.WaitAsync(token);
        ExpectedShutdownTransportEnd=EventsError is not null&&IsExpectedShutdownTransportEnd(EventsError,CloseAcknowledged,Exit);
        closed=true;eventsLifetime.Cancel();await client.DisposeAsync();if(EventsError is not null&&!ExpectedShutdownTransportEnd)throw new IOException("CDP event handling failed.",EventsError);
    }
    internal static bool IsExpectedShutdownTransportEnd(Exception error,bool acknowledged,ProcessResult exit)=>
        acknowledged&&exit.ExitCode==0&&!exit.Cancelled&&error is WebSocketException{WebSocketErrorCode:WebSocketError.ConnectionClosedPrematurely};
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        if(!closed){Stopped=true;closing=true;eventsLifetime.Cancel();await process.StopAsync();Exit=await process.Completion;await client.DisposeAsync();try{await events;}catch(OperationCanceledException){}catch(CdpDisconnectedException){}}
        await process.DisposeAsync();eventsLifetime.Dispose();
    }
}
