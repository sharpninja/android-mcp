using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record PlatformFactsCase(string Id,string? RowJson,string? TransportJson,bool FactsNeeded,
    string? StringProjection=null,bool NativeOverride=true,bool GZip=false,bool Generic=false,bool Bounded=true,string? Error=null)
{ public override string ToString()=>Id; }
public static class RehydrationPlatformFactsCases
{
    public static IEnumerable<object[]> Cases=>new PlatformFactsCase[]{
        new("FACT-ROW-TRUE","true","\"36.0\"",true),new("FACT-ROW-FALSE","false","\"37.2\"",true),
        new("FACT-ROW-NUMBER","37.2","\"36.0\"",true),new("FACT-ROW-ZERO","0","\"37.2\"",true),
        new("FACT-ROW-NEGATIVE-ZERO","-0.0","\"37.2\"",true),new("FACT-ROW-EMPTY-ARRAY","[]","\"37.2\"",true),
        new("FACT-ROW-EMPTY-OBJECT","{}","false",true),new("FACT-ROW-ARRAY","[\"x\"]","true",true),
        new("FACT-ROW-OBJECT","{\"x\":1}","null",true),new("FACT-MISSING-ROW-NUMERIC-TRANSPORT",null,"37.2",true),
        new("FACT-NULL-ROW-NUMERIC-TRANSPORT","null","37.2",true),new("FACT-EMPTY-ROW-FALSE-TRANSPORT","\"\"","false",true,StringProjection:""),
        new("FACT-MISSING-ROW-NULL-TRANSPORT",null,"null",true),new("FACT-FALSE-ROW-MISSING-TRANSPORT","false",null,true),
        new("FACT-FALSE-ROW-NULL-TRANSPORT","false","null",true),new("FACT-STRING-DOMINATES-OBJECT","\"37.2\"","{\"ignored\":true}",false,"37.2"),
        new("FACT-WHITESPACE-DOMINATES-ARRAY","\" \"","[]",false," "),new("FACT-MISSING-ROW-STRING",null,"\"37.2\"",false,"37.2"),
        new("FACT-NULL-ROW-STRING","null","\"37.2\"",false,"37.2"),new("FACT-EMPTY-ROW-STRING","\"\"","\"37.2\"",false,"37.2"),
        new("FACT-BOTH-MISSING",null,null,false),new("FACT-OLD-ROW-REFUSAL","7","\"37.2\"",false,NativeOverride:false,Error:"INVALID_RECIPE"),
        new("FACT-GZIP","false","37.2",true,GZip:true),new("FACT-GENERIC-BIG-INTEGER",null,"9007199254740993123456789",true,Generic:true),
        new("FACT-UNRELATED-KIND","false","37.2",false,Bounded:false)
    }.Select(c=>new object[]{c});
    private static MetadataReferenceProjectionCase MetadataCase(PlatformFactsCase c)=>new(c.Id,GZip:c.GZip,
        Profile:c.Generic?AcquisitionCompatibilityProfile.GenericRehydrate:AcquisitionCompatibilityProfile.CanonicalRestore,Error:c.Error);
    public static RehydrationPlanRequest Request(PlatformFactsCase c)=>RehydrationMetadataReferenceCases.Request(MetadataCase(c),root=>{
        var row=root["inventory"]!["sources"]![0]!.AsObject();var transport=row["transport"]!.AsObject();
        void Set(JsonObject target,string key,string? raw){if(raw is null)target.Remove(key);else target[key]=JsonNode.Parse(raw);}
        Set(row,"platform_api_version",c.RowJson);Set(transport,"platform_api_version",c.TransportJson);
        if(!c.NativeOverride)transport.Remove("platform_version");
        if(!c.Bounded)transport["kind"]="sdk-zip-member";
    });
    public static RehydrationLegacyPlatformValue Fact(string? raw)
    {
        if(raw is null)return new(false,JsonValueKind.Undefined,null);
        // The existing Core authority owns numeric formatting. This fixture
        // does not implement scalar truth, Python str/repr, or another parser.
        using var document=JsonDocument.Parse(new CanonicalJson().Serialize(Encoding.UTF8.GetBytes(raw)));
        var clone=document.RootElement.Clone();return new(true,clone.ValueKind,clone);
    }
    public static RehydrationPlanResult Baseline(PlatformFactsCase c)=>
        RehydrationContractCases.Correct(new(c.Id,Request(c),c.Error,Rows:2,Unique:1,KnownBytes:3,UnknownSizes:0));
    public static RehydrationSeedPlanResult Seed(PlatformFactsCase c)=>
        new(Baseline(c),c.Error is not null?null:new("fixture-release",PublicationTestData.Hash(Request(c).Recipe.Bytes),[]));
    public static RehydrationSnapshotPlanResult Correct(PlatformFactsCase c)
    {
        var baseline=Baseline(c);if(c.Error is not null)return new(baseline,null);
        var old=RehydrationMetadataReferenceCases.Correct(MetadataCase(c)).SnapshotProjection!;
        var rows=old.Rows.ToArray();
        rows[0]=rows[0] with{MetadataReference=!c.Bounded?null:rows[0].MetadataReference! with{
            PlatformVersion=c.StringProjection,LegacyPlatformFacts=c.FactsNeeded?new(Fact(c.RowJson),Fact(c.TransportJson)):null}};
        return new(baseline,old with{RecipeSha256=PublicationTestData.Hash(Request(c).Recipe.Bytes),Rows=Array.AsReadOnly(rows)});
    }
    private static void AssertFact(RehydrationLegacyPlatformValue expected,RehydrationLegacyPlatformValue actual)
    {
        Assert.Equal(expected.Present,actual.Present);Assert.Equal(expected.Kind,actual.Kind);
        Assert.Equal(expected.Value.HasValue,actual.Value.HasValue);
        if(expected.Value is {} wanted){
            var received=Assert.IsType<JsonElement>(actual.Value);
            Assert.Equal(wanted.ValueKind,received.ValueKind);
            Assert.Equal(wanted.GetRawText(),received.GetRawText());
        }
    }
    public static void AssertCase(PlatformFactsCase c,ISnapshotProjectionHarness harness)
    {
        var request=Request(c);var bytes=request.Recipe.Bytes.ToArray();var pin=request.Lock.Bytes.ToArray();
        var baseline=harness.Planner.Plan(request);var seed=harness.SeedPlanner.PlanSeed(request);var count=harness.RecipeParseCalls;
        var result=harness.Planner.PlanSnapshot(request);Assert.Equal(1,harness.RecipeParseCalls-count);
        Assert.Equal(bytes,request.Recipe.Bytes.ToArray());Assert.Equal(pin,request.Lock.Bytes.ToArray());
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(baseline),JsonSerializer.SerializeToUtf8Bytes(result.Baseline));
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(seed),JsonSerializer.SerializeToUtf8Bytes(harness.SeedPlanner.PlanSeed(request)));
        Assert.Equal(c.Error is null,result.IsValid);Assert.Equal(c.Error is null?Array.Empty<string>():new[]{c.Error},result.Baseline.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(result.SnapshotProjection);return;}
        var expected=Correct(c).SnapshotProjection!;var actual=Assert.IsType<RehydrationSnapshotProjection>(result.SnapshotProjection);
        Assert.Equal(expected.Release,actual.Release);Assert.Equal(expected.RecipeSha256,actual.RecipeSha256);Assert.Equal(expected.Profile,actual.Profile);Assert.Equal(expected.Resume,actual.Resume);
        Assert.Equal(2,actual.Rows.Count);
        for(var i=0;i<2;i++){
            var e=expected.Rows[i];var a=actual.Rows[i];Assert.Equal(e.RowIndex,a.RowIndex);Assert.Equal(e.Pin,a.Pin);
            Assert.Equal(e.Status,a.Status);Assert.Equal(e.StatusPresent,a.StatusPresent);Assert.Equal(e.StatusKind,a.StatusKind);
            Assert.Equal(e.RequiredSnapshots,a.RequiredSnapshots);Assert.Equal(e.Prerequisites.Count,a.Prerequisites.Count);
            for(var j=0;j<e.Prerequisites.Count;j++){
                Assert.Equal(e.Prerequisites[j].Code,a.Prerequisites[j].Code);Assert.Equal(e.Prerequisites[j].Path,a.Prerequisites[j].Path);
                Assert.Equal(e.Prerequisites[j].Reason,a.Prerequisites[j].Reason);Assert.Equal(e.Prerequisites[j].SelectionRowIndices,a.Prerequisites[j].SelectionRowIndices);
            }
            if(e.MetadataReference is not {} reference){Assert.Null(a.MetadataReference);continue;}
            var received=Assert.IsType<RehydrationCanonicalMetadataReference>(a.MetadataReference);
            var wire=JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(received))!.AsObject();
            Assert.Equal(reference.LegacyPlatformFacts is not null,wire.Remove(nameof(RehydrationCanonicalMetadataReference.LegacyPlatformFacts)));
            Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(reference with{LegacyPlatformFacts=null}),JsonSerializer.SerializeToUtf8Bytes(wire));
            if(reference.LegacyPlatformFacts is {} facts){
                var receivedFacts=Assert.IsType<RehydrationLegacyPlatformFacts>(received.LegacyPlatformFacts);
                AssertFact(facts.Row,receivedFacts.Row);AssertFact(facts.Transport,receivedFacts.Transport);
            }else Assert.Null(received.LegacyPlatformFacts);
        }
        if(actual.Rows is IList<RehydrationSnapshotRow> rows)Assert.Throws<NotSupportedException>(()=>rows[0]=rows[1]);
    }
    private static JsonElement? DifferentValue(RehydrationLegacyPlatformValue fact)=>Fact(fact.Kind switch{
        JsonValueKind.String=>"\"wrong\"",JsonValueKind.Number=>"12345",JsonValueKind.True=>"false",JsonValueKind.False=>"true",
        JsonValueKind.Array=>"[\"different\"]",JsonValueKind.Object=>"{\"different\":true}",_=>"0"}).Value;
    private static JsonElement DisposedValue(RehydrationLegacyPlatformValue fact)
    {
        var raw=fact.Value?.GetRawText()??"null";using var document=JsonDocument.Parse(raw);return document.RootElement;
    }
    private static IEnumerable<RehydrationLegacyPlatformValue> WrongFact(RehydrationLegacyPlatformValue fact)
    {
        yield return fact with{Present=!fact.Present};yield return fact with{Kind=(JsonValueKind)99};
        yield return fact with{Value=fact.Value.HasValue?null:Fact("null").Value};
        yield return fact with{Value=DifferentValue(fact)};
        if(fact.Value.HasValue)yield return fact with{Value=DisposedValue(fact)};
        yield return Fact("\"38.0\""); // Other logical row, never this row's fact.
    }
    public static IEnumerable<RehydrationSnapshotPlanResult> Wrong(PlatformFactsCase c)
    {
        var good=Correct(c);yield return good with{Baseline=good.Baseline with{Issues=[new("WRONG","")]}};
        if(c.Error is not null){yield return new(new(null,[]),null);yield break;}
        var p=good.SnapshotProjection!;yield return good with{SnapshotProjection=null};yield return good with{SnapshotProjection=p with{RecipeSha256="wrong"}};
        yield return good with{SnapshotProjection=p with{Rows=p.Rows.ToArray()}};
        for(var i=0;i<2;i++){
            var row=p.Rows[i];var archive=row.Pin.Archive??throw new InvalidOperationException("Pinned fixture archive missing");
            var alternatives=new List<RehydrationSnapshotRow>{row with{RowIndex=99},row with{Pin=row.Pin with{Archive=archive with{PlatformVersion="changed"}}}};
            if(row.MetadataReference is {} reference){
                alternatives.Add(row with{MetadataReference=reference with{PlatformVersion="changed"}});
                if(reference.LegacyPlatformFacts is {} facts){
                    alternatives.Add(row with{MetadataReference=reference with{LegacyPlatformFacts=null}});
                    foreach(var wrong in WrongFact(facts.Row))alternatives.Add(row with{MetadataReference=reference with{LegacyPlatformFacts=facts with{Row=wrong}}});
                    foreach(var wrong in WrongFact(facts.Transport))alternatives.Add(row with{MetadataReference=reference with{LegacyPlatformFacts=facts with{Transport=wrong}}});
                }else alternatives.Add(row with{MetadataReference=reference with{LegacyPlatformFacts=new(Fact(null),Fact(null))}});
            }else alternatives.Add(row with{MetadataReference=RehydrationMetadataReferenceCases.Reference(MetadataCase(c),i)});
            var index=i;foreach(var alternative in alternatives)yield return good with{SnapshotProjection=p with{Rows=Array.AsReadOnly(p.Rows.Select((x,k)=>k==index?alternative:x).ToArray())}};
        }
    }
}
public sealed class ScriptedPlatformFactsHarness(PlatformFactsCase c,RehydrationSnapshotPlanResult? wrong=null,bool doubleParse=false)
    :ISnapshotProjectionHarness,IRehydrationSnapshotPlanner,IRehydrationSeedPlanner
{
    public IRehydrationSnapshotPlanner Planner=>this;public IRehydrationSeedPlanner SeedPlanner=>this;public int RecipeParseCalls{get;private set;}
    public RehydrationPlanResult Plan(RehydrationPlanRequest request){RecipeParseCalls++;return RehydrationPlatformFactsCases.Baseline(c);}
    public RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request){RecipeParseCalls++;return RehydrationPlatformFactsCases.Seed(c);}
    public RehydrationSnapshotPlanResult PlanSnapshot(RehydrationPlanRequest request){RecipeParseCalls+=doubleParse?2:1;return wrong??RehydrationPlatformFactsCases.Correct(c);}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationPlatformFactsMockTests
{
    [Theory,MemberData(nameof(RehydrationPlatformFactsCases.Cases),MemberType=typeof(RehydrationPlatformFactsCases))]
    public void CorrectDetachedFactsPreservePriorWires(PlatformFactsCase c)=>RehydrationPlatformFactsCases.AssertCase(c,new ScriptedPlatformFactsHarness(c));
    [Theory,MemberData(nameof(RehydrationPlatformFactsCases.Cases),MemberType=typeof(RehydrationPlatformFactsCases))]
    public void IndividualWrongPresenceKindLifetimeRowAndParseCountsReject(PlatformFactsCase c)
    {
        foreach(var wrong in RehydrationPlatformFactsCases.Wrong(c))
            Assert.NotNull(Record.Exception(()=>RehydrationPlatformFactsCases.AssertCase(c,new ScriptedPlatformFactsHarness(c,wrong))));
        Assert.NotNull(Record.Exception(()=>RehydrationPlatformFactsCases.AssertCase(c,new ScriptedPlatformFactsHarness(c,doubleParse:true))));
    }
}
