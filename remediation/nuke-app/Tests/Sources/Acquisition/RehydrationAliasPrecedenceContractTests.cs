using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Tests.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

// Published tools/rehydrate.py:454 replaces a value at the same local_path with
// the LAST complete row while preserving that key's first insertion position.
// CanonicalRestore is a separate, unchanged compatibility contract.
public sealed record AliasPrecedenceCase(string Shape, AcquisitionCompatibilityProfile Profile)
{
    public override string ToString() => $"{Profile}-{Shape}";
    public string? Error => Shape is "ConflictSha" or "ConflictSize" or "ConflictGit" or "BridgedConflictSize" or "BridgedConflictGit"
        ? "CONFLICTING_SOURCE_ALIAS" : null;
}

public interface IAliasPrecedenceHarness
{
    IRehydrationPlanner Planner { get; }
    int RecipeParseCalls { get; }
}

public static class RehydrationAliasPrecedenceCases
{
    private static readonly string A = new('a', 64), B = new('b', 64), Git = new('c', 40);
    public static IEnumerable<object[]> Cases =>
        new[] { "Direct", "SnapshotRemoved", "SnapshotReplaced", "SnapshotMissingLast", "Archive", "OptionalCleared",
            "ConflictSha", "ConflictSize", "ConflictGit", "BridgedConflictSize", "BridgedConflictGit" }
        .SelectMany(shape => new[] { AcquisitionCompatibilityProfile.CanonicalRestore,
            AcquisitionCompatibilityProfile.GenericRehydrate }.Select(profile => new object[] { new AliasPrecedenceCase(shape, profile) }));

    private static JsonObject Row(string label, string local, string sha) => new()
    {
        ["path"]=$"api/{label}.java", ["local_path"]=local, ["sha256"]=sha, ["size_bytes"]=3,
        ["url"]=$"https://example.invalid/{label}", ["revision"]=label, ["kind"]="java-source",
        ["repository"]=$"fixture/{label}", ["selection_reasons"]=new JsonArray(label),
        ["unknown_evidence"]=new JsonObject { ["nullable"]=null, ["label"]=label }
    };
    private static JsonObject Snapshot(string? path) => new() { ["immutable_url"]=false, ["required_local_snapshot"]=path };
    private static JsonObject Archive(string label) => new()
    {
        ["archive_url"]=$"https://example.invalid/{label}.zip", ["archive_member"]=$"{label}/api.xml",
        ["archive_sha256"]=new string(label == "first" ? '1' : '2', 64),
        ["archive_sha1"]=new string(label == "first" ? '1' : '2', 40), ["archive_size_bytes"]=label == "first" ? 100 : 200,
        ["etag"]=$"\"{label}\"", ["archive_local_path"]=$"archives/{label}.zip", ["repository_xml"]=$"<{label}/>",
        ["platform_version"]=label, ["package_path"]=$"platforms;{label}"
    };
    public static RehydrationPlanRequest Request(AliasPrecedenceCase c) => RehydrationContractCases.Request(root =>
    {
        var first=Row("first", "blobs/a", A); var other=Row("other", "blobs/b", B); var last=Row("last", "blobs/a", A);
        switch(c.Shape)
        {
            case "Direct": last["transport"]=new JsonObject { ["kind"]="gitiles-text" }; last["git_blob"]=Git; break;
            case "SnapshotRemoved": first["transport"]=Snapshot("snapshots/first"); break;
            case "SnapshotReplaced": first["transport"]=Snapshot(null); last["transport"]=Snapshot("snapshots/last"); break;
            case "SnapshotMissingLast": first["transport"]=Snapshot("snapshots/first"); last["transport"]=Snapshot(null); break;
            case "Archive": first["transport"]=Archive("first"); last["transport"]=Archive("last"); break;
            case "OptionalCleared": first["git_blob"]=Git; last.Remove("size_bytes"); break;
            case "ConflictSha": last["sha256"]=B; break;
            case "ConflictSize": last["size_bytes"]=4; break;
            case "ConflictGit": first["git_blob"]=Git; last["git_blob"]=new string('d',40); break;
            case "BridgedConflictSize":
            case "BridgedConflictGit":
                // The operational middle row lacks a formerly known integrity
                // pin. Selecting it must not erase conflict admission evidence.
                var git=c.Shape == "BridgedConflictGit";
                if(git)first["git_blob"]=Git;
                other=first.DeepClone().AsObject(); other["path"]="api/middle.java"; other["revision"]="middle";
                other.Remove(git ? "git_blob" : "size_bytes");
                last=other.DeepClone().AsObject(); last["path"]="api/last.java"; last["revision"]="last";
                if(git)last["git_blob"]=new string('d',40); else last["size_bytes"]=4;
                break;
        }
        root["inventory"]!["sources"]=new JsonArray(first,other,last);
    }, limit:1) with { Profile=c.Profile };

