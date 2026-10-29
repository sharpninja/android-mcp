using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public enum PrecommitHookScope { OldNull,Pass,Throw,Cancel,RequiredSourceChanged,WriteFail }
public sealed class PrecommitHookEnvironment
{
    public PrecommitHookScope Scope {get;}
    public List<string> Trace {get;}=[];
    public List<CancellationToken> HookTokens {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public IOException HookFailure {get;}=new("precommit fixture failure");
    public Artifact Input {get;}=new("source/input.txt",new byte[]{1},"text/plain");
    public Artifact Output {get;}=new("docs/output.txt",new byte[]{2},"text/plain");
    public Artifact Manifest {get;}=new("generation-manifest.json",new byte[]{3},"application/json");
    public IArtifactReceiptVerifier Receipts {get;}=new ArtifactReceiptVerifier(new NativeContentHash());
    public PrecommitSourceStore Source {get;}
    public PrecommitOutputStore Target {get;}
    public PrecommitLeaseProvider Leases {get;}
    public PrecommitManifestFactory Manifests {get;}
    public ArtifactGenerationRecipe Recipe {get;}
    public PrecommitHookEnvironment(PrecommitHookScope scope)
    {
        Scope=scope;Source=new(this);Target=new(this);Leases=new(this);Manifests=new(this);
        Recipe=new([new(Input.RelativePath,Input.MediaType)],inputs=>
        {
            Trace.Add("prepare");Assert.Single(inputs);Assert.Equal(Input.Bytes.ToArray(),inputs[0].Bytes.ToArray());
            return new([Output.RelativePath],token=>
            {Trace.Add("materialize");Assert.Equal(Cancellation.Token,token);return Task.FromResult<IReadOnlyList<Artifact>>([Output]);},scope==PrecommitHookScope.OldNull?null:HookAsync);
        });
    }
    public Task HookAsync(CancellationToken token)
    {
        Trace.Add("hook");HookTokens.Add(token);
        if(Scope==PrecommitHookScope.Throw)throw HookFailure;
        if(Scope==PrecommitHookScope.Cancel){Cancellation.Cancel();token.ThrowIfCancellationRequested();}
        return Task.CompletedTask;
    }
}
public sealed class PrecommitSourceStore(PrecommitHookEnvironment e):IArtifactStore
{
    private int reads;
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
    {cancellationToken.ThrowIfCancellationRequested();Assert.Equal(e.Input.RelativePath,path);e.Trace.Add(++reads==1?"source.initial":"source.recheck");return Task.FromResult<ReadOnlyMemory<byte>>(reads>1&&e.Scope==PrecommitHookScope.RequiredSourceChanged?new byte[]{9}:e.Input.Bytes);}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)=>throw new InvalidOperationException("Generation must not write its source store");
}
public sealed class PrecommitOutputStore(PrecommitHookEnvironment e):IArtifactStore
{
    public List<Artifact> Written {get;}=[];
    public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken cancellationToken=default)
    {cancellationToken.ThrowIfCancellationRequested();e.Trace.Add("probe:"+path);return Task.FromException<ReadOnlyMemory<byte>>(new FileNotFoundException(path));}
    public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();e.Trace.Add(artifact.RelativePath==e.Manifest.RelativePath?"manifest.write":"artifact.write");
        if(e.Scope==PrecommitHookScope.WriteFail&&artifact.RelativePath==e.Output.RelativePath)throw new IOException("fixture write failure");
        Written.Add(artifact);return Task.FromResult(e.Receipts.Describe(artifact));
    }
}
public sealed class PrecommitLeaseProvider(PrecommitHookEnvironment e):IArtifactLeaseProvider
{
    public ValueTask<IAsyncDisposable> AcquireAsync(string scope,CancellationToken cancellationToken=default)
    {Assert.Equal("fixture-root",scope);Assert.Equal(e.Cancellation.Token,cancellationToken);e.Trace.Add("lease.acquire");return ValueTask.FromResult<IAsyncDisposable>(new Lease(e));}
    private sealed class Lease(PrecommitHookEnvironment e):IAsyncDisposable
    {public ValueTask DisposeAsync(){e.Trace.Add("lease.dispose");return ValueTask.CompletedTask;}}
}
public sealed class PrecommitManifestFactory(PrecommitHookEnvironment e):IArtifactGenerationManifestFactory
{
    public string RelativePath=>e.Manifest.RelativePath;
    public Artifact Create(string id,IReadOnlyList<StoredArtifact> inputs,IReadOnlyList<StoredArtifact> outputs)
    {e.Trace.Add("manifest.create");Assert.Equal("fixture",id);Assert.Equal(new[]{e.Receipts.Describe(e.Input)},inputs);Assert.Equal(new[]{e.Receipts.Describe(e.Output)},outputs);return e.Manifest;}
}
public static class ArtifactGenerationPrecommitHookCases
{
    public static IEnumerable<object[]> Data=>Enum.GetValues<PrecommitHookScope>().Select(scope=>new object[]{scope});
    public static async Task AssertContract(PrecommitHookEnvironment e,IArtifactGenerationRuntime provider)
    {
        var input=e.Input.Bytes.ToArray();var output=e.Output.Bytes.ToArray();ArtifactGenerationResult? result=null;
        var error=await Record.ExceptionAsync(async()=>result=await provider.ExecuteAsync("fixture",e.Recipe,e.Cancellation.Token));
        switch(e.Scope)
        {
            case PrecommitHookScope.OldNull:case PrecommitHookScope.Pass:
                Assert.Null(error);Assert.NotNull(result);Assert.Equal("fixture",result.GenerationId);Assert.Equal(new[]{e.Receipts.Describe(e.Input)},result.Inputs);Assert.Equal(new[]{e.Receipts.Describe(e.Output)},result.Outputs);Assert.Equal(e.Receipts.Describe(e.Manifest),result.Manifest);break;
            case PrecommitHookScope.Throw:
                var failure=Assert.IsType<ArtifactGenerationException>(error);Assert.Equal("PRECOMMIT_VALIDATION_FAILED",failure.Code);Assert.Same(e.HookFailure,failure.InnerException);Assert.Null(result);break;
            case PrecommitHookScope.Cancel:Assert.IsAssignableFrom<OperationCanceledException>(error);Assert.Null(result);break;
            default:Assert.Equal(e.Scope==PrecommitHookScope.RequiredSourceChanged?"INPUT_CHANGED":"WRITE_FAILED",Assert.IsType<ArtifactGenerationException>(error).Code);Assert.Null(result);break;
        }
        var expected=new List<string>{"lease.acquire","source.initial","prepare","probe:"+e.Output.RelativePath,"probe:"+e.Manifest.RelativePath,"materialize","artifact.write"};
        if(e.Scope!=PrecommitHookScope.WriteFail)expected.Add("source.recheck");
        if(e.Scope is PrecommitHookScope.Pass or PrecommitHookScope.Throw or PrecommitHookScope.Cancel)expected.Add("hook");
        if(e.Scope is PrecommitHookScope.OldNull or PrecommitHookScope.Pass){expected.Add("manifest.create");expected.Add("manifest.write");}
        expected.Add("lease.dispose");Assert.Equal(expected,e.Trace);
        Assert.Equal(e.Scope is PrecommitHookScope.Pass or PrecommitHookScope.Throw or PrecommitHookScope.Cancel?1:0,e.HookTokens.Count);
        Assert.All(e.HookTokens,token=>Assert.Equal(e.Cancellation.Token,token));
        Assert.Equal(e.Scope is PrecommitHookScope.OldNull or PrecommitHookScope.Pass,e.Target.Written.Any(artifact=>artifact.RelativePath==e.Manifest.RelativePath));
        Assert.Equal(e.Scope==PrecommitHookScope.WriteFail?0:1,e.Target.Written.Count(artifact=>artifact.RelativePath==e.Output.RelativePath));
        Assert.Equal(input,e.Input.Bytes.ToArray());Assert.Equal(output,e.Output.Bytes.ToArray());
    }
}
public sealed class ScriptedPrecommitGenerationRuntime(PrecommitHookEnvironment e,string mutation=""):IArtifactGenerationRuntime
{
    public async Task<ArtifactGenerationResult> ExecuteAsync(string id,ArtifactGenerationRecipe recipe,CancellationToken cancellationToken=default)
    {
        if(mutation=="outcome")return new("wrong",[],[],new("wrong","wrong",0,"wrong"));
        await using var lease=await e.Leases.AcquireAsync("fixture-root",cancellationToken);
        var input=new Artifact(e.Input.RelativePath,await e.Source.ReadAsync(e.Input.RelativePath,cancellationToken),e.Input.MediaType);
        var preparation=recipe.Prepare([input]);
        foreach(var path in preparation.OutputPaths.Append(e.Manifests.RelativePath).Order(StringComparer.Ordinal))
        {try{await e.Target.ReadAsync(path,cancellationToken);}catch(FileNotFoundException){}}
        var artifacts=await preparation.MaterializeAsync(cancellationToken);
        try{foreach(var artifact in artifacts)await e.Target.WriteAsync(artifact,cancellationToken);}
        catch(IOException error){if(mutation=="write-fail-hook")await e.HookAsync(cancellationToken);throw new ArtifactGenerationException("WRITE_FAILED","fixture",error);}
        if(mutation=="early-hook"&&preparation.VerifyBeforeCommitAsync is {} early)await early(cancellationToken);
        var current=await e.Source.ReadAsync(e.Input.RelativePath,cancellationToken);
        if(!current.Span.SequenceEqual(input.Bytes.Span))
        {if(mutation=="changed-source-hook")await e.HookAsync(cancellationToken);throw new ArtifactGenerationException("INPUT_CHANGED","fixture");}
        if(mutation=="early-manifest")e.Manifests.Create(id,[e.Receipts.Describe(input)],artifacts.Select(e.Receipts.Describe).ToArray());
        if(preparation.VerifyBeforeCommitAsync is {} hook&&mutation!="omitted-hook")
        {
            try{await hook(mutation=="hook-token"?CancellationToken.None:cancellationToken);if(mutation=="hook-twice")await hook(cancellationToken);}
            catch(OperationCanceledException){throw;}
            catch(Exception error){if(mutation!="failure-commits")throw new ArtifactGenerationException("PRECOMMIT_VALIDATION_FAILED","fixture",error);}
        }
        var inputs=new[]{e.Receipts.Describe(input)};var outputs=artifacts.Select(e.Receipts.Describe).ToArray();
        var manifest=e.Manifests.Create(id,inputs,outputs);var commit=await e.Target.WriteAsync(manifest,cancellationToken);return new(id,inputs,outputs,commit);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArtifactGenerationPrecommitHookMockTests
{
    [Theory,MemberData(nameof(ArtifactGenerationPrecommitHookCases.Data),MemberType=typeof(ArtifactGenerationPrecommitHookCases))]
    public Task CorrectSixScopeMocksPass(PrecommitHookScope scope){var e=new PrecommitHookEnvironment(scope);return ArtifactGenerationPrecommitHookCases.AssertContract(e,new ScriptedPrecommitGenerationRuntime(e));}
    [Theory,MemberData(nameof(ArtifactGenerationPrecommitHookCases.Data),MemberType=typeof(ArtifactGenerationPrecommitHookCases))]
    public async Task EveryWrongScopeOutcomeIsRejected(PrecommitHookScope scope){var e=new PrecommitHookEnvironment(scope);Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactGenerationPrecommitHookCases.AssertContract(e,new ScriptedPrecommitGenerationRuntime(e,"outcome"))));}
    [Theory]
    [InlineData(PrecommitHookScope.Pass,"early-hook")][InlineData(PrecommitHookScope.Pass,"early-manifest")][InlineData(PrecommitHookScope.Pass,"hook-token")][InlineData(PrecommitHookScope.Pass,"hook-twice")][InlineData(PrecommitHookScope.Pass,"omitted-hook")]
    [InlineData(PrecommitHookScope.Throw,"failure-commits")][InlineData(PrecommitHookScope.RequiredSourceChanged,"changed-source-hook")][InlineData(PrecommitHookScope.WriteFail,"write-fail-hook")]
    public async Task EachOrderingTokenCountAndCommitBypassIsRejected(PrecommitHookScope scope,string mutation){var e=new PrecommitHookEnvironment(scope);Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactGenerationPrecommitHookCases.AssertContract(e,new ScriptedPrecommitGenerationRuntime(e,mutation))));}
}
