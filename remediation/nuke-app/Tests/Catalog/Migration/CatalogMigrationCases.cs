using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

public sealed record CatalogMigrationCall(string Variant,string TargetProfile);
public sealed record CatalogMigrationCase(string Id,string[] OriginalCallers,CatalogMigrationCall Call,JsonObject Expected);
public interface ICatalogMigrationCaseProvider { Task<JsonObject> ExecuteAsync(CatalogMigrationCall call); }

public static class CatalogMigrationCases
{
    public const string Binding="5f87cd3688062954f3186c8fa5fd9eb0da6e9150837d816e29bf94d9889e82d0";
    public const string Hash="aaa9402664f1a41f40ebbc52c9993eb66aeb366602958fdfaa283b71e64db123";
    public static readonly string[] TargetNames=["001-initial.sql","002-evidence-and-publication.sql"];
    public static readonly string[] TargetHashes=["d7c9796cb53a455a569fbfc214c5efa74129627ee944c17a5ab3c3aed9b739ee","fc58a9b62816797c90a966c9f7ebb9dbd66a7abdc69386e6869a7e82d2a1a0a3"];
    public const string MetadataHash="2b585541c4463fc6b6ae408a80a573b81b635adc4206f960d2b7d8284e79a655";
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<CatalogMigrationCase> Generate()
    {
        foreach(var profile in new[]{"release-011","current"})
        foreach(var variant in new[]{"offline-analyze","published-five","retired-before-published","deleted-local-collision","matching-base","null-base","stale-base","transforms","no-active-empty","destination-exists","stage-exists","version-one","version-three","source-fk","source-corrupt","entries-without-active","target-constraint","publish-conflict","missing-overlay-history"})
        {
            var success=variant is "offline-analyze" or "published-five" or "retired-before-published" or "matching-base" or "transforms" or "no-active-empty";
            var stage=success||variant is "stage-exists" or "deleted-local-collision" or "null-base" or "stale-base" or "entries-without-active" or "target-constraint" or "publish-conflict" or "missing-overlay-history";
            var reason=variant switch
            {
                "destination-exists" or "stage-exists"=>"Destination must be new",
                "version-one" or "version-three"=>"Only storage user_version=2 can migrate to v3",
                "source-fk"=>"Source integrity/FK failure",
                "source-corrupt"=>"file is not a database",
                "entries-without-active"=>"No active registry for legacy effective entries",
                "deleted-local-collision"=>"Use an imported overlay for an active imported identity",
                "null-base" or "stale-base"=>"Imported overlay base must match active entry",
                "target-constraint"=>"CHECK constraint failed",
                "missing-overlay-history"=>"Migrated integrity/FK failure",
                "publish-conflict"=>"Artifact already exists and was retained",
                _=>""
            };
            var expected=new JsonObject
            {
                ["outcome"]=success?"migrated":"rejected",["diagnostic"]=reason,["source_bytes_unchanged"]=true,
                ["destination_exists"]=success||variant is "destination-exists" or "publish-conflict",["stage_exists"]=stage,
                ["existing_bytes_retained"]=true,["source_open_readonly"]=variant is not ("destination-exists" or "stage-exists"),
                ["target_list_exact"]=true,["failed_transaction_rolled_back"]=true
            };
            if(success)
            {
                expected["user_version"]=3;expected["rebuilt_with_corrected_extractor"]="false";expected["integrity"]="ok";expected["foreign_keys"]=0;
                expected["stage_equals_final"]=true;expected["schema_equals_exact_target"]=true;expected["metadata_tables"]=profile=="current";
                expected["registry_generation"]=variant is "published-five" or "retired-before-published"?5:1;
                expected["original_assertions"]=true;
            }
            var callers=variant switch
            {
                "offline-analyze"=>new[]{"test_remediation.test_offline_copy_migration"},
                "published-five"=>new[]{"test_round3.test_migration_seeds_published_version_and_marks_legacy"},
                "retired-before-published"=>new[]{"test_sealed_registry.test_copy_migration_restores_retired_before_published"},
                "deleted-local-collision"=>new[]{"test_round5.test_copy_migration_rejects_deleted_local_collision"},
                "matching-base" or "null-base" or "stale-base"=>new[]{"test_round8.test_copy_migration_admits_only_matching_imported_overlay_base"},
                _=>new[]{"migrate_v3.migrate body/transform/error/effect"}
            };
            yield return new("COPY-"+profile+"-"+variant,callers,new(variant,profile),expected);
        }
    }
    public static async Task AssertCase(CatalogMigrationCase test,ICatalogMigrationCaseProvider provider)
    {
        var original=test.Call with{};var result=await provider.ExecuteAsync(test.Call);
        Assert.True(JsonNode.DeepEquals(test.Expected,result),$"{test.Id}: expected {test.Expected}; actual {result}");
        Assert.Equal(original,test.Call);
    }
}
