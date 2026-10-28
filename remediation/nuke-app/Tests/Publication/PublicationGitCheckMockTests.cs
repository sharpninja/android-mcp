using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

public sealed record GitCheckCase(string Id,string? Error=null)
{ public override string ToString()=>Id; }

public static class PublicationGitCheckCases
{
    public static IEnumerable<object[]> Cases=>new GitCheckCase[]
    {
        new("ACCEPT"),new("EMPTY"),new("UNICODE-SPACES"),new("DUPLICATE"),new("MISSING"),new("DIRECTORY"),new("LINK-METADATA"),new("LARGE"),new("CHECKER-VERDICT"),
        new("RELATIVE-ROOT","PUBLICATION_REQUEST"),new("UNSAFE-POLICY","PUBLICATION_REQUEST"),
        new("POLICY-READ","PUBLICATION_POLICY"),new("INVALID-POLICY","PUBLICATION_POLICY"),new("GIT-EXIT","PUBLICATION_GIT"),
        new("CAPTURE-READ","PUBLICATION_CAPTURE"),new("INVALID-UTF8","PUBLICATION_CAPTURE"),new("MISSING-NUL","PUBLICATION_CAPTURE"),new("EMPTY-NAME","PUBLICATION_CAPTURE"),
        new("UNSAFE-TRACKED","PUBLICATION_PATH"),new("CONTROL-NAME","PUBLICATION_PATH"),new("OBSERVATION-READ","PUBLICATION_SNAPSHOT"),new("OBSERVATION-MISMATCH","PUBLICATION_SNAPSHOT"),
        new("CANCEL-EARLY","CANCEL"),new("CANCEL-PROCESS","CANCEL")
    }.Select(c=>new object[]{c});
    public static byte[] Policy=>JsonSerializer.SerializeToUtf8Bytes(new{maximum_file_bytes=10,allow=new[]{"*.md"},deny=Array.Empty<string>()});
    public static PublicationGitCheckRequest Request(GitCheckCase c)=>new(
        c.Id=="RELATIVE-ROOT"?"relative-root":Path.Combine(Path.GetTempPath(),"publication git fixture"),
        new(Path.Combine(Path.GetTempPath(),"pinned git fixture"),new string('1',64),"git version fixture"),
        Path.Combine(Path.GetTempPath(),"publication capture fixture"),c.Id=="UNSAFE-POLICY"?"../outside.json":"deliverables.json");
    public static string[] Paths(GitCheckCase c)=>c.Id switch
    {
        "EMPTY"=>[],"UNICODE-SPACES"=>["space name.md","雪.md"],"DUPLICATE"=>["a.md","a.md"],
        "UNSAFE-TRACKED"=>["../escape.md"],"CONTROL-NAME"=>["line\nbreak.md"],_=>["a.md","b.md"]
    };
    public static byte[] Stdout(GitCheckCase c)=>c.Id switch
    {"INVALID-UTF8"=>[255,0],"MISSING-NUL"=>Encoding.UTF8.GetBytes("a.md"),"EMPTY-NAME"=>Encoding.UTF8.GetBytes("a.md\0\0"),_=>Encoding.UTF8.GetBytes(string.Concat(Paths(c).Select(x=>x+"\0")))};
    public static PublicationFile[] Files(GitCheckCase c)=>Paths(c).Select((p,i)=>new PublicationFile(p,
        i==0 && c.Id=="LARGE"?11:i==0 && c.Id is "MISSING" or "DIRECTORY" or "LINK-METADATA"?0:i==0?3:5,
        i==0?c.Id switch{"MISSING"=>PublicationFileKind.Missing,"DIRECTORY"=>PublicationFileKind.Directory,"LINK-METADATA"=>PublicationFileKind.SymbolicLink,_=>PublicationFileKind.Regular}:PublicationFileKind.Regular)).ToArray();
    public static PublicationCheckResult Verdict(GitCheckCase c)=>new(Files(c).Length,Files(c).Sum(x=>x.Length),c.Id switch
    {"DUPLICATE"=>[new("DUPLICATE_PATH","a.md")],"MISSING" or "DIRECTORY" or "LINK-METADATA"=>[new("NOT_REGULAR","a.md")],"LARGE"=>[new("TOO_LARGE","a.md")],"CHECKER-VERDICT"=>[new("FROM_CANONICAL_CHECKER","a.md")],_=>[]});
    public static ProcessResult Process(GitCheckCase c)=>new(c.Id=="GIT-EXIT"?7:0,
        Path.Combine(Request(c).ReceiptDirectory,"stdout.bin"),Path.Combine(Request(c).ReceiptDirectory,"stderr.bin"),DateTimeOffset.UnixEpoch,DateTimeOffset.UnixEpoch.AddSeconds(1),c.Id=="CANCEL-PROCESS");
    public static string[] Events(GitCheckCase c)=>c.Id switch
    {
        "RELATIVE-ROOT" or "UNSAFE-POLICY" or "CANCEL-EARLY"=>[],"POLICY-READ"=>["policy"],"INVALID-POLICY"=>["policy","check-empty"],
        "GIT-EXIT" or "CANCEL-PROCESS"=>["policy","check-empty","git"],
        "CAPTURE-READ" or "INVALID-UTF8" or "MISSING-NUL" or "EMPTY-NAME" or "UNSAFE-TRACKED" or "CONTROL-NAME"=>["policy","check-empty","git","capture"],
        "OBSERVATION-READ" or "OBSERVATION-MISMATCH"=>["policy","check-empty","git","capture","observe"],
        _=>["policy","check-empty","git","capture","observe","check-files"]
    };
    public static string[] GitArguments=>["-c","core.fsmonitor=false","-c","core.untrackedCache=false","ls-files","--cached","-z","--"];
    public static PublicationGitCheckResult Correct(GitCheckCase c)=>new(Verdict(c),Array.AsReadOnly(Files(c)),ContentHash.Sha256(Policy),ContentHash.Sha256(Stdout(c)),Process(c));

