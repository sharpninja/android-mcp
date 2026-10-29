using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record PermissionDefinitionSeed(string Name,string Source,string? Protection,string Metadata);
public sealed record PermissionSymbolSeed(string Name,string Symbol,string Source,long Line);
public sealed record PermissionRelationSeed(string Logic,string Binding,string Source,long Line,string Expression,string? Raw);
public sealed record PermissionExportCase(string Id,string Mode);
public sealed record PermissionExportObservation(string SourceCatalog,string SourceBefore,string SourceAfter,
    bool? ReadOnlyRequested,PermissionExportResult? Result,IReadOnlyDictionary<string,byte[]> Files,Exception? Error,
    byte[]? ExistingOutputBefore=null,byte[]? ExistingOutputAfter=null);
public interface IPermissionExportProbe { Task<PermissionExportObservation> RunAsync(PermissionExportCase test); }
public static class PermissionExportCases
{
    public const string A="android.permission.A";
    public const string B="android.permission.B";
    public const string Unicode="android.permission.\U0001f600";
    public static readonly string S1=new('a',64),S2=new('b',64),Binding=new('d',64);
    public static readonly string[] LogicOrder=["permission","all","any","access","conditional","manual","explicit-none","unknown"];
    public static IEnumerable<object[]> Data=>new PermissionExportCase[]{new("PERM-EMPTY","empty"),new("PERM-UNION","union"),new("PERM-UNICODE","unicode"),
        new("PERM-SAMPLES","samples"),new("PERM-NULL-RAW","null-raw"),new("PERM-BAD-METADATA","bad-metadata"),new("PERM-BAD-EXPRESSION","bad-expression"),
        new("PERM-BAD-RAW","bad-raw"),new("PERM-BAD-SUMMARY","bad-summary"),new("PERM-BAD-SCHEMA","bad-schema"),new("PERM-SOURCE-MISSING","missing"),
        new("PERM-SAME-ROOT","same-root"),new("PERM-DESCENDANT","descendant"),new("PERM-NONEMPTY","nonempty"),new("PERM-CANCEL","cancel"),new("PERM-WRITE-FAILURE","write-failure")}.Select(value=>new object[]{value});
    public static bool Succeeds(PermissionExportCase test)=>test.Mode is "empty" or "union" or "unicode" or "samples" or "null-raw";
    public static bool Opens(PermissionExportCase test)=>test.Mode is not("same-root" or "descendant" or "nonempty" or "cancel" or "bad-schema" or "missing");
    public static PermissionDefinitionSeed[] Definitions(PermissionExportCase test)=>test.Mode=="empty"?[]:
        [new(A,S1,null,test.Mode=="bad-metadata"?"{":"{\"note\":\"manifest fact\"}"),new(A,S2,"signature","{\"flag\":true}")];
    public static PermissionSymbolSeed[] Symbols(PermissionExportCase test)=>test.Mode=="empty"?[]:
        [new(test.Mode=="unicode"?Unicode:B,"Manifest.permission.Z",S1,8),new(A,"Manifest.permission.A",S2,2),new(A,"Manifest.permission.A",S1,test.Mode=="bad-summary"?0:9)];
    public static PermissionRelationSeed[] Relations(PermissionExportCase test)=>test.Mode=="samples"?
        LogicOrder.SelectMany(logic=>Enumerable.Range(1,4).Select(line=>new PermissionRelationSeed(logic,Binding,S1,line,"{\"op\":\""+logic+"\"}","{\"source\":\"raw\"}"))).ToArray():
        test.Mode is "null-raw" or "bad-expression" or "bad-raw"?[new("permission",Binding,S1,1,test.Mode=="bad-expression"?"{":"{\"op\":\"permission\"}",
            test.Mode=="bad-raw"?"{":null),new("unknown",Binding,S2,2,"{\"op\":\"unknown\"}","null")]:[];
    public static string[] Names(PermissionExportCase test)=>Definitions(test).Select(row=>row.Name).Concat(Symbols(test).Select(row=>row.Name))
        .Distinct(StringComparer.Ordinal).Order(Comparer<string>.Create(StrictJsonInspector.CompareScalars)).ToArray();
    public static string Code(PermissionExportCase test)=>test.Mode switch{
        "same-root" or "descendant" or "nonempty"=>"PERMISSION_EXPORT_SCOPE","bad-schema" or "bad-summary"=>"PERMISSION_EXPORT_SCHEMA",
        "bad-metadata" or "bad-expression" or "bad-raw"=>"PERMISSION_EXPORT_JSON","missing"=>"PERMISSION_EXPORT_SOURCE","write-failure"=>"PERMISSION_EXPORT_OUTPUT",_=>""};
    public static JsonObject Summary(string name,PermissionExportCase test)=>new(){["schema_version"]="1.1.0",["permission_id"]=ContentHash.Sha256(Encoding.UTF8.GetBytes("permission:"+name)),["name"]=name,
        ["manifest_definitions"]=new JsonArray(Definitions(test).Where(row=>row.Name==name).OrderBy(row=>row.Source,StringComparer.Ordinal).Select(row=>(JsonNode)new JsonObject{
            ["source_id"]=row.Source,["protection_level"]=row.Protection,["metadata"]=JsonNode.Parse(row.Metadata)}).ToArray()),
        ["symbols"]=new JsonArray(Symbols(test).Where(row=>row.Name==name).OrderBy(row=>row.Symbol,StringComparer.Ordinal).ThenBy(row=>row.Source,StringComparer.Ordinal).ThenBy(row=>row.Line).Select(row=>(JsonNode)new JsonObject{
            ["symbol"]=row.Symbol,["source_id"]=row.Source,["line"]=row.Line}).ToArray()),
        ["metadata_status"]=Definitions(test).Any(row=>row.Name==name)?"manifest-evidence-present":"constant-only; protection-level-and-attributes-unknown",["os_authority"]="unknown",["mcp_client_grants"]="not-inferred"};
    public static JsonArray Samples(PermissionExportCase test)=>new(LogicOrder.SelectMany(logic=>Relations(test).Where(row=>row.Logic==logic).Take(3)).Select(row=>(JsonNode)new JsonObject{
        ["logic"]=row.Logic,["binding_id"]=row.Binding,["source_id"]=row.Source,["line"]=row.Line,["expression"]=JsonNode.Parse(row.Expression),["raw_annotation"]=string.IsNullOrEmpty(row.Raw)?null:JsonNode.Parse(row.Raw)}).ToArray());
    public static async Task AssertCase(PermissionExportCase test,IPermissionExportProbe probe)
    {
        var observed=await probe.RunAsync(test);
        Assert.Equal(observed.SourceBefore,observed.SourceAfter);Assert.Matches("^[0-9a-f]{64}$",observed.SourceBefore);
        if(test.Mode=="nonempty"){Assert.Equal("keep"u8.ToArray(),observed.ExistingOutputBefore);Assert.Equal(observed.ExistingOutputBefore,observed.ExistingOutputAfter);}
        if(Opens(test))Assert.True(observed.ReadOnlyRequested);else Assert.Null(observed.ReadOnlyRequested);
        if(!Succeeds(test))
        {
            Assert.Null(observed.Result);Assert.NotNull(observed.Error);
            if(test.Mode=="cancel")Assert.IsAssignableFrom<OperationCanceledException>(observed.Error);
            else Assert.Equal(Code(test),Assert.IsType<PermissionExportException>(observed.Error).Code);
            Assert.False(observed.Files.ContainsKey("permission-coverage.json"));
            if(test.Mode is "same-root" or "descendant" or "nonempty" or "cancel" or "bad-schema" or "missing")Assert.Empty(observed.Files);
            if(test.Mode=="write-failure")Assert.True(observed.Files.ContainsKey("permissions.jsonl"));
            return;
        }
        Assert.Null(observed.Error);Assert.NotNull(observed.Result);
        AssertContents(test,observed.Result!,observed.Files,observed.SourceCatalog);
    }
    // The CLI has no in-process opener spy. Share every artifact assertion,
    // while the native service lane retains its separate strict mode check.
    public static void AssertContents(PermissionExportCase test,PermissionExportResult result,IReadOnlyDictionary<string,byte[]> files,string sourceCatalog)
    {
        Assert.Equal(Names(test).LongLength,result.Summary.PermissionNames);Assert.Equal(Definitions(test).LongLength,result.Summary.ManifestDefinitionRecords);
        Assert.Equal(sourceCatalog,result.Summary.SourceCatalog);Assert.False(result.Summary.Complete);
        var expectedPaths=new[]{"permission-coverage.json","permission-expression-samples.json","permissions.jsonl"};
        Assert.Equal(expectedPaths,files.Keys.Order(StringComparer.Ordinal));Assert.Equal(expectedPaths,result.Artifacts.Select(value=>value.RelativePath).Order(StringComparer.Ordinal));
        foreach(var artifact in result.Artifacts){var bytes=files[artifact.RelativePath];Assert.Equal(bytes.LongLength,artifact.Length);Assert.Equal(ContentHash.Sha256(bytes),artifact.Sha256);Assert.Equal(artifact.RelativePath.EndsWith(".jsonl",StringComparison.Ordinal)?"application/x-ndjson":"application/json",artifact.MediaType);}
        var lines=Encoding.UTF8.GetString(files["permissions.jsonl"]).Split('\n',StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(Names(test).Length,lines.Length);
        for(var index=0;index<lines.Length;index++)Assert.True(JsonNode.DeepEquals(Summary(Names(test)[index],test),JsonNode.Parse(lines[index])));
        Assert.True(JsonNode.DeepEquals(Samples(test),JsonNode.Parse(files["permission-expression-samples.json"])));
        var coverage=JsonNode.Parse(files["permission-coverage.json"])!.AsObject();
        Assert.Equal(4,coverage.Count);Assert.Equal(result.Summary.PermissionNames,coverage["permission_names"]!.GetValue<long>());
        Assert.Equal(result.Summary.ManifestDefinitionRecords,coverage["manifest_definition_records"]!.GetValue<long>());
        Assert.Equal(sourceCatalog,coverage["source_catalog"]!.GetValue<string>());Assert.False(coverage["complete"]!.GetValue<bool>());
    }
}
public sealed class ScriptedPermissionExportProbe(string? mutation=null):IPermissionExportProbe
{
    public Task<PermissionExportObservation> RunAsync(PermissionExportCase test)
    {
        const string root="/operator-selected/sealed-release";var hash=new string('f',64);
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);PermissionExportResult? result=null;Exception? error=null;
        byte[] Bytes(JsonNode value)=>Encoding.UTF8.GetBytes(value.ToJsonString());
        if(PermissionExportCases.Succeeds(test))
        {
            files["permissions.jsonl"]=Encoding.UTF8.GetBytes(string.Join("\n",PermissionExportCases.Names(test).Select(name=>PermissionExportCases.Summary(name,test).ToJsonString()))+(PermissionExportCases.Names(test).Length>0?"\n":""));
            files["permission-expression-samples.json"]=Bytes(PermissionExportCases.Samples(test));
            var summary=new PermissionExportSummary(PermissionExportCases.Names(test).LongLength,PermissionExportCases.Definitions(test).LongLength,root,false);
            files["permission-coverage.json"]=Bytes(new JsonObject{["permission_names"]=summary.PermissionNames,["manifest_definition_records"]=summary.ManifestDefinitionRecords,["source_catalog"]=root,["complete"]=false});
            result=new(summary,files.Select(pair=>new StoredArtifact(pair.Key,ContentHash.Sha256(pair.Value),pair.Value.Length,pair.Key.EndsWith(".jsonl",StringComparison.Ordinal)?"application/x-ndjson":"application/json")).ToArray());
            if(mutation=="count")result=result with{Summary=summary with{PermissionNames=summary.PermissionNames+1}};
            if(mutation=="complete")result=result with{Summary=summary with{Complete=true}};
            if(mutation=="source")result=result with{Summary=summary with{SourceCatalog="wrong"}};
            if(mutation=="artifact-hash")result=result with{Artifacts=result.Artifacts.Select(artifact=>artifact with{Sha256=new string('0',64)}).ToArray()};
            if(mutation=="artifact-size")result=result with{Artifacts=result.Artifacts.Select(artifact=>artifact with{Length=artifact.Length+1}).ToArray()};
            if(mutation=="artifact-media")result=result with{Artifacts=result.Artifacts.Select(artifact=>artifact with{MediaType="wrong"}).ToArray()};
            if(mutation=="omit")files.Remove("permissions.jsonl");
            if(mutation=="extra")files["unexpected.json"]=[];
            if(mutation=="content")files["permission-expression-samples.json"]="[false]"u8.ToArray();
            if(mutation?.StartsWith("doc-",StringComparison.Ordinal)==true)
            {
                var lines=Encoding.UTF8.GetString(files["permissions.jsonl"]).Split('\n',StringSplitOptions.RemoveEmptyEntries);
                var document=JsonNode.Parse(lines[0])!.AsObject();
                var field=mutation[4..];document[field]=field is "manifest_definitions" or "symbols"?new JsonArray():JsonValue.Create("WRONG");
                lines[0]=document.ToJsonString();files["permissions.jsonl"]=Encoding.UTF8.GetBytes(string.Join("\n",lines)+"\n");
            }
            if(mutation?.StartsWith("coverage-",StringComparison.Ordinal)==true)
            {
                var coverage=JsonNode.Parse(files["permission-coverage.json"])!.AsObject();var field=mutation[9..];
                coverage[field]=field switch{"complete"=>JsonValue.Create(true),"source_catalog"=>JsonValue.Create("WRONG"),_=>JsonValue.Create(99)};
                files["permission-coverage.json"]=Bytes(coverage);
            }
            // Semantic controls carry correct updated integrity receipts, so
            // rejection must come from the original content predicates.
            if(mutation=="content"||mutation?.StartsWith("doc-",StringComparison.Ordinal)==true||mutation?.StartsWith("coverage-",StringComparison.Ordinal)==true)
                result=result with{Artifacts=result.Artifacts.Select(artifact=>artifact with{Sha256=ContentHash.Sha256(files[artifact.RelativePath]),Length=files[artifact.RelativePath].Length}).ToArray()};
            if(mutation=="outcome"){result=null;error=new PermissionExportException("WRONG","Wrong failure");}
        }
        else
        {
            error=test.Mode=="cancel"?new OperationCanceledException():new PermissionExportException(PermissionExportCases.Code(test),"Expected export refusal");
            if(test.Mode=="write-failure")files["permissions.jsonl"]=[];
            if(mutation=="outcome")error=null;
            if(mutation=="error")error=new InvalidOperationException("Wrong error type");
            if(mutation=="marker")files["permission-coverage.json"]="{}"u8.ToArray();
        }
        bool? readOnly=PermissionExportCases.Opens(test)?true:null;
        if(mutation=="mode")readOnly=readOnly is null?true:false;
        return Task.FromResult(new PermissionExportObservation(root,hash,mutation=="input"?new string('0',64):hash,readOnly,result,files,error,
            test.Mode=="nonempty"?"keep"u8.ToArray():null,test.Mode=="nonempty"?(mutation=="existing"?"wrong"u8.ToArray():"keep"u8.ToArray()):null));
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class PermissionExportMockTests
{
    [Theory,MemberData(nameof(PermissionExportCases.Data),MemberType=typeof(PermissionExportCases))]
    public Task CorrectMocksPreserveOriginalExportResponsibilities(PermissionExportCase test)=>PermissionExportCases.AssertCase(test,new ScriptedPermissionExportProbe());
    [Theory,MemberData(nameof(PermissionExportCases.Data),MemberType=typeof(PermissionExportCases))]
    public async Task EachOutputAndEffectHasAnIndependentWrongControl(PermissionExportCase test)
    {
        var mutations=PermissionExportCases.Succeeds(test)?new[]{"input","mode","outcome","count","complete","source","artifact-hash","artifact-size","artifact-media","omit","extra","content",
            "coverage-permission_names","coverage-manifest_definition_records","coverage-source_catalog","coverage-complete"}:new[]{"input","mode","outcome","error","marker"};
        if(PermissionExportCases.Succeeds(test)&&PermissionExportCases.Names(test).Length>0)mutations=mutations.Concat(new[]{"doc-schema_version","doc-permission_id","doc-name","doc-manifest_definitions","doc-symbols","doc-metadata_status","doc-os_authority","doc-mcp_client_grants"}).ToArray();
        if(test.Mode=="nonempty")mutations=mutations.Append("existing").ToArray();
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>PermissionExportCases.AssertCase(test,new ScriptedPermissionExportProbe(mutation))));
    }
}
