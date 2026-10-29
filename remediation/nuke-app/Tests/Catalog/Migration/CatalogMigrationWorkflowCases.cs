using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

public static class CatalogMigrationPlannerCases
{
    private static readonly (string Key,string Value)[] Required=[("project-root","project"),("pipeline-request","migrate.json"),("pipeline-request-sha256",new string('a',64)),("pipeline-request-length","123")];
    private static readonly string[] Steps=["admit-pinned-migration-request","admit-roots-and-source","copy-migrate-v3","emit-migration-receipt"];
    public static IEnumerable<object[]> All
    {
        get
        {
            var key=new OperationKey(Category.Catalog,Operation.Migrate);var edges=Steps.Skip(1).Select((step,index)=>step+"<-"+Steps[index]).ToArray();
            yield return new object[]{new PlannerCase("MIGRATE",PlannerContractCases.Request("Catalog","Migrate",Required),key,Steps,[],edges)};
            yield return new object[]{new PlannerCase("MIGRATE-CASE",PlannerContractCases.Request("catalog","migrate",Required),key,Steps,[],edges)};
            foreach(var pair in Required)yield return new object[]{new PlannerCase("MISSING-"+pair.Key,PlannerContractCases.Request("Catalog","Migrate",Required.Where(value=>value.Key!=pair.Key).ToArray()),null,[],["MISSING_PARAMETER:"+pair.Key])};
            foreach(var parameter in new[]{"source-artifact","catalog-root"})yield return new object[]{new PlannerCase("UNSUPPORTED-"+parameter,PlannerContractCases.Request("Catalog","Migrate",Required.Append((parameter,"value")).ToArray()),null,[],["UNSUPPORTED_PARAMETER:"+parameter])};
        }
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogMigrationPlannerMockTests
{
    [Theory,MemberData(nameof(CatalogMigrationPlannerCases.All),MemberType=typeof(CatalogMigrationPlannerCases))]
    public void CorrectPlansPass(PlannerCase test)=>new PlannerMockGateTests().CorrectMocksPassEveryAssertion(test);
    [Theory,MemberData(nameof(CatalogMigrationPlannerCases.All),MemberType=typeof(CatalogMigrationPlannerCases))]
    public void IndependentWrongPlansEffectsEdgesAndErrorsReject(PlannerCase test)
    {var gate=new PlannerMockGateTests();gate.WrongEffectsAreRejectedInsidePassingHarness(test);gate.WrongKeysPlansAndErrorsAreRejected(test);gate.IndividualKeyStepAndErrorMutationsAreRejected(test);}
}
public sealed record MigrationWorkflowCall(string Variant);
public sealed record MigrationWorkflowCase(MigrationWorkflowCall Call,JsonObject Expected);
public interface IMigrationWorkflowProvider { Task<JsonObject> ExecuteAsync(MigrationWorkflowCall call); }
public static class CatalogMigrationWorkflowCases
{
    public const string RequestError="CATALOG_MIGRATION_REQUEST";
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<MigrationWorkflowCase> Generate()
    {
        foreach(var variant in new[]{"frozen-success","current-success","destination-exists","stage-exists","version-one","bad-sha","bad-length","missing-catalog","missing-source","missing-destination","blank-catalog","blank-source","blank-destination","unknown-member","duplicate-member","malformed","source-reparse","destination-nul"})
        {
            var success=variant.EndsWith("-success",StringComparison.Ordinal);
            var error=variant switch{"destination-exists" or "stage-exists"=>"Destination must be new","version-one"=>"Only storage user_version=2 can migrate to v3","bad-sha"=>"ARTIFACT_PIN_HASH","bad-length"=>"ARTIFACT_PIN_LENGTH","source-reparse"=>"reparse points","destination-nul"=>"Null character",_=>success?"":RequestError};
            yield return new(new(variant),new JsonObject
            {
                ["exit"]=success?0:1,["error"]=error,["request_retained"]=true,["source_bytes_retained"]=true,["host_dll_pinned"]=true,["runtime_root_separate"]=true,
                ["arguments_exact"]=true,["no_build_restore_or_probe"]=true,["existing_files_retained"]=true,["final_absent"]=!success&&variant!="destination-exists",
                ["receipt_count"]=success?1:0,["observed_artifact_pins_exact"]=true,["target_script_pins_exact"]=true,["registry_generation"]=success?5:0,["rebuilt_false"]=success,
                ["request_failure_no_stage"]=true
            });
        }
    }
    public static JsonObject Payload(MigrationWorkflowCall call,string catalog)
    {
        var file=new JsonObject{["CatalogRoot"]=catalog,["SourceDatabase"]="old.sqlite",["DestinationDatabase"]="new.sqlite"};
        var field=call.Variant.EndsWith("catalog",StringComparison.Ordinal)?"CatalogRoot":call.Variant.EndsWith("source",StringComparison.Ordinal)?"SourceDatabase":"DestinationDatabase";
        if(call.Variant.StartsWith("missing-",StringComparison.Ordinal))file.Remove(field);
        if(call.Variant.StartsWith("blank-",StringComparison.Ordinal))file[field]=" ";
        if(call.Variant=="unknown-member")file["InventedAuthority"]=true;
        if(call.Variant=="source-reparse")file["SourceDatabase"]="linked.sqlite";
        if(call.Variant=="destination-nul")file["DestinationDatabase"]="bad\0.sqlite";
        return file;
    }
    public static async Task AssertCase(MigrationWorkflowCase test,IMigrationWorkflowProvider provider)
    {var before=test.Call with{};var observed=await provider.ExecuteAsync(test.Call);Assert.True(JsonNode.DeepEquals(test.Expected,observed),$"{test.Call}: expected {test.Expected}; actual {observed}");Assert.Equal(before,test.Call);}
}
public sealed class ScriptedMigrationWorkflow(MigrationWorkflowCase test,string? wrong=null):IMigrationWorkflowProvider
{
    public Task<JsonObject> ExecuteAsync(MigrationWorkflowCall call)
    {
        Assert.Equal(test.Call,call);var value=test.Expected.DeepClone().AsObject();
        if(wrong=="$omit")value.Remove("request_failure_no_stage");
        else if(wrong is not null){var field=value[wrong];value[wrong]=field is JsonValue scalar&&scalar.TryGetValue<bool>(out var flag)?JsonValue.Create(!flag):JsonValue.Create("wrong");}
        return Task.FromResult(value);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMigrationWorkflowMockTests
{
    [Theory,MemberData(nameof(CatalogMigrationWorkflowCases.All),MemberType=typeof(CatalogMigrationWorkflowCases))]
    public Task CorrectPinnedRequestAndCompiledCallerMocksPass(MigrationWorkflowCase test)=>CatalogMigrationWorkflowCases.AssertCase(test,new ScriptedMigrationWorkflow(test));
    [Theory,MemberData(nameof(CatalogMigrationWorkflowCases.All),MemberType=typeof(CatalogMigrationWorkflowCases))]
    public async Task EveryRequestPathPinReceiptAndRetentionAssertionDiscriminates(MigrationWorkflowCase test)
    {foreach(var key in test.Expected.Select(pair=>pair.Key).Append("$omit"))Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMigrationWorkflowCases.AssertCase(test,new ScriptedMigrationWorkflow(test,key))));}
}
