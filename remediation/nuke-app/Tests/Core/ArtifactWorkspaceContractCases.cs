using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public interface IArtifactWorkspaceCaseProvider { Task<JsonObject> ExecuteAsync(string scenario); }
public sealed record ArtifactWorkspaceCase(string Id, JsonObject Expected)
{
    public SourceExtractionCase MutationCase() => new(Id,"artifact-workspace",Id,StructuralParseStatus.Clean,[Expected],[],"Original documentation bootstrap OR0111/chmod0755 and read-only checker tree boundaries");
}
public static class ArtifactWorkspaceContractCases
{
    private static JsonArray A(params string[] text) => new(text.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
    private static JsonObject Permission(string? error=null,int before=420,int after=493,int sets=1,int hashes=0,params string[] trace) => new()
    {
        ["error"]=error,["previous_mode"]=before,["applied_mode"]=after,["set_calls"]=sets,["hash_calls"]=hashes,
        ["same_owned_handle"]=true,["all_handles_closed"]=true,["identity_matches"]=true,["original_retained"]=true,["content_unchanged"]=true,["trace"]=A(trace)
    };
    private static JsonObject Entry(string path,string kind) => new(){["path"]=path,["kind"]=kind,["identity_matches"]=true,["length_matches"]=true};
    private static JsonObject Tree(bool immediate=false,bool changed=false) => new()
    {
        ["error"]=changed?"IOException":null,
        ["entries"]=changed?new JsonArray():immediate?new JsonArray(Entry("sub/nested","Directory"),Entry("sub/tool.txt","RegularFile")):
            new JsonArray(Entry(".git","Directory"),Entry("link","Other"),Entry("private.txt","RegularFile"),Entry("sub","Directory"),Entry("sub/nested","Directory"),Entry("sub/nested/deep.md","RegularFile"),Entry("sub/tool.txt","RegularFile"),Entry("untracked.md","RegularFile")),
        ["set_calls"]=0,["hash_calls"]=0,["all_handles_closed"]=true,["original_retained"]=true,
        ["trace"]=immediate?A("enumerate:sub"):A("enumerate:","enumerate:sub","enumerate:sub/nested")
    };
    public static IReadOnlyList<ArtifactWorkspaceCase> All { get; } =
    [
        new("tree-full",Tree()),new("tree-immediate",Tree(true)),new("tree-changed",Tree(changed:true)),
        new("add-file",Permission(trace:["open","observe","get-mode","observe","set-mode","get-mode","observe"])),
        new("set-file",Permission(before:384,trace:["open","observe","get-mode","observe","set-mode","get-mode","observe"])),
        new("add-directory",Permission(before:448,after:457,trace:["open","observe","get-mode","observe","set-mode","get-mode","observe"])),
        new("pin-file",Permission(hashes:1,trace:["open","observe","hash","get-mode","observe","set-mode","get-mode","observe"])),
        new("pin-bad-hash",Permission("ARTIFACT_PIN_HASH",after:420,sets:0,hashes:1,trace:["open","observe","hash"])),
        new("pin-directory",Permission("ArgumentException",before:448,after:448,sets:0,trace:["open","observe"])),
        new("stale-open",Permission("IOException",after:420,sets:0,trace:["open","observe"])),
        new("changed-before-write",Permission("IOException",after:420,sets:0,trace:["open","observe","get-mode","observe"])),
        new("cancelled",Permission("OperationCanceledException",after:420,sets:0,trace:[])),
        new("cancel-before-write",Permission("OperationCanceledException",after:420,sets:0,trace:["open","observe","get-mode"])),
        new("unsupported",Permission("PlatformNotSupportedException",after:420,sets:0,trace:[])),
        new("invalid-operation",Permission("ArgumentException",after:420,sets:0,trace:[]))
    ];
    public static IEnumerable<object[]> Cases => All.Select(test => new object[]{test.Id});
    public static IEnumerable<object[]> Mutations => All.SelectMany(test => SourceMutationCases.For(test.MutationCase()));
    public static ArtifactWorkspaceCase Get(string id) => All.Single(test => test.Id==id);
    public static async Task AssertContract(ArtifactWorkspaceCase test,IArtifactWorkspaceCaseProvider provider)
    {
        var observed=await provider.ExecuteAsync(test.Id);
        Assert.True(JsonNode.DeepEquals(test.Expected,observed),"Workspace capability outcome differs: "+test.Id+" expected "+test.Expected.ToJsonString()+" observed "+observed.ToJsonString());
    }
}