    public static async Task AssertCase(GitCheckCase c,Func<GitCheckHarness,IPublicationGitCheck> factory)
    {
        var harness=new GitCheckHarness(c);var provider=factory(harness);var request=Request(c);
        using var cancellation=new CancellationTokenSource();if(c.Id=="CANCEL-EARLY")cancellation.Cancel();
        PublicationGitCheckResult? result=null;
        var error=await Record.ExceptionAsync(async()=>result=await provider.CheckAsync(request,cancellation.Token));
        if(c.Error=="CANCEL")Assert.IsAssignableFrom<OperationCanceledException>(error);
        else if(c.Error is not null)
        {
            var failure=Assert.IsType<PublicationGitCheckException>(error);Assert.Equal(c.Error,failure.Code);
            Assert.Equal(Events(c).Contains("git")?Process(c):null,failure.GitProcess);
            if(c.Id=="INVALID-POLICY")Assert.Equal("INVALID_POLICY",Assert.Single(Assert.IsType<PublicationCheckResult>(failure.PolicyCheck).Issues).Code);
        }
        else
        {
            Assert.Null(error);var actual=Assert.IsType<PublicationGitCheckResult>(result);var expected=Correct(c);
            Assert.Equal(expected.Check.FileCount,actual.Check.FileCount);Assert.Equal(expected.Check.TotalBytes,actual.Check.TotalBytes);
            Assert.Equal(expected.Check.Issues,actual.Check.Issues);Assert.Equal(expected.Files,actual.Files);
            Assert.Equal(expected.PolicySha256,actual.PolicySha256);Assert.Equal(expected.TrackedPathsSha256,actual.TrackedPathsSha256);Assert.Equal(expected.GitProcess,actual.GitProcess);
        }
        Assert.Equal(Events(c),harness.Trace);
        Assert.Equal(harness.Trace.Count(x=>x=="git"),harness.Runs.Count);
        foreach(var run in harness.Runs)
        {
            Assert.Equal(request.Git,run.Tool);Assert.Equal(GitArguments,run.Arguments);Assert.Equal(Path.GetFullPath(request.CheckoutRoot),run.WorkingDirectory);
            Assert.Equal(request.ReceiptDirectory,run.ReceiptDirectory);Assert.Null(run.StandardInputPath);Assert.True(run.ClearEnvironment);
            Assert.Equal(new[]{"GIT_OPTIONAL_LOCKS","GIT_TERMINAL_PROMPT"},run.PublicEnvironment.Keys.Order(StringComparer.Ordinal));
            Assert.Equal("0",run.PublicEnvironment["GIT_OPTIONAL_LOCKS"]);Assert.Equal("0",run.PublicEnvironment["GIT_TERMINAL_PROMPT"]);
        }
        Assert.Equal(harness.Trace.Count(x=>x=="policy"),harness.PolicyReads.Count);
        foreach(var read in harness.PolicyReads){Assert.Equal(request.CheckoutRoot,read.Root);Assert.Equal(request.PolicyRelativePath,read.Path);}
        Assert.Equal(harness.Trace.Count(x=>x=="capture"),harness.Captures.Count);
        foreach(var read in harness.Captures){Assert.Equal(Process(c),read.Process);Assert.False(read.Stderr);}
        Assert.Equal(harness.Trace.Count(x=>x=="observe"),harness.Observations.Count);
        foreach(var read in harness.Observations){Assert.Equal(request.CheckoutRoot,read.Root);Assert.Equal(Paths(c),read.Paths);}
        Assert.Equal(harness.Trace.Count(x=>x is "check-empty" or "check-files"),harness.Checks.Count);
        for(var i=0;i<harness.Checks.Count;i++){Assert.Equal(Policy,harness.Checks[i].Policy);Assert.Equal(i==0?Array.Empty<PublicationFile>():Files(c),harness.Checks[i].Files);}
    }
}

