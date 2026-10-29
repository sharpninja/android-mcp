using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record MetadataReferenceProjectionCase(string Id,string Change="default",bool GZip=false,
    AcquisitionCompatibilityProfile Profile=AcquisitionCompatibilityProfile.CanonicalRestore,string? Error=null)
{ public override string ToString()=>Id; }
public static class RehydrationMetadataReferenceCases
{
    private static readonly string Sha=new('a',64),Git=new('b',40);
    public static IEnumerable<object[]> Cases=>new MetadataReferenceProjectionCase[]{
        new("META-ROW-PRECEDENCE"),new("META-ROW-MISSING-FALLBACK","row-missing"),
        new("META-ROW-EMPTY-FALLBACK","row-empty"),new("META-ROW-NULL-FALLBACK","row-null"),
        new("META-ROW-WHITESPACE-PRESERVED","row-space"),new("META-NATIVE-KEY-IGNORED","native-only"),
        new("META-RECEIPT-MISSING","receipt-missing"),new("META-RECEIPT-NULL","receipt-null"),
        new("META-RECEIPT-NUMBER","receipt-number"),new("META-RECEIPT-TRUE","receipt-true"),
        new("META-RECEIPT-FALSE","receipt-false"),new("META-RECEIPT-ARRAY","receipt-array"),
        new("META-RECEIPT-OBJECT","receipt-object"),new("META-RECEIPT-UNSAFE-DEFERRED","receipt-unsafe"),
        new("META-PACKAGE-NULL","package-null"),new("META-PACKAGE-EXPLICIT","package-explicit"),
        new("META-PACKAGE-EMPTY","package-empty"),new("META-PACKAGE-NUMBER-OLD-REFUSAL","package-number",Error:"INVALID_RECIPE"),
        new("META-KIND-CASE","kind-case"),new("META-UNRELATED-KIND","kind-unrelated"),
        new("META-GZIP",GZip:true),new("META-GENERIC-FACTS-RETAINED",Profile:AcquisitionCompatibilityProfile.GenericRehydrate),
        new("META-ROW-NUMBER-OLD-REFUSAL","row-number",Error:"INVALID_RECIPE"),new("META-KIND-MISSING","kind-missing")
    }.Select(c=>new object[]{c});

