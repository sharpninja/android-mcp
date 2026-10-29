using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation.Browser;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Browser;

internal sealed record BrowserQaCliInvocation(BrowserQaRequest Request,string ArchitectureRoot,string UiManifestSha256,string ArchitectureManifestSha256,
    string Host,string Dll,string RuntimeRoot,string BrowserIdentityPath);
internal sealed record BrowserQaCliObservation(BrowserQaResult Result,int ExitCode,bool StdoutRetained,bool StderrRetained,bool CurrentDllPinned,
    bool NativeHostPinned,bool RegisteredOperationObserved,bool InputGenerationsUnchanged,bool NoSourceBuild,IReadOnlyList<string> Arguments,
    string UiManifestSha256,string ArchitectureManifestSha256,string UiGenerationId,string ArchitectureGenerationId);
internal interface IBrowserQaCliProbe
{Task<BrowserQaCliObservation> RunAsync(BrowserQaCliInvocation call,CancellationToken cancellationToken=default);}

internal static class BrowserQaCliCases
{
    internal static string[] Arguments(BrowserQaCliInvocation call)=>["exec",call.Dll,"--root",call.RuntimeRoot,"--target","Design","--operation","BrowserQa",
        "--dotnet-host",call.Host,"--project-root",call.Request.ProjectRoot,"--architecture-root",call.ArchitectureRoot,"--output-root",call.Request.OutputRoot,
        "--browser-identity",call.BrowserIdentityPath,"--ui-manifest-sha256",call.UiManifestSha256,"--architecture-manifest-sha256",call.ArchitectureManifestSha256,
        "--expected-architecture-diagrams","1","--viewport-width","1440","--viewport-height","960"];
    internal static BrowserQaCliInvocation Input(string root,BrowserQaCase test,ToolIdentity? browser=null)
    {
        // Retain the original single-root fixture. The new two-root fixture is
        // copied from it without editing either source or the original cases.
        var seed=BrowserQaCases.CreateInput(Path.Combine(root,"retained-seed"),test,browser);
        var ui=Path.Combine(root,"ui");var architecture=Path.Combine(root,"architecture");
        foreach(var path in Directory.EnumerateFiles(seed.ProjectRoot,"*",SearchOption.AllDirectories))
        {
            var relative=Path.GetRelativePath(seed.ProjectRoot,path);var destination=relative.Replace(Path.DirectorySeparatorChar,'/').StartsWith("docs/diagrams/",StringComparison.Ordinal)?architecture:ui;
            var target=Path.Combine(destination,relative);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(path,target);
        }
        string Seal(string directory,string generation)
        {
            var files=Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path=>new{path=Path.GetRelativePath(directory,path).Replace(Path.DirectorySeparatorChar,'/'),sha256=ContentHash.Sha256(File.ReadAllBytes(path)),size_bytes=new FileInfo(path).Length,media_type=path.EndsWith(".svg",StringComparison.Ordinal)?"image/svg+xml":"application/octet-stream"}).ToArray();
            var bytes=JsonSerializer.SerializeToUtf8Bytes(new{schema_version="1.0.0",kind="android-mcp-design-generation",generation_id=generation,inputs=Array.Empty<object>(),artifacts=files});
            File.WriteAllBytes(Path.Combine(directory,"generation-manifest.json"),bytes);return ContentHash.Sha256(bytes);
        }
        var uiPin=Seal(ui,"fixture-ui");var architecturePin=Seal(architecture,"fixture-architecture");
        var identity=Path.Combine(root,"browser-identity.json");File.WriteAllBytes(identity,JsonSerializer.SerializeToUtf8Bytes(seed.Browser));
        return new(seed with{ProjectRoot=ui,OutputRoot=Path.Combine(root,"output")},architecture,uiPin,architecturePin,
            "/native host/dotnet","/current compiled/AndroidMcp.Build.dll","/isolated nuke root",identity);
    }
    private sealed class ObservedQa(IBrowserQaCliProbe probe,BrowserQaCliInvocation call):IBrowserQaOperation
    {
        internal BrowserQaCliObservation? Observation {get;private set;}
        public async Task<BrowserQaResult> RunAsync(BrowserQaRequest request,CancellationToken cancellationToken=default)
        {Assert.Same(call.Request,request);Observation=await probe.RunAsync(call,cancellationToken);return Observation.Result;}
    }
    internal static async Task AssertContract(BrowserQaCase test,IBrowserQaCliProbe probe,BrowserQaCliInvocation call,IArtifactStore output)
    {
        var before=Directory.EnumerateFiles(call.ArchitectureRoot,"*",SearchOption.AllDirectories).ToDictionary(path=>path,path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal);
        var provider=new ObservedQa(probe,call);await BrowserQaCases.AssertContract(test,provider,output,call.Request);var observed=provider.Observation!;
        Assert.Equal(test.Failure is null?0:1,observed.ExitCode);Assert.True(observed.StdoutRetained);Assert.True(observed.StderrRetained);
        Assert.True(observed.CurrentDllPinned);Assert.True(observed.NativeHostPinned);Assert.True(observed.RegisteredOperationObserved);
        Assert.True(observed.InputGenerationsUnchanged);Assert.True(observed.NoSourceBuild);
        Assert.Equal(call.UiManifestSha256,observed.UiManifestSha256);Assert.Equal(call.ArchitectureManifestSha256,observed.ArchitectureManifestSha256);
        Assert.Equal("fixture-ui",observed.UiGenerationId);Assert.Equal("fixture-architecture",observed.ArchitectureGenerationId);
        var arguments=observed.Arguments.ToList();var config=arguments.IndexOf("--runtimeconfig");
        if(config>=0){Assert.Equal(1,config);Assert.Equal("--depsfile",arguments[config+2]);arguments.RemoveRange(config,4);}
        Assert.Equal(Arguments(call),arguments);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal),Directory.EnumerateFiles(call.ArchitectureRoot,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal));
        foreach(var path in before.Keys)Assert.Equal(before[path],ContentHash.Sha256(File.ReadAllBytes(path)));
    }
}

internal sealed class ScriptedBrowserQaCli(BrowserQaCase test,IArtifactStore output,int wrong=-1,string? wrongQa=null):IBrowserQaCliProbe
{
    public async Task<BrowserQaCliObservation> RunAsync(BrowserQaCliInvocation call,CancellationToken cancellationToken=default)
    {
        var result=await new ScriptedBrowserQa(test,output,wrongQa).RunAsync(call.Request,cancellationToken);var arguments=BrowserQaCliCases.Arguments(call);
        if(wrong is >=8 and <=21){var positions=new[]{1,3,5,7,9,11,13,15,17,19,21,23,25,27};arguments[positions[wrong-8]]="wrong";}
        return new(result,wrong==0?(test.Failure is null?1:0):(test.Failure is null?0:1),wrong!=1,wrong!=2,wrong!=3,wrong!=4,wrong!=5,wrong!=6,wrong!=7,arguments,
            wrong==22?new string('0',64):call.UiManifestSha256,wrong==23?new string('0',64):call.ArchitectureManifestSha256,
            wrong==24?"wrong":"fixture-ui",wrong==25?"wrong":"fixture-architecture");
    }
}
