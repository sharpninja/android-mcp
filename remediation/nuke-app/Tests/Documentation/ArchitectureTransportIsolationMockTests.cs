using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public static class ArchitectureTransportIsolationCases
{
    public static ProcessRequest Isolate(ProcessRequest request)=>request.Arguments.Count>=4&&request.Arguments[0]=="--host"?request:request with
    {Arguments=new[]{"--host","unix:///var/run/docker.sock","--config",request.WorkingDirectory+"/.docker-cli"}.Concat(request.Arguments).ToArray(),ClearEnvironment=true};
    public static Task AssertContract(ArchitectureTransportCase test,ArchitectureTransportEnvironment environment,IArchitectureRendererTransport provider)=>ArchitectureTransportCases.AssertContract(test,environment,provider,Isolate);
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureTransportIsolationMockTests
{
    [Theory,MemberData(nameof(ArchitectureTransportCases.Data),MemberType=typeof(ArchitectureTransportCases))]
    public Task CorrectIsolatedTransportMocksPass(ArchitectureTransportCase test)
    {var e=new ArchitectureTransportEnvironment(test);return ArchitectureTransportIsolationCases.AssertContract(test,e,new ScriptedArchitectureTransport(e,requestTransform:ArchitectureTransportIsolationCases.Isolate));}
    [Theory]
    [InlineData("host-flag")][InlineData("host-socket")][InlineData("config-flag")][InlineData("config-directory")][InlineData("inherited-environment")][InlineData("global-config")][InlineData("remote-daemon")]
    public async Task EachDaemonAndEnvironmentEscapeIsRejected(string mutation)
    {
        var test=(ArchitectureTransportCase)ArchitectureTransportCases.Data.First()[0];var e=new ArchitectureTransportEnvironment(test);
        ProcessRequest Wrong(ProcessRequest original)
        {
            var request=ArchitectureTransportIsolationCases.Isolate(original);var args=request.Arguments.ToArray();
            if(mutation=="inherited-environment")return request with{ClearEnvironment=false};
            if(mutation=="host-flag")args[0]="wrong";if(mutation=="host-socket")args[1]="wrong";
            if(mutation=="config-flag")args[2]="wrong";if(mutation=="config-directory")args[3]="wrong";
            if(mutation=="global-config")args[3]="/home/fixture/.docker";if(mutation=="remote-daemon")args[1]="tcp://remote.invalid:2376";
            return request with{Arguments=args};
        }
        Assert.NotNull(await Record.ExceptionAsync(()=>ArchitectureTransportIsolationCases.AssertContract(test,e,new ScriptedArchitectureTransport(e,requestTransform:Wrong))));
    }
}
