using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativeCatalogSourceNormalizationProvider:ICatalogSourceNormalizationCaseProvider
{
    public CatalogSourceNormalizationReply Normalize(CatalogSourceNormalizationCall call)
    {
        var canonical=new CanonicalJson();var normalizer=new CatalogSourceNormalizer(canonical);
        // Same extraction context as the shared SDK recipe: absent Catalog API
        // and channel, exact source context37.2. This pure component neither
        // parses a recipe nor establishes acquisition/cache provenance.
        var original=JsonSerializer.SerializeToElement(new{path="sdk/api.xml",local_path="raw/api.xml",kind="sdk-api-index",
            status="failed",platform_api_version="37.2",repository="fixture-repository",revision="pinned-revision"});
        var context=new RehydrationRowContext("fixture-repository","pinned-revision","fixture-surface","source","fixture-license",
            JsonSerializer.SerializeToElement(new{platform_api_version="37.2"}),[],[]);
        var row=new RehydrationSourceRow(0,"sdk/api.xml","sdk-api-index",0,null,context,original);
        var projection=new RehydrationSourceProjection("fixture",new string('a',64),AcquisitionCompatibilityProfile.CanonicalRestore,[row],[],[],[0],true);
        var sourceContext=new SourceContext("fixture-repository","pinned-revision",projection.RecipeSha256,"37.2");
        var unusedGlobalMetadata=new CatalogPipelineMetadata(1,"fixture",999,"partial fixture");
        Assert.Equal(999,unusedGlobalMetadata.ApiLevel);
        var projectionBefore=canonical.Serialize(JsonSerializer.SerializeToElement(projection));var rowBefore=canonical.Serialize(row.OriginalRow);
        var contextBefore=canonical.Serialize(JsonSerializer.SerializeToElement(sourceContext));
        var metadata=new CatalogSourceNormalization(0,canonical.Hash(original),call.Variant=="null-api"?null:37,"fixture");
        if(call.Variant=="foreign-row")metadata=metadata with{RowIndex=7};
        if(call.Variant=="foreign-hash")metadata=metadata with{ExpectedOriginalRowCanonicalSha256=new string('0',64)};
        if(call.Variant=="invalid-api")metadata=metadata with{ApiLevel=0};
        if(call.Variant=="empty-channel")metadata=metadata with{Channel=""};
        var sidecar=new JsonArray(JsonSerializer.SerializeToNode(metadata));
        if(call.Variant=="identity-field")sidecar[0]!["Kind"]="forged-source-kind";
        bool RowUnchanged()=>rowBefore.AsSpan().SequenceEqual(canonical.Serialize(row.OriginalRow));
        bool ProjectionUnchanged()=>projectionBefore.AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(projection)));
        bool ContextUnchanged()=>contextBefore.AsSpan().SequenceEqual(canonical.Serialize(JsonSerializer.SerializeToElement(sourceContext)));
        try
        {
            var decoded=normalizer.Decode(call.Variant=="missing"?null:JsonSerializer.SerializeToElement(sidecar));
            var result=normalizer.Normalize(projection,decoded,new HashSet<int>{0});var value=result[0];
            var audit=value["x-native-catalog-normalization"]!.AsObject();
            var provided=audit["provided"]!;
            var api=value["api_level"]?.GetValue<long>();
            var inferred=api!=metadata.ApiLevel || value["platform_api_version"]!.GetValue<string>()!="37.2";
            var identityPreserved=new[]{"kind","repository","revision","status"}.All(name=>
                JsonSerializer.SerializeToElement(value[name]).GetRawText()==original.GetProperty(name).GetRawText());
            return new("ok",api,value["channel"]!.GetValue<string>(),RowUnchanged(),ProjectionUnchanged(),ContextUnchanged(),
                audit["disposition"]!.GetValue<string>()=="provided-catalog-normalization" && !audit["source_evidence"]!.GetValue<bool>() &&
                audit["original_row_canonical_sha256"]!.GetValue<string>()==canonical.Hash(original) &&
                provided["ApiLevel"]?.GetValue<long>()==metadata.ApiLevel && identityPreserved,!inferred,true,RowUnchanged());
        }
        catch(CatalogBuildException error)
        {
            return new(error.Code,OriginalRowUnchanged:RowUnchanged(),ProjectionUnchanged:ProjectionUnchanged(),SourceContextUnchanged:ContextUnchanged(),InputsRetained:RowUnchanged());
        }
    }
}
[Trait("Gate","Real")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogSourceNormalizationRealTests
{
    [Theory,MemberData(nameof(CatalogSourceNormalizationCases.All),MemberType=typeof(CatalogSourceNormalizationCases))]
    public void NativeNormalizerUsesSameEightMetadataIdentityAndUnknownAssertions(CatalogSourceNormalizationCase test)=>
        CatalogSourceNormalizationCases.AssertCase(test,new NativeCatalogSourceNormalizationProvider());
}
