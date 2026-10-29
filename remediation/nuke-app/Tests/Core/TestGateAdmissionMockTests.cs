using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record TrxAdmissionObservation(TrxAdmissionRequest Request,IReadOnlyList<TrxAdmissionRequest> Calls,
    TrxAdmissionResult Result,IReadOnlyList<string> Effects);
public sealed class TrxAdmissionFixture
{
    public static readonly XNamespace Ns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
    public const string A="Fixture.Unit.AlphaTests",B="Fixture.Unit.BetaTests";
    public TrxAdmissionRequest Request { get; }
    public TrxAdmissionResult Expected { get; }
    public string Name { get; }
    public TrxAdmissionFixture(string name)
    {
        Name=name;var required=new[]{A,B};
        var root=new XElement(Ns+"TestRun",new XAttribute("id","11111111-1111-1111-1111-111111111111"),
            new XElement(Ns+"Results",Row("1","a"),Row("2","b")),
            new XElement(Ns+"TestDefinitions",Definition("a",A),Definition("b",B)),
            new XElement(Ns+"ResultSummary",new XAttribute("outcome","Completed"),new XElement(Ns+"Counters",
                new XAttribute("total",2),new XAttribute("executed",2),new XAttribute("passed",2),new XAttribute("failed",0),
                new XAttribute("notExecuted",0),new XAttribute("aborted",0),new XAttribute("inProgress",0))));
        var counters=root.Element(Ns+"ResultSummary")!.Element(Ns+"Counters")!;
        var rows=root.Element(Ns+"Results")!;
        var definitions=root.Element(Ns+"TestDefinitions")!;
        var code="OK";TrxRunCounters? counts=new(2,2,2,0,0);var resultCount=2;
        var classes=new Dictionary<string,int>{{A,1},{B,1}};string[] missing=[];
        switch(name)
        {
            case "pass-without-expected-classes":required=[];break;
            case "zero-discovered":rows.RemoveNodes();definitions.RemoveNodes();foreach(var key in new[]{"total","executed","passed"})counters.SetAttributeValue(key,0);counts=new(0,0,0,0,0);resultCount=0;classes.Clear();missing=[A,B];code="NO_TESTS";break;
            case "missing-trx":code="MISSING_TRX";counts=null;resultCount=0;classes.Clear();missing=[A,B];break;
            case "malformed-trx":case "prohibited-dtd":case "unknown-namespace":case "missing-summary":case "duplicate-summary":case "missing-counters":case "duplicate-counters":case "negative-counter":case "overflow-counter":case "noninteger-counter":
                code="INVALID_TRX";counts=null;resultCount=0;classes.Clear();missing=[A,B];break;
            case "aborted-summary":root.Element(Ns+"ResultSummary")!.SetAttributeValue("outcome","Aborted");code="RUN_ABORTED";break;
            case "aborted-counter":counters.SetAttributeValue("aborted",1);code="RUN_ABORTED";break;
            case "incomplete-run":counters.SetAttributeValue("inProgress",1);code="RUN_INCOMPLETE";break;
            case "failed-result":counters.SetAttributeValue("passed",1);counters.SetAttributeValue("failed",1);rows.Elements().Last().SetAttributeValue("outcome","Failed");counts=new(2,2,1,1,0);code="TESTS_FAILED";break;
            case "skipped-result":counters.SetAttributeValue("passed",1);counters.SetAttributeValue("notExecuted",1);counters.SetAttributeValue("executed",1);rows.Elements().Last().SetAttributeValue("outcome","NotExecuted");counts=new(2,1,1,0,1);code="TESTS_SKIPPED";break;
            case "counter-result-count-mismatch":counters.SetAttributeValue("total",3);counters.SetAttributeValue("executed",3);counters.SetAttributeValue("passed",3);counts=new(3,3,3,0,0);code="AGGREGATE_MISMATCH";break;
            case "passed-counter-mismatch":counters.SetAttributeValue("passed",1);counts=new(2,2,1,0,0);code="AGGREGATE_MISMATCH";break;
            case "result-outcome-counter-mismatch":rows.Elements().Last().SetAttributeValue("outcome","Failed");code="AGGREGATE_MISMATCH";break;
            case "missing-result-body":rows.RemoveNodes();resultCount=0;classes.Clear();missing=[A,B];code="AGGREGATE_MISMATCH";break;
            case "missing-expected-class":required=[A,"Fixture.Unit.MissingTests"];missing=["Fixture.Unit.MissingTests"];code="EXPECTED_CLASS_MISSING";break;
            case "substring-not-exact-class":required=["AlphaTests"];missing=["AlphaTests"];code="EXPECTED_CLASS_MISSING";break;
            case "class-case-is-exact":required=[A.ToLowerInvariant()];missing=[A.ToLowerInvariant()];code="EXPECTED_CLASS_MISSING";break;
            case "missing-definitions":definitions.Remove();classes.Clear();missing=[A,B];code="DEFINITION_MISMATCH";break;
            case "unresolved-test-id":rows.Elements().Last().SetAttributeValue("testId","absent");classes.Remove(B);missing=[B];code="DEFINITION_MISMATCH";break;
            case "duplicate-execution-id":rows.Elements().Last().SetAttributeValue("executionId","execution-1");code="DUPLICATE_RESULT";break;
        }
        switch(name)
        {
            case "unknown-namespace":foreach(var node in root.DescendantsAndSelf())node.Name=XNamespace.Get("urn:unknown")+node.Name.LocalName;break;
            case "missing-summary":root.Element(Ns+"ResultSummary")!.Remove();break;
            case "duplicate-summary":root.Add(new XElement(root.Element(Ns+"ResultSummary")!));break;
            case "missing-counters":counters.Remove();break;
            case "duplicate-counters":counters.Parent!.Add(new XElement(counters));break;
            case "negative-counter":counters.SetAttributeValue("total",-1);break;
            case "overflow-counter":counters.SetAttributeValue("total","999999999999999999999999");break;
            case "noninteger-counter":counters.SetAttributeValue("total","two");break;
        }
        var document=new XDocument(root);
        if(name=="prohibited-dtd")document.AddFirst(new XDocumentType("TestRun",null,"file:///must-never-be-opened.dtd",null));
        byte[]? bytes=Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
        if(name=="malformed-trx")bytes=bytes[..^1];
        if(name=="missing-trx")bytes=null;
        Request=new(bytes is null ? (ReadOnlyMemory<byte>?)null : new ReadOnlyMemory<byte>(bytes),required);
        Expected=new(code=="OK",code,counts,resultCount,classes,missing,bytes is null ? null : ContentHash.Sha256(bytes));
    }
    private static XElement Row(string execution,string id)=>new(Ns+"UnitTestResult",new XAttribute("executionId","execution-"+execution),new XAttribute("testId",id),new XAttribute("testName",id),new XAttribute("outcome","Passed"));
    private static XElement Definition(string id,string className)=>new(Ns+"UnitTest",new XAttribute("id",id),new XAttribute("name",id),new XElement(Ns+"TestMethod",new XAttribute("className",className),new XAttribute("name","Method")));
    public TrxAdmissionObservation Observation()=>new(Request,[Request],Expected,[]);
}
public sealed class ScriptedTrxAdmission(TrxAdmissionFixture fixture):ITrxResultAdmission
{
    public List<TrxAdmissionRequest> Calls { get; }=[];
    public TrxAdmissionResult Evaluate(TrxAdmissionRequest request){Calls.Add(request);return fixture.Expected;}
}
public sealed record TestBuildObservation(TestBuildPlanRequest Request,TestBuildPlan Plan,TestBuildEvidence Evidence,
    IReadOnlyList<TestBuildPlanRequest> PlanCalls,IReadOnlyList<TestBuildEvidence> EvidenceCalls,TestBuildAdmission Result,IReadOnlyList<string> Effects);
