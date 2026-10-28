using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogInventoryBridgeProvider:ICatalogInventoryBridgeCaseProvider
{
    public CatalogInventoryBridgeReply Adapt(CatalogInventoryBridgeCall call)
    {
        var canonical=new CanonicalJson();var selected=call.Variant=="none-selected" ? Array.Empty<int>() : call.Variant=="all-selected" ? new[]{0,1} : new[]{0};
        var context=new RehydrationRowContext("fixture/repository","pinned-revision","fixture-surface","source","fixture-license",JsonSerializer.SerializeToElement(new{platform="acquisition context only"}),[],["fixture-selection"]);
        var rows=Enumerable.Range(0,3).Select(index=>new RehydrationSourceRow(index,"logical/"+index+".txt","sdk-api-index",index<2 ? 0 : 1,index<2 ? 0 : null,context,
            JsonSerializer.SerializeToElement(new{path="logical/"+index+".txt",local_path=index<2 ? "physical/shared.txt" : "physical/other.txt",kind="sdk-api-index",status=call.Variant=="all-selected" ? "historical-failed" : "downloaded",platform_api_version="37.2",source_release_context=new{platform="acquisition context only"}}))).ToArray();
        var physical=new[]
        {
            new RehydrationPhysicalSource(0,new("physical/shared.txt",new("https://example.invalid/shared"),"pinned-revision",new string('a',64),17),[0,1],[],[]),
            new RehydrationPhysicalSource(1,new("physical/other.txt",new("https://example.invalid/other"),"pinned-revision",new string('b',64),23),[2],[],[])
        };
        var projection=new RehydrationSourceProjection("fixture",new string('c',64),AcquisitionCompatibilityProfile.CanonicalRestore,rows,physical,[new(0,"historical/shared.gz",null,[0,1],[])],selected,true);
        var statuses=selected.ToDictionary(index=>index,index=>index==0 && call.Variant=="all-selected" ? "downloaded" : "reused");
        var acquired=physical.Where(source=>selected.Contains(source.PhysicalSourceIndex)).Select(source=>new SourceBatchSource(source.PhysicalSourceIndex,
            new(new(source.Pin.LocalPath,source.Pin.Sha256,source.Pin.SizeBytes!.Value,"application/octet-stream"),statuses[source.PhysicalSourceIndex],source.Pin.Url,source.Pin.Revision,source.Pin.GitBlob))).ToArray();
        var selectedRows=rows.Count(row=>selected.Contains(row.PhysicalSourceIndex));
        var acquisitionReceipt=new SourceBatchReceipt("fixture-acquisition",new string('d',64),projection,SourceBatchCachePolicy.PreserveAliases,3,selectedRows,selectedRows,2,selected.Length,selected.Length==2,false,acquired,[],acquired.Select(source=>source.Source.Artifact).ToArray());
        var acquisition=new SourceBatchResult(acquisitionReceipt,new("sources/acquisition.json",new string('e',64),123,"application/json"),true);
        var entries=physical.Select(source=>new SourceInventoryEntry(source.PhysicalSourceIndex,selected.Contains(source.PhysicalSourceIndex) ? SourceInventoryDisposition.VerifiedSelected : SourceInventoryDisposition.NotSelected,
            acquired.SingleOrDefault(item=>item.PhysicalSourceIndex==source.PhysicalSourceIndex)?.Source)).ToArray();
        if(call.Variant=="missing-verified-source") entries[0]=entries[0] with{VerifiedSource=null};
        var receipt=new SourcesInventoryReceipt(acquisition,entries,selected.Length,2-selected.Length,selected.Length==2,false,false);
        var reader=new NoBridgeEffectsReader();var sourceProjection=call.Variant=="foreign-extraction" ? projection with{RecipeSha256=new string('f',64)} : projection;
        var extraction=new CatalogSourceIntake(reader,new(sourceProjection,SourceExtractionProfile.CurrentCandidate,new("csharp-fixture-binding",new Dictionary<string,string>{{"fixture",new string('9',64)}},"net10",new string('8',64),new string('7',64))));
        var metadata=new CatalogPipelineMetadata(call.Variant=="invalid-metadata" ? 0 : 7,"fixture Android",37,"selected coverage");
        var rawRoot=call.Variant=="relative-raw-root" ? "relative raw source" : Path.Combine(Path.GetPathRoot(Path.GetTempPath())!,"verified acquisition raw");
        var input=new CatalogInventoryBridgeInput(receipt,metadata,rawRoot,extraction);
        var before=canonical.Serialize(JsonSerializer.SerializeToElement(receipt));
        try
        {
            var result=new CatalogInventoryBridge(canonical).Adapt(input);var manifest=JsonNode.Parse(result.LegacyManifest.Bytes.Span)!;
            var exact=result.Projection.Rows.All(row=>canonical.Hash(JsonSerializer.SerializeToElement(manifest["sources"]![row.RowIndex]))==canonical.Hash(row.OriginalRow));
            var catalog=manifest["catalog"]!;var metadataMatches=catalog["registry_version"]!.GetValue<long>()==7 && catalog["android_version"]!.GetValue<string>()=="fixture Android" && catalog["api_level"]!.GetValue<long>()==37 && catalog["coverage"]!.GetValue<string>()=="selected coverage";
            var native=string.Join(';',result.NativeStatuses.OrderBy(item=>item.Key).Select(item=>item.Key+":"+item.Value));
            return new("ok",manifest["sources"]!.AsArray().Count,string.Join(',',result.VerifiedLogicalRows.Order()),native,exact,ReferenceEquals(result.Projection,projection),metadataMatches,result.AcquisitionRawRoot==rawRoot,
                ReferenceEquals(result.SourceExtraction,extraction),rows.Where(row=>!selected.Contains(row.PhysicalSourceIndex)).All(row=>!result.VerifiedLogicalRows.Contains(row.RowIndex) && result.NativeStatuses[row.RowIndex]=="not-selected"),
                result.Projection.Rows.Count(row=>row.PhysicalSourceIndex==0)==2 && result.Projection.Rows[0].LogicalPath!=result.Projection.Rows[1].LogicalPath,
                result.RequiresTypedContext && manifest["x-native-catalog-input"]!["requires_typed_context"]!.GetValue<bool>(),reader.Calls==0,before.AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(receipt))));
        }
        catch(CatalogBuildException error)
        {return new(error.Code,NoEffects:reader.Calls==0,InputsRetained:before.AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(receipt))));}
    }
    private sealed class NoBridgeEffectsReader:ISourceExtractGenerationReader
    {
        public int Calls{get;private set;}
        public Task<ISourceExtractGeneration> OpenAsync(SourceExtractGenerationRequest request,CancellationToken token=default){Calls++;throw new InvalidOperationException("Pure bridge must not perform Source reader admission");}
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogInventoryBridgeRealTests
{
    [Theory,MemberData(nameof(CatalogInventoryBridgeCases.All),MemberType=typeof(CatalogInventoryBridgeCases))]
    public void NativePureAdapterUsesSameSevenAssertions(CatalogInventoryBridgeCase test)=>CatalogInventoryBridgeCases.AssertCase(test,new NativeCatalogInventoryBridgeProvider());
}
