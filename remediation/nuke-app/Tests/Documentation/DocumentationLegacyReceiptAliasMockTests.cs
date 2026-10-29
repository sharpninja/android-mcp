using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record LegacyReceiptSelectionCase(string Id,DesignStagePlan Selection,bool Enabled,IReadOnlyList<string> Expected,bool Reject=false)
{public override string ToString()=>Id;}
public static class LegacyReceiptSelectionCases
{
    public static IReadOnlyList<LegacyReceiptSelectionCase> Create()
    {
        // Independent oracle from retained original Gradle task output/dependency
        // declarations. Production must filter the one admitted Core stage union.
        var matrix=new (string Alias,int[] ReceiptIndices)[]
        {
            ("verifyToolchain",[]),("validateModuleGraph",[0]),("validateModelContracts",[1,2]),("extractCodeViews",[]),("renderCodeViews",[]),
            ("validateArchitecture",[0,1,2,3]),("exportArchitecture",[0,1,2,3]),("generateArchitectureDiagrams",[0,1,2,3]),
            ("generateUiAssets",[2]),("renderStoryboards",[2]),("verifyDesignDocs",[0,1,2,3,4]),("generateArtifactManifest",[0,1,2,3,4]),("packageDesignDocs",[0,1,2,3,4])
        };
        var planner=new DesignStagePlanner();var pin=planner.Describe().Sha256;
        IReadOnlyList<string> Paths(IEnumerable<int> indices,bool package)=>indices.Select(index=>"build/receipts/"+LegacyReceiptCases.Names[index])
            .Concat(package?LegacyReceiptCases.Names.Select(name=>"validation/"+name):[]).Order(StringComparer.Ordinal).ToArray();
        var rows=matrix.Select(row=>new LegacyReceiptSelectionCase(row.Alias,planner.Select(new([row.Alias],pin)),true,Paths(row.ReceiptIndices,row.Alias=="packageDesignDocs"))).ToList();
        var full=rows.Single(row=>row.Id=="generateArchitectureDiagrams");
        rows.Add(full with{Id="OLD-DEFAULT-DISABLED",Enabled=false,Expected=[]});
        rows.Add(new("UNION-NARROW",planner.Select(new(["extractCodeViews","validateModuleGraph"],pin)),true,Paths([0],false)));
        rows.Add(new("UNION-PACKAGE",planner.Select(new(["exportArchitecture","verifyDesignDocs","packageDesignDocs"],pin)),true,Paths([0,1,2,3,4],true)));
        rows.Add(full with{Id="STALE-REGISTRY",Selection=full.Selection with{RegistrySha256=new string('0',64)},Expected=[],Reject=true});
        rows.Add(full with{Id="FORGED-STAGES",Selection=full.Selection with{Stages=[]},Expected=[],Reject=true});
        rows.Add(full with{Id="UNKNOWN-ALIAS",Selection=planner.Select(new(["unknown"],pin)),Expected=[],Reject=true});
        return rows;
    }
    public static IEnumerable<object[]> Cases=>Create().Select(test=>new object[]{test});
    public static void AssertPaths(LegacyReceiptSelectionCase test,Func<DesignStagePlan,bool,IReadOnlyList<string>> provider)
    {
        var before=JsonSerializer.SerializeToUtf8Bytes(test.Selection);
        if(test.Reject)Assert.Equal("DESIGN_STAGE_SELECTION",Assert.Throws<ArchitectureAssetException>(()=>provider(test.Selection,test.Enabled)).Code);
        else
        {var paths=provider(test.Selection,test.Enabled);Assert.Equal(test.Expected,paths.Order(StringComparer.Ordinal));Assert.Equal(paths.Count,paths.Distinct(StringComparer.OrdinalIgnoreCase).Count());}
        Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(test.Selection));
    }
}
public sealed class ScriptedLegacyReceiptSelection(LegacyReceiptSelectionCase test,string mutation="")
{
    public IReadOnlyList<string> Describe(DesignStagePlan selection,bool enabled)
    {
        Assert.Same(test.Selection,selection);Assert.Equal(test.Enabled,enabled);
        if(test.Reject&&mutation!="outcome")throw new ArchitectureAssetException("DESIGN_STAGE_SELECTION","Use the current admitted Core union");
        if(mutation=="outcome")return test.Expected.Count==0?["wrong"]:[];
        var paths=test.Expected.ToArray();
        if(mutation=="missing")return paths.Skip(1).ToArray();
        if(mutation=="extra")return paths.Append("build/receipts/undeclared.txt").ToArray();
        if(mutation=="duplicate")return paths.Append(paths[0]).ToArray();
        if(mutation=="alias-omission")return paths.Where(path=>!path.StartsWith("validation/",StringComparison.Ordinal)).ToArray();
        if(mutation=="path")return paths.Select((path,index)=>index==0?"../unsafe":path).ToArray();
        if(mutation=="disabled")return LegacyReceiptCases.Names.Select(name=>"build/receipts/"+name).ToArray();
        return paths;
    }
}