    private static SourceArchivePin ArchivePin(string label, AcquisitionCompatibilityProfile profile) =>
        new(new($"https://example.invalid/{label}.zip"), $"{label}/api.xml",
            new string(label == "first" ? '1' : '2',64), new string(label == "first" ? '1' : '2',40),
            label == "first" ? 100 : 200, $"\"{label}\"", $"archives/{label}.zip", profile,
            System.Text.Encoding.UTF8.GetBytes($"<{label}/>"), label, $"platforms;{label}");

    private static SourcePin Pin(AliasPrecedenceCase c, bool last)
    {
        var label=last ? "last" : "first";
        var bundle=c.Shape switch {
            "SnapshotRemoved" when !last => "snapshots/first",
            "SnapshotReplaced" when last => "snapshots/last",
            "SnapshotMissingLast" when !last => "snapshots/first", _ => null };
        return new("blobs/a",new($"https://example.invalid/{label}"),label,A,
            last && c.Shape == "OptionalCleared" ? null : 3,
            c.Shape == "Direct" || c.Shape == "OptionalCleared" && !last ? Git : null,
            last && c.Shape == "Direct" ? SourceWireEncoding.GitilesText : SourceWireEncoding.Raw,
            bundle,c.Shape == "Archive" ? ArchivePin(label,c.Profile) : null,c.Profile);
    }
    private static SnapshotRequirement[] AuditSnapshots(AliasPrecedenceCase c) => c.Shape switch
    {
        "SnapshotRemoved" => [new("api/first.java","blobs/a","snapshots/first",A,3)],
        "SnapshotReplaced" => [new("api/first.java","blobs/a",null,A,3),new("api/last.java","blobs/a","snapshots/last",A,3)],
        "SnapshotMissingLast" => [new("api/first.java","blobs/a","snapshots/first",A,3),new("api/last.java","blobs/a",null,A,3)],
        _ => []
    };
    public static RehydrationPlanResult Correct(AliasPrecedenceCase c)
    {
        var request=Request(c);
        var baseline=RehydrationContractCases.Correct(new(c.ToString(),request,c.Error,Rows:3,Unique:2,KnownBytes:6,UnknownSizes:0));
        if(c.Error is not null)return baseline;
        var generic=c.Profile == AcquisitionCompatibilityProfile.GenericRehydrate;
        var audit=AuditSnapshots(c);
        var snapshots=generic ? audit.Where(s=>s.SourcePath == "api/last.java").ToArray() : audit;
        var pin=Pin(c,generic);
        // Existing CanonicalRestore Freeze selects the first available bundle,
        // including when the first alias's own bundle is unavailable.
        if(!generic && snapshots.Length>0)pin=pin with { RequiredLocalSnapshot=snapshots.Select(s=>s.BundlePath).FirstOrDefault(s=>s is not null) };
        RehydrationPrerequisite[] prerequisites=snapshots.Any(s=>s.BundlePath is null)
            ? [new(RehydrationPrerequisiteCode.RequiredImmutableSnapshot,[0,2],"blobs/a","Missing pinned prerequisite; exact existing target may be reused.")] : [];
        using var doc=JsonDocument.Parse(request.Recipe.Bytes);
        var rows=doc.RootElement.GetProperty("inventory").GetProperty("sources").EnumerateArray().Select((row,index) =>
        {
            var label=index == 0 ? "first" : index == 1 ? "other" : "last";
            return new RehydrationSourceRow(index,$"api/{label}.java","java-source",index == 1 ? 1 : 0,null,
                new($"fixture/{label}",label,null,null,null,null,[],[label]),row.Clone());
        }).ToArray();
        var physical=new RehydrationPhysicalSource[] {
            new(0,pin,[0,2],snapshots,prerequisites),
            new(1,new("blobs/b",new("https://example.invalid/other"),"other",B,3,Compatibility:c.Profile),[1],[],[]) };
        var projection=new RehydrationSourceProjection("fixture-release",baseline.Plan!.RecipeSha256,c.Profile,rows,physical,[],[0],true);
        return baseline with { Plan=baseline.Plan with {
            KnownSourceBytes=generic && c.Shape == "OptionalCleared" ? 3 : 6,
            UnknownSizePaths=generic && c.Shape == "OptionalCleared" ? 1 : 0,
            RequiredSnapshots=audit,SourceProjection=projection } };
    }
    public static void AssertCase(AliasPrecedenceCase c, IAliasPrecedenceHarness harness)
    {
        var request=Request(c); var recipe=request.Recipe.Bytes.ToArray(); var recipeLock=request.Lock.Bytes.ToArray();
        var before=harness.RecipeParseCalls; var actual=harness.Planner.Plan(request);
        Assert.Equal(1,harness.RecipeParseCalls-before);
        Assert.Equal(recipe,request.Recipe.Bytes.ToArray()); Assert.Equal(recipeLock,request.Lock.Bytes.ToArray());
        Assert.Equal(c.Error is null,actual.IsValid);
        Assert.Equal(c.Error is null ? Array.Empty<string>() : [c.Error],actual.Issues.Select(i=>i.Code));
        if(c.Error is not null) { Assert.Null(actual.Plan); return; }
        var wanted=Correct(c).Plan!; var plan=Assert.IsType<RehydrationPlan>(actual.Plan);
        Assert.Equal(wanted.Release,plan.Release); Assert.Equal(ContentHash.Sha256(recipe),plan.RecipeSha256);
        Assert.Equal(3,plan.SourceRows); Assert.Equal(2,plan.UniqueSourcePaths);
        Assert.Equal(wanted.KnownSourceBytes,plan.KnownSourceBytes); Assert.Equal(wanted.UnknownSizePaths,plan.UnknownSizePaths);
        Assert.False(plan.Network); Assert.False(plan.Complete); Assert.False(plan.HistoricalByteIdentityClaimed);
        Assert.Equal(wanted.RequiredSnapshots,plan.RequiredSnapshots);
        var p=Assert.IsType<RehydrationSourceProjection>(plan.SourceProjection); var e=wanted.SourceProjection!;
        Assert.Equal(e.Release,p.Release); Assert.Equal(e.RecipeSha256,p.RecipeSha256); Assert.Equal(c.Profile,p.Profile); Assert.True(p.Resume);
        Assert.Equal(new[] {0},p.SelectedPhysicalSourceIndices); Assert.Empty(p.CacheAliases);
        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(e.PhysicalSources),JsonSerializer.SerializeToUtf8Bytes(p.PhysicalSources));
        Assert.Equal(e.Rows.Count,p.Rows.Count);
        var canonical=new CanonicalJson();
        for(var i=0;i<e.Rows.Count;i++)
        {
            Assert.Equal(e.Rows[i].RowIndex,p.Rows[i].RowIndex); Assert.Equal(e.Rows[i].LogicalPath,p.Rows[i].LogicalPath);
            Assert.Equal(e.Rows[i].Kind,p.Rows[i].Kind); Assert.Equal(e.Rows[i].PhysicalSourceIndex,p.Rows[i].PhysicalSourceIndex);
            Assert.Equal(e.Rows[i].CacheAliasIndex,p.Rows[i].CacheAliasIndex);
            Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(e.Rows[i].Context),JsonSerializer.SerializeToUtf8Bytes(p.Rows[i].Context));
            Assert.Equal(canonical.Serialize(e.Rows[i].OriginalRow),canonical.Serialize(p.Rows[i].OriginalRow));
        }
        // This is the complete existing request identity payload, not a new
        // identity format. CanonicalRestore must retain these exact bytes.
        Assert.Equal(canonical.Serialize(JsonSerializer.SerializeToUtf8Bytes(new SourceBatchRequest("alias-generation","sources/acquisition.json",e))),
            canonical.Serialize(JsonSerializer.SerializeToUtf8Bytes(new SourceBatchRequest("alias-generation","sources/acquisition.json",p))));
        using var manifest=JsonDocument.Parse(plan.Manifest.Bytes);
        Assert.Equal(plan.RecipeSha256,manifest.RootElement.GetProperty("recipe_sha256").GetString());
        Assert.False(manifest.RootElement.GetProperty("network").GetBoolean());
        // A corrected projection is input to the existing SourceBatch request
        // hash. This unit does not rewrite, migrate, or accept any old receipt.
    }
    public static IEnumerable<RehydrationPlanResult> Wrong(AliasPrecedenceCase c)
    {
        var good=Correct(c); yield return good with { Issues=[new("WRONG","")] };
        if(c.Error is not null) { yield return good with { Issues=[] }; yield break; }
        var plan=good.Plan!; var p=plan.SourceProjection!;
        RehydrationPlanResult Changed(RehydrationSourceProjection changed) => good with { Plan=plan with { SourceProjection=changed } };
        var source=p.PhysicalSources[0];
        var wrongPin=Pin(c,c.Profile != AcquisitionCompatibilityProfile.GenericRehydrate);
        yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { Pin=wrongPin } : s).ToArray() });
        foreach(var pin in new[] { source.Pin with { Url=new("https://example.invalid/wrong") },source.Pin with { Revision="wrong" },
            source.Pin with { Sha256=B },source.Pin with { SizeBytes=99 },source.Pin with { GitBlob="wrong" },
            source.Pin with { Encoding=(SourceWireEncoding)99 },source.Pin with { RequiredLocalSnapshot="wrong" },
            source.Pin with { Compatibility=(AcquisitionCompatibilityProfile)99 } })
            yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { Pin=pin } : s).ToArray() });
        if(source.Pin.Archive is {} archive)
        {
            foreach(var pin in new[] { source.Pin with { Archive=null },source.Pin with { Archive=archive with { Member="wrong" } },
                source.Pin with { Archive=archive with { RepositoryXml=new byte[] {1} } },source.Pin with { Archive=archive with { ETag="wrong" } } })
                yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { Pin=pin } : s).ToArray() });
        }
        yield return Changed(p with { PhysicalSources=p.PhysicalSources.Reverse().ToArray() });
        yield return Changed(p with { SelectedPhysicalSourceIndices=[1] });
        yield return Changed(p with { RecipeSha256="wrong" });
        yield return good with { Plan=plan with { RecipeSha256="wrong" } };
        yield return good with { Plan=plan with { KnownSourceBytes=99 } };
        yield return good with { Plan=plan with { UnknownSizePaths=99 } };
        yield return Changed(p with { Rows=p.Rows.Take(2).ToArray() });
        yield return Changed(p with { Rows=p.Rows.Select((r,i)=>i == 2 ? r with { OriginalRow=p.Rows[0].OriginalRow } : r).ToArray() });
        yield return Changed(p with { Rows=p.Rows.Select((r,i)=>i == 2 ? r with { Context=r.Context with { Revision="first" } } : r).ToArray() });
        if(source.RequiredSnapshots.Count>0)
        {
            yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { RequiredSnapshots=[] } : s).ToArray() });
        }
        else if(AuditSnapshots(c).Length>0)
            yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { RequiredSnapshots=AuditSnapshots(c) } : s).ToArray() });
        if(AuditSnapshots(c).Length>0)yield return good with { Plan=plan with { RequiredSnapshots=[] } };
        if(source.Prerequisites.Count>0)
            yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { Prerequisites=[] } : s).ToArray() });
        else if(c.Shape == "SnapshotReplaced")
            yield return Changed(p with { PhysicalSources=p.PhysicalSources.Select((s,i)=>i == 0 ? s with { Prerequisites=[new(RehydrationPrerequisiteCode.RequiredImmutableSnapshot,[0,2],"blobs/a","wrong")] } : s).ToArray() });
    }
}

