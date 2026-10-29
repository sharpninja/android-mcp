using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
public static class ArtifactLeaseCases
{
    // A bounded unit-test wait detects a broken lock. No review execution uses this timer.
    public static async Task AssertContract(IArtifactLeaseProvider provider)
    {
        await Assert.ThrowsAsync<ArgumentException>(async()=>await provider.AcquireAsync(""));
        var first=await provider.AcquireAsync("one");
        using var cancel=new CancellationTokenSource();
        var waiter=provider.AcquireAsync("one",cancel.Token).AsTask();
        Assert.False(waiter.IsCompleted);
        var other=await provider.AcquireAsync("two").AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>waiter);
        await other.DisposeAsync();
        await first.DisposeAsync();await first.DisposeAsync();
        await using var next=await provider.AcquireAsync("one").AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        using var cancelThird=new CancellationTokenSource();
        var third=provider.AcquireAsync("one",cancelThird.Token).AsTask();
        Assert.False(third.IsCompleted); // double disposal must not create extra permits
        cancelThird.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>third);
    }
}
public sealed class ScriptedLeaseProvider(bool broken=false):IArtifactLeaseProvider
{
    private readonly HashSet<string> held=[];
    public ValueTask<IAsyncDisposable> AcquireAsync(string scope,CancellationToken cancellationToken=default)
    {
        if(string.IsNullOrWhiteSpace(scope))throw new ArgumentException("Scope required.");
        if(cancellationToken.IsCancellationRequested)return ValueTask.FromCanceled<IAsyncDisposable>(cancellationToken);
        if(held.Contains(scope) && !broken)
        {
            var waiter=new TaskCompletionSource<IAsyncDisposable>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(()=>waiter.TrySetCanceled(cancellationToken));
            return new(waiter.Task);
        }
        held.Add(scope);
        return ValueTask.FromResult<IAsyncDisposable>(new Lease(()=>held.Remove(scope)));
    }
    private sealed class Lease(Action release):IAsyncDisposable
    {
        private bool disposed;
        public ValueTask DisposeAsync(){if(!disposed){disposed=true;release();}return ValueTask.CompletedTask;}
    }
}
[Trait("Gate","Mock")]
public sealed class ArtifactLeaseMockTests
{
    [Fact] public Task CorrectMock()=>ArtifactLeaseCases.AssertContract(new ScriptedLeaseProvider());
    [Fact] public async Task OverlappingLeaseRejected()=>Assert.NotNull(await Record.ExceptionAsync(()=>ArtifactLeaseCases.AssertContract(new ScriptedLeaseProvider(true))));
}