    public static RehydrationPlanRequest Request(MetadataReferenceProjectionCase c)=>RehydrationContractCases.Request(root=>{
        JsonObject Row(int i)=>new(){["path"]=$"api/member{i}.xml",["local_path"]="blobs/shared",["sha256"]=Sha,["size_bytes"]=3,
            ["git_blob"]=Git,["url"]=$"https://example.invalid/member{i}",["revision"]=$"revision-{i}",["status"]="downloaded",
            ["platform_api_version"]=i==0?"37.2":"38.0",["transport"]=new JsonObject{
                ["kind"]="bounded-sdk-zip-range-member",["archive_url"]="https://example.invalid/archive.zip",
                ["archive_member"]=$"member/part{i}.xml",["metadata_receipt"]=$"proof/row{i}.json",
                ["platform_api_version"]="36.0",["platform_version"]="99.0"}};
        var first=Row(0);var second=Row(1);var t=first["transport"]!.AsObject();
        switch(c.Change){
            case "row-missing":first.Remove("platform_api_version");break;
            case "row-empty":first["platform_api_version"]="";break;
            case "row-null":first["platform_api_version"]=null;break;
            case "row-space":first["platform_api_version"]=" ";break;
            case "native-only":first.Remove("platform_api_version");t.Remove("platform_api_version");break;
            case "receipt-missing":t.Remove("metadata_receipt");break;
            case "receipt-null":t["metadata_receipt"]=null;break;
            case "receipt-number":t["metadata_receipt"]=7;break;
            case "receipt-true":t["metadata_receipt"]=true;break;
            case "receipt-false":t["metadata_receipt"]=false;break;
            case "receipt-array":t["metadata_receipt"]=new JsonArray("proof/wrong.json");break;
            case "receipt-object":t["metadata_receipt"]=new JsonObject{["path"]="proof/wrong.json"};break;
            case "receipt-unsafe":t["metadata_receipt"]="../untrusted-proof.json";break;
            case "package-null":t["package_path"]=null;break;
            case "package-explicit":t["package_path"]="platforms;android-special";break;
            case "package-empty":t["package_path"]="";break;
            case "package-number":t["package_path"]=7;break;
            case "kind-case":t["kind"]="Bounded-sdk-zip-range-member";break;
            case "kind-unrelated":t["kind"]="sdk-zip-member";break;
            case "kind-missing":t.Remove("kind");break;
            case "row-number":first["platform_api_version"]=7;t.Remove("platform_version");break;
        }
        root["inventory"]!["sources"]=new JsonArray(first,second);
    },gzip:c.GZip) with{Profile=c.Profile};
    public static RehydrationPlanResult Baseline(MetadataReferenceProjectionCase c)=>
        RehydrationContractCases.Correct(new(c.Id,Request(c),c.Error,Rows:2,Unique:1,KnownBytes:3,UnknownSizes:0));
    public static RehydrationSeedPlanResult Seed(MetadataReferenceProjectionCase c)=>
        new(Baseline(c),c.Error is not null?null:new("fixture-release",PublicationTestData.Hash(Request(c).Recipe.Bytes),[]));
    public static RehydrationCanonicalMetadataReference? Reference(MetadataReferenceProjectionCase c,int row)
    {
        var change=row==0?c.Change:"default";
        if(change is "kind-case" or "kind-unrelated" or "kind-missing")return null;
        var receiptPresent=change!="receipt-missing";
        var receiptKind=change switch{"receipt-missing"=>JsonValueKind.Undefined,"receipt-null"=>JsonValueKind.Null,
            "receipt-number"=>JsonValueKind.Number,"receipt-true"=>JsonValueKind.True,"receipt-false"=>JsonValueKind.False,
            "receipt-array"=>JsonValueKind.Array,"receipt-object"=>JsonValueKind.Object,_=>JsonValueKind.String};
        var receipt=receiptKind!=JsonValueKind.String?null:change=="receipt-unsafe"?"../untrusted-proof.json":$"proof/row{row}.json";
        var platform=row==1?"38.0":change switch{"row-missing" or "row-empty" or "row-null"=>"36.0","row-space"=>" ","native-only"=>null,_=>"37.2"};
        var packagePresent=change is "package-null" or "package-explicit" or "package-empty";
        var packageKind=!packagePresent?JsonValueKind.Undefined:change=="package-null"?JsonValueKind.Null:JsonValueKind.String;
        var package=change=="package-explicit"?"platforms;android-special":change=="package-empty"?"":null;
        return new(receiptPresent,receiptKind,receipt,platform,packagePresent,packageKind,package);
    }
    public static RehydrationSnapshotPlanResult Correct(MetadataReferenceProjectionCase c)
    {
        var baseline=Baseline(c);if(c.Error is not null)return new(baseline,null);
        var rows=new List<RehydrationSnapshotRow>();
        for(var i=0;i<2;i++){
            var package=i==0&&c.Change=="package-explicit"?"platforms;android-special":i==0&&c.Change=="package-empty"?"":null;
            var archive=new SourceArchivePin(new("https://example.invalid/archive.zip"),$"member/part{i}.xml",Compatibility:c.Profile,PlatformVersion:"99.0",PackagePath:package);
            var pin=new SourcePin("blobs/shared",new($"https://example.invalid/member{i}"),$"revision-{i}",Sha,3,Git,Archive:archive,Compatibility:c.Profile);
            var prerequisites=c.Profile!=AcquisitionCompatibilityProfile.CanonicalRestore?Array.Empty<RehydrationPrerequisite>():
                new[]{RehydrationPrerequisiteCode.CanonicalArchiveETag,RehydrationPrerequisiteCode.CanonicalArchiveRepositoryXml,RehydrationPrerequisiteCode.CanonicalArchiveSize}
                .Select(code=>new RehydrationPrerequisite(code,Array.AsReadOnly(new[]{i}),"blobs/shared","Missing pinned prerequisite; exact existing target may be reused.")).ToArray();
            rows.Add(new(i,"downloaded",pin,Array.AsReadOnly(Array.Empty<SnapshotRequirement>()),Array.AsReadOnly(prerequisites),
                MetadataReference:Reference(c,i)));
        }
        return new(baseline,new("fixture-release",PublicationTestData.Hash(Request(c).Recipe.Bytes),c.Profile,Array.AsReadOnly(rows.ToArray()),true));
    }
    public static void AssertCase(MetadataReferenceProjectionCase c,ISnapshotProjectionHarness harness)
    {
        var request=Request(c);var before=request.Recipe.Bytes.ToArray();var beforeLock=request.Lock.Bytes.ToArray();
        var baseline=harness.Planner.Plan(request);var seed=harness.SeedPlanner.PlanSeed(request);var parses=harness.RecipeParseCalls;
        var result=harness.Planner.PlanSnapshot(request);
        Assert.Equal(1,harness.RecipeParseCalls-parses);Assert.Equal(before,request.Recipe.Bytes.ToArray());Assert.Equal(beforeLock,request.Lock.Bytes.ToArray());
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(baseline),JsonSerializer.SerializeToUtf8Bytes(result.Baseline));
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(seed),JsonSerializer.SerializeToUtf8Bytes(harness.SeedPlanner.PlanSeed(request)));
        Assert.Equal(c.Error is null,result.IsValid);
        Assert.Equal(c.Error is null?Array.Empty<string>():new[]{c.Error},result.Baseline.Issues.Select(x=>x.Code));
        if(c.Error is not null){Assert.Null(result.SnapshotProjection);return;}
        var expected=Correct(c).SnapshotProjection!;var actual=Assert.IsType<RehydrationSnapshotProjection>(result.SnapshotProjection);
        Assert.Equal(expected.Release,actual.Release);Assert.Equal(expected.RecipeSha256,actual.RecipeSha256);Assert.Equal(expected.Profile,actual.Profile);
        Assert.True(actual.Resume);Assert.Equal(2,actual.Rows.Count);
        for(var i=0;i<2;i++){
            var e=expected.Rows[i];var a=actual.Rows[i];Assert.Equal(e.RowIndex,a.RowIndex);Assert.Equal(e.Pin,a.Pin);
            Assert.Equal(e.Status,a.Status);Assert.Equal(e.StatusPresent,a.StatusPresent);Assert.Equal(e.StatusKind,a.StatusKind);Assert.True(a.DownloadedEligible);
            Assert.Equal(e.RequiredSnapshots,a.RequiredSnapshots);Assert.Equal(e.Prerequisites.Count,a.Prerequisites.Count);
            for(var j=0;j<e.Prerequisites.Count;j++){
                var x=e.Prerequisites[j];var y=a.Prerequisites[j];Assert.Equal(x.Code,y.Code);Assert.Equal(x.Path,y.Path);
                Assert.Equal(x.Reason,y.Reason);Assert.Equal(x.SelectionRowIndices,y.SelectionRowIndices);
            }
            Assert.Equal(e.MetadataReference,a.MetadataReference);
            using var wire=JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(a));
            Assert.Equal(a.MetadataReference is not null,wire.RootElement.TryGetProperty(nameof(RehydrationSnapshotRow.MetadataReference),out _));
        }
        if(actual.Rows is IList<RehydrationSnapshotRow> rows)Assert.Throws<NotSupportedException>(()=>rows[0]=rows[1]);
    }
    public static IEnumerable<RehydrationSnapshotPlanResult> Wrong(MetadataReferenceProjectionCase c)
    {
        var good=Correct(c);yield return good with{Baseline=good.Baseline with{Issues=[new("WRONG","")]}};
        if(c.Error is not null){yield return new(new(null,[]),null);yield break;}
        var p=good.SnapshotProjection!;yield return good with{SnapshotProjection=null};
        yield return good with{SnapshotProjection=p with{Rows=p.Rows.ToArray()}};
        yield return good with{SnapshotProjection=p with{Rows=Array.AsReadOnly(p.Rows.Reverse().ToArray())}};
        for(var i=0;i<2;i++){
            var row=p.Rows[i];var alternatives=new List<RehydrationSnapshotRow>{row with{Pin=row.Pin with{Revision="wrong"}},row with{RowIndex=99},row with{StatusPresent=false}};
            if(row.MetadataReference is {} reference){
                alternatives.Add(row with{MetadataReference=null});
                foreach(var wrong in new[]{reference with{ReceiptPathPresent=!reference.ReceiptPathPresent},reference with{ReceiptPathKind=(JsonValueKind)99},
                    reference with{ReceiptPath="proof/wrong"},reference with{PlatformVersion="wrong"},reference with{PackagePathPresent=!reference.PackagePathPresent},
                    reference with{PackagePathKind=(JsonValueKind)99},reference with{PackagePath="wrong"}})
                    alternatives.Add(row with{MetadataReference=wrong});
            }else alternatives.Add(row with{MetadataReference=new(false,JsonValueKind.Undefined,null,null,false,JsonValueKind.Undefined,null)});
            var index=i;foreach(var alternative in alternatives)yield return good with{SnapshotProjection=p with{Rows=Array.AsReadOnly(p.Rows.Select((x,k)=>k==index?alternative:x).ToArray())}};
        }
    }
}
public sealed class ScriptedMetadataReferenceHarness(MetadataReferenceProjectionCase c,RehydrationSnapshotPlanResult? wrong=null,bool doubleParse=false)
    :ISnapshotProjectionHarness,IRehydrationSnapshotPlanner,IRehydrationSeedPlanner
{
    public IRehydrationSnapshotPlanner Planner=>this;public IRehydrationSeedPlanner SeedPlanner=>this;
    public int RecipeParseCalls {get;private set;}
    public RehydrationPlanResult Plan(RehydrationPlanRequest request){RecipeParseCalls++;return RehydrationMetadataReferenceCases.Baseline(c);}
    public RehydrationSeedPlanResult PlanSeed(RehydrationPlanRequest request){RecipeParseCalls++;return RehydrationMetadataReferenceCases.Seed(c);}
    public RehydrationSnapshotPlanResult PlanSnapshot(RehydrationPlanRequest request){RecipeParseCalls+=doubleParse?2:1;return wrong??RehydrationMetadataReferenceCases.Correct(c);}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationMetadataReferenceMockTests
{
    [Theory,MemberData(nameof(RehydrationMetadataReferenceCases.Cases),MemberType=typeof(RehydrationMetadataReferenceCases))]
    public void CorrectExactReferenceAndBaselineCases(MetadataReferenceProjectionCase c)=>
        RehydrationMetadataReferenceCases.AssertCase(c,new ScriptedMetadataReferenceHarness(c));
    [Theory,MemberData(nameof(RehydrationMetadataReferenceCases.Cases),MemberType=typeof(RehydrationMetadataReferenceCases))]
    public void IndividualWrongReferencesRowsAndParseCountsDiscriminate(MetadataReferenceProjectionCase c)
    {
        foreach(var wrong in RehydrationMetadataReferenceCases.Wrong(c))
            Assert.NotNull(Record.Exception(()=>RehydrationMetadataReferenceCases.AssertCase(c,new ScriptedMetadataReferenceHarness(c,wrong))));
        Assert.NotNull(Record.Exception(()=>RehydrationMetadataReferenceCases.AssertCase(c,new ScriptedMetadataReferenceHarness(c,doubleParse:true))));
    }
}
