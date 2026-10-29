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
    IReadOnlyList<Artifact> Sources,Artifact? BootstrapReceipt=null,Artifact? BootstrapChildManifest=null);
public static class BootstrapCliCases
{
    public const string PropertiesPath="gradle/wrapper/gradle-wrapper.properties";
    public static IEnumerable<object[]> Cases=>new[]{"verifyToolchain","generateUiAssets","renderStoryboards"}
        .SelectMany(alias=>new[]{"GOOD","BAD-PROPERTIES","MISSING-PROPERTIES"}.Select(failure=>new object[]{new BootstrapCliCase(alias,failure)}));
    public static async Task AssertContract(BootstrapCliCase test,Func<BootstrapCliCase,Task<BootstrapCliObservation>> probe)
    {
        var observation=await probe(test);var good=test.Failure=="GOOD";
        Assert.Equal(good?0:1,observation.ExitCode);Assert.True(observation.SourceUnchanged);Assert.True(observation.ToolBytesUnchanged);Assert.True(observation.CapturesRetained);Assert.True(observation.NoSourceBuild);
        const bool effects=true; // Original missing read follows all executable-mode effects.
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
        Assert.Contains("toolchain-bootstrap-receipt.json",observation.FinalPaths);
        if(test.Alias=="verifyToolchain")Assert.Equal(new[]{"generation-manifest.json","toolchain-bootstrap-receipt.json"},observation.FinalPaths);
        else {Assert.Contains("build/receipts/ui-projection.json",observation.FinalPaths);Assert.Contains("docs/storyboards.html",observation.FinalPaths);}
        Assert.NotNull(observation.BootstrapReceipt);Assert.NotNull(observation.BootstrapChildManifest);
        var receipt=observation.BootstrapReceipt;var child=JsonNode.Parse(observation.BootstrapChildManifest.Bytes.Span)!;
        Assert.Equal("bootstrap-caller-bootstrap",child["generation_id"]!.GetValue<string>());
        Assert.Equal(new[]{PropertiesPath,"toolchain-lock.json"},child["inputs"]!.AsArray().Select(value=>value!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        foreach(var input in child["inputs"]!.AsArray()){
            var original=Assert.Single(expected,artifact=>artifact.RelativePath==input!["path"]!.GetValue<string>());
            Assert.Equal(ContentHash.Sha256(original.Bytes.Span),input!["sha256"]!.GetValue<string>());
        }
        foreach(var outputs in new[]{child["artifacts"]!.AsArray(),manifest["artifacts"]!.AsArray()}){
            var identity=Assert.Single(outputs,value=>value!["path"]!.GetValue<string>()==receipt.RelativePath)!;
            Assert.Equal(ContentHash.Sha256(receipt.Bytes.Span),identity["sha256"]!.GetValue<string>());
        }
        var receiptJson=JsonNode.Parse(receipt.Bytes.Span)!;
        Assert.Equal(ContentHash.Sha256(expected.Single(artifact=>artifact.RelativePath=="toolchain-lock.json").Bytes.Span),receiptJson["original_lock_sha256"]!.GetValue<string>());
        var printed=observation.Diagnostic.IndexOf(BootstrapReportCases.Summary,StringComparison.Ordinal);Assert.True(printed>=0);
        Assert.True(observation.Diagnostic.IndexOf("\"GenerationId\"",printed+BootstrapReportCases.Summary.Length,StringComparison.Ordinal)>printed);
    }
    public static BootstrapCliObservation Correct(BootstrapCliCase test)
    {
        var sources=new List<Artifact>{new("toolchain-lock.json","{}"u8.ToArray(),"application/json"),new("renderer-lock.json","{}"u8.ToArray(),"application/json"),
            new("build.gradle.kts","projection"u8.ToArray(),"text/plain"),new("tooling/nuke-design-stages.json","descriptor"u8.ToArray(),"application/json")};
        if(test.Failure!="MISSING-PROPERTIES")sources.Add(new(PropertiesPath,"original captured properties\n"u8.ToArray(),"application/octet-stream"));
        if(test.Alias!="verifyToolchain"){sources.Add(new("docs/ui-blueprint.md","blueprint"u8.ToArray(),"text/markdown"));sources.Add(new("docs/Android MCP Bridge Design.md","design"u8.ToArray(),"text/markdown"));}
        var good=test.Failure=="GOOD";const bool effects=true;
        var modes=new Dictionary<string,int>(StringComparer.Ordinal){[".tools/gradle-8.14.3/bin/gradle"]=effects?489:416,[".tools/gradle-8.14.3/bin/extra"]=effects?457:384,[".tools/gradle-8.14.3/bin/retained-directory"]=effects?457:448,["gradlew"]=effects?493:438,["unrelated"]=438};
        var verifier=new ArtifactReceiptVerifier(new NativeContentHash());
        var receipt=new Artifact("toolchain-bootstrap-receipt.json",Encoding.UTF8.GetBytes(new JsonObject{["original_lock_sha256"]=ContentHash.Sha256(sources.Single(artifact=>artifact.RelativePath=="toolchain-lock.json").Bytes.Span)}.ToJsonString()),"application/json");
        var manifest=good?new DocumentationGenerationManifestFactory().Create("bootstrap-caller",sources.Select(verifier.Describe).ToArray(),[verifier.Describe(receipt)]):null;
        var child=good?new DocumentationGenerationManifestFactory().Create("bootstrap-caller-bootstrap",sources.Where(artifact=>artifact.RelativePath is "toolchain-lock.json" or PropertiesPath).Select(verifier.Describe).ToArray(),[verifier.Describe(receipt)]):null;
        return new(good?0:1,good?BootstrapReportCases.Summary+"{\"GenerationId\":\"bootstrap-caller\"}\n":test.Failure=="BAD-PROPERTIES"?"Committed wrapper properties differ from pinned expected configuration":PropertiesPath,manifest,modes,true,true,true,true,
            !good?[]:test.Alias=="verifyToolchain"?["generation-manifest.json","toolchain-bootstrap-receipt.json"]:["build/receipts/ui-projection.json","docs/storyboards.html","generation-manifest.json","toolchain-bootstrap-receipt.json"],sources,good?receipt:null,child);
    }
    public static BootstrapCliObservation Wrong(BootstrapCliCase test,string mutation)
    {
        var value=Correct(test);
        if(mutation=="exit")return value with{ExitCode=1-value.ExitCode};if(mutation=="source")return value with{SourceUnchanged=false};if(mutation=="bytes")return value with{ToolBytesUnchanged=false};if(mutation=="captures")return value with{CapturesRetained=false};if(mutation=="build")return value with{NoSourceBuild=false};
        if(mutation.StartsWith("mode:",StringComparison.Ordinal)){var modes=value.Modes.ToDictionary(pair=>pair.Key,pair=>pair.Value,StringComparer.Ordinal);modes[mutation[5..]]=0;return value with{Modes=modes};}
        if(mutation=="diagnostic")return value with{Diagnostic="wrong"};if(mutation=="failure-output")return value with{FinalPaths=["incorrect-commit"]};
        if(mutation=="failure-manifest")return value with{Manifest=new("generation-manifest.json","{}"u8.ToArray(),"application/json")};
        if(mutation=="missing-manifest")return value with{Manifest=null};if(mutation=="final-paths")return value with{FinalPaths=[]};
        if(mutation=="receipt-missing")return value with{BootstrapReceipt=null};if(mutation=="child-missing")return value with{BootstrapChildManifest=null};
        if(mutation=="summary-missing")return value with{Diagnostic="{\"GenerationId\":\"bootstrap-caller\"}\n"};if(mutation=="summary-late")return value with{Diagnostic="{\"GenerationId\":\"bootstrap-caller\"}\n"+BootstrapReportCases.Summary};
        if(mutation=="receipt-lock"){var receiptJson=JsonNode.Parse(value.BootstrapReceipt!.Bytes.Span)!;receiptJson["original_lock_sha256"]="wrong";return value with{BootstrapReceipt=value.BootstrapReceipt with{Bytes=Encoding.UTF8.GetBytes(receiptJson.ToJsonString())}};}
        if(mutation.StartsWith("child-",StringComparison.Ordinal)){
            var child=JsonNode.Parse(value.BootstrapChildManifest!.Bytes.Span)!;
            if(mutation=="child-generation")child["generation_id"]="wrong";if(mutation=="child-inputs")child["inputs"]!.AsArray().RemoveAt(0);
            if(mutation=="child-input-hash")child["inputs"]![0]!["sha256"]="wrong";if(mutation=="child-output-hash")child["artifacts"]![0]!["sha256"]="wrong";
            return value with{BootstrapChildManifest=value.BootstrapChildManifest with{Bytes=Encoding.UTF8.GetBytes(child.ToJsonString())}};
        }
        var json=JsonNode.Parse(value.Manifest!.Bytes.Span)!;var row=json["inputs"]!.AsArray().Single(node=>node!["path"]!.GetValue<string>()==PropertiesPath)!;
        if(mutation=="final-output-hash")json["artifacts"]![0]!["sha256"]="wrong";
        if(mutation=="generation")json["generation_id"]="wrong";else if(mutation=="input-set")json["inputs"]!.AsArray().RemoveAt(0);else if(mutation=="hash")row["sha256"]="wrong";else if(mutation=="length")row["size_bytes"]=0;else if(mutation=="media")row["media_type"]="wrong";
        return value with{Manifest=value.Manifest with{Bytes=Encoding.UTF8.GetBytes(json.ToJsonString())}};
    }
    public static IEnumerable<object[]> Mutations
    {
        get {
            foreach(var row in Cases){var test=(BootstrapCliCase)row[0];foreach(var mutation in new[]{"exit","source","bytes","captures","build"})yield return new object[]{test,mutation};
                foreach(var path in Correct(test).Modes.Keys)yield return new object[]{test,"mode:"+path};
                foreach(var mutation in test.Failure=="GOOD"?new[]{"missing-manifest","final-paths","generation","input-set","hash","length","media","receipt-missing","child-missing","summary-missing","summary-late","receipt-lock","child-generation","child-inputs","child-input-hash","child-output-hash","final-output-hash"}:new[]{"diagnostic","failure-output","failure-manifest"})yield return new object[]{test,mutation};}
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
