using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record BootstrapCliCase(string Alias,string Failure)
{public override string ToString()=>Alias+"-"+Failure;}
public sealed record BootstrapCliObservation(int ExitCode,string Diagnostic,Artifact? Manifest,IReadOnlyDictionary<string,int> Modes,
    bool SourceUnchanged,bool ToolBytesUnchanged,bool CapturesRetained,bool NoSourceBuild,IReadOnlyList<string> FinalPaths,
    IReadOnlyList<Artifact> Sources);
public static class BootstrapCliCases
{
    public const string PropertiesPath="gradle/wrapper/gradle-wrapper.properties";
    public static IEnumerable<object[]> Cases=>new[]{"verifyToolchain","generateUiAssets","renderStoryboards"}
        .SelectMany(alias=>new[]{"GOOD","BAD-PROPERTIES","MISSING-PROPERTIES"}.Select(failure=>new object[]{new BootstrapCliCase(alias,failure)}));
    public static async Task AssertContract(BootstrapCliCase test,Func<BootstrapCliCase,Task<BootstrapCliObservation>> probe)
    {
        var observation=await probe(test);var good=test.Failure=="GOOD";
        Assert.Equal(good?0:1,observation.ExitCode);Assert.True(observation.SourceUnchanged);Assert.True(observation.ToolBytesUnchanged);Assert.True(observation.CapturesRetained);Assert.True(observation.NoSourceBuild);
        var effects=test.Failure!="MISSING-PROPERTIES";
        foreach(var (path,before,after) in new[]{(".tools/gradle-8.14.3/bin/gradle",416,489),(".tools/gradle-8.14.3/bin/extra",384,457),(".tools/gradle-8.14.3/bin/retained-directory",448,457),("gradlew",438,493),("unrelated",438,438)})
            Assert.Equal(effects?after:before,observation.Modes[path]);
        if(!good){Assert.Null(observation.Manifest);Assert.Empty(observation.FinalPaths);Assert.Contains(test.Failure=="BAD-PROPERTIES"?"Committed wrapper properties differ from pinned expected configuration":PropertiesPath,observation.Diagnostic,StringComparison.Ordinal);return;}
        Assert.NotNull(observation.Manifest);var manifest=JsonNode.Parse(observation.Manifest.Bytes.Span)!;
        Assert.Equal("bootstrap-caller",manifest["generation_id"]!.GetValue<string>());
        var expected=observation.Sources;var inputs=manifest["inputs"]!.AsArray();Assert.Equal(test.Alias=="verifyToolchain"?5:7,inputs.Count);
        Assert.Equal(expected.Select(artifact=>artifact.RelativePath).Order(StringComparer.Ordinal),inputs.Select(value=>value!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        var property=Assert.Single(expected,artifact=>artifact.RelativePath==PropertiesPath);var row=Assert.Single(inputs,value=>value!["path"]!.GetValue<string>()==PropertiesPath)!;
        Assert.Equal(ContentHash.Sha256(property.Bytes.Span),row["sha256"]!.GetValue<string>());Assert.Equal(property.Bytes.Length,row["size_bytes"]!.GetValue<int>());Assert.Equal("application/octet-stream",row["media_type"]!.GetValue<string>());
        Assert.Contains("generation-manifest.json",observation.FinalPaths);
        if(test.Alias=="verifyToolchain")Assert.Equal(new[]{"generation-manifest.json"},observation.FinalPaths);
        else {Assert.Contains("build/receipts/ui-projection.json",observation.FinalPaths);Assert.Contains("docs/storyboards.html",observation.FinalPaths);}
    }
    public static BootstrapCliObservation Correct(BootstrapCliCase test)
    {
        var sources=new List<Artifact>{new("toolchain-lock.json","{}"u8.ToArray(),"application/json"),new("renderer-lock.json","{}"u8.ToArray(),"application/json"),
            new("build.gradle.kts","projection"u8.ToArray(),"text/plain"),new("tooling/nuke-design-stages.json","descriptor"u8.ToArray(),"application/json")};
        if(test.Failure!="MISSING-PROPERTIES")sources.Add(new(PropertiesPath,"original captured properties\n"u8.ToArray(),"application/octet-stream"));
        if(test.Alias!="verifyToolchain"){sources.Add(new("docs/ui-blueprint.md","blueprint"u8.ToArray(),"text/markdown"));sources.Add(new("docs/Android MCP Bridge Design.md","design"u8.ToArray(),"text/markdown"));}
        var good=test.Failure=="GOOD";var effects=test.Failure!="MISSING-PROPERTIES";
        var modes=new Dictionary<string,int>(StringComparer.Ordinal){[".tools/gradle-8.14.3/bin/gradle"]=effects?489:416,[".tools/gradle-8.14.3/bin/extra"]=effects?457:384,[".tools/gradle-8.14.3/bin/retained-directory"]=effects?457:448,["gradlew"]=effects?493:438,["unrelated"]=438};
        var manifest=good?new DocumentationGenerationManifestFactory().Create("bootstrap-caller",sources.Select(artifact=>new StoredArtifact(artifact.RelativePath,ContentHash.Sha256(artifact.Bytes.Span),artifact.Bytes.Length,artifact.MediaType)).ToArray(),[]):null;
        return new(good?0:1,test.Failure=="BAD-PROPERTIES"?"Committed wrapper properties differ from pinned expected configuration":PropertiesPath,manifest,modes,true,true,true,true,
            !good?[]:test.Alias=="verifyToolchain"?["generation-manifest.json"]:["build/receipts/ui-projection.json","docs/storyboards.html","generation-manifest.json"],sources);
    }
    public static BootstrapCliObservation Wrong(BootstrapCliCase test,string mutation)
    {
        var value=Correct(test);
        if(mutation=="exit")return value with{ExitCode=1-value.ExitCode};if(mutation=="source")return value with{SourceUnchanged=false};if(mutation=="bytes")return value with{ToolBytesUnchanged=false};if(mutation=="captures")return value with{CapturesRetained=false};if(mutation=="build")return value with{NoSourceBuild=false};
        if(mutation.StartsWith("mode:",StringComparison.Ordinal)){var modes=value.Modes.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);modes[mutation[5..]]=0;return value with{Modes=modes};}
        if(mutation=="diagnostic")return value with{Diagnostic="wrong"};if(mutation=="failure-output")return value with{FinalPaths=["incorrect-commit"]};
        if(mutation=="failure-manifest")return value with{Manifest=new("generation-manifest.json","{}"u8.ToArray(),"application/json")};
        if(mutation=="missing-manifest")return value with{Manifest=null};if(mutation=="final-paths")return value with{FinalPaths=[]};
        var json=JsonNode.Parse(value.Manifest!.Bytes.Span)!;var row=json["inputs"]!.AsArray().Single(node=>node!["path"]!.GetValue<string>()==PropertiesPath)!;
        if(mutation=="generation")json["generation_id"]="wrong";else if(mutation=="input-set")json["inputs"]!.AsArray().RemoveAt(0);else if(mutation=="hash")row["sha256"]="wrong";else if(mutation=="length")row["size_bytes"]=0;else if(mutation=="media")row["media_type"]="wrong";
        return value with{Manifest=value.Manifest with{Bytes=Encoding.UTF8.GetBytes(json.ToJsonString())}};
    }
    public static IEnumerable<object[]> Mutations
    {
        get {
            foreach(var row in Cases){var test=(BootstrapCliCase)row[0];foreach(var mutation in new[]{"exit","source","bytes","captures","build"})yield return new object[]{test,mutation};
                foreach(var path in Correct(test).Modes.Keys)yield return new object[]{test,"mode:"+path};
                foreach(var mutation in test.Failure=="GOOD"?new[]{"missing-manifest","final-paths","generation","input-set","hash","length","media"}:new[]{"diagnostic","failure-output","failure-manifest"})yield return new object[]{test,mutation};}
        }
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapCliMockTests
{
    [Theory,MemberData(nameof(BootstrapCliCases.Cases),MemberType=typeof(BootstrapCliCases))]
    public Task CorrectIndependentPublicCallerObservationsPass(BootstrapCliCase test)=>BootstrapCliCases.AssertContract(test,value=>Task.FromResult(BootstrapCliCases.Correct(value)));
    [Theory,MemberData(nameof(BootstrapCliCases.Mutations),MemberType=typeof(BootstrapCliCases))]
    public async Task EveryCallerInputPinFailureModeAndCommitPredicateRejectsWrongMock(BootstrapCliCase test,string mutation)
        =>Assert.NotNull(await Record.ExceptionAsync(()=>BootstrapCliCases.AssertContract(test,value=>Task.FromResult(BootstrapCliCases.Wrong(value,mutation)))));
}
