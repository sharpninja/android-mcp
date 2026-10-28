using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

// Independent source-pinned expected values. These contracts are a test seam;
// a later adapter binds the unchanged inputs/assertions to native services.
public sealed record CatalogCall(string Operation, string? Signature = null, string? Key = null,
    string? ETag = null, JsonObject? Document = null, string? Resolution = null,
    string? ExpectedBase = null, IReadOnlyDictionary<string, (string Signature, JsonObject Document)>? Imports = null);
public sealed record CatalogReply(int Status, string? Key = null, string? ETag = null,
    string? State = null, int Revision = 0, int HistoryCount = 0,
    long RegistryGeneration = 1, long OverlayGeneration = 0, string? Document = null);
public interface ICatalogCaseProvider { CatalogReply Execute(CatalogCall call); }
public sealed record CatalogCase(string Id, CatalogCall[] Calls, CatalogReply[] Expected);

public static class CatalogCases
{
    public const string Signature = "java:a.A#x()";
    public const string Key = "5d65b8acc70bb725dd3e6966688038b149e6125fa8996dd37e3f9adafdaa7c4c";
    public const string BaseHash = "afbf9d0f3560b0fd7795e81c42a0a79ee6b6fc67e064f77826aee642cad28d91";
    public static string Tag(int registry, string state, int revision, string basis = "local") => $"\"cat1:{registry}:{basis}:{Key}:{revision}:{state}\"";
    public static JsonObject Doc(int value) => new() { ["v"] = value };
    public static CatalogCall Create() => new("create", Signature, Document: Doc(1));
    public static CatalogReply Local(int revision=1, string state="active", int history=1, int generation=1, string document="{\"v\":1}") =>
        new(200,Key,Tag(1,state,revision),state,revision,history,1,generation,document);
    public static IReadOnlyDictionary<string,(string Signature,JsonObject Document)> Import(string key=Key) =>
        new Dictionary<string,(string Signature,JsonObject Document)> { [key]=(Signature,Doc(1)) };

    public static IEnumerable<object[]> All => new[]
    {
        new CatalogCase("CAT-01-create-stable-identity",[Create()],[Local()]),
        new CatalogCase("CAT-02-duplicate",[Create(),Create()],[Local(),new(409)]),
        new CatalogCase("CAT-03-missing-etag",[Create(),new("put",Key:Key,Document:Doc(2))],[Local(),new(428)]),
        new CatalogCase("CAT-04-weak-etag",[Create(),new("put",Key:Key,ETag:"*",Document:Doc(2))],[Local(),new(400)]),
        new CatalogCase("CAT-05-not-found-after-valid-shape",[new("put",Key:Key,ETag:Tag(1,"active",1),Document:Doc(2))],[new(404)]),
        new CatalogCase("CAT-06-delete-idempotent-restore",[Create(),new("delete",Key:Key,ETag:Tag(1,"active",1)),new("delete",Key:Key,ETag:Tag(1,"deleted",2)),new("put",Key:Key,ETag:Tag(1,"deleted",2),Document:Doc(2))],
            [Local(),Local(2,"deleted",2,2),Local(2,"deleted",2,2),Local(3,"active",3,3,"{\"v\":2}")]),
        new CatalogCase("CAT-07-stale-etag",[Create(),new("delete",Key:Key,ETag:Tag(1,"active",1)),new("put",Key:Key,ETag:Tag(1,"active",1),Document:Doc(2))],
            [Local(),Local(2,"deleted",2,2),new(412)]),
        new CatalogCase("CAT-08-rejected-import-atomic",[Create(),new("publish",Imports:Import("wrong-id")),new("get",Key:Key)],
            [Local(),new(422),Local()]),
        new CatalogCase("CAT-09-cursor-expired",[Create(),new("capture-cursor"),new("put",Key:Key,ETag:Tag(1,"active",1),Document:Doc(2)),new("continue-cursor")],
            [Local(),new(200),Local(2,"active",2,2,"{\"v\":2}"),new(410)]),
        new CatalogCase("CAT-10-malformed-etag-before-notfound",[new("delete",Key:"absent",ETag:"*")],[new(400)]),
        new CatalogCase("CAT-11-imported-create-collision",[new("publish",Imports:Import()),Create()],[new(200),new(409)]),
        new CatalogCase("CAT-12-imported-suppression",[new("publish",Imports:Import()),new("delete",Key:Key,ETag:Tag(2,"active",0,BaseHash))],
            [new(200),new(200,Key,Tag(2,"suppressed",1,BaseHash),"suppressed",1,1,2,1,"{\"v\":1}")]),
        new CatalogCase("CAT-13-local-import-conflict",[Create(),new("publish",Imports:Import()),new("get",Key:Key),new("delete",Key:Key,ETag:Tag(2,"conflict",2,BaseHash))],
            [Local(),new(200),new(200,Key,Tag(2,"conflict",2,BaseHash),"conflict",2,2,2,2,"{\"v\":1}"),new(409)]),
        new CatalogCase("CAT-14-explicit-rebase",[Create(),new("publish",Imports:Import()),new("put",Key:Key,ETag:Tag(2,"conflict",2,BaseHash),Document:Doc(2)),new("put",Key:Key,ETag:Tag(2,"conflict",2,BaseHash),Document:Doc(2),Resolution:"rebase",ExpectedBase:BaseHash)],
            [Local(),new(200),new(409),new(200,Key,Tag(2,"active",3,BaseHash),"active",3,3,2,3,"{\"v\":2}")]),
        new CatalogCase("CAT-15-removed-clean-base-absent",[new("publish",Imports:Import()),new("publish",Imports:new Dictionary<string,(string,JsonObject)>()),new("get",Key:Key)],
            [new(200),new(200),new(404)]),
        new CatalogCase("CAT-16-removed-suppression-retain-local",[new("publish",Imports:Import()),new("delete",Key:Key,ETag:Tag(2,"active",0,BaseHash)),new("publish",Imports:new Dictionary<string,(string,JsonObject)>()),new("put",Key:Key,ETag:Tag(3,"conflict",2),Document:Doc(2),Resolution:"retain-local")],
            [new(200),new(200,Key,Tag(2,"suppressed",1,BaseHash),"suppressed",1,1,2,1,"{\"v\":1}"),new(200),new(200,Key,Tag(3,"active",3),"active",3,3,3,3,"{\"v\":2}")]),
    }.Select(x=>new object[]{x});

