using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-003")]
public sealed class ProcessSessionFixtureRealTests
{
    [Fact]
    public async Task StandaloneWaitFileFixtureExitsOnlyAfterItsOwnedGateIsReleased()
    {
        var root=Path.Combine(Path.GetTempPath(),"androidmcp-session-fixture",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var gate=Path.Combine(root,"release");
        using var cancellation=new CancellationTokenSource();var runner=new NativeProcessRunner(new SystemClock());
        var pending=runner.RunAsync(ProcessRunnerRealTests.Request(root,17,"wait-file",[gate]),cancellation.Token);
        try
        {
            Assert.False(pending.IsCompleted);Assert.False(File.Exists(gate));await File.WriteAllTextAsync(gate,"release");
            var result=await pending;Assert.Equal(17,result.ExitCode);Assert.False(result.Cancelled);
            Assert.Equal("ready",await File.ReadAllTextAsync(result.StandardOutputPath));Assert.Empty(await File.ReadAllBytesAsync(result.StandardErrorPath));
        }
        finally {cancellation.Cancel();await pending;}
    }
}
