using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogDevicePipelineCall(string Variant);
public sealed record CatalogDevicePipelineReply(string Outcome, IReadOnlyDictionary<string,string> Facts,
    bool InputsRetained=true);
public sealed record CatalogDevicePipelineCase(string Id, string LegacyMethod,
    CatalogDevicePipelineCall Call, CatalogDevicePipelineReply Expected);
public interface ICatalogDevicePipelineCaseProvider
{
    Task<CatalogDevicePipelineReply> RunAsync(CatalogDevicePipelineCall call);
}
public static class CatalogDevicePipelineCases
{
    // Literal source bytes from test_device_catalog_pipeline.py. Source owns XML
    // extraction; Catalog owns composition, applicability and selected aliases.
    public const string HistoryXml="<api version=\"4\"><sdk id=\"30\"/><class name=\"a/Old\" since=\"1\" removed=\"36\"/><class name=\"a/Future\" since=\"38\"><method name=\"f()V\" since=\"1\"/></class><class name=\"a/Backport\" since=\"34\" sdks=\"30:4,0:34\"/></api>";
    public const string StructuredAidl="package a; interface I { parcelable P { int x; } void take(in P p); }";
    private static CatalogDevicePipelineReply Reply(params (string Key,string Value)[] facts) =>
        new("ok",facts.ToDictionary(value=>value.Key,value=>value.Value,StringComparer.Ordinal));
    public static IEnumerable<object[]> All => new[]
    {
        new CatalogDevicePipelineCase("PIPE-01-source-index-history-container", "test_index_retains_past_and_future_and_container_lifecycle",new("source-index"),Reply(
            ("retains.a/Old","true"),("retains.a/Future#f()V","true"),("future.member.container.since","38"),
            ("backport.extensions","30:4,0:34"))),
        new CatalogDevicePipelineCase("PIPE-02-build-inventory-resolve-atomic-store", "test_real_build_exports_inventory_and_resolves_before_atomic_storage",new("atomic-store"),Reply(
            ("inventory.count.equals-effective-entries","true"),("inventory.every-entity-valid","true"),
            ("snapshot.inventory-count.equals-raw","true"),("java:a.A#go(int).state@36.0","unknown"),
            ("published.snapshot.equals-resolved","true"),("read.stale.after-device-build-change","true"))),
        new CatalogDevicePipelineCase("PIPE-03-selected-alias-shared-schema-policy", "test_generated_aliases_pin_the_selected_device_schema_and_shared_policy",new("selected-aliases"),Reply(
            ("java:a.A#go(int).state@37.0","available"),("alias.schema.equals-selected-schema","true"),
            ("alias.graph.equals-selected-graph","true"),("generated.plan.equals-generic-plan","true"),
            ("stale.alias-generation.error","Fresh active"))),
        new CatalogDevicePipelineCase("PIPE-04-full-index-history-extensions", "test_full_index_build_applies_history_extensions_and_container_constraints",new("history-build"),Reply(
            ("jvm:a/Old@35.0","available"),("jvm:a/Old@36.0","unavailable"),
            ("jvm:a/Future#f()V@37.2","unavailable"),("jvm:a/Future#f()V@38.0","available"),
            ("jvm:a/Backport@33.0:30=4","available"),("jvm:a/Backport@33.0:30=3","unavailable"),
            ("jvm:a/Backport@33.0:no-extension","unknown"))),
        new CatalogDevicePipelineCase("PIPE-05-structured-AIDL-common-contract", "test_aidl_type_reference_proof_and_contract_digest_are_shared",new("structured-contract"),Reply(
            ("take(P).argument.status","resolved"),("take(P).argument.resolved-id.equals-aidl:a.I.P","true"),
            ("take(P).variant.schema.equals-contract-digest","true"),("take(P).variant.dependencies.contains-aidl:a.I.P","true"))),
        new CatalogDevicePipelineCase("PIPE-06-independent-inventory-export-check", "test_independent_validator_rejects_inventory_export_mutation",new("mutated-export"),Reply(
            ("appended-export-object.error","Inventory stream/manifest"),("original.db.retained","true"))),
        new CatalogDevicePipelineCase("PIPE-07-no-missing-projection-synthesis", "test_loader_rejects_missing_projection_without_synthesizing_rows",new("missing-projection"),Reply(
            ("missing-projection.error","Incomplete registry inventory"),("missing-projection.not-repaired","true")))
    }.Select(test=>new object[]{test});
    public static async Task AssertCase(CatalogDevicePipelineCase test,ICatalogDevicePipelineCaseProvider provider)
    {
        var actual=await provider.RunAsync(test.Call);
        Assert.Equal(test.Expected.Outcome,actual.Outcome);
        Assert.Equal(test.Expected.Facts.OrderBy(pair=>pair.Key,StringComparer.Ordinal),actual.Facts.OrderBy(pair=>pair.Key,StringComparer.Ordinal));
        Assert.Equal(test.Expected.InputsRetained,actual.InputsRetained);
    }
}
public sealed class ScriptedCatalogDevicePipeline(CatalogDevicePipelineCase test,string? wrongKey=null,bool omit=false,
    bool wrongOutcome=false,bool discardInput=false):ICatalogDevicePipelineCaseProvider
{
    public Task<CatalogDevicePipelineReply> RunAsync(CatalogDevicePipelineCall call)
    {
        Assert.Same(test.Call,call);var facts=test.Expected.Facts.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);
        if(wrongKey is not null){if(omit)facts.Remove(wrongKey);else facts[wrongKey]="wrong";}
        return Task.FromResult(test.Expected with{Outcome=wrongOutcome?"wrong":test.Expected.Outcome,Facts=facts,
            InputsRetained=discardInput?!test.Expected.InputsRetained:test.Expected.InputsRetained});
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogDevicePipelineMockTests
{
    [Theory,MemberData(nameof(CatalogDevicePipelineCases.All),MemberType=typeof(CatalogDevicePipelineCases))]
    public Task CorrectOriginalPipelineAssertionsPass(CatalogDevicePipelineCase test) =>
        CatalogDevicePipelineCases.AssertCase(test,new ScriptedCatalogDevicePipeline(test));
    [Theory,MemberData(nameof(CatalogDevicePipelineCases.All),MemberType=typeof(CatalogDevicePipelineCases))]
    public async Task EveryOriginalFactWrongAndMissingAndInputLossRejected(CatalogDevicePipelineCase test)
    {
        foreach(var key in test.Expected.Facts.Keys)
        {
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogDevicePipelineCases.AssertCase(test,new ScriptedCatalogDevicePipeline(test,key))));
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogDevicePipelineCases.AssertCase(test,new ScriptedCatalogDevicePipeline(test,key,true))));
        }
        Assert.NotNull(await Record.ExceptionAsync(()=>CatalogDevicePipelineCases.AssertCase(test,new ScriptedCatalogDevicePipeline(test,wrongOutcome:true))));
        Assert.NotNull(await Record.ExceptionAsync(()=>CatalogDevicePipelineCases.AssertCase(test,new ScriptedCatalogDevicePipeline(test,discardInput:true))));
    }
}