    public static void AssertCase(CatalogCase test, ICatalogCaseProvider provider)
    {
        Assert.Equal(test.Calls.Length,test.Expected.Length);
        for(var i=0;i<test.Calls.Length;i++) Assert.Equal(test.Expected[i],provider.Execute(test.Calls[i]));
    }
}

public sealed class ScriptedCatalog(CatalogCase test, Func<CatalogReply,CatalogReply>? corrupt = null) : ICatalogCaseProvider
{
    private int index;
    public CatalogReply Execute(CatalogCall call)
    {
        Assert.Same(test.Calls[index],call);
        var reply=test.Expected[index++];
        return corrupt is null ? reply : corrupt(reply);
    }
    public void AssertConsumed() => Assert.Equal(test.Calls.Length,index);
}

[Trait("Gate","Mock")]
public sealed class CatalogMockGateTests
{
    [Theory,MemberData(nameof(CatalogCases.All),MemberType=typeof(CatalogCases))]
    public void CorrectResponsesPass(CatalogCase test)
    {
        var fake=new ScriptedCatalog(test); CatalogCases.AssertCase(test,fake); fake.AssertConsumed();
    }
    [Theory,MemberData(nameof(CatalogCases.All),MemberType=typeof(CatalogCases))]
    public void WrongStatusRejected(CatalogCase test) => Assert.NotNull(Record.Exception(()=>CatalogCases.AssertCase(test,new ScriptedCatalog(test,x=>x with {Status=x.Status+1}))));
    [Theory,MemberData(nameof(CatalogCases.All),MemberType=typeof(CatalogCases))]
    public void EveryWrongResponseFieldRejected(CatalogCase test)
    {
        Func<CatalogReply,CatalogReply>[] corruptions = [x=>x with {Key="wrong"},x=>x with {ETag="wrong"},x=>x with {State="wrong"},x=>x with {Revision=x.Revision+1},x=>x with {HistoryCount=x.HistoryCount+1},x=>x with {RegistryGeneration=x.RegistryGeneration+1},x=>x with {OverlayGeneration=x.OverlayGeneration+1},x=>x with {Document="wrong"}];
        foreach(var corrupt in corruptions) Assert.NotNull(Record.Exception(()=>CatalogCases.AssertCase(test,new ScriptedCatalog(test,corrupt))));
    }
}
