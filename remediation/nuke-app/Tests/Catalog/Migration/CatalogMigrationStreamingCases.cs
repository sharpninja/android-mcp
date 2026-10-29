using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog.Migration;

public sealed record MigrationStreamingCall(string Variant);
public sealed record MigrationStreamingCase(MigrationStreamingCall Call,JsonObject Expected);
public interface IMigrationStreamingProvider { Task<JsonObject> ExecuteAsync(MigrationStreamingCall call); }
public static class CatalogMigrationStreamingCases
{
    public static IEnumerable<object[]> All=>Generate().Select(test=>new object[]{test});
    public static IEnumerable<MigrationStreamingCase> Generate()
    {
        foreach(var variant in new[]{"bounded-copy","source-hash-change","stage-hash-change","source-identity-change","stage-identity-change","source-long-read-fault","stage-long-read-fault","source-nonseekable"})
        {
            var outcome=variant switch
            {
                "bounded-copy"=>"migrated",
                "source-hash-change" or "source-identity-change"=>"source-changed",
                "stage-hash-change" or "stage-identity-change"=>"stage-changed",
                "source-long-read-fault"=>"long-source-read-fault",
                "stage-long-read-fault"=>"long-stage-read-fault",
                _=>"seekable-required"
            };
            yield return new(new(variant),new JsonObject
            {
                ["outcome"]=outcome,["source_session_opened_once"]=true,
                ["stage_session_opened_once"]=variant is "bounded-copy" or "stage-hash-change" or "stage-identity-change" or "stage-long-read-fault",
                ["bounded_read_requests"]=true,["same_sessions_rewound"]=true,["every_session_disposed"]=true,
                ["source_bytes_retained"]=true,["stage_bytes_retained"]=true,["final_absent"]=variant!="bounded-copy",
                ["long_length_reached_read_without_truncation"]=true,["success_pins_match_actual_stream_hashes"]=true
            });
        }
    }
    public static async Task AssertCase(MigrationStreamingCase test,IMigrationStreamingProvider provider)
    {
        var before=test.Call with{};var result=await provider.ExecuteAsync(test.Call);
        Assert.True(JsonNode.DeepEquals(test.Expected,result),$"{test.Call.Variant}: expected {test.Expected}; actual {result}");Assert.Equal(before,test.Call);
    }
}
public sealed class ScriptedMigrationStreaming(MigrationStreamingCase test,string? wrong=null):IMigrationStreamingProvider
{
    public Task<JsonObject> ExecuteAsync(MigrationStreamingCall call)
    {
        Assert.Equal(test.Call,call);var result=test.Expected.DeepClone().AsObject();
        if(wrong=="$omit")result.Remove("bounded_read_requests");
        else if(wrong is not null){var value=result[wrong];result[wrong]=value is JsonValue scalar&&scalar.TryGetValue<bool>(out var flag)?JsonValue.Create(!flag):JsonValue.Create("wrong outcome");}
        return Task.FromResult(result);
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004"),Trait("Requirement","TEST-BUILD-007")]
public sealed class CatalogMigrationStreamingMockTests
{
    [Theory,MemberData(nameof(CatalogMigrationStreamingCases.All),MemberType=typeof(CatalogMigrationStreamingCases))]
    public Task CorrectStreamingLongLengthAndRecheckMocksPass(MigrationStreamingCase test)=>CatalogMigrationStreamingCases.AssertCase(test,new ScriptedMigrationStreaming(test));
    [Theory,MemberData(nameof(CatalogMigrationStreamingCases.All),MemberType=typeof(CatalogMigrationStreamingCases))]
    public async Task EveryBoundHashIdentityRetentionAndLongLengthAssertionDiscriminates(MigrationStreamingCase test)
    {
        foreach(var field in test.Expected.Select(pair=>pair.Key).Append("$omit"))
            Assert.NotNull(await Record.ExceptionAsync(()=>CatalogMigrationStreamingCases.AssertCase(test,new ScriptedMigrationStreaming(test,field))));
    }
}
