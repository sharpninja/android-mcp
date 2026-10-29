using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogHidlImportProvider:ICatalogHidlImportCaseProvider
{
    internal JsonObject? RetentionReport {get;private set;}
    internal JsonObject? ObservedSource {get;private set;}
    public Task<CatalogHidlImportReply> ImportAsync(CatalogHidlImportCall call)=>Task.FromResult(Import(call));
    private CatalogHidlImportReply Import(CatalogHidlImportCall call)
    {
        JsonObject? fixtureSource=null;var fixtureSourceCount=0;var published=call.Variant.StartsWith("published-",StringComparison.Ordinal);
        CatalogHidlImportReply Context(CatalogHidlImportReply reply)
        {
            ObservedSource=(JsonObject)fixtureSource!.DeepClone();
            return reply with{FixtureUrl=fixtureSource["url"]!.GetValue<string>(),FixtureChannel=fixtureSource["channel"]!.GetValue<string>(),
                FixtureSourceCount=fixtureSourceCount,ReleaseContextRetained=call.Path!="raw-schema" || fixtureSource["source_release_context"]?["platform_api_version"]?.GetValue<string>()=="37.0"};
        }
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        using var provider=new NativeCatalogBuildProvider
        {
            SourceDispatcher=runtime.Dispatcher,
            SourceValues=values=>values.Add(("evidence",call.Path,"owned fixture preparation placeholder")),
            InventorySetup=(inventory,root,originals)=>
            {
                // Original helpers deep-copy source[2] for HIDL and source[0]
                // for raw retention. In particular their URL is inherited.
                var sources=inventory["sources"]!.AsArray();var index=sources.Select((value,index)=>(value,index)).Single(item=>item.value!["path"]!.GetValue<string>()==call.Path).index;
                var source=(JsonObject)sources[call.Path=="IFoo.hal"?2:0]!.DeepClone();sources[index]=source;
                source["kind"]="evidence";source["path"]=call.Path;source["local_path"]=call.Path;
                File.WriteAllBytes(Path.Combine(root,call.Path),call.Raw);originals[call.Path]=call.Raw.ToArray();
                source["sha256"]=ContentHash.Sha256(call.Raw);source["git_blob"]=ContentHash.GitBlobSha1Legacy(call.Raw);source["size_bytes"]=call.Raw.Length;
                source["scope"]=call.Scope;source["api_level"]=null;source["platform_api_version"]=call.PlatformVersion;
                source["channel"]=published || call.Path=="IFoo.hal"?"unknown":"stable";
                // Frozen SourceTargets.source overrides URL/channel and does
                // not add the local fixture's source_release_context.
                if(published)source["url"]="https://dl.google.com/fixture/"+call.Path;
                if(call.Path=="raw-schema" && !published)source["source_release_context"]=new JsonObject{["platform_api_version"]="37.0"};
                fixtureSource=source;fixtureSourceCount=sources.Count;
            }
        };
        var actual=provider.Build(new("hidl-import"));var output=Path.Combine(provider.EvidenceRoot,"out");
        if(actual.Outcome!="ok")
        {
            var receipt=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"validation-failure.json")))!;var message=receipt["error"]!.GetValue<string>();
            Assert.Contains("does not establish Android applicability",message,StringComparison.Ordinal);
            return Context(new(actual.Outcome,actual.FinalDatabase,ErrorContains:"does not establish Android applicability",OriginalInputsRetained:actual.OriginalInputsRetained));
        }
        using var database=CatalogDatabase.OpenExisting(Path.Combine(output,"catalog.sqlite"));
        using(var context=database.Command("SELECT document_json FROM source_details WHERE source_id=(SELECT source_id FROM sources WHERE path=$path)",null,("$path",call.Path)))
            fixtureSource=JsonNode.Parse((string)context.ExecuteScalar()!)!.AsObject();
        if(call.Variant=="structural")
        {
            using var query=database.Command(call.Sql);using var rows=query.ExecuteReader();var docs=new List<JsonObject>();while(rows.Read())docs.Add(JsonNode.Parse(rows.GetString(0))!.AsObject());
            var method=Assert.Single(docs,value=>value["kind"]!.GetValue<string>()=="method");
            return Context(new("ok",actual.FinalDatabase,HidlEntries:docs.Count,MethodResults:method["extensions"]!["results"]!.AsArray().Count,
                PermissionOp:method["permission"]!["op"]!.GetValue<string>(),CodecStatus:method["codec_status"]!.GetValue<string>(),
                ContainsPoint:docs.Any(doc=>doc["canonical_signature"]!.GetValue<string>()=="hidl:hardware.test@2.7::IFoo.Point"),
                EveryAvailabilityUnknown:docs.All(doc=>doc["extensions"]!["device_resolution_variants"]!.AsArray().All(variant=>variant!["availability"]!.AsObject().ContainsKey("unknown_reason"))),
                OriginalInputsRetained:actual.OriginalInputsRetained));
        }
        if(call.Variant=="invalid-hidl")
        {
            using var query=database.Command(call.Sql);using var rows=query.ExecuteReader();var values=new List<string>();while(rows.Read())values.Add(rows.GetString(0));
            var raw=Assert.Single(values);rows.Close();
            using var count=database.Command("SELECT count(*) FROM registry_entries WHERE json_extract(document_json,'$.extensions.language')='hidl'");
            return Context(new("ok",actual.FinalDatabase,HidlEntries:(int)(long)count.ExecuteScalar()!,QueriedIssueRows:values.Count,
                DecodedRawHex:Convert.ToHexStringLower(Convert.FromBase64String(raw)),OriginalInputsRetained:actual.OriginalInputsRetained));
        }
        using(var query=database.Command(call.Sql))
        using(var rows=query.ExecuteReader())
        {
            var values=new List<(string Raw,JsonObject Report)>();while(rows.Read())values.Add((rows.GetString(0),JsonNode.Parse(rows.GetString(1))!.AsObject()));
            var row=Assert.Single(values);var report=row.Report;RetentionReport=(JsonObject)report.DeepClone();
            if(call.Variant is "retained-native" or "published-retained-native")return Context(new("ok",actual.FinalDatabase,QueriedIssueRows:values.Count,
                DecodedRawHex:Convert.ToHexStringLower(Convert.FromBase64String(row.Raw)),FlatRawEncoding:report["raw_source_encoding"]!.GetValue<string>(),OriginalInputsRetained:actual.OriginalInputsRetained));
            return Context(new("ok",actual.FinalDatabase,QueriedIssueRows:values.Count,SqlRawText:row.Raw,FlatRawEncoding:report["raw_source_encoding"]!.GetValue<string>(),
                AtLeastTwoDeclarations:report["declarations"]!.GetValue<int>()>=2,FlatNormalizerComplete:report["normalizer_complete"]!.GetValue<bool>(),
                FlatCompilerValidation:report["compiler_validation"]!.GetValue<bool>(),FlatWireResolution:report["wire_resolution"]!.GetValue<string>(),
                FlatAndroidAvailability:report["android_availability"]!.GetValue<string>(),OriginalInputsRetained:actual.OriginalInputsRetained));
        }
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogHidlImportRealTests
{
    [Theory,MemberData(nameof(CatalogHidlImportCases.All),MemberType=typeof(CatalogHidlImportCases))]
    public Task SameFiveOriginalSqlRawAndPublicReportAssertions(CatalogHidlImportCase test)=>CatalogHidlImportCases.AssertCase(test,new NativeCatalogHidlImportProvider());
}