// Shared dependency fixtures will be used unchanged by the real orchestration.
public sealed class GitCheckHarness(GitCheckCase scenario):IProcessRunner,IProcessCaptureReader,IPublicationWorkspaceReader,IPublicationChecker
{
    public GitCheckCase Scenario {get;}=scenario;
    public List<string> Trace {get;}=[];
    public List<ProcessRequest> Runs {get;}=[];
    public List<(string Root,string Path)> PolicyReads {get;}=[];
    public List<(string Root,string[] Paths)> Observations {get;}=[];
    public List<(ProcessResult Process,bool Stderr)> Captures {get;}=[];
    public List<(byte[] Policy,PublicationFile[] Files)> Checks {get;}=[];
    public Task<ReadOnlyMemory<byte>> ReadPolicyAsync(string root,string path,CancellationToken cancellationToken=default)
    {Trace.Add("policy");PolicyReads.Add((root,path));if(Scenario.Id=="POLICY-READ")throw new IOException("Fixture policy unavailable");return Task.FromResult<ReadOnlyMemory<byte>>(PublicationGitCheckCases.Policy);}
    public Task<IReadOnlyList<PublicationFile>> ObserveAsync(string root,IReadOnlyList<string> paths,CancellationToken cancellationToken=default)
    {Trace.Add("observe");Observations.Add((root,paths.ToArray()));if(Scenario.Id=="OBSERVATION-READ")throw new IOException("Fixture metadata unavailable");IReadOnlyList<PublicationFile> files=Scenario.Id=="OBSERVATION-MISMATCH"?[new("wrong.md",0)]:PublicationGitCheckCases.Files(Scenario);return Task.FromResult(files);}
    public Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {Trace.Add("git");Runs.Add(request);return Task.FromResult(PublicationGitCheckCases.Process(Scenario));}
    public Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {Trace.Add("capture");Captures.Add((result,standardError));if(Scenario.Id=="CAPTURE-READ")throw new IOException("Fixture capture unavailable");return Task.FromResult<ReadOnlyMemory<byte>>(PublicationGitCheckCases.Stdout(Scenario));}
    public PublicationCheckResult Check(ReadOnlyMemory<byte> policy,IReadOnlyList<PublicationFile> files)
    {var first=Checks.Count==0;Trace.Add(first?"check-empty":"check-files");Checks.Add((policy.ToArray(),files.ToArray()));return first?new(0,0,Scenario.Id=="INVALID-POLICY"?[new("INVALID_POLICY","")]:[]):PublicationGitCheckCases.Verdict(Scenario);}
}