public sealed class TestBuildFixture
{
    public TestBuildPlanRequest Request { get; }=new("/fixture/app/App.csproj","/fixture/app/Tests/Tests.csproj",new("/fixture/dotnet",new string('d',64),"10.0"));
    public TestBuildPlan Plan { get; }
    public TestBuildEvidence Evidence { get; }
    public TestBuildAdmission Expected { get; }
    public TestBuildFixture(string name)
    {
        TestBuildStep[] steps=[new(TestBuildStepKind.ForceHostBuild,Request.HostProject,true,true,false,true),
            new(TestBuildStepKind.ForceTestsBuild,Request.TestsProject,true,true,false,false),new(TestBuildStepKind.TestWithoutBuild,Request.TestsProject,false,true,true,false)];
        Plan=new(Request.DotNet,steps);
        var fresh=Guid.Parse("11111111-1111-1111-1111-111111111111");
        Evidence=new(steps,new string('a',64),new string('a',64),new string('a',64),new string('b',64),fresh,fresh,new string('b',64),new string('c',64),new string('c',64));
        var code="OK";
        switch(name)
        {
            case "host-incremental-not-forced":Evidence=Evidence with { ExecutedSteps=steps.Select((x,i)=>i==0 ? x with { NoIncremental=false } : x).ToArray() };code="BUILD_PLAN_MISMATCH";break;
            case "tests-incremental-not-forced":Evidence=Evidence with { ExecutedSteps=steps.Select((x,i)=>i==1 ? x with { NoIncremental=false } : x).ToArray() };code="BUILD_PLAN_MISMATCH";break;
            case "wrong-build-order":Evidence=Evidence with { ExecutedSteps=[steps[1],steps[0],steps[2]] };code="BUILD_PLAN_MISMATCH";break;
            case "source-changed-after-build":Evidence=Evidence with { FinalSourceFingerprint=new string('e',64) };code="SOURCE_CHANGED";break;
            case "stale-built-source-with-preserved-mtime":Evidence=Evidence with { BuiltSourceFingerprint=new string('e',64) };code="SOURCE_CHANGED";break;
            case "stale-running-host":Evidence=Evidence with { RunningHostModuleId=Guid.Parse("22222222-2222-2222-2222-222222222222") };code="STALE_HOST";break;
            case "tests-reference-old-host":Evidence=Evidence with { TestsHostReferenceSha256=new string('e',64) };code="STALE_TEST_REFERENCE";break;
            case "executed-old-test-binary":Evidence=Evidence with { ExecutedTestsSha256=new string('e',64) };code="STALE_TEST_BINARY";break;
            case "missing-source-fingerprint":Evidence=Evidence with { CurrentSourceFingerprint="",BuiltSourceFingerprint="",FinalSourceFingerprint="" };code="INVALID_BUILD_EVIDENCE";break;
            case "missing-host-identity":Evidence=Evidence with { FreshHostSha256="",TestsHostReferenceSha256="",FreshHostModuleId=Guid.Empty,RunningHostModuleId=Guid.Empty };code="INVALID_BUILD_EVIDENCE";break;
            case "missing-tests-identity":Evidence=Evidence with { FreshTestsSha256="",ExecutedTestsSha256="" };code="INVALID_BUILD_EVIDENCE";break;
        }
        Expected=new(code=="OK",code);
    }
    public TestBuildObservation Observation()=>new(Request,Plan,Evidence,[Request],[Evidence],Expected,[]);
}
public sealed class ScriptedBuildAdmission(TestBuildFixture fixture):ITestBuildPlanAdmission
{
    public List<TestBuildPlanRequest> PlanCalls { get; }=[];public List<TestBuildEvidence> EvidenceCalls { get; }=[];
    public TestBuildPlan Plan(TestBuildPlanRequest request){PlanCalls.Add(request);return fixture.Plan;}
    public TestBuildAdmission Evaluate(TestBuildPlan plan,TestBuildEvidence evidence){EvidenceCalls.Add(evidence);return fixture.Expected;}
}
public static class TestGateAdmissionCases
{
    public static readonly string[] TrxNames=["pass-explicit-classes","pass-without-expected-classes","zero-discovered","missing-trx","malformed-trx","prohibited-dtd","unknown-namespace",
        "missing-summary","duplicate-summary","missing-counters","duplicate-counters","negative-counter","overflow-counter","noninteger-counter","aborted-summary","aborted-counter","incomplete-run",
        "failed-result","skipped-result","counter-result-count-mismatch","passed-counter-mismatch","result-outcome-counter-mismatch","missing-result-body","missing-expected-class","substring-not-exact-class",
        "class-case-is-exact","missing-definitions","unresolved-test-id","duplicate-execution-id"];
    public static readonly string[] BuildNames=["fresh-plan-and-source","host-incremental-not-forced","tests-incremental-not-forced","wrong-build-order","source-changed-after-build",
        "stale-built-source-with-preserved-mtime","stale-running-host","tests-reference-old-host","executed-old-test-binary","missing-source-fingerprint","missing-host-identity","missing-tests-identity"];
    public static IEnumerable<object[]> Data=>TrxNames.Select(x=>new object[]{"trx",x}).Concat(BuildNames.Select(x=>new object[]{"build",x}));
    public static JsonNode Expected(string kind,string name)=>JsonSerializer.SerializeToNode(kind=="trx" ? (object)new TrxAdmissionFixture(name).Observation() : new TestBuildFixture(name).Observation())!;
    public static JsonNode ExecuteMock(string kind,string name)
    {
        if(kind=="trx")
        {
            var fixture=new TrxAdmissionFixture(name);var service=new ScriptedTrxAdmission(fixture);var result=service.Evaluate(fixture.Request);
            return JsonSerializer.SerializeToNode(new TrxAdmissionObservation(fixture.Request,service.Calls,result,[]))!;
        }
        else
        {
            var fixture=new TestBuildFixture(name);var service=new ScriptedBuildAdmission(fixture);var plan=service.Plan(fixture.Request);var result=service.Evaluate(plan,fixture.Evidence);
            return JsonSerializer.SerializeToNode(new TestBuildObservation(fixture.Request,plan,fixture.Evidence,service.PlanCalls,service.EvidenceCalls,result,[]))!;
        }
    }
    public static void AssertContract(string kind,string name,JsonNode actual)=>Assert.Equal(
        new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(Expected(kind,name))),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
}
[Trait("Gate","Mock")]
public sealed class TestGateAdmissionMockTests
{
    [Theory,MemberData(nameof(TestGateAdmissionCases.Data),MemberType=typeof(TestGateAdmissionCases))]
    public void CorrectTypedMock(string kind,string name)=>TestGateAdmissionCases.AssertContract(kind,name,TestGateAdmissionCases.ExecuteMock(kind,name));
    public static IEnumerable<object[]> NegativeControls
    {
        get{foreach(var item in TestGateAdmissionCases.Data){var kind=(string)item[0];var name=(string)item[1];
            foreach(var leaf in ReviewOfficialTransportCases.Leaves(TestGateAdmissionCases.Expected(kind,name)))yield return[kind,name,leaf.Label,leaf.Path];}}
    }
    [Theory,MemberData(nameof(NegativeControls))]
    public void EachResultPlanEvidenceAndInteractionFieldRejectsItsMutation(string kind,string name,string field,string[] path)
    {
        Assert.False(string.IsNullOrWhiteSpace(field));var actual=TestGateAdmissionCases.ExecuteMock(kind,name);ReviewOfficialTransportCases.Mutate(actual,path);
        Assert.NotNull(Record.Exception(()=>TestGateAdmissionCases.AssertContract(kind,name,actual)));
    }
}
