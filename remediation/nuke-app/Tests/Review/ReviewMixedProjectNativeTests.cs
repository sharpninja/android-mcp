using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Review;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Review;
public sealed class ReviewMixedProjectNativeTests
{
    [Theory,MemberData(nameof(ReviewMixedProjectCases.Data),MemberType=typeof(ReviewMixedProjectCases))]
    public async Task Same_mixed_role_and_shared_full_TRX_cases_bind_to_existing_sources_and_prepare(string name)
    {
        var store=new ReviewMemoryStore();var runner=new ReviewFakeRunner();var hash=ReviewFixture.Hash;
        var head=new string('a',40);const string project="Tests.csproj";
        var definitions=new Dictionary<string,byte[]>{{"Tests.csproj",Encoding.UTF8.GetBytes("<Project><IsTestProject>true</IsTestProject><AndroidMcpTestKind>mixed</AndroidMcpTestKind></Project>")},
            {"app.csproj",Encoding.UTF8.GetBytes("<Project><IsTestProject>false</IsTestProject></Project>")},{"design/build.gradle.kts",Encoding.UTF8.GetBytes("plugins { base }")}};
        runner.Replies.Enqueue(Encoding.UTF8.GetBytes(head+"\n"));
        runner.Replies.Enqueue(Encoding.UTF8.GetBytes(string.Join('\0',definitions.Keys)+"\0"));
        foreach(var pair in definitions.OrderBy(x=>x.Key,StringComparer.Ordinal))runner.Replies.Enqueue(pair.Value);
        var evidence=new List<ReviewEvidence>();
        void Evidence(string path,byte[] body,string kind,string? testProject=null)
        {store.Values[path]=body;evidence.Add(new(path,hash(body),kind,testProject,"full-run","fixture-host",head,"NUKE Validate Tests"));}
        Evidence("design.md",Encoding.UTF8.GetBytes("Complete design"),"design");
        Evidence("full.trx",Encoding.UTF8.GetBytes("<TestRun><ResultSummary><Counters total='1' passed='1' failed='0' notExecuted='0'/></ResultSummary><Results><UnitTestResult outcome='Passed'/></Results></TestRun>"),"unit-trx",project);
        var requirements=JsonSerializer.SerializeToNode(new{head,source="required-mcp-plugin",plugin="mcpserver-codex-plugin",pluginVersion="fixture",workspace="/workspace",
            functional=new[]{new{id="FR-BUILD-001",acceptanceCriteria=new[]{new{id="ac-1",text="Complete native mixed-project evidence"}}}},
            technical=new[]{new{id="TR-BUILD-TOOL-001"}},testing=new[]{new{id="TEST-BUILD-001"}},
            mappings=new[]{new{frId="FR-BUILD-001",trIds=new[]{"TR-BUILD-TOOL-001"},testIds=new[]{"TEST-BUILD-001"},criteria=new[]{new{acId="ac-1",testIds=new[]{"TEST-BUILD-001"}}}}}})!;
        Evidence("requirements.json",Encoding.UTF8.GetBytes(requirements.ToJsonString()),"requirements");
        var full=evidence.Single(x=>x.Kind=="unit-trx");
        var evaluation=JsonSerializer.SerializeToNode(new{head,source="native-project-evaluation",toolSha256=new string('b',64),
            projects=definitions.Select(pair=>new{path=pair.Key,definitionSha256=hash(pair.Value),isTestProject=pair.Key==project,kind=pair.Key==project?"mixed":"none"}),
            fullUnfilteredTrx=new[]{new{project,path=full.Path,sha256=full.Sha256,head,runId=full.RunId,host=full.Host,command=full.Command,filter=(string?)null}}})!.AsObject();
        var projects=evaluation["projects"]!.AsArray();var mixed=projects.Single(x=>x!["path"]!.GetValue<string>()==project)!;
        switch(name)
        {
            case "mixed-filtered-run":evaluation["fullUnfilteredTrx"]![0]!["filter"]="FullyQualifiedName~one";break;
            case "mixed-missing-run":evaluation.Remove("fullUnfilteredTrx");break;
            case "mixed-wrong-trx-pin":evaluation["fullUnfilteredTrx"]![0]!["sha256"]=new string('0',64);break;
            case "mixed-wrong-run-metadata":evaluation["fullUnfilteredTrx"]![0]!["command"]="different";break;
            case "mixed-collapsed-unit":mixed["kind"]="unit";break;
            case "duplicate-project-row":projects.Add(mixed.DeepClone());break;
            case "unknown-role":mixed["kind"]="unknown";break;
            case "mixed-nontest-contradiction":mixed["isTestProject"]=false;break;
        }
        ReviewEvidence Put(string path,JsonNode body)
        {var bytes=Encoding.UTF8.GetBytes(body.ToJsonString());store.Values[path]=bytes;return new(path,hash(bytes),"nuke");}
        var evaluationPin=Put("evaluation.json",evaluation);var req=evidence.Single(x=>x.Kind=="requirements");
        var checks=GitReviewPrerequisiteSource.RequiredChecks.Select(code=>
        {var path="checks/"+code+".txt";store.Values[path]=Encoding.UTF8.GetBytes(head+":"+code);return new{code,path,sha256=hash(store.Values[path])};}).ToArray();
        var checklistPin=Put("checklist.json",JsonSerializer.SerializeToNode(new{head,scope="design-and-nuke-only",scopeIntegrationProjects=new[]{project},checks})!);
        var snapshot=new ReviewSnapshot("mixed", "Codex-parent","req-parent",head,"https://github.com/owner/repo/pull/1",[project],[project],
            ["FR-BUILD-001","TR-BUILD-TOOL-001","TEST-BUILD-001"],evidence);
        var source=new GitReviewPrerequisiteSource(runner,new ReviewCaptureReader(runner),store,new CanonicalJson(hash),hash,
            new("/approved/git",new string('c',64),"fixture"),"/workspace","/private/git",
            new("/workspace","mcpserver-codex-plugin","fixture",evaluationPin,req,checklistPin,[project]));
        var ready=new CountedReady(head);var code="prepared";PreparedReview? result=null;
        try{result=await new ReviewPrepareService(store,new ReviewLease(),new ReviewClock(),new CanonicalJson(hash),hash,ready,source).PrepareAsync(snapshot);}
        catch(ReviewException error){code=error.Code;}
        ReviewMixedProjectCases.AssertContract(name,new(code,result is null?"":string.Join(',',result.Prerequisites!.UnitProjects),
            result is null?"":string.Join(',',result.Prerequisites!.IntegrationProjects),evidence.Count(x=>x.Kind.EndsWith("-trx",StringComparison.Ordinal)),ready.Calls,runner.Calls.Count,0));
        if(result is not null)Assert.Equal(store.Values[full.Path],store.Values["reviews/mixed/input/"+full.Path]);
    }
    private sealed class CountedReady(string head):IReviewReadinessSource
    {
        public int Calls{get;private set;}
        public Task<ReviewReadiness> ObserveAsync(string url,CancellationToken cancellationToken=default)
        {Calls++;return Task.FromResult(new ReviewReadiness(head,false,true,url,ReviewFixture.Json(new{head})));}
    }
}
