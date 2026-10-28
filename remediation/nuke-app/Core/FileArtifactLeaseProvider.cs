using System.Collections.Concurrent;
using System.Text;
namespace AndroidMcp.BuildTool.Core;

/// <summary>Cooperating in-process and cross-process generation leases; no lease-file deletion.</summary>
public sealed class FileArtifactLeaseProvider(string leaseRoot):IArtifactLeaseProvider
{
    private static readonly ConcurrentDictionary<string,SemaphoreSlim> Gates=new(StringComparer.Ordinal);
    public async ValueTask<IAsyncDisposable> AcquireAsync(string scope,CancellationToken cancellationToken=default)
    {
        if(string.IsNullOrWhiteSpace(scope))throw new ArgumentException("Lease scope is required.",nameof(scope));
        if(!OperatingSystem.IsLinux() && !OperatingSystem.IsWindows())throw new PlatformNotSupportedException("File leases are verified on Linux/Windows only.");
        var relativeLeasePath=ContentHash.Sha256(Encoding.UTF8.GetBytes(scope))+".lock";
        var path=ArtifactPaths.Resolve(leaseRoot,relativeLeasePath);
        var gate=Gates.GetOrAdd(path,_=>new SemaphoreSlim(1,1));
        await gate.WaitAsync(cancellationToken);
        FileStream? stream=null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            path=ArtifactPaths.Resolve(leaseRoot,relativeLeasePath);
            stream=new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite);
            while(true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try {
                    // Keep the OS guard at the call after await for platform analysis.
                    if(OperatingSystem.IsLinux()||OperatingSystem.IsWindows())stream.Lock(0,1);
                    else throw new PlatformNotSupportedException("Native file locking is unavailable.");
                    break;
                }
                catch(IOException error) when((error.HResult & 0xffff) is 11 or 13 or 32 or 33)
                { await Task.Delay(50,cancellationToken); }
            }
            return new Lease(stream,gate);
        }
        catch { stream?.Dispose();gate.Release();throw; }
    }
    private sealed class Lease(FileStream stream,SemaphoreSlim gate):IAsyncDisposable
    {
        private int released;
        public ValueTask DisposeAsync()
        {
            if(Interlocked.Exchange(ref released,1)==0)
            {
                try { if(OperatingSystem.IsLinux()||OperatingSystem.IsWindows())stream.Unlock(0,1); }
                finally {stream.Dispose();gate.Release();}
            }
            return ValueTask.CompletedTask;
        }
    }
}
