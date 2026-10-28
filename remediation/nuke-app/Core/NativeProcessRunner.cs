namespace AndroidMcp.BuildTool.Core;

/// <summary>Convenience await-to-exit facade over the one native session engine.</summary>
public sealed class NativeProcessRunner(IClock clock):IProcessRunner,IProcessSessionFactory
{
    private readonly NativeProcessSessionFactory sessions=new(clock);
    public Task<IProcessSession> StartAsync(ProcessRequest request,CancellationToken cancellationToken=default)=>sessions.StartAsync(request,cancellationToken);
    public async Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {await using var session=await StartAsync(request,cancellationToken);return await session.Completion;}
}
public sealed class SystemClock:IClock {public DateTimeOffset Now=>DateTimeOffset.Now;}

public sealed class ProcessCaptureReader(string receiptRoot):IProcessCaptureReader
{
    public async Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {
        var path=Path.GetFullPath(standardError?result.StandardErrorPath:result.StandardOutputPath);
        var root=Path.GetFullPath(receiptRoot)+Path.DirectorySeparatorChar;
        if(!path.StartsWith(root,StringComparison.Ordinal)||Path.GetFileName(path)!=(standardError?"stderr.bin":"stdout.bin"))throw new InvalidOperationException("Capture path is outside the selected receipt root.");
        return await File.ReadAllBytesAsync(path,cancellationToken);
    }
}
