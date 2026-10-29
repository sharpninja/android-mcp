using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

/// <summary>Only pinned original source data and explicit synthetic input mutations. No native validation output is read.</summary>
public sealed class ModelContractFixture
{
    public static ModelContractFixture Current {get;}=new();
    public Artifact Code {get;}
    public Artifact Dsl {get;}
    public Artifact Design {get;}=DocumentationFixture.Current.Input.DesignMarkdown;
    public JsonObject Blueprint {get;}=DocumentationFixture.Current.Blueprint;
    public JsonObject Module {get;}
    public JsonObject Api {get;}
    public IReadOnlyList<ContractCopyObservation> Copies {get;}
    public string[] Classes {get;}
    private ModelContractFixture()
    {
        var fixture=DocumentationFixture.Current;var root=Path.Combine(fixture.Workspace,"remediation/design-c4");
        Artifact Read(string path,string media)
        {
            var bytes=fixture.ReadOriginalDesignBytes(path);
            if(fixture.ExpectedHashes.TryGetValue("remediation/design-c4/"+path,out var expected))Assert.Equal(expected,DocumentationFixture.Hash(bytes));
            return new(path,bytes,media);
        }
        Code=Read("architecture/code-diagrams.md","text/markdown");Dsl=Read("architecture/workspace.dsl","text/plain");
        Module=JsonNode.Parse(Read("architecture/module-contract.json","application/json").Bytes.Span)!.AsObject();
        var pins=JsonNode.Parse(File.ReadAllBytes(Path.Combine(root,"contracts/source-pins.json")))!["files"]!.AsArray();
        Copies=pins.Select(node=>
        {
            var file=node!["file"]!.GetValue<string>();var snapshot=Read("contracts/"+file,file.EndsWith(".json",StringComparison.Ordinal)?"application/json":"text/markdown");
            var expected=node["sha256"]!.GetValue<string>();Assert.Equal(expected,DocumentationFixture.Hash(snapshot.Bytes.Span));
            var path=Path.GetFullPath(node["canonical_relative_path"]!.GetValue<string>(),root);Artifact? canonical=null;
            if(File.Exists(path)){canonical=new(Path.GetRelativePath(fixture.Workspace,path).Replace('\\','/'),File.ReadAllBytes(path),snapshot.MediaType);Assert.Equal(expected,DocumentationFixture.Hash(canonical.Bytes.Span));}
            return new ContractCopyObservation(file,expected,snapshot,canonical);
        }).ToArray();
        Api=JsonNode.Parse(Copies.Single(copy=>copy.File=="openapi.json").Snapshot.Bytes.Span)!.AsObject();
        var index=JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/fixtures/model-contract-type-index.fixture.json")))!;
        Assert.Equal(DocumentationFixture.CurrentSourceHash("remediation/design-c4/architecture/code-diagrams.md",index["codeSha256"]!.GetValue<string>()),DocumentationFixture.Hash(Code.Bytes.Span));
        Classes=index["declaredClasses"]!.AsArray().Select(node=>node!.GetValue<string>()).ToArray();Assert.Equal(96,Classes.Length);
    }
}
public sealed class ModelContractEnvironment
{
    public Artifact Code {get;set;}
    public Artifact Dsl {get;set;}
    public Artifact Design {get;set;}
    public JsonObject Blueprint {get;}
    public JsonObject Api {get;}
    public JsonObject Module {get;}
    public ContractCopyObservation[] Copies {get;set;}
    public string[] DeclaredClasses {get;set;}
    public bool OmitModule {get;set;}
    public ModelContractEnvironment()
    {
        var f=ModelContractFixture.Current;
        Code=f.Code with{Bytes=f.Code.Bytes.ToArray()};Dsl=f.Dsl with{Bytes=f.Dsl.Bytes.ToArray()};Design=f.Design with{Bytes=f.Design.Bytes.ToArray()};
        Blueprint=(JsonObject)f.Blueprint.DeepClone();Api=(JsonObject)f.Api.DeepClone();Module=(JsonObject)f.Module.DeepClone();
        DeclaredClasses=f.Classes.ToArray();Copies=f.Copies.Select(copy=>copy with{Snapshot=copy.Snapshot with{Bytes=copy.Snapshot.Bytes.ToArray()},Canonical=copy.Canonical is {} canonical?canonical with{Bytes=canonical.Bytes.ToArray()}:null}).ToArray();
    }
    public void CodeText(Func<string,string> mutate)=>Code=Code with{Bytes=Encoding.UTF8.GetBytes(mutate(DocumentationFixture.Utf8(Code)))};
    public void DslText(Func<string,string> mutate)=>Dsl=Dsl with{Bytes=Encoding.UTF8.GetBytes(mutate(DocumentationFixture.Utf8(Dsl)))};
    public JsonObject Screen(string id)=>Blueprint["screens"]!.AsArray().Select(node=>node!.AsObject()).Single(screen=>screen["id"]!.GetValue<string>()==id);
    public JsonObject Step(string id)=>Blueprint["storyboards"]!.AsArray().SelectMany(story=>story!["steps"]!.AsArray()).Select(step=>step!.AsObject()).Single(step=>step["step_id"]!.GetValue<string>()==id);
    public ModelContractInput Input()
    {
        // API variants are independently authored INPUT fixtures with their own synthetic pins, not refreshed expected output.
        if(!JsonNode.DeepEquals(Api,ModelContractFixture.Current.Api))
        {
            var index=Array.FindIndex(Copies,copy=>copy.File=="openapi.json");var snapshot=Copies[index].Snapshot with{Bytes=Encoding.UTF8.GetBytes(Api.ToJsonString())};
            Copies[index]=Copies[index] with{ExpectedSha256=DocumentationFixture.Hash(snapshot.Bytes.Span),Snapshot=snapshot,Canonical=snapshot};
        }
        var screens=Blueprint["screens"]!.AsArray().Select(node=>node!.AsObject()).ToArray();
        var model=new DocumentationModel(Blueprint,screens,Blueprint["storyboards"]!.AsArray().Select(node=>node!.AsObject()).ToArray(),screens.ToDictionary(screen=>screen["id"]!.GetValue<string>(),StringComparer.Ordinal));
        return new(Code,Dsl,Design,model,OmitModule?null:Module,OmitModule?null:DeclaredClasses,Copies);
    }
}