public sealed class ScriptedAliasPrecedenceHarness(RehydrationPlanResult response, int parseDelta=1) : IAliasPrecedenceHarness, IRehydrationPlanner
{
    public IRehydrationPlanner Planner => this;
    public int RecipeParseCalls { get; private set; }
    public RehydrationPlanResult Plan(RehydrationPlanRequest request) { RecipeParseCalls+=parseDelta; return response; }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationAliasPrecedenceMockTests
{
    [Theory,MemberData(nameof(RehydrationAliasPrecedenceCases.Cases),MemberType=typeof(RehydrationAliasPrecedenceCases))]
    public void CorrectProfileScopedMocksPass(AliasPrecedenceCase c) =>
        RehydrationAliasPrecedenceCases.AssertCase(c,new ScriptedAliasPrecedenceHarness(RehydrationAliasPrecedenceCases.Correct(c)));

    [Theory,MemberData(nameof(RehydrationAliasPrecedenceCases.Cases),MemberType=typeof(RehydrationAliasPrecedenceCases))]
    public void WrongAliasPinAuditOrderPrerequisitesAndParseCountsAreRejected(AliasPrecedenceCase c)
    {
        foreach(var wrong in RehydrationAliasPrecedenceCases.Wrong(c))
            Assert.NotNull(Record.Exception(()=>RehydrationAliasPrecedenceCases.AssertCase(c,new ScriptedAliasPrecedenceHarness(wrong))));
        foreach(var wrongCount in new[] {0,2})
            Assert.NotNull(Record.Exception(()=>RehydrationAliasPrecedenceCases.AssertCase(c,
                new ScriptedAliasPrecedenceHarness(RehydrationAliasPrecedenceCases.Correct(c),wrongCount))));
    }
}
