using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogHidlImportProvider:ICatalogHidlImportCaseProvider
{
    public Task<CatalogHidlImportReply> ImportAsync(CatalogHidlImportCall call)=>Task.FromResult(Import(call));
    private static CatalogHidlImportReply Import(CatalogHidlImportCall call)
    {
        using var runtime=new SourcesRuntimeFactory().Create(SourceExtractionProfile.CurrentCandidate);
        using var provider=new NativeCatalogBuildProvider
        {
            SourceDispatcher=runtime.Dispatcher,
            SourceValues=values=>values.Add(("evidence",call.Path,"owned fixture preparation placeholder")),
            InventorySetup=(inventory,root,originals)=>
            {
                var source=inventory["sources"]!.AsArray().OfType<JsonObject>().Single(value=>value["path"]!.GetValue<string>()==call.Path);
                File.WriteAllBytes(Path.Combine(root,call.Path),call.Raw);originals[call.Path]=call.Raw.ToArray();
                source["sha256"]=ContentHash.Sha256(call.Raw);source["git_blob"]=ContentHash.GitBlobSha1Legacy(call.Raw);source["size_bytes"]=call.Raw.Length;
                source["scope"]=call.Scope;source["api_level"]=null;source["platform_api_version"]=call.PlatformVersion;
                source["channel"]=call.Path=="IFoo.hal"?"unknown":"stable";
                if(call.Path=="raw-schema")source["source_release_context"]=new JsonObject{["platform_api_version"]="37.0"};
            }
        };
        var actual=provider.Build(new("hidl-import"));var output=Path.Combine(provider.EvidenceRoot,"out");
        if(actual.Outcome!="ok")
        {
            var receipt=JsonNode.Parse(File.ReadAllText(Path.Combine(output,"validation-failure.json")))!;var message=receipt["error"]!.GetValue<string>();
            Assert.Contains("does not establish Android applicability",message,StringComparison.Ordinal);
            return new(actual.Outcome,actual.FinalDatabase,ErrorContains:"does not establish Android applicability",OriginalInputsRetained:actual.OriginalInputsRetained);
        }
        using var database=CatalogDatabase.OpenExisting(Path.Combine(output,"catalog.sqlite"));
        if(call.Variant=="structural")
        {
            using var query=database.Command(call.Sql);using var rows=query.ExecuteReader();var docs=new List<JsonObject>();while(rows.Read())docs.Add(JsonNode.Parse(rows.GetString(0))!.AsObject());
            var method=Assert.Single(docs,value=>value["kind"]!.GetValue<string>()=="method");
            return new("ok",actual.FinalDatabase,HidlEntries:docs.Count,MethodResults:method["extensions"]!["results"]!.AsArray().Count,
                PermissionOp:method["permission"]!["op"]!.GetValue<string>(),CodecStatus:method["codec_status"]!.GetValue<string>(),
                ContainsPoint:docs.Any(doc=>doc["canonical_signature"]!.GetValue<string>()=="hidl:hardware.test@2.7::IFoo.Point"),
                EveryAvailabilityUnknown:docs.All(doc=>doc["extensions"]!["device_resolution_variants"]!.AsArray().All(variant=>variant!["availability"]!.AsObject().ContainsKey("unknown_reason"))),
                OriginalInputsRetained:actual.OriginalInputsRetained);
        }
        if(call.Variant=="invalid-hidl")
        {
            using var query=database.Command(call.Sql);using var rows=query.ExecuteReader();var values=new List<string>();while(rows.Read())values.Add(rows.GetString(0));
            var raw=Assert.Single(values);rows.Close();
            using var count=database.Command("SELECT count(*) FROM registry_entries WHERE json_extract(document_json,'$.extensions.language')='hidl'");
            return new("ok",actual.FinalDatabase,HidlEntries:(int)(long)count.ExecuteScalar()!,QueriedIssueRows:values.Count,
                DecodedRawHex:Convert.ToHexStringLower(Convert.FromBase64String(raw)),OriginalInputsRetained:actual.OriginalInputsRetained);
        }
        using(var query=database.Command(call.Sql))
        using(var rows=query.ExecuteReader())
        {
            var values=new List<(string Raw,JsonObject Report)>();while(rows.Read())values.Add((rows.GetString(0),JsonNode.Parse(rows.GetString(1))!.AsObject()));
            var row=Assert.Single(values);var report=row.Report;
            if(call.Variant=="retained-native")return new("ok",actual.FinalDatabase,QueriedIssueRows:values.Count,
                DecodedRawHex:Convert.ToHexStringLower(Convert.FromBase64String(row.Raw)),FlatRawEncoding:report["raw_source_encoding"]!.GetValue<string>(),OriginalInputsRetained:actual.OriginalInputsRetained);
            return new("ok",actual.FinalDatabase,QueriedIssueRows:values.Count,SqlRawText:row.Raw,FlatRawEncoding:report["raw_source_encoding"]!.GetValue<string>(),
                AtLeastTwoDeclarations:report["declarations"]!.GetValue<int>()>=2,FlatNormalizerComplete:report["normalizer_complete"]!.GetValue<bool>(),
                FlatCompilerValidation:report["compiler_validation"]!.GetValue<bool>(),FlatWireResolution:report["wire_resolution"]!.GetValue<string>(),
                FlatAndroidAvailability:report["android_availability"]!.GetValue<string>(),OriginalInputsRetained:actual.OriginalInputsRetained);
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
