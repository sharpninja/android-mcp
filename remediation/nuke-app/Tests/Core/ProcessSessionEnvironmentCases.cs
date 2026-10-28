using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

internal static class ProcessSessionEnvironmentCases
{
    internal const string Name="ANDROIDMCP_DUMMY_ENV",Ambient="test-owned ambient 雪",Public="test-owned public override";
    internal static async Task AssertContract(bool clear,IProcessSessionFactory factory,ProcessRequest original)
    {
        var previous=Environment.GetEnvironmentVariable(Name);var argv=original.Arguments.ToArray();
        Environment.SetEnvironmentVariable(Name,Ambient);
        try
        {
            for(var phase=0;phase<2;phase++)
            {
                var values=phase==0?new Dictionary<string,string>():new Dictionary<string,string>{{Name,Public}};
                var request=original with{ClearEnvironment=clear,PublicEnvironment=values,ReceiptDirectory=Path.Combine(original.ReceiptDirectory,phase.ToString(System.Globalization.CultureInfo.InvariantCulture))};
                await using var session=await factory.StartAsync(request);var result=await session.Completion;Assert.Equal(0,result.ExitCode);Assert.False(result.Cancelled);
                var actual=JsonSerializer.Deserialize<Dictionary<string,string?>>(await File.ReadAllBytesAsync(result.StandardOutputPath))!;
                Assert.Single(actual);Assert.True(actual.ContainsKey(Name));Assert.Equal(phase==1?Public:clear?null:Ambient,actual[Name]);
                Assert.Empty(await File.ReadAllBytesAsync(result.StandardErrorPath));Assert.Equal(Ambient,Environment.GetEnvironmentVariable(Name));Assert.Equal(argv,request.Arguments);
                Assert.Equal(phase==0?Array.Empty<KeyValuePair<string,string>>():new[]{new KeyValuePair<string,string>(Name,Public)},request.PublicEnvironment);Assert.Equal(clear,request.ClearEnvironment);
            }
        }
        finally{Environment.SetEnvironmentVariable(Name,previous);}
    }
}
internal sealed class ScriptedEnvironmentFactory(string? mutation=null):IProcessSessionFactory
{
    public async Task<IProcessSession> StartAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        string? value=request.ClearEnvironment?null:ProcessSessionEnvironmentCases.Ambient;
        if(request.PublicEnvironment.TryGetValue(ProcessSessionEnvironmentCases.Name,out var supplied))value=mutation=="override"?value:supplied;
        else if(mutation=="inherit")value=request.ClearEnvironment?ProcessSessionEnvironmentCases.Ambient:null;
        Directory.CreateDirectory(request.ReceiptDirectory);var stdout=Path.Combine(request.ReceiptDirectory,"stdout.bin");var stderr=Path.Combine(request.ReceiptDirectory,"stderr.bin");
        await File.WriteAllBytesAsync(stdout,JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,string?>{{ProcessSessionEnvironmentCases.Name,value}}),cancellationToken);await File.WriteAllBytesAsync(stderr,[],cancellationToken);
        return new ScriptedEnvironmentSession(new(0,stdout,stderr,DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch,false));
    }
    private sealed class ScriptedEnvironmentSession(ProcessResult result):IProcessSession
    {
        public int ProcessId=>42;public string StandardOutputPath=>result.StandardOutputPath;public string StandardErrorPath=>result.StandardErrorPath;public Task<ProcessResult> Completion{get;}=Task.FromResult(result);
        public Task StopAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
}
