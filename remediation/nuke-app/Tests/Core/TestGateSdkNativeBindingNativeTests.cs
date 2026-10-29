using System.Runtime.InteropServices;
using System.Text;
using AndroidMcp.BuildTool.Core;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

// This is an actual selected-SDK API integration fixture when Root runs it through
// native NUKE. It does not restore/build projects or execute a helper process.
[Collection("SDK native binding isolation")]
public sealed class TestGateSdkNativeBindingNativeTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Same_required_StringTools_omission_is_rejected_before_any_SDK_read()
    {
        var fixture=await PrepareAsync("explicit-native-project");
        await SdkBindingCases.AssertMissingStringToolsAsync(fixture,pins=>new LoggingFactory(
            new NativeTestGateSdkSessionFactory(fixture.SdkRoot,pins),output));
    }
    [Theory,MemberData(nameof(SdkBindingCases.Data),MemberType=typeof(SdkBindingCases))]
    public async Task Same_eight_explicit_inputs_bind_to_the_private_native_SDK_session(string name)
    {
        var fixture=await PrepareAsync(name);
        var actual=await SdkBindingCases.ExecuteAsync(fixture,pins=>new LoggingFactory(
            new NativeTestGateSdkSessionFactory(fixture.SdkRoot,pins),output));
        SdkBindingCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    private static async Task<SdkBindingFixture> PrepareAsync(string name)
    {
        // The repository already locks this SDK version. Resolve it only beside
        // the executing selected runtime, with no home scan or alternate SDK.
        var runtime=Path.TrimEndingDirectorySeparator(RuntimeEnvironment.GetRuntimeDirectory());
        var dotnetRoot=Path.GetFullPath(Path.Combine(runtime,"..","..",".."));
        var sdkRoot=Path.Combine(dotnetRoot,"sdk",SdkBindingFixture.SelectedSdkVersion);
        var sdkStore=new FileArtifactStore(sdkRoot,bytes=>ContentHash.Sha256(bytes.Span));
        var version=Encoding.UTF8.GetString((await sdkStore.ReadAsync(".version")).Span)
            .Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        Assert.True(version.Length>1,"Selected SDK version receipt is incomplete.");
        Assert.Equal(SdkBindingFixture.SelectedSdkVersion,version[1]);
        var bytes=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var path in SdkBindingFixture.RequiredPins)bytes.Add(path,(await sdkStore.ReadAsync(path)).ToArray());
        var hostName=OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var hostBytes=await new FileArtifactStore(dotnetRoot,body=>ContentHash.Sha256(body.Span)).ReadAsync(hostName);
        var owned=Path.Combine(Path.GetTempPath(),"androidmcp-sdk-binding",Guid.NewGuid().ToString("N"));
        var root=Path.Combine(owned,"workspace");
        var fixture=new SdkBindingFixture(name,root,sdkRoot,bytes,
            new(Path.Combine(dotnetRoot,hostName),ContentHash.Sha256(hostBytes.Span),version[1]));
        var store=new FileArtifactStore(owned,body=>ContentHash.Sha256(body.Span));
        await store.WriteNewAsync(new("workspace/Host.csproj",fixture.ProjectBytes,"application/xml"));
        await store.WriteNewAsync(new("workspace/inputs/Shared.props",fixture.ImportBytes,"application/xml"));
        await store.WriteNewAsync(new("outside/External.props",fixture.ImportBytes,"application/xml"));
        await store.WriteNewAsync(new("outside/Other.csproj",fixture.ProjectBytes,"application/xml"));
        await store.WriteNewAsync(new("workspace/Core/Host.cs",Encoding.UTF8.GetBytes("class Host {}"),"text/plain"));
        await store.WriteNewAsync(new("workspace/assets/input.bin",new byte[]{1,2,3},"application/octet-stream"));
        await store.WriteNewAsync(new("workspace/config/input.txt",Encoding.UTF8.GetBytes("explicit fixture input"),"text/plain"));
        await store.WriteNewAsync(new("workspace/tools/fixture-analyzer.dll",new byte[]{4,5},"application/octet-stream"));
        await store.WriteNewAsync(new("workspace/Library/Library.csproj",Encoding.UTF8.GetBytes("<Project />"),"application/xml"));
        await store.WriteNewAsync(new("workspace/obj/project.assets.json",JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,object>()),"application/json"));
        // Originals remain retained for exact failed-API diagnostics.
        return fixture;
    }
    private sealed class LoggingFactory(ITestGateSdkNativeSessionFactory native,ITestOutputHelper output):ITestGateSdkNativeSessionFactory
    {
        public async Task<ITestGateSdkNativeSession> OpenAsync(TestGateSdkNativeOpenRequest request,CancellationToken cancellationToken=default)
        {
            try{return new LoggingSession(await native.OpenAsync(request,cancellationToken),output);}
            catch(TestGateFactsException error){output.WriteLine(error.Message);throw;}
        }
    }
    private sealed class LoggingSession(ITestGateSdkNativeSession native,ITestOutputHelper output):ITestGateSdkNativeSession
    {
        public async Task<TestGateSdkNativeProject> LoadProjectAsync(string absoluteProjectPath,CancellationToken cancellationToken=default)
        {
            try{return await native.LoadProjectAsync(absoluteProjectPath,cancellationToken);}
            catch(TestGateFactsException error){output.WriteLine(error.Message);throw;}
        }
        public ValueTask DisposeAsync()=>native.DisposeAsync();
    }
}
