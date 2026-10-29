using System.Text;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DesignAliasCliCase(string Id,CodeViewCase Code,string Aliases,string RegistrySha256,string PolicyRelativePath,string Error,bool StaleProjection=false)
{public override string ToString()=>Id;}
public sealed record DesignAliasCliCall(DesignAliasCliCase Case,string Root,string Host,string Dll)
{
    public string ProjectRoot=>Path.Combine(Root,"project");
    public string OutputRoot=>Path.Combine(Root,"final");
    public string RuntimeRoot=>Path.Combine(Root,"nuke-runtime");
}
public sealed record DesignAliasCliObservation(int ExitCode,string Diagnostic,bool StdoutRetained,bool StderrRetained,
    bool NativeHostUnchanged,bool CurrentDllUnchanged,bool SourceSetUnchanged,bool NoToolEffects,bool NoSourceBuild,
    IReadOnlyList<string> Arguments,IReadOnlyList<Artifact> Outputs,Artifact? Manifest,IReadOnlyList<string> UnexpectedOutputPaths);

public static class DesignAliasCliCases
{
    public const string Generation="alias-code-extract";
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var registry=new DesignStagePlanner().Describe().Sha256;var code=ArchitecturePreparationCases.CreateCode();
            // Pure service assertions retain typed error codes; NUKE prints the
            // existing exception message, so the CLI checks that exact message.
            var rows=code.Select(test=>new DesignAliasCliCase(test.Id,test,"extractCodeViews",registry,"../must-not-be-read",test.Error switch
            {
                ""=>"", "MISSING_CODE_VIEW"=>"Missing editable code view:",
                "DUPLICATE_CODE_VIEW"=>"Duplicate editable code view:",
                "UNEXPECTED_CODE_VIEW"=>"Unexpected editable code view:",
                _=>throw new InvalidOperationException("Unmapped original code-preparation case")
            })).ToList();
            rows.Add(new("EMPTY-ALIAS",code[0],"",registry,"","MISSING_PARAMETER"));
            rows.Add(new("UNKNOWN-ALIAS",code[0],"notAnAlias",registry,"","UNKNOWN_ALIAS"));
            rows.Add(new("STALE-REGISTRY",code[0],"extractCodeViews",new string('0',64),"","REGISTRY_MISMATCH"));
            rows.Add(new("STALE-PROJECTION",code[0],"extractCodeViews",registry,"","STALE_GRADLE_PROJECTION",true));
            rows.Add(new("PACKAGE-MISSING-POLICY",code[0],"packageDesignDocs",registry,"","Package selection requires explicit --policy-relative-path"));
            rows.Add(new("PACKAGE-UNSAFE-POLICY",code[0],"packageDesignDocs",registry,"../must-not-be-read","Artifact path"));
            return rows.Select(test=>new object[]{test});
        }
    }
    public static IReadOnlyList<Artifact> SourceArtifacts(DesignAliasCliCase test)
    {
        var projection=DesignGradleProjectionCases.Expected(DesignGradleCliPlanningCases.ProjectionCase());
        var descriptor=projection.Descriptor;
        if(test.StaleProjection)descriptor=descriptor with{Bytes=Encoding.UTF8.GetBytes("{}\n")};
        return [test.Code.Input.Workspace,test.Code.Input.CodeMarkdown,descriptor,projection.BuildScript];
    }
    public static string[] Arguments(DesignAliasCliCall call)
    {
        var args=new List<string>{"exec",call.Dll,"--root",call.RuntimeRoot,"--target","Design","--operation","GradleCompatibility",
            "--dotnet-host",call.Host,"--project-root",call.ProjectRoot,"--output-root",call.OutputRoot,
            "--cache-root",Path.Combine(call.Root,"cache"),"--work-root",Path.Combine(call.Root,"work"),
            "--receipt-root",Path.Combine(call.Root,"tool-receipts"),"--docker-identity",Path.Combine(call.Root,"absent-docker.json"),
            "--renderer-user","1000:1000","--tool-access","Online","--design-aliases",call.Case.Aliases,
            "--design-registry-sha256",call.Case.RegistrySha256,"--generation",Generation,
            "--catalog-root",Path.Combine(call.Root,"not-a-catalog")};
        if(call.Case.PolicyRelativePath.Length!=0)args.AddRange(["--policy-relative-path",call.Case.PolicyRelativePath]);
        return args.ToArray();
    }
    public static async Task AssertContract(DesignAliasCliCall call,Func<DesignAliasCliCall,CancellationToken,Task<DesignAliasCliObservation>> probe)
    {
        using var token=new CancellationTokenSource();var observed=await probe(call,token.Token);
        Assert.Equal(call.Case.Error.Length==0?0:1,observed.ExitCode);
        Assert.True(observed.StdoutRetained);Assert.True(observed.StderrRetained);Assert.True(observed.NativeHostUnchanged);
        Assert.True(observed.CurrentDllUnchanged);Assert.True(observed.SourceSetUnchanged);Assert.True(observed.NoToolEffects);Assert.True(observed.NoSourceBuild);
        var arguments=observed.Arguments.ToList();var config=arguments.IndexOf("--runtimeconfig");
        if(config>=0){Assert.Equal(1,config);Assert.Equal("--depsfile",arguments[config+2]);arguments.RemoveRange(config,4);}
        Assert.Equal(Arguments(call),arguments);Assert.Empty(observed.UnexpectedOutputPaths);
        if(call.Case.Error.Length!=0)
        {Assert.Contains(call.Case.Error,observed.Diagnostic,StringComparison.Ordinal);Assert.Empty(observed.Outputs);Assert.Null(observed.Manifest);return;}
        DesignDocumentationStageCases.AssertArtifacts(call.Case.Code.Expected,observed.Outputs);
        Assert.NotNull(observed.Manifest);Assert.Equal("generation-manifest.json",observed.Manifest.RelativePath);Assert.Equal("application/json",observed.Manifest.MediaType);
        var manifest=JsonNode.Parse(observed.Manifest.Bytes.Span)!.AsObject();
        Assert.Equal("1.0.0",manifest["schema_version"]!.GetValue<string>());Assert.Equal("android-mcp-design-generation",manifest["kind"]!.GetValue<string>());
        Assert.Equal(Generation,manifest["generation_id"]!.GetValue<string>());
        AssertRows(SourceArtifacts(call.Case),manifest["inputs"]!.AsArray());AssertRows(call.Case.Code.Expected,manifest["artifacts"]!.AsArray());
    }
    private static void AssertRows(IReadOnlyList<Artifact> expected,JsonArray rows)
    {
        var verifier=new ArtifactReceiptVerifier(new NativeContentHash());
        Assert.Equal(expected.Select(artifact=>artifact.RelativePath).Order(StringComparer.Ordinal),rows.Select(row=>row!["path"]!.GetValue<string>()).Order(StringComparer.Ordinal));
        foreach(var artifact in expected)
        {
            var identity=verifier.Describe(artifact);var row=Assert.Single(rows,node=>node!["path"]!.GetValue<string>()==artifact.RelativePath)!;
            Assert.Equal(identity.Sha256,row["sha256"]!.GetValue<string>());Assert.Equal(identity.Length,row["size_bytes"]!.GetValue<long>());Assert.Equal(identity.MediaType,row["media_type"]!.GetValue<string>());
        }
    }
}
public sealed class ScriptedDesignAliasCli(string mutation="")
{
    public Task<DesignAliasCliObservation> Run(DesignAliasCliCall call,CancellationToken token)
    {
        Assert.True(token.CanBeCanceled);var success=call.Case.Error.Length==0;
        var output=success?call.Case.Code.Expected.ToArray():Array.Empty<Artifact>();
        var verifier=new ArtifactReceiptVerifier(new NativeContentHash());
        Artifact? manifest=success?new DocumentationGenerationManifestFactory().Create(DesignAliasCliCases.Generation,
            DesignAliasCliCases.SourceArtifacts(call.Case).Select(verifier.Describe).ToArray(),output.Select(verifier.Describe).ToArray()):null;
        if(mutation=="missing-output")output=output.Skip(1).ToArray();
        if(mutation=="output-bytes")output[0]=output[0] with{Bytes=new byte[]{0}};
        if(mutation=="output-media")output[0]=output[0] with{MediaType="wrong"};
        if(mutation=="missing-manifest")manifest=null;
        if(mutation.StartsWith("manifest-",StringComparison.Ordinal)&&manifest is not null)
        {
            var json=JsonNode.Parse(manifest.Bytes.Span)!.AsObject();
            switch(mutation)
            {
                case "manifest-generation":json["generation_id"]="wrong";break;
                case "manifest-kind":json["kind"]="wrong";break;
                case "manifest-schema":json["schema_version"]="wrong";break;
                case "manifest-input-set":json["inputs"]!.AsArray().RemoveAt(0);break;
                case "manifest-output-set":json["artifacts"]!.AsArray().RemoveAt(0);break;
                case "manifest-hash":json["artifacts"]![0]!["sha256"]=new string('0',64);break;
                case "manifest-length":json["artifacts"]![0]!["size_bytes"]=0;break;
                case "manifest-media":json["artifacts"]![0]!["media_type"]="wrong";break;
            }
            manifest=manifest with{Bytes=Encoding.UTF8.GetBytes(json.ToJsonString())};
        }
        var args=DesignAliasCliCases.Arguments(call);if(mutation=="argv")args[7]="Architecture";
        return Task.FromResult(new DesignAliasCliObservation(mutation=="exit"?(success?1:0):(success?0:1),mutation=="diagnostic"?"wrong":call.Case.Error,
            mutation!="stdout",mutation!="stderr",mutation!="host",mutation!="dll",mutation!="source",mutation!="tools",mutation!="build",args,output,manifest,
            mutation=="unexpected-output"?["unexpected.txt"]:[]));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleAliasCliMockTests
{
    private static DesignAliasCliCall Call(DesignAliasCliCase test)=>new(test,"/fixture/isolated alias","/native/dotnet","/current/AndroidMcp.Build.dll");
    [Theory,MemberData(nameof(DesignAliasCliCases.Cases),MemberType=typeof(DesignAliasCliCases))]
    public Task CorrectFiniteAliasCliOutcomesPass(DesignAliasCliCase test)=>DesignAliasCliCases.AssertContract(Call(test),new ScriptedDesignAliasCli().Run);
    [Theory,MemberData(nameof(DesignAliasCliCases.Cases),MemberType=typeof(DesignAliasCliCases))]
    public async Task EveryWrongCliExitIsRejected(DesignAliasCliCase test)=>Assert.NotNull(await Record.ExceptionAsync(()=>DesignAliasCliCases.AssertContract(Call(test),new ScriptedDesignAliasCli("exit").Run)));
    [Theory]
    [InlineData("stdout")][InlineData("stderr")][InlineData("host")][InlineData("dll")][InlineData("source")][InlineData("tools")][InlineData("build")][InlineData("argv")]
    [InlineData("missing-output")][InlineData("output-bytes")][InlineData("output-media")][InlineData("missing-manifest")][InlineData("unexpected-output")]
    [InlineData("manifest-generation")][InlineData("manifest-kind")][InlineData("manifest-schema")][InlineData("manifest-input-set")][InlineData("manifest-output-set")]
    [InlineData("manifest-hash")][InlineData("manifest-length")][InlineData("manifest-media")]
    public async Task EachCliArtifactProvenanceAndEffectFaultIsRejected(string mutation)
    {var test=(DesignAliasCliCase)DesignAliasCliCases.Cases.First()[0];Assert.NotNull(await Record.ExceptionAsync(()=>DesignAliasCliCases.AssertContract(Call(test),new ScriptedDesignAliasCli(mutation).Run)));}
    [Fact]
    public async Task AFailureCannotLoseItsDiagnostic()
    {var test=DesignAliasCliCases.Cases.Select(row=>(DesignAliasCliCase)row[0]).First(test=>test.Error.Length!=0);Assert.NotNull(await Record.ExceptionAsync(()=>DesignAliasCliCases.AssertContract(Call(test),new ScriptedDesignAliasCli("diagnostic").Run)));}
}
