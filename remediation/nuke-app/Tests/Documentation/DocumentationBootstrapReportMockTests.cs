using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed class BootstrapReportEnvironment:IClock
{
    public string Scenario{get;}
    public DocumentationBootstrapEnvironment Original{get;}=new(new("BOOTSTRAP-ONLINE",ToolArtifactAccess.Online,null,""));
    public Artifact Lock{get;}=new("toolchain-lock.json","abc"u8.ToArray(),"application/json");
    public ToolArtifactReceipt[] Tools{get;}
    public int ClockReads{get;private set;}
    public DateTimeOffset Now{get{ClockReads++;return new DateTimeOffset(2026,10,29,1,2,3,TimeSpan.FromHours(-5)).AddTicks(Scenario=="ZERO"?0:Scenario=="SUBMICRO"?7:1234567);}}
    public string ExpectedTime=>Scenario is "ZERO" or "SUBMICRO"?"2026-10-29T06:02:03+00:00":"2026-10-29T06:02:03.123456+00:00";
    public bool Rejected=>Scenario is not("GOOD" or "ZERO" or "SUBMICRO");
    public BootstrapReportEnvironment(string scenario)
    {
        Scenario=scenario;Tools=Original.Receipts.ToArray();Tools[0]=Tools[0] with{ArtifactUrl=new("https://redirect.test/different")};
        if(scenario=="COUNT")Tools=Tools.Skip(1).ToArray();if(scenario=="ORDER")Array.Reverse(Tools);
        if(scenario=="PIN")Tools[0]=Tools[0] with{Pin=Tools[0].Pin with{Sha256=new string('0',64)}};
        if(scenario=="SOURCE")Tools[0]=Tools[0] with{Artifact=Tools[0].Artifact with{RelativePath="different"}};
        if(scenario=="HASH")Tools[0]=Tools[0] with{Artifact=Tools[0].Artifact with{Sha256=new string('0',64)}};
        if(scenario=="SIZE")Tools[0]=Tools[0] with{Artifact=Tools[0].Artifact with{Length=0}};
        if(scenario=="STATUS")Tools[0]=Tools[0] with{Status="failed"};
        if(scenario=="LOCK-PATH")Lock=Lock with{RelativePath="different.json"};if(scenario=="LOCK-MEDIA")Lock=Lock with{MediaType="text/plain"};
    }
}
public static class BootstrapReportCases
{
    public const string Summary="{\"files\": 5, \"gradle\": \"8.14.3\", \"plantuml\": \"1.2025.10\", \"task_local_only\": true}\n";
    public static IEnumerable<object[]> Cases=>new[]{"GOOD","ZERO","SUBMICRO","COUNT","ORDER","PIN","SOURCE","HASH","SIZE","STATUS","LOCK-PATH","LOCK-MEDIA"}.Select(value=>new object[]{value});
    public static void AssertContract(BootstrapReportEnvironment e,IDocumentationBootstrapReportProjector projector)
    {
        var before=e.Lock.Bytes.ToArray();DocumentationBootstrapReport? result=null;var failure=Record.Exception(()=>result=projector.Project(e.Original.Profile,e.Lock,e.Tools));
        if(e.Rejected){Assert.Equal("BOOTSTRAP_RECEIPT",Assert.IsType<ArchitectureAssetException>(failure).Code);Assert.Null(result);Assert.Equal(0,e.ClockReads);}
        else {
            Assert.Null(failure);Assert.NotNull(result);Assert.Equal(1,e.ClockReads);Assert.Equal("toolchain-bootstrap-receipt.json",result.Receipt.RelativePath);Assert.Equal("application/json",result.Receipt.MediaType);
            var text=Encoding.UTF8.GetString(result.Receipt.Bytes.Span);Assert.EndsWith("\n",text,StringComparison.Ordinal);Assert.Contains("\n  \"created_at\": ",text,StringComparison.Ordinal);
            var json=JsonNode.Parse(text)!.AsObject();Assert.Equal(new[]{"created_at","files","original_lock_sha256"},json.Select(value=>value.Key));Assert.Equal(e.ExpectedTime,json["created_at"]!.GetValue<string>());
            Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",json["original_lock_sha256"]!.GetValue<string>());
            var rows=json["files"]!.AsArray();Assert.Equal(5,rows.Count);
            for(var index=0;index<5;index++){
                var pin=e.Original.Profile.Downloads[index];var row=rows[index]!.AsObject();Assert.Equal(new[]{"file","url","size_bytes","sha256"},row.Select(value=>value.Key));
                Assert.Equal(pin.LocalPath,row["file"]!.GetValue<string>());Assert.Equal(pin.Url.AbsoluteUri,row["url"]!.GetValue<string>());Assert.Equal(pin.SizeBytes,row["size_bytes"]!.GetValue<long>());Assert.Equal(pin.Sha256,row["sha256"]!.GetValue<string>());
            }
            Assert.Equal(Summary,result.Summary);
        }
        Assert.Equal(before,e.Lock.Bytes.ToArray());Assert.Empty(e.Original.Calls);
    }
}
public sealed class ScriptedBootstrapReport(BootstrapReportEnvironment e,string mutation="",int index=0):IDocumentationBootstrapReportProjector
{
    public DocumentationBootstrapReport Project(DocumentationRendererProfile profile,Artifact finalLock,IReadOnlyList<ToolArtifactReceipt> tools)
    {
        Assert.Same(e.Original.Profile,profile);Assert.Same(e.Lock,finalLock);Assert.Same(e.Tools,tools);
        if(mutation=="outcome")throw new InvalidOperationException("wrong outcome");if(e.Rejected)throw new ArchitectureAssetException("BOOTSTRAP_RECEIPT","independent rejected-input oracle");
        _=e.Now;if(mutation=="clock")_=e.Now;
        var json=new JsonObject{["created_at"]=e.ExpectedTime,["files"]=new JsonArray(e.Original.Profile.Downloads.Select(pin=>(JsonNode)new JsonObject{["file"]=pin.LocalPath,["url"]=pin.Url.AbsoluteUri,["size_bytes"]=pin.SizeBytes,["sha256"]=pin.Sha256}).ToArray()),["original_lock_sha256"]="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"};
        if(mutation=="created")json["created_at"]="wrong";if(mutation=="lock-hash")json["original_lock_sha256"]="wrong";if(mutation=="root-fields")json["extra"]=true;
        if(mutation=="row-count")json["files"]!.AsArray().RemoveAt(0);if(mutation=="row-order"){var rows=json["files"]!.AsArray();var first=rows[0]!.DeepClone();rows.RemoveAt(0);rows.Add(first);}
        if(mutation=="row-fields")json["files"]![0]!["extra"]=true;
        if(mutation.StartsWith("row:",StringComparison.Ordinal))json["files"]![index]![mutation[4..]]=mutation=="row:size_bytes"?JsonValue.Create(0):JsonValue.Create("wrong");
        var bytes=Encoding.UTF8.GetBytes(json.ToJsonString(new(){WriteIndented=mutation!="indent"})+(mutation=="newline"?"":"\n"));
        return new(new(mutation=="path"?"wrong":"toolchain-bootstrap-receipt.json",bytes,mutation=="media"?"wrong":"application/json"),mutation=="summary"?"wrong":BootstrapReportCases.Summary);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapReportMockTests
{
    [Theory,MemberData(nameof(BootstrapReportCases.Cases),MemberType=typeof(BootstrapReportCases))]
    public void OriginalReceiptRowsUtcClockRawLockAndSummaryMocksPass(string scenario){var e=new BootstrapReportEnvironment(scenario);BootstrapReportCases.AssertContract(e,new ScriptedBootstrapReport(e));}
    [Theory,MemberData(nameof(BootstrapReportCases.Cases),MemberType=typeof(BootstrapReportCases))]
    public void EveryWrongReceiptOutcomeIsDiscriminating(string scenario){var e=new BootstrapReportEnvironment(scenario);Assert.NotNull(Record.Exception(()=>BootstrapReportCases.AssertContract(e,new ScriptedBootstrapReport(e,"outcome"))));}
    [Theory,InlineData("path"),InlineData("media"),InlineData("clock"),InlineData("created"),InlineData("lock-hash"),InlineData("root-fields"),InlineData("row-count"),InlineData("row-order"),InlineData("row-fields"),InlineData("indent"),InlineData("newline"),InlineData("summary")]
    public void EveryOriginalReceiptAndSummaryAssertionRejectsItsFault(string mutation){var e=new BootstrapReportEnvironment("GOOD");Assert.NotNull(Record.Exception(()=>BootstrapReportCases.AssertContract(e,new ScriptedBootstrapReport(e,mutation))));}
    public static IEnumerable<object[]> RowFaults=>Enumerable.Range(0,5).SelectMany(index=>new[]{"file","url","size_bytes","sha256"}.Select(member=>new object[]{index,"row:"+member}));
    [Theory,MemberData(nameof(RowFaults))]
    public void EachFieldOfEveryObservedToolRowRejectsDrift(int index,string mutation){var e=new BootstrapReportEnvironment("GOOD");Assert.NotNull(Record.Exception(()=>BootstrapReportCases.AssertContract(e,new ScriptedBootstrapReport(e,mutation,index))));}
}
