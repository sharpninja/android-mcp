using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003"),Trait("Requirement","TEST-BUILD-005")]
public sealed class ProcessSessionMockTests
{
    [Theory,MemberData(nameof(ProcessSessionCases.Cases),MemberType=typeof(ProcessSessionCases))]
    public async Task CorrectLifecycleMocksProveStartBeforeExitAndExplicitCleanup(string id)
    {
        var test=ProcessSessionCases.Get(id);var request=new ProcessRequest(new("/fixture/native","pin","fixture"),["space argument","雪;$literal"],"/fixture/work",new Dictionary<string,string>(),"/fixture/receipt");
        var factory=new ScriptedProcessSessionFactory(test);
        await ProcessSessionCases.AssertContract(test,factory,request,session=>{((ScriptedProcessSession)session).Complete(false);return Task.CompletedTask;});Assert.Same(request,factory.Received);
    }
    [Theory,MemberData(nameof(ProcessSessionCases.Cases),MemberType=typeof(ProcessSessionCases))]
    public async Task IndependentLifecycleWrongValuesAndEarlyCompletionAreRejected(string id)
    {
        var test=ProcessSessionCases.Get(id);var request=new ProcessRequest(new("/fixture/native","pin","fixture"),[],"/fixture/work",new Dictionary<string,string>(),"/fixture/receipt");
        foreach(var mutation in new[]{"pid","stdout","stderr","result-stdout","result-stderr","exit","cancel","time"}.Concat(test.Action=="before"?[]:new[]{"early"}))
            Assert.NotNull(await Record.ExceptionAsync(()=>ProcessSessionCases.AssertContract(test,new ScriptedProcessSessionFactory(test,mutation),request,session=>{((ScriptedProcessSession)session).Complete(false);return Task.CompletedTask;})));
    }
    [Theory,MemberData(nameof(ProcessAdmissionCases.Cases),MemberType=typeof(ProcessAdmissionCases))]
    public async Task CorrectAdmissionFailuresAndWrongSuccessSideEffectsAreDiscriminated(string id)
    {
        var test=ProcessAdmissionCases.Get(id);var root=Path.Combine(Path.GetTempPath(),"androidmcp-session-mocks",Guid.NewGuid().ToString("N"));
        var request=ProcessAdmissionCases.Request(test,root);
        await ProcessAdmissionCases.AssertContract(new AdmissionFactory(false),request);
        Assert.NotNull(await Record.ExceptionAsync(()=>ProcessAdmissionCases.AssertContract(new AdmissionFactory(true),request)));
        Assert.NotNull(await Record.ExceptionAsync(()=>ProcessAdmissionCases.AssertContract(new AdmissionFactory(false,true),request)));
    }
    private sealed class AdmissionFactory(bool succeed,bool effect=false):IProcessSessionFactory
    {
        public Task<IProcessSession> StartAsync(ProcessRequest request,CancellationToken cancellationToken=default)
        {
            if(effect){Directory.CreateDirectory(request.ReceiptDirectory);File.WriteAllText(Path.Combine(request.ReceiptDirectory,"forbidden.txt"),"unexpected effect");}
            if(!succeed)throw new InvalidOperationException("Script, unknown native image, hash or preflight denied before effects.");
            return Task.FromResult<IProcessSession>(new ScriptedProcessSession(request,new("wrong","before",-1,true),new CancellationToken(true)));
        }
    }
}
