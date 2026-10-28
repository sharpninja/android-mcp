using System.Diagnostics;
namespace AndroidMcp.BuildTool.Core;

/// <summary>Binary stream capture is intentionally below NUKE's line-oriented logger.</summary>
public sealed class NativeProcessRunner(IClock clock) : IProcessRunner
{
    private static readonly HashSet<string> Forbidden=new(StringComparer.OrdinalIgnoreCase)
        {"python","python3","py","pypy","node","npm","npx","bash","sh","zsh","fish","cmd","powershell","pwsh","mise"};
    public async Task<ProcessResult> RunAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        var executable=Path.GetFullPath(request.Tool.ExecutablePath);
        var name=Path.GetFileNameWithoutExtension(executable);
        if(Forbidden.Contains(name) || name.StartsWith("python",StringComparison.OrdinalIgnoreCase) ||
            new[]{".py",".js",".cjs",".mjs",".sh",".ps1",".cmd",".bat"}.Contains(Path.GetExtension(executable),StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Script/interpreter/provisioning launch is prohibited.");
        if(!File.Exists(executable) || ContentHash.Sha256(await File.ReadAllBytesAsync(executable))!=request.Tool.Sha256)
            throw new InvalidOperationException("Native tool content pin does not match.");
        if(!Directory.Exists(request.WorkingDirectory))throw new InvalidOperationException("Working directory does not exist.");
        if(Directory.Exists(request.ReceiptDirectory) && Directory.EnumerateFileSystemEntries(request.ReceiptDirectory).Any())
            throw new InvalidOperationException("Process receipt directory must be new or empty.");
        if(request.StandardInputPath is not null && !File.Exists(request.StandardInputPath))throw new InvalidOperationException("Standard input artifact is absent.");
        var start=new ProcessStartInfo(executable) {
            WorkingDirectory=request.WorkingDirectory,UseShellExecute=false,
            RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=request.StandardInputPath is not null,
            CreateNoWindow=true
        };
        foreach(var argument in request.Arguments)start.ArgumentList.Add(argument);
        foreach(var item in request.PublicEnvironment)
        {
            var upper=item.Key.ToUpperInvariant();
            if((upper.Contains("PASSWORD")||upper.Contains("SECRET")||upper.Contains("API_KEY")||upper.Contains("AUTHORIZATION")||upper.Contains("TOKEN")) && upper!="CLAUDE_CODE_MAX_OUTPUT_TOKENS")
                throw new InvalidOperationException("Credential values cannot be supplied as public environment metadata.");
            start.Environment[item.Key]=item.Value;
        }
        Directory.CreateDirectory(request.ReceiptDirectory);
        var stdout=Path.Combine(request.ReceiptDirectory,"stdout.bin");
        var stderr=Path.Combine(request.ReceiptDirectory,"stderr.bin");
        var began=clock.Now;
        await using var output=new FileStream(stdout,FileMode.CreateNew,FileAccess.Write,FileShare.Read,81920,true);
        await using var error=new FileStream(stderr,FileMode.CreateNew,FileAccess.Write,FileShare.Read,81920,true);
        if(cancellationToken.IsCancellationRequested)return new(-1,stdout,stderr,began,clock.Now,true);
        using var process=new Process {StartInfo=start};
        process.Start();
        var copyOutput=process.StandardOutput.BaseStream.CopyToAsync(output);
        var copyError=process.StandardError.BaseStream.CopyToAsync(error);
        using var cancellation=cancellationToken.Register(()=> {
            try { if(!process.HasExited)process.Kill(true); } catch(InvalidOperationException) { }
        });
        var input=CopyInput();
        await process.WaitForExitAsync(); // no assistant timeout or output/turn limit
        try { await input; } catch(IOException) when(process.HasExited) { }
        await Task.WhenAll(copyOutput,copyError);
        await output.FlushAsync();await error.FlushAsync();
        return new(cancellationToken.IsCancellationRequested?-1:process.ExitCode,stdout,stderr,began,clock.Now,cancellationToken.IsCancellationRequested);

        async Task CopyInput()
        {
            if(request.StandardInputPath is null)return;
            await using var source=File.OpenRead(request.StandardInputPath);
            await source.CopyToAsync(process.StandardInput.BaseStream);
            await process.StandardInput.BaseStream.FlushAsync();
            process.StandardInput.Close();
        }
    }
}
public sealed class SystemClock : IClock { public DateTimeOffset Now=>DateTimeOffset.Now; }

public sealed class ProcessCaptureReader(string receiptRoot) : IProcessCaptureReader
{
    public async Task<ReadOnlyMemory<byte>> ReadAsync(ProcessResult result,bool standardError,CancellationToken cancellationToken=default)
    {
        var path=Path.GetFullPath(standardError?result.StandardErrorPath:result.StandardOutputPath);
        var root=Path.GetFullPath(receiptRoot)+Path.DirectorySeparatorChar;
        if(!path.StartsWith(root,StringComparison.Ordinal) || Path.GetFileName(path)!=(standardError?"stderr.bin":"stdout.bin"))
            throw new InvalidOperationException("Capture path is outside the selected receipt root.");
        return await File.ReadAllBytesAsync(path,cancellationToken);
    }
}