// Canned protocol transcript proves the assertion harness before production.
public sealed class ScriptedPublicationGitCheck(GitCheckCase c,GitCheckHarness harness,string? mutation=null):IPublicationGitCheck
{
    public Task<PublicationGitCheckResult> CheckAsync(PublicationGitCheckRequest request,CancellationToken cancellationToken=default)
    {
        harness.Trace.AddRange(PublicationGitCheckCases.Events(c));
        if(harness.Trace.Contains("policy"))harness.PolicyReads.Add((request.CheckoutRoot,request.PolicyRelativePath));
        if(harness.Trace.Contains("check-empty"))harness.Checks.Add((PublicationGitCheckCases.Policy,[]));
        if(harness.Trace.Contains("git"))harness.Runs.Add(new(request.Git,PublicationGitCheckCases.GitArguments,Path.GetFullPath(request.CheckoutRoot),new Dictionary<string,string>{{"GIT_OPTIONAL_LOCKS","0"},{"GIT_TERMINAL_PROMPT","0"}},request.ReceiptDirectory,ClearEnvironment:true));
        if(harness.Trace.Contains("capture"))harness.Captures.Add((PublicationGitCheckCases.Process(c),false));
        if(harness.Trace.Contains("observe"))harness.Observations.Add((request.CheckoutRoot,PublicationGitCheckCases.Paths(c)));
        if(harness.Trace.Contains("check-files"))harness.Checks.Add((PublicationGitCheckCases.Policy,PublicationGitCheckCases.Files(c)));
        if(mutation=="trace")harness.Trace.Add("git-write");
        if(mutation=="argv" && harness.Runs.Count>0)harness.Runs[0]=harness.Runs[0] with{Arguments=["push"]};
        if(mutation=="environment" && harness.Runs.Count>0)harness.Runs[0]=harness.Runs[0] with{ClearEnvironment=false};
        if(mutation=="cwd" && harness.Runs.Count>0)harness.Runs[0]=harness.Runs[0] with{WorkingDirectory=Path.GetTempPath()};
        if(mutation=="capture" && harness.Captures.Count>0)harness.Captures[0]=(harness.Captures[0].Process,true);
        if(mutation=="observation" && harness.Observations.Count>0)harness.Observations[0]=(request.CheckoutRoot,["wrong.md"]);
        if(mutation=="policy" && harness.Checks.Count>0)harness.Checks[0]=([1],[]);
        if(c.Error=="CANCEL"){if(mutation=="error")throw new InvalidOperationException("Wrong cancellation");throw new OperationCanceledException(cancellationToken);}
        if(c.Error is not null)throw new PublicationGitCheckException(mutation=="error"?"WRONG":c.Error,"Fixture failure",harness.Trace.Contains("git")?PublicationGitCheckCases.Process(c):null,c.Id=="INVALID-POLICY"?new(0,0,[new("INVALID_POLICY","")]):null);
        var result=PublicationGitCheckCases.Correct(c);
        result=mutation switch
        {"count"=>result with{Check=result.Check with{FileCount=99}},"bytes"=>result with{Check=result.Check with{TotalBytes=99}},"issues"=>result with{Check=result.Check with{Issues=[new("WRONG","")]}},"files"=>result with{Files=[new("wrong.md",0)]},"policy-hash"=>result with{PolicySha256="wrong"},"paths-hash"=>result with{TrackedPathsSha256="wrong"},"process"=>result with{GitProcess=result.GitProcess with{ExitCode=99}},_=>result};
        return Task.FromResult(result);
    }
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-007")]
public sealed class PublicationGitCheckMockTests
{
    [Theory,MemberData(nameof(PublicationGitCheckCases.Cases),MemberType=typeof(PublicationGitCheckCases))]
    public Task CorrectReadOnlyMocksPass(GitCheckCase c)=>PublicationGitCheckCases.AssertCase(c,h=>new ScriptedPublicationGitCheck(c,h));
    [Theory,MemberData(nameof(PublicationGitCheckCases.Cases),MemberType=typeof(PublicationGitCheckCases))]
    public async Task IndividualWrongResultsOrInteractionsAreRejected(GitCheckCase c)
    {
        var mutations=new List<string>{"trace"};var events=PublicationGitCheckCases.Events(c);
        if(c.Error is not null)mutations.Add("error");else mutations.AddRange(["count","bytes","issues","files","policy-hash","paths-hash","process"]);
        if(events.Contains("git"))mutations.AddRange(["argv","environment","cwd"]);
        if(events.Contains("capture"))mutations.Add("capture");if(events.Contains("observe"))mutations.Add("observation");if(events.Contains("check-empty"))mutations.Add("policy");
        foreach(var mutation in mutations)Assert.NotNull(await Record.ExceptionAsync(()=>PublicationGitCheckCases.AssertCase(c,h=>new ScriptedPublicationGitCheck(c,h,mutation))));
    }
}
