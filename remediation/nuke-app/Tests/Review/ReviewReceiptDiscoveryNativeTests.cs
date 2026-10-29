using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;

public sealed class ReviewReceiptDiscoveryNativeTests
{
    [Theory,MemberData(nameof(ReviewReceiptDiscoveryCases.Data),MemberType=typeof(ReviewReceiptDiscoveryCases))]
    public async Task Same_scope_packets_bind_to_actual_native_temp_tree_and_shared_collector(string name)
    {
        var f=await ReceiptDiscoveryTree.Create(name);var source=new CollectorTrackingStore(f.Source);var tree=new TrackingReceiptTree(new NativeArtifactWorkspace(f.Root));
        var collector=new ReviewReceiptCollector(new PinnedArtifactReader(source),f.Output,new CanonicalJson(),new ReviewClock(),new ReviewAssessService(ReviewFixture.Hash));
        var code="";var rounds="";
        try
        {
            var result=await new ReviewReceiptAuditWorkflow(tree,source,collector).AssessAsync();
            var body=JsonNode.Parse((await f.Output.ReadAsync(result.Audit.RelativePath)).Span)!;
            code="collected";rounds=string.Join(',',body["rounds"]!.AsArray().Select(x=>x!["round"]!.GetValue<string>()));
            if(name=="derived-priority")Assert.False(body["rounds"]![0]!["complete_derived_report"]!.GetValue<bool>());
        }
        catch(ReviewException error){code=error.Code;}
        var outputRetained=name!="output-conflict"||ReviewPrepareService.Utf8.GetString((await f.Output.ReadAsync("review-receipt-audit.json")).Span)=="retained old audit";
        ReviewReceiptDiscoveryCases.AssertContract(name,new(code,rounds,File.Exists(Path.Combine(f.OutputRoot,"review-receipt-audit.json"))?1:0,
            await f.Unchanged()&&outputRetained,source.Reads.Any(x=>x.Contains("unrelated",StringComparison.Ordinal)||x.Contains("PROFILE-MERGED",StringComparison.Ordinal)),false));
        Assert.All(tree.Calls,call=>Assert.False(call.Recursive));
        Assert.DoesNotContain(tree.Calls,call=>call.Path is not "reviews"&&!(call.Path?.StartsWith("reviews/round-",StringComparison.Ordinal)??false));
    }
}
public sealed class ReviewAssessCliNativeTests
{
    [Theory,MemberData(nameof(ReviewAssessCliCases.Data),MemberType=typeof(ReviewAssessCliCases))]
    public async Task Same_packets_run_ordinary_compiled_Review_Assess_without_provider(string name)
    {
        var f=await ReceiptDiscoveryTree.Create(name=="empty"?"missing-reviews":name=="malformed"?"malformed-request":name=="private-path"?"manifest-private":"single-round");
        var runtime=Path.Combine(f.Container,"nuke-runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var original=ProcessRunnerRealTests.Request(Path.Combine(f.Container,"cli"),0,"arguments",[]);
        var args=original.Arguments.TakeWhile(x=>x!="--internal-process-probe").Concat(new[]{"--target","Review","--root",runtime,"--operation",name=="unsupported-operation"?"Inventory":"Assess","--dotnet-host",Environment.ProcessPath!}).ToList();
        if(name!="missing-project")args.AddRange(["--project-root",f.Root]);if(name!="missing-output")args.AddRange(["--output-root",f.OutputRoot]);
        if(name=="extra-snapshot")args.AddRange(["--snapshot","not-a-receipt-request"]);
        var result=await new NativeProcessRunner(new SystemClock()).RunAsync(original with{Arguments=args});
        var output=await File.ReadAllTextAsync(result.StandardOutputPath);var error=await File.ReadAllTextAsync(result.StandardErrorPath);var combined=output+error;
        var code=name is "success" or "empty"?"collected":name=="malformed"?"INVALID_REVIEW_RECEIPT":name=="private-path"?"PRIVATE_PAYLOAD":"plan-rejected";
        if(name is "malformed" or "private-path")Assert.Contains(code,combined);
        var outputContract=true;
        if(name is "success" or "empty")
        {
            var persisted=await File.ReadAllTextAsync(Path.Combine(f.OutputRoot,"review-receipt-audit.json"));
            outputContract=output.Contains(persisted,StringComparison.Ordinal);
            Assert.Contains("Local original receipt integrity/metadata only",persisted);
        }
        ReviewAssessCliCases.AssertContract(name,new(code,result.ExitCode==0?0:1,File.Exists(Path.Combine(f.OutputRoot,"review-receipt-audit.json"))?1:0,await f.Unchanged(),false,outputContract));
        Assert.False(result.Cancelled);
    }
}
internal sealed class TrackingReceiptTree(IArtifactTreeReader inner):IArtifactTreeReader
{
    internal List<(string? Path,bool Recursive)> Calls {get;}=[];
    public Task<IReadOnlyList<ArtifactTreeEntry>> EnumerateAsync(string? relativeDirectory=null,bool recursive=true,CancellationToken token=default)
    {Calls.Add((relativeDirectory,recursive));return inner.EnumerateAsync(relativeDirectory,recursive,token);}
}
internal sealed class ReceiptDiscoveryTree
{
    internal string Container {get;}=Path.Combine(Path.GetTempPath(),"receipt-discovery",Guid.NewGuid().ToString("N"));
    internal string Root=>Path.Combine(Container,"project");internal string OutputRoot=>Path.Combine(Container,"output");
    internal FileArtifactStore Source {get;}internal FileArtifactStore Output {get;}
    private readonly Dictionary<string,byte[]> originals=new(StringComparer.Ordinal);
    private ReceiptDiscoveryTree(){Directory.CreateDirectory(Root);Source=new(Root,ReviewFixture.Hash);Output=new(OutputRoot,ReviewFixture.Hash);}
    internal async Task<bool> Unchanged()
    {foreach(var item in originals){if(item.Key.Contains("PROFILE-MERGED",StringComparison.Ordinal))continue;if(!(await Source.ReadAsync(item.Key)).Span.SequenceEqual(item.Value))return false;}return true;}
    internal static async Task<ReceiptDiscoveryTree> Create(string name)
    {
        var result=new ReceiptDiscoveryTree();
        async Task Put(string path,byte[] bytes){await result.Source.WriteAsync(new(path,bytes,"application/octet-stream"));result.originals[path]=bytes;}
        if(name=="missing-reviews")return result;
        Directory.CreateDirectory(Path.Combine(result.Root,"reviews"));
        if(name=="no-rounds")return result;
        if(name=="round-is-file"){await Put("reviews/round-file",Encoding.UTF8.GetBytes("not a directory"));return result;}
        if(name=="round-is-link")
        {var target=Path.Combine(result.Container,"unrelated-target");Directory.CreateDirectory(target);Directory.CreateSymbolicLink(Path.Combine(result.Root,"reviews/round-link"),target);return result;}
        var fixture=new ReceiptCollectorFixture(name=="sorted-rounds"?"batch-order":name=="derived-priority"?name:name=="malformed-request"?"invalid-request":name=="manifest-escape"?"invalid-input-path":name=="manifest-private"?"private-input-path":"complete");
        foreach(var item in fixture.Values.Where(x=>x.Key.StartsWith("reviews/",StringComparison.Ordinal)))
        {
            if(name is "required-missing" or "receipt-is-link"&&item.Key=="reviews/round-001/request.jsonl")continue;
            var bytes=item.Value;
            if(name=="private-request-content"&&item.Key.EndsWith("/request.jsonl",StringComparison.Ordinal))
            {
                var body=JsonNode.Parse(bytes)!;const string secretShape="apiKey: synthetic-test-fixture-only";
                body["prompt"]=secretShape;body["prompt_sha256"]=ReviewFixture.Hash(Encoding.UTF8.GetBytes(secretShape));bytes=Encoding.UTF8.GetBytes(body.ToJsonString());
            }
            await Put(item.Key,bytes);
        }
        if(name=="receipt-is-link")
        {
            var path="reviews/round-001/request.jsonl";
            var target=Path.Combine(result.Container,"owned-link-target.json");await File.WriteAllTextAsync(target,"test-owned unread link target");File.CreateSymbolicLink(Path.Combine(result.Root,path),target);
        }
        if(name=="ignore-unrelated")await Put("reviews/unrelated/must-not-read.json",Encoding.UTF8.GetBytes("unrelated synthetic data"));
        if(name=="ignore-nested")await Put("reviews/round-001/nested/round-extra/request.jsonl",Encoding.UTF8.GetBytes("not a selected direct round"));
        if(name=="ignore-private-file")await Put("reviews/round-001/private/PROFILE-MERGED.md",Encoding.UTF8.GetBytes("synthetic fixture only"));
        if(name=="output-conflict")await result.Output.WriteAsync(new("review-receipt-audit.json",Encoding.UTF8.GetBytes("retained old audit"),"application/json"));
        return result;
    }
}
