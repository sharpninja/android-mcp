using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record CatalogBuildCliObservation(CatalogBuildReply Build,int ExitCode,bool StdoutRetained,bool StderrRetained,bool CurrentDllPinned,bool NativeHostPinned,bool RegisteredOperationObserved,
    IReadOnlyList<string> Arguments,string Host,string Dll,string RuntimeRoot,string ProjectRoot,string Manifest,string OutputRoot,string StageRoot);
public interface ICatalogBuildCliProbe { Task<CatalogBuildCliObservation> RunAsync(CatalogBuildCase test); }
public static class CatalogBuildCliCases
{
    public static string[] Arguments(string host,string dll,string runtime,string project,string manifest,string output,string stage)=>
        ["exec",dll,"--root",runtime,"--target","Catalog","--operation","Build","--dotnet-host",host,"--project-root",project,"--source-manifest",manifest,"--output-root",output,"--stage-root",stage,"--extraction-profile","current-candidate"];
    private sealed class ObservedBuild(CatalogBuildReply reply):ICatalogBuildCaseProvider
    {public CatalogBuildReply Build(CatalogBuildCall call)=>reply;public void Dispose() { }}
    public static async Task AssertCase(CatalogBuildCase test,ICatalogBuildCliProbe probe)
    {
        var value=await probe.RunAsync(test);CatalogBuildCases.AssertCase(test,new ObservedBuild(value.Build));
        if(test.Expected.Outcome=="ok") Assert.Equal(0,value.ExitCode);else Assert.NotEqual(0,value.ExitCode);
        Assert.True(value.StdoutRetained);Assert.True(value.StderrRetained);Assert.True(value.CurrentDllPinned);Assert.True(value.NativeHostPinned);Assert.True(value.RegisteredOperationObserved);
        // Runtimeconfig/depsfile are an explicit transport insertion after exec;
        // removing that optional quartet leaves this exact registered invocation.
        var arguments=value.Arguments.ToList();var runtimeIndex=arguments.IndexOf("--runtimeconfig");
        if(runtimeIndex>=0){Assert.Equal(1,runtimeIndex);Assert.Equal("--depsfile",arguments[runtimeIndex+2]);arguments.RemoveRange(runtimeIndex,4);}
        Assert.Equal(Arguments(value.Host,value.Dll,value.RuntimeRoot,value.ProjectRoot,value.Manifest,value.OutputRoot,value.StageRoot),arguments);
    }
}
public sealed class ScriptedCatalogBuildCli(int wrong=-1,int wrongBuild=-1):ICatalogBuildCliProbe
{
    public Task<CatalogBuildCliObservation> RunAsync(CatalogBuildCase test)
    {
        const string host="/native host/dotnet",dll="/current compiled/AndroidMcp.Build.dll",runtime="/test owned/isolated nuke root",project="/pinned schemas/catalog",manifest="/test owned/raw inputs/inventory.json",output="/test owned/catalog output",stage="/test owned/private stage";
        var args=CatalogBuildCliCases.Arguments(host,dll,runtime,project,manifest,output,stage);var exit=test.Expected.Outcome=="ok" ? 0 : 1;
        if(wrong is >=5 and <=12)
        {var at=wrong switch{5=>19,6=>5,7=>7,8=>11,9=>13,10=>15,11=>17,_=>1};args[at]="wrong";}
        var reply=wrongBuild<0 ? test.Expected : new ScriptedCatalogBuild(test,wrongBuild).Build(test.Call);
        return Task.FromResult(new CatalogBuildCliObservation(reply,wrong==0 ? exit==0 ? 1 : 0 : exit,wrong!=1,wrong!=2,wrong!=3,wrong!=4,wrong!=13,args,host,dll,runtime,project,manifest,output,stage));
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CatalogBuildCliMockTests
{
    [Theory,MemberData(nameof(CatalogBuildCases.All),MemberType=typeof(CatalogBuildCases))]
    public Task CorrectRegisteredCliAndSameBuildAssertionsPass(CatalogBuildCase test)=>CatalogBuildCliCases.AssertCase(test,new ScriptedCatalogBuildCli());
    [Theory,MemberData(nameof(CatalogBuildCases.All),MemberType=typeof(CatalogBuildCases))]
    public async Task EveryProcessPinArgumentCaptureAndBuildMutationRejected(CatalogBuildCase test)
    {
        for(var wrong=0;wrong<14;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogBuildCliCases.AssertCase(test,new ScriptedCatalogBuildCli(wrong))));
        for(var wrong=0;wrong<24;wrong++) Assert.NotNull(await Record.ExceptionAsync(()=>CatalogBuildCliCases.AssertCase(test,new ScriptedCatalogBuildCli(wrongBuild:wrong))));
    }
}
