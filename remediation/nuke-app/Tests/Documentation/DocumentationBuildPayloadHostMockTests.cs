using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record BuildPayloadHostCase(string Id,Artifact Request,DocumentationBuildPayloadHostConfiguration Expected,
    string ExpectedHost,string? Error=null)
{public override string ToString()=>Id;}
public static class BuildPayloadHostCases
{
    public static IReadOnlyList<BuildPayloadHostCase> Create()
    {
        var payload=DocumentationBuildPayloadCases.Create()[0].Input;
        var host=Path.Combine(payload.SourceRoot,OperatingSystem.IsWindows()?"native/dotnet.exe":"native/dotnet");
        var config=new DocumentationBuildPayloadHostConfiguration(1,new(host,new string('a',64),"native expected version"),payload.NativeConfiguration,payload.PrerequisiteScopes);
        var artifact=new Artifact("tooling/native-build-host.json",new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(config)),"application/json");
        var rows=new List<BuildPayloadHostCase>{new("FULL",artifact,config,host)};
        void Change(string id,Action<JsonObject> mutate,string error="BUILD_SOURCE_REQUEST")
        {var json=JsonNode.Parse(artifact.Bytes.Span)!.AsObject();mutate(json);rows.Add(new(id,artifact with{Bytes=Encoding.UTF8.GetBytes(json.ToJsonString())},config,host,error));}
        Change("VERSION",value=>value["SchemaVersion"]=2);
        Change("HOST-PATH",value=>value["DotNet"]!["ExecutablePath"]=Path.Combine(payload.SourceRoot,"other-host"),"BUILD_SOURCE_HOST_TOOL");
        Change("HOST-SHA",value=>value["DotNet"]!["Sha256"]="");
        Change("HOST-VERSION",value=>value["DotNet"]!["Version"]="");
        Change("NATIVE-MISSING",value=>value["Native"]=null);
        Change("SDK-MISSING",value=>value["Native"]!["Sdk"]=null);
        Change("SDK-PINS-MISSING",value=>value["Native"]!["Sdk"]!["AssemblyFiles"]=new JsonArray());
        Change("SDK-ROOT-RELATIVE",value=>value["Native"]!["Sdk"]!["SdkDirectory"]="relative");
        Change("FRAMEWORK-MISSING",value=>value["Native"]!["TargetFramework"]="");
        Change("READ-ROOTS-MISSING",value=>value["Native"]!["ApprovedReadRoots"]=new JsonArray());
        Change("SCOPES-MISSING",value=>value["PrerequisiteScopes"]=null);
        Change("UNKNOWN-FIELD",value=>value["CallerSourceFileList"]=new JsonArray("unchecked.cs"));
        rows.Add(new("INVALID-JSON",artifact with{Bytes=new byte[]{0xff}},config,host,"BUILD_SOURCE_REQUEST"));
        rows.Add(new("DUPLICATE-JSON",artifact with{Bytes=Encoding.UTF8.GetBytes("{\"SchemaVersion\":1,\"SchemaVersion\":1}")},config,host,"BUILD_SOURCE_REQUEST"));
        rows.Add(new("READ-FAIL",artifact,config,host,"IO"));
        rows.Add(new("CANCEL",artifact,config,host,"CANCEL"));return rows;
    }
    public static IEnumerable<object[]> Cases=>Create().Select(test=>new object[]{test});
    public static async Task AssertContract(BuildPayloadHostCase test,Func<StoredArtifact,string,CancellationToken,Task<DocumentationBuildPayloadHostConfiguration>> read,List<string> trace)
    {
        var before=test.Request.Bytes.ToArray();var pin=new ArtifactReceiptVerifier(new NativeContentHash()).Describe(test.Request);
        using var cancellation=new CancellationTokenSource();if(test.Error=="CANCEL")cancellation.Cancel();
        DocumentationBuildPayloadHostConfiguration? config=null;var error=await Record.ExceptionAsync(async()=>config=await read(pin,test.ExpectedHost,cancellation.Token));
        if(test.Error is not null)
        {
            if(test.Error=="CANCEL")Assert.IsAssignableFrom<OperationCanceledException>(error);
            else if(test.Error=="IO")Assert.IsAssignableFrom<IOException>(error);
            else Assert.Equal(test.Error,Assert.IsType<DocumentationBuildPayloadException>(error).Code);
            Assert.Null(config);
        }
        else
        {
            Assert.Null(error);Assert.NotNull(config);Assert.Equal(JsonSerializer.Serialize(test.Expected),JsonSerializer.Serialize(config));
            var roots=Assert.IsAssignableFrom<IList<string>>(config.Native.ApprovedReadRoots);Assert.Throws<NotSupportedException>(()=>roots[0]=roots[0]);
            var pins=Assert.IsAssignableFrom<IList<StoredArtifact>>(config.Native.Sdk.AssemblyFiles);Assert.Throws<NotSupportedException>(()=>pins[0]=pins[0]);
            var scopes=Assert.IsAssignableFrom<IList<DocumentationBuildPrerequisiteScope>>(config.PrerequisiteScopes);Assert.Throws<NotSupportedException>(()=>scopes[0]=scopes[0]);
        }
        Assert.Equal(test.Error=="CANCEL"?[]:new[]{"read"},trace);Assert.Equal(before,test.Request.Bytes.ToArray());
    }
}
public sealed class BuildPayloadHostPinnedReader(BuildPayloadHostCase test,List<string> trace):IPinnedArtifactReader
{
    public Task<ReadOnlyMemory<byte>> ReadAsync(StoredArtifact pin,CancellationToken token=default)
    {trace.Add("read");Assert.True(token.CanBeCanceled);Assert.Equal(new ArtifactReceiptVerifier(new NativeContentHash()).Describe(test.Request),pin);if(test.Error=="IO")throw new IOException("scripted request read failure");return Task.FromResult<ReadOnlyMemory<byte>>(test.Request.Bytes.ToArray());}
}
public sealed class ScriptedBuildPayloadHostReader(BuildPayloadHostCase test,List<string> trace,string mutation=""):IDocumentationBuildPayloadHostReader
{
    public async Task<DocumentationBuildPayloadHostConfiguration> ReadAsync(StoredArtifact request,string expected,CancellationToken token=default)
    {
        if(mutation!="cancel")token.ThrowIfCancellationRequested();Assert.Equal(test.ExpectedHost,expected);
        var readCase=mutation=="outcome"&&test.Error=="IO"?test with{Error=null}:test;
        await new BuildPayloadHostPinnedReader(readCase,trace).ReadAsync(request,token);
        if(test.Error is not null&&mutation!="outcome")throw new DocumentationBuildPayloadException(test.Error);
        var config=test.Expected with{Native=test.Expected.Native with{ApprovedReadRoots=Array.AsReadOnly(test.Expected.Native.ApprovedReadRoots.ToArray()),
            Sdk=test.Expected.Native.Sdk with{AssemblyFiles=Array.AsReadOnly(test.Expected.Native.Sdk.AssemblyFiles.ToArray())}},PrerequisiteScopes=Array.AsReadOnly(test.Expected.PrerequisiteScopes.ToArray())};
        return mutation switch
        {
            "version"=>config with{SchemaVersion=3},"tool"=>config with{DotNet=config.DotNet with{Sha256="wrong"}},
            "framework"=>config with{Native=config.Native with{TargetFramework="wrong"}},
            "roots"=>config with{Native=config.Native with{ApprovedReadRoots=Array.Empty<string>()}},
            "pins"=>config with{Native=config.Native with{Sdk=config.Native.Sdk with{AssemblyFiles=Array.Empty<StoredArtifact>()}}},
            "scopes"=>config with{PrerequisiteScopes=Array.Empty<DocumentationBuildPrerequisiteScope>()},
            "mutable"=>test.Expected,_=>config
        };
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBuildPayloadHostMockTests
{
    [Theory,MemberData(nameof(BuildPayloadHostCases.Cases),MemberType=typeof(BuildPayloadHostCases))]
    public Task CorrectPinnedHostRequestCasesPass(BuildPayloadHostCase test)
    {var trace=new List<string>();return BuildPayloadHostCases.AssertContract(test,new ScriptedBuildPayloadHostReader(test,trace).ReadAsync,trace);}
    [Theory,MemberData(nameof(BuildPayloadHostCases.Cases),MemberType=typeof(BuildPayloadHostCases))]
    public async Task EveryWrongOutcomeIsRejected(BuildPayloadHostCase test)
    {var trace=new List<string>();Assert.NotNull(await Record.ExceptionAsync(()=>BuildPayloadHostCases.AssertContract(test,new ScriptedBuildPayloadHostReader(test,trace,test.Error=="CANCEL"?"cancel":test.Error is null?"version":"outcome").ReadAsync,trace)));}
    [Theory]
    [InlineData("tool")][InlineData("framework")][InlineData("roots")][InlineData("pins")][InlineData("scopes")][InlineData("mutable")]
    public async Task EachAlteredOrMutableConfigurationIsRejected(string mutation)
    {var test=BuildPayloadHostCases.Create()[0];var trace=new List<string>();Assert.NotNull(await Record.ExceptionAsync(()=>BuildPayloadHostCases.AssertContract(test,new ScriptedBuildPayloadHostReader(test,trace,mutation).ReadAsync,trace)));}
}
