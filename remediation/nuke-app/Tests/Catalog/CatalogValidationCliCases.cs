using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogValidationCliCase(string Id,string Variant,CatalogValidationMode? Mode,
    CatalogFullValidationCase? Original,int Exit,string? Error,bool ReportCreated,bool ExistingReportRetained=false);
public sealed record CatalogValidationCliInput(CatalogValidationWorkflowFile File,ReadOnlyMemory<byte> Bytes,StoredArtifact Pin);
public sealed record CatalogValidationCliObservation(int Exit,string? Error,CatalogFullValidationReply? Validation,
    IReadOnlyList<string> Arguments,StoredArtifact CallerPin,string ProjectRoot,string RuntimeRoot,
    bool InputsUnchanged,bool RequestRetained,bool NoUnexpectedEffects,bool HostAndDllPinned,
    bool DatabaseHasNoReleaseInputs,bool ReportCreated,bool ExistingReportRetained,
    CatalogValidationMode? ReportMode,ReadOnlyMemory<byte> ReportBytes,StoredArtifact? ReportPin,string ExpectedProducerFingerprint);
public interface ICatalogValidationCliProbe
{
    Task<CatalogValidationCliObservation> RunAsync(CatalogValidationCliCase test);
}
public static class CatalogValidationCliCases
{
    public const string RequestError=CatalogValidationWorkflowErrors.Request;
    public const string Project="/fixture project";
    public const string Runtime="/fixture runtime";
    public static string[] Arguments(string runtime,string project,StoredArtifact pin)=>[
        "exec","AndroidMcp.Build.dll","--root",runtime,"--target","Validate","--operation","Catalog",
        "--project-root",project,"--pipeline-request",pin.RelativePath,"--pipeline-request-sha256",pin.Sha256,
        "--pipeline-request-length",pin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)];
    private static IEnumerable<CatalogValidationCliCase> Cases()
    {
        foreach(var original in CatalogFullValidationCases.All.Select(row=>(CatalogFullValidationCase)row[0]))
            yield return new("CLI-"+original.Id,original.Call.Variant,original.Call.Variant=="database-only"?CatalogValidationMode.Database:CatalogValidationMode.Release,
                original,original.Expected.Outcome=="ok"?0:1,original.Expected.ErrorContains,original.Expected.ReportCreated==true,original.Expected.ExistingReportRetained==true);
        yield return new("VAL-CLI-07-database-report-create-new","database-existing-report",CatalogValidationMode.Database,null,1,"already exists",false,true);
        yield return new("VAL-CLI-08-wrong-request-hash","bad-pin-hash",CatalogValidationMode.Release,null,1,"ARTIFACT_PIN_HASH",false);
        yield return new("VAL-CLI-09-wrong-request-length","bad-pin-length",CatalogValidationMode.Release,null,1,"ARTIFACT_PIN_LENGTH",false);
        foreach(var (id,variant,mode) in new[]{
            ("10","unknown-mode",(CatalogValidationMode?)null),("11","missing-mode",null),
            ("12","missing-release-inventory",CatalogValidationMode.Release),("13","missing-release-output",CatalogValidationMode.Release),
            ("14","missing-database",CatalogValidationMode.Database),("15","blank-report",CatalogValidationMode.Release),
            ("16","mixed-mode",CatalogValidationMode.Database),("17","unknown-member",CatalogValidationMode.Release)})
            yield return new("VAL-CLI-"+id+"-"+variant,variant,mode,null,1,RequestError,false);
    }
    public static IEnumerable<object[]> All=>Cases().Select(test=>new object[]{test});
    public static CatalogValidationCliInput Input(CatalogValidationCliCase test,CatalogValidationWorkflowFile? bindings=null)
    {
        var mode=test.Mode??CatalogValidationMode.Release;
        var file=bindings??(mode==CatalogValidationMode.Release
            ?new CatalogValidationWorkflowFile(mode,"validation report.json","release output","inventory.json")
            :new CatalogValidationWorkflowFile(mode,"validation report.json",DatabasePath:"standalone database.sqlite"));
        var options=new JsonSerializerOptions();options.Converters.Add(new JsonStringEnumConverter<CatalogValidationMode>(allowIntegerValues:false));
        var json=JsonSerializer.SerializeToNode(file,options)!.AsObject();
        switch(test.Variant)
        {
            case "unknown-mode":json["Mode"]="Unexpected";break;
            case "missing-mode":json.Remove("Mode");break;
            case "missing-release-inventory":json.Remove("SourceManifest");break;
            case "missing-release-output":json.Remove("OutputRoot");break;
            case "missing-database":json.Remove("DatabasePath");break;
            case "blank-report":json["ReportPath"]=" ";break;
            case "mixed-mode":json["OutputRoot"]="foreign release output";break;
            case "unknown-member":json["InventedAuthority"]=true;break;
        }
        var bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(json));
        var pin=new StoredArtifact("workflows/catalog validation.json",ContentHash.Sha256(bytes),bytes.Length,"application/json");
        if(test.Variant=="bad-pin-hash")pin=pin with{Sha256=new string('0',64)};
        if(test.Variant=="bad-pin-length")pin=pin with{Length=pin.Length+1};
        return new(file,bytes,pin);
    }
    public static async Task AssertCase(CatalogValidationCliCase test,ICatalogValidationCliProbe provider)
    {
        var actual=await provider.RunAsync(test);Assert.Equal(test.Exit,actual.Exit);Assert.Equal(test.Error,actual.Error);
        Assert.Equal(test.Original?.Expected,actual.Validation);
        Assert.True(actual.InputsUnchanged);Assert.True(actual.RequestRetained);Assert.True(actual.NoUnexpectedEffects);Assert.True(actual.HostAndDllPinned);
        Assert.Equal(test.Mode==CatalogValidationMode.Database,actual.DatabaseHasNoReleaseInputs);
        Assert.Equal(test.ReportCreated,actual.ReportCreated);Assert.Equal(test.ExistingReportRetained,actual.ExistingReportRetained);
        Assert.Equal(test.Exit==0?test.Mode:null,actual.ReportMode);
        Assert.NotEqual(actual.ProjectRoot,actual.RuntimeRoot);
        var args=actual.Arguments;Assert.Equal("exec",args[0]);Assert.DoesNotContain(args,value=>value is "build" or "restore" or "run" or "--internal-process-probe");
        string Value(string name){var index=args.ToList().IndexOf(name);Assert.InRange(index,0,args.Count-2);return args[index+1];}
        Assert.Equal("Validate",Value("--target"));Assert.Equal("Catalog",Value("--operation"));
        Assert.Equal(actual.ProjectRoot,Value("--project-root"));Assert.Equal(actual.RuntimeRoot,Value("--root"));
        Assert.Equal(actual.CallerPin.RelativePath,Value("--pipeline-request"));Assert.Equal(actual.CallerPin.Sha256,Value("--pipeline-request-sha256"));
        Assert.Equal(actual.CallerPin.Length.ToString(System.Globalization.CultureInfo.InvariantCulture),Value("--pipeline-request-length"));
        Assert.DoesNotContain("--source-manifest",args);Assert.DoesNotContain("--database",args);
        if(test.Exit==0)
        {
            Assert.NotNull(actual.ReportPin);Assert.Equal(actual.ReportBytes.Length,actual.ReportPin!.Length);Assert.Equal(ContentHash.Sha256(actual.ReportBytes.Span),actual.ReportPin.Sha256);
            using var doc=JsonDocument.Parse(actual.ReportBytes);var report=doc.RootElement;
            Assert.Equal(10,report.GetProperty("entries_validated").GetInt32());Assert.Empty(report.GetProperty("json_schema_failures").EnumerateArray());
            Assert.Equal("ok",report.GetProperty("integrity_check").GetString());Assert.Empty(report.GetProperty("foreign_key_check").EnumerateArray());Assert.False(report.GetProperty("complete").GetBoolean());
            Assert.Equal(CatalogValidator.ProducerId,report.GetProperty("native_producer").GetProperty("id").GetString());
            Assert.Equal(actual.ExpectedProducerFingerprint,report.GetProperty("native_producer").GetProperty("sha256").GetString());
            Assert.False(report.GetProperty("native_producer").GetProperty("historical_python_identity").GetBoolean());
            if(test.Mode==CatalogValidationMode.Database){Assert.Equal("read-only",report.GetProperty("database_open_mode").GetString());Assert.False(report.GetProperty("source_applicability_facts_revalidated").GetBoolean());Assert.Equal(0,report.GetProperty("declaration_variant_omissions").GetInt32());Assert.False(report.TryGetProperty("device_inventory",out _));}
            else Assert.Equal("validated",report.GetProperty("device_inventory").GetProperty("status").GetString());
        }
        else {Assert.Null(actual.ReportPin);Assert.True(actual.ReportBytes.IsEmpty);}
    }
}
public sealed class ScriptedCatalogValidationCli(string mutation=""):ICatalogValidationCliProbe
{
    public Task<CatalogValidationCliObservation> RunAsync(CatalogValidationCliCase test)
    {
        var input=CatalogValidationCliCases.Input(test);var args=CatalogValidationCliCases.Arguments(CatalogValidationCliCases.Runtime,CatalogValidationCliCases.Project,input.Pin);
        var expectedProducer=new string('e',64);var mode=test.Exit==0?test.Mode:null;
        var report=new JsonObject{["entries_validated"]=10,["json_schema_failures"]=new JsonArray(),["integrity_check"]="ok",["foreign_key_check"]=new JsonArray(),["complete"]=false,
            ["native_producer"]=new JsonObject{["id"]=CatalogValidator.ProducerId,["sha256"]=expectedProducer,["historical_python_identity"]=false}};
        if(test.Mode==CatalogValidationMode.Database){report["database_open_mode"]="read-only";report["source_applicability_facts_revalidated"]=false;report["declaration_variant_omissions"]=0;}
        else report["device_inventory"]=new JsonObject{["status"]="validated"};
        if(mutation.StartsWith("report:",StringComparison.Ordinal))report.Remove(mutation[7..]);
        ReadOnlyMemory<byte> bytes=test.Exit==0?new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(report)):ReadOnlyMemory<byte>.Empty;
        StoredArtifact? pin=test.Exit==0?new("validation report.json",ContentHash.Sha256(bytes.Span),bytes.Length,"application/json"):null;
        if(mutation=="report-pin")pin=pin is null?new("unexpected.json",new('0',64),1,"application/json"):pin with{Sha256=new('0',64)};
        if(mutation=="command")args[0]="build";
        if(mutation=="category")args[Array.IndexOf(args,"--target")+1]="Catalog";
        if(mutation=="operation")args[Array.IndexOf(args,"--operation")+1]="Tests";
        if(mutation=="command-pin")args[Array.IndexOf(args,"--pipeline-request-sha256")+1]="wrong";
        var reply=test.Original?.Expected;
        if(mutation=="original")reply=reply is null?CatalogFullValidationCases.All.Select(row=>((CatalogFullValidationCase)row[0]).Expected).First():reply with{ExistingOutputsUnchanged=false};
        if(mutation.StartsWith("original-field:",StringComparison.Ordinal))
        {
            Assert.NotNull(test.Original);var field=int.Parse(mutation[15..],System.Globalization.CultureInfo.InvariantCulture);
            reply=new ScriptedCatalogFullValidation(test.Original!,field<20?field:-1,field>=20?field-20:-1).ExecuteAsync(test.Original!.Call).GetAwaiter().GetResult();
        }
        return Task.FromResult(new CatalogValidationCliObservation(mutation=="exit"?test.Exit==0?1:0:test.Exit,mutation=="error"?"wrong":test.Error,reply,args,input.Pin,
            CatalogValidationCliCases.Project,mutation=="runtime"?CatalogValidationCliCases.Project:CatalogValidationCliCases.Runtime,
            mutation!="inputs",mutation!="request",mutation!="effects",mutation!="host-dll",mutation=="db-inputs"?test.Mode!=CatalogValidationMode.Database:test.Mode==CatalogValidationMode.Database,
            mutation=="report-created"?!test.ReportCreated:test.ReportCreated,mutation=="old-report"?!test.ExistingReportRetained:test.ExistingReportRetained,
            mutation=="mode"?mode==CatalogValidationMode.Release?CatalogValidationMode.Database:CatalogValidationMode.Release:mode,bytes,pin,mutation=="producer"?"wrong":expectedProducer));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogValidationCliMockTests
{
    [Theory,MemberData(nameof(CatalogValidationCliCases.All),MemberType=typeof(CatalogValidationCliCases))]
    public Task CorrectOriginalValidatorAndCallerAdmissionResponsesPass(CatalogValidationCliCase test)=>CatalogValidationCliCases.AssertCase(test,new ScriptedCatalogValidationCli());
    [Theory,MemberData(nameof(CatalogValidationCliCases.All),MemberType=typeof(CatalogValidationCliCases))]
    public async Task EveryCallerCommandResultRetentionAndReportAssertionRejectsItsOwnWrongResponse(CatalogValidationCliCase test)
    {
        var mutations=new[]{"exit","error","original","command","category","operation","command-pin","runtime","inputs","request","effects","host-dll","db-inputs","report-created","old-report","mode","report-pin"}.AsEnumerable();
        if(test.Original is not null)mutations=mutations.Concat(Enumerable.Range(0,44).Select(field=>"original-field:"+field));
        if(test.Exit==0)mutations=mutations.Concat(new[]{"entries_validated","json_schema_failures","integrity_check","foreign_key_check","complete","native_producer"}.Select(name=>"report:"+name)).Append("producer")
            .Concat((test.Mode==CatalogValidationMode.Database?new[]{"database_open_mode","source_applicability_facts_revalidated","declaration_variant_omissions"}:new[]{"device_inventory"}).Select(name=>"report:"+name));
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>CatalogValidationCliCases.AssertCase(test,new ScriptedCatalogValidationCli(mutation))));
    }
}