public sealed record LegacyReceiptAliasCase(string Id,IReadOnlyList<Artifact> Input,bool Package,IReadOnlyList<Artifact> Expected,string? Error=null)
{public override string ToString()=>Id;}
public sealed class ObservedLegacyManifest(IArtifactReceiptVerifier receipts):IDocumentationManifestBuilder
{
    private readonly IDocumentationManifestBuilder authority=new DocumentationManifestBuilder(receipts);
    public int Builds {get;private set;}public int Verifications {get;private set;}
    public DocumentationManifest Build(IReadOnlyList<Artifact> inputs){Builds++;return authority.Build(inputs);}
    public void Verify(IReadOnlyList<Artifact> inputs,DocumentationManifest manifest){Verifications++;authority.Verify(inputs,manifest);}
}
public static class LegacyReceiptAliasCases
{
    public static IReadOnlyList<LegacyReceiptAliasCase> Create()
    {
        var sources=Enum.GetValues<LegacyReceiptKind>().Select(LegacyReceiptCases.Original).ToArray();
        var expected=sources.Concat(sources.Select(source=>source with{RelativePath="validation/"+Path.GetFileName(source.RelativePath)})).ToArray();
        var full=new LegacyReceiptAliasCase("PACKAGE-FIVE",sources,true,expected);
        var ordinary=new Artifact("docs/source.md",Encoding.UTF8.GetBytes("# Existing source\n"),"text/markdown");
        var observation=new Artifact("validation/model-sources.json",Encoding.UTF8.GetBytes("{}\n"),"application/json");
        var rows=new List<LegacyReceiptAliasCase>{full,
            new("PACKAGE-WITH-SOURCES",new[]{ordinary,observation}.Concat(sources).ToArray(),true,new[]{ordinary,observation}.Concat(expected).ToArray()),
            new("NO-PACKAGE-NO-RECEIPT-REQUIREMENT",[ordinary],false,[ordinary])};
        foreach(var name in LegacyReceiptCases.Names)
        {
            rows.Add(full with{Id="MISSING-"+name,Input=sources.Where(source=>Path.GetFileName(source.RelativePath)!=name).ToArray(),Error="LEGACY_RECEIPT_MISSING"});
            rows.Add(full with{Id="COLLISION-"+name,Input=sources.Append(sources.Single(source=>Path.GetFileName(source.RelativePath)==name) with{RelativePath="validation/"+name}).ToArray(),Error="LEGACY_RECEIPT_ALIAS_COLLISION"});
        }
        rows.Add(full with{Id="CASE-COLLISION",Input=sources.Append(sources[0] with{RelativePath="Validation/Module-Graph.json"}).ToArray(),Error="LEGACY_RECEIPT_ALIAS_COLLISION"});
        rows.Add(full with{Id="UNKNOWN-RECEIPT",Input=sources.Append(new("build/receipts/unknown.json",new byte[]{0},"application/json")).ToArray(),Error="LEGACY_RECEIPT_UNKNOWN"});
        rows.Add(full with{Id="UNSAFE-PATH",Input=sources.Append(new("../unsafe",new byte[]{0},"text/plain")).ToArray(),Error="LEGACY_RECEIPT_ALIAS_PATH"});
        rows.Add(full with{Id="DUPLICATE-INPUT",Input=sources.Append(sources[0]).ToArray(),Error="LEGACY_RECEIPT_ALIAS_COLLISION"});
        return rows;
    }
    public static IEnumerable<object[]> Cases=>Create().Select(test=>new object[]{test});
    public static void AssertMap(LegacyReceiptAliasCase test,Func<IReadOnlyList<Artifact>,bool,IReadOnlyList<Artifact>> provider)
    {
        var before=JsonSerializer.SerializeToUtf8Bytes(test.Input);IReadOnlyList<Artifact>? result=null;var error=Record.Exception(()=>result=provider(test.Input,test.Package));
        if(test.Error is not null){Assert.Equal(test.Error,Assert.IsType<ArchitectureAssetException>(error).Code);Assert.Null(result);}
        else
        {
            Assert.Null(error);Assert.NotNull(result);DesignDocumentationStageCases.AssertArtifacts(test.Expected,result);
            for(var index=0;index<test.Input.Count;index++)Assert.Same(test.Input[index],result[index]);
            if(test.Package)
            {
                // Alias bytes join the declared published set before ONE manifest
                // build. Existing prebuilt package authority verifies the same set.
                var verifier=new ArtifactReceiptVerifier(new NativeContentHash());var manifestAuthority=new ObservedLegacyManifest(verifier);
                var manifest=manifestAuthority.Build(result);var checker=new RecordingDocumentationPackageChecker(false);
                var package=new DocumentationPackageBuilder(checker,verifier,manifestAuthority).Build(new(result,result.Select(artifact=>artifact.RelativePath).ToArray(),new byte[]{1}),manifest);
                Assert.Equal(1,manifestAuthority.Builds);Assert.Equal(1,manifestAuthority.Verifications);Assert.Same(manifest.Checksums,package.Checksums);
                Assert.Equal(test.Expected.Select(artifact=>artifact.RelativePath).Order(StringComparer.Ordinal),manifest.CoveredInputs.Select(input=>input.RelativePath));
                using var bytes=new MemoryStream(package.Archive.Bytes.ToArray(),false);using var zip=new ZipArchive(bytes,ZipArchiveMode.Read);
                var all=test.Expected.Append(manifest.Checksums).OrderBy(artifact=>artifact.RelativePath,StringComparer.Ordinal).ToArray();
                Assert.Equal(all.Select(artifact=>artifact.RelativePath),zip.Entries.Select(entry=>entry.FullName));
                foreach(var artifact in all){using var entry=zip.GetEntry(artifact.RelativePath)!.Open();using var body=new MemoryStream();entry.CopyTo(body);Assert.Equal(artifact.Bytes.ToArray(),body.ToArray());}
            }
        }
        Assert.Equal(before,JsonSerializer.SerializeToUtf8Bytes(test.Input));
    }
}
public sealed class ScriptedLegacyReceiptAliases(LegacyReceiptAliasCase test,string mutation="")
{
    public IReadOnlyList<Artifact> Add(IReadOnlyList<Artifact> inputs,bool package)
    {
        Assert.Same(test.Input,inputs);Assert.Equal(test.Package,package);
        if(test.Error is not null&&mutation!="outcome")throw new ArchitectureAssetException(test.Error,"Reject undeclared receipt aliases before publication");
        if(mutation=="outcome")return test.Error is null?[]:test.Expected;
        var output=test.Expected.ToArray();
        if(mutation=="missing-source")return output.Skip(1).ToArray();
        if(mutation=="missing-alias")return output.Take(inputs.Count).ToArray();
        if(mutation=="extra")return output.Append(new Artifact("validation/unrelated.txt",new byte[]{0},"text/plain")).ToArray();
        if(mutation=="path")output[^1]=output[^1] with{RelativePath="validation/wrong.txt"};
        if(mutation=="media")output[^1]=output[^1] with{MediaType="wrong"};
        if(mutation=="bytes")output[^1]=output[^1] with{Bytes=new byte[]{0}};
        if(mutation=="clone-source")output[0]=output[0] with{Bytes=output[0].Bytes.ToArray()};
        if(mutation=="duplicate")return output.Append(output[^1]).ToArray();
        return output;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationLegacyReceiptAliasMockTests
{
    [Theory,MemberData(nameof(LegacyReceiptSelectionCases.Cases),MemberType=typeof(LegacyReceiptSelectionCases))]
    public void SameCoreUnionSelectsOnlyOriginalReceiptObligations(LegacyReceiptSelectionCase test)=>LegacyReceiptSelectionCases.AssertPaths(test,new ScriptedLegacyReceiptSelection(test).Describe);
    [Theory,MemberData(nameof(LegacyReceiptSelectionCases.Cases),MemberType=typeof(LegacyReceiptSelectionCases))]
    public void EveryWrongStageReceiptSelectionIsRejected(LegacyReceiptSelectionCase test)=>Assert.NotNull(Record.Exception(()=>LegacyReceiptSelectionCases.AssertPaths(test,new ScriptedLegacyReceiptSelection(test,"outcome").Describe)));
    [Theory]
    [InlineData("missing")][InlineData("extra")][InlineData("duplicate")][InlineData("alias-omission")][InlineData("path")][InlineData("disabled")]
    public void EveryStageAliasAndOptInBypassIsRejected(string mutation)
    {var test=LegacyReceiptSelectionCases.Create().Single(test=>test.Id==(mutation=="disabled"?"OLD-DEFAULT-DISABLED":"packageDesignDocs"));Assert.NotNull(Record.Exception(()=>LegacyReceiptSelectionCases.AssertPaths(test,new ScriptedLegacyReceiptSelection(test,mutation).Describe)));}
    [Theory,MemberData(nameof(LegacyReceiptAliasCases.Cases),MemberType=typeof(LegacyReceiptAliasCases))]
    public void CorrectFiveAliasesPreserveSourcesAndOneManifest(LegacyReceiptAliasCase test)=>LegacyReceiptAliasCases.AssertMap(test,new ScriptedLegacyReceiptAliases(test).Add);
    [Theory,MemberData(nameof(LegacyReceiptAliasCases.Cases),MemberType=typeof(LegacyReceiptAliasCases))]
    public void EveryWrongAliasAdmissionOutcomeIsRejected(LegacyReceiptAliasCase test)=>Assert.NotNull(Record.Exception(()=>LegacyReceiptAliasCases.AssertMap(test,new ScriptedLegacyReceiptAliases(test,"outcome").Add)));
    [Theory]
    [InlineData("missing-source")][InlineData("missing-alias")][InlineData("extra")][InlineData("path")][InlineData("media")][InlineData("bytes")][InlineData("clone-source")][InlineData("duplicate")]
    public void EachMappedArchiveArtifactAndRetainedSourceIsChecked(string mutation)
    {var test=LegacyReceiptAliasCases.Create().Single(test=>test.Id=="PACKAGE-FIVE");Assert.NotNull(Record.Exception(()=>LegacyReceiptAliasCases.AssertMap(test,new ScriptedLegacyReceiptAliases(test,mutation).Add)));}
}
