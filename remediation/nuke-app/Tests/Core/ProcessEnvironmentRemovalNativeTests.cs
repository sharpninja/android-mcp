using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

[Collection("OwnedProcessEnvironment")]
public sealed class ProcessEnvironmentRemovalNativeTests
{
    [Theory,MemberData(nameof(ProcessEnvironmentRemovalCases.Data),MemberType=typeof(ProcessEnvironmentRemovalCases)),Trait("Gate","Real")]
    public async Task Same_name_only_policy_packets_bind_to_single_native_engine_and_owned_dummy_child(string name)
    {
        var packet=ProcessEnvironmentRemovalCases.Input(name);var root=ProcessSessionFixturePaths.NewRoot("process-environment-removal");
        var values=packet.PublicOverride?new Dictionary<string,string>{{ProcessEnvironmentRemovalCases.Name,ProcessEnvironmentRemovalCases.Public}}:new Dictionary<string,string>();
        var request=ProcessRunnerRealTests.Request(root,0,"environment",[ProcessEnvironmentRemovalCases.Name]) with
        {ClearEnvironment=packet.Clear,PublicEnvironment=values,RemovedEnvironmentKeys=packet.Keys,
            RemovedEnvironmentNameRules=packet.Rules?.Select(rule=>new ProcessEnvironmentNameRule(rule.Prefix,rule.ContainsAny)).ToArray()};
        var before=JsonSerializer.Serialize(request);var previous=Environment.GetEnvironmentVariable(ProcessEnvironmentRemovalCases.Name);
        Environment.SetEnvironmentVariable(ProcessEnvironmentRemovalCases.Name,ProcessEnvironmentRemovalCases.Ambient);
        RemovalObservation observation;
        try
        {
            try
            {
                var result=await new NativeProcessRunner(new SystemClock()).RunAsync(request);
                Assert.Equal(0,result.ExitCode);Assert.False(result.Cancelled);Assert.Empty(await File.ReadAllBytesAsync(result.StandardErrorPath));
                var body=JsonSerializer.Deserialize<Dictionary<string,string?>>(await File.ReadAllBytesAsync(result.StandardOutputPath))!;
                Assert.Single(body);Assert.True(body.ContainsKey(ProcessEnvironmentRemovalCases.Name));
                observation=new("OK",body[ProcessEnvironmentRemovalCases.Name],Environment.GetEnvironmentVariable(ProcessEnvironmentRemovalCases.Name)==ProcessEnvironmentRemovalCases.Ambient,
                    before==JsonSerializer.Serialize(request),1,Directory.Exists(request.ReceiptDirectory));
            }
            catch(InvalidOperationException error)when(error.Message.StartsWith("ENVIRONMENT_REMOVAL_",StringComparison.Ordinal))
            {observation=new(error.Message,null,Environment.GetEnvironmentVariable(ProcessEnvironmentRemovalCases.Name)==ProcessEnvironmentRemovalCases.Ambient,before==JsonSerializer.Serialize(request),0,Directory.Exists(request.ReceiptDirectory));}
            ProcessEnvironmentRemovalCases.AssertContract(name,observation);
            if(name=="default-preserves")
            {Assert.DoesNotContain("RemovedEnvironmentKeys",before);Assert.DoesNotContain("RemovedEnvironmentNameRules",before);}
        }
        finally{Environment.SetEnvironmentVariable(ProcessEnvironmentRemovalCases.Name,previous);}
    }
}
