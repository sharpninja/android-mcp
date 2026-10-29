using System.Buffers.Binary;
using System.Diagnostics;

namespace AndroidMcp.BuildTool.Core;

// The single native process engine. RunAsync and browser sessions share all
// admission, typed argv, stdin, cancellation and unbounded byte capture logic.
public sealed class NativeProcessSessionFactory(IClock clock):IProcessSessionFactory
{
    private static readonly HashSet<string> Forbidden=new(StringComparer.OrdinalIgnoreCase)
        {"python","python3","py","pypy","node","npm","npx","bash","sh","zsh","fish","cmd","powershell","pwsh","mise"};
    public async Task<IProcessSession> StartAsync(ProcessRequest request,CancellationToken cancellationToken=default)
    {
        // Admit the removal contract before any process/receipt effects. This uses only
        // explicit names and later the single ProcessStartInfo's existing name collection.
        var exact=request.RemovedEnvironmentKeys?.ToArray()??[];
        var suppliedRules=request.RemovedEnvironmentNameRules?.ToArray()??[];
        static bool ValidName(string? name)=>!string.IsNullOrWhiteSpace(name)&&name.IndexOfAny(['=','\0'])<0;
        var environmentComparer=OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
        if(exact.Any(name=>!ValidName(name))||exact.Distinct(environmentComparer).Count()!=exact.Length||
            suppliedRules.Any(rule=>rule is null||!ValidName(rule.Prefix)||rule.ContainsAny is null||rule.ContainsAny.Count==0||rule.ContainsAny.Any(token=>!ValidName(token))))
            throw new InvalidOperationException("ENVIRONMENT_REMOVAL_INVALID");
        var rules=suppliedRules.Select(rule=>new ProcessEnvironmentNameRule(rule.Prefix,rule.ContainsAny.ToArray())).ToArray();
        bool Selected(string name)=>exact.Contains(name,environmentComparer)||rules.Any(rule=>
            name.StartsWith(rule.Prefix,StringComparison.Ordinal)&&rule.ContainsAny.Any(token=>name.Contains(token,StringComparison.Ordinal)));
        if(request.PublicEnvironment.Keys.Any(Selected))throw new InvalidOperationException("ENVIRONMENT_REMOVAL_CONFLICT");
        var executable=Path.GetFullPath(request.Tool.ExecutablePath);var name=Path.GetFileNameWithoutExtension(executable);
        if(Forbidden.Contains(name)||name.StartsWith("python",StringComparison.OrdinalIgnoreCase)||
            new[]{".py",".js",".cjs",".mjs",".sh",".ps1",".cmd",".bat"}.Contains(Path.GetExtension(executable),StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Script/interpreter/provisioning launch is prohibited.");
        if(!File.Exists(executable))throw new InvalidOperationException("Native tool is absent.");
        using var binary=File.OpenRead(executable);ValidateNativeImage(binary);binary.Position=0;
        if(ContentHash.Sha256(binary)!=request.Tool.Sha256)throw new InvalidOperationException("Native tool content pin does not match.");
        if(!Directory.Exists(request.WorkingDirectory))throw new InvalidOperationException("Working directory does not exist.");
        if(Directory.Exists(request.ReceiptDirectory)&&Directory.EnumerateFileSystemEntries(request.ReceiptDirectory).Any())throw new InvalidOperationException("Process receipt directory must be new or empty.");
        if(request.StandardInputPath is not null&&!File.Exists(request.StandardInputPath))throw new InvalidOperationException("Standard input artifact is absent.");
        var start=new ProcessStartInfo(executable){WorkingDirectory=request.WorkingDirectory,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=request.StandardInputPath is not null,CreateNoWindow=true};
        if(request.ClearEnvironment)start.Environment.Clear();
        if(exact.Length>0||rules.Length>0)
            foreach(var key in start.Environment.Keys.Where(Selected).ToArray())start.Environment.Remove(key);
        foreach(var argument in request.Arguments)start.ArgumentList.Add(argument);
        foreach(var item in request.PublicEnvironment)
        {
            var upper=item.Key.ToUpperInvariant();
            if((upper.Contains("PASSWORD")||upper.Contains("SECRET")||upper.Contains("API_KEY")||upper.Contains("AUTHORIZATION")||upper.Contains("TOKEN"))&&upper!="CLAUDE_CODE_MAX_OUTPUT_TOKENS")
                throw new InvalidOperationException("Credential values cannot be supplied as public environment metadata.");
            start.Environment[item.Key]=item.Value;
        }
        Directory.CreateDirectory(request.ReceiptDirectory);var stdout=Path.Combine(request.ReceiptDirectory,"stdout.bin");var stderr=Path.Combine(request.ReceiptDirectory,"stderr.bin");var began=clock.Now;
        var output=new FileStream(stdout,FileMode.CreateNew,FileAccess.Write,FileShare.Read,81920,true);FileStream? error=null;Process? process=null;
        try
        {
            error=new FileStream(stderr,FileMode.CreateNew,FileAccess.Write,FileShare.Read,81920,true);
            if(cancellationToken.IsCancellationRequested){await output.DisposeAsync();await error.DisposeAsync();return new CompletedSession(new(-1,stdout,stderr,began,clock.Now,true));}
            process=new Process{StartInfo=start};if(!process.Start())throw new InvalidOperationException("Native process did not start.");
            return new RunningSession(process,output,error,stdout,stderr,began,clock,request.StandardInputPath,cancellationToken);
        }
        catch {process?.Dispose();await output.DisposeAsync();if(error is not null)await error.DisposeAsync();throw;}
    }
    private static void ValidateNativeImage(FileStream input)
    {
        Span<byte> header=stackalloc byte[64];var count=input.ReadAtLeast(header,64,false);
        if(count>=2&&header[0]==35&&header[1]==33)throw new InvalidOperationException("Shebang files are not native executable tools.");
        if(count>=52&&header[0]==0x7f&&header[1]==69&&header[2]==76&&header[3]==70&&header[4] is 1 or 2&&header[5] is 1 or 2&&header[6]==1)
        {if(header[4]==1||count>=64)return;}
        if(count>=64&&header[0]==77&&header[1]==90)
        {
            var offset=BinaryPrimitives.ReadInt32LittleEndian(header[60..64]);
            if(offset>=64&&offset<=input.Length-24)
            {input.Position=offset;Span<byte> signature=stackalloc byte[4];input.ReadExactly(signature);if(signature.SequenceEqual("PE\0\0"u8))return;}
        }
        throw new InvalidOperationException("Tool does not contain a complete recognized native ELF or PE header.");
    }
    private sealed class CompletedSession(ProcessResult result):IProcessSession
    {
        public int ProcessId=>0;public string StandardOutputPath=>result.StandardOutputPath;public string StandardErrorPath=>result.StandardErrorPath;
        public Task<ProcessResult> Completion {get;}=Task.FromResult(result);public Task StopAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class RunningSession:IProcessSession
    {
        private readonly Process process;private readonly FileStream output,error;private readonly IClock clock;private readonly DateTimeOffset began;
        private readonly Task<ProcessResult> completion;private CancellationTokenRegistration cancellation;private int stopRequested;private bool inputFailedWhileRunning;
        public int ProcessId {get;}public string StandardOutputPath {get;}public string StandardErrorPath {get;}public Task<ProcessResult> Completion=>completion;
        internal RunningSession(Process process,FileStream output,FileStream error,string stdout,string stderr,DateTimeOffset began,IClock clock,string? input,CancellationToken token)
        {
            this.process=process;this.output=output;this.error=error;this.began=began;this.clock=clock;ProcessId=process.Id;StandardOutputPath=stdout;StandardErrorPath=stderr;
            var copyOutput=process.StandardOutput.BaseStream.CopyToAsync(output);var copyError=process.StandardError.BaseStream.CopyToAsync(error);var copyInput=CopyInputAsync(input);
            ObserveCopyFailure(copyOutput,false);ObserveCopyFailure(copyError,false);ObserveCopyFailure(copyInput,true);
            completion=CompleteAsync(copyOutput,copyError,copyInput);cancellation=token.Register(RequestStop);if(completion.IsCompleted)cancellation.Dispose();
        }
        private async Task CopyInputAsync(string? path)
        {
            if(path is null)return;
            try {await using var source=File.OpenRead(path);await source.CopyToAsync(process.StandardInput.BaseStream);await process.StandardInput.BaseStream.FlushAsync();}
            finally {process.StandardInput.Close();}
        }
        private void ObserveCopyFailure(Task task,bool input)
        {
            _=task.ContinueWith(failed=>
            {
                _=failed.Exception;
                try {if(!process.HasExited){if(input)inputFailedWhileRunning=true;process.Kill(true);}}catch(InvalidOperationException){}
            },CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        }
        private async Task<ProcessResult> CompleteAsync(Task copyOutput,Task copyError,Task copyInput)
        {
            try
            {
                await process.WaitForExitAsync();
                try {await copyInput;}catch(IOException)when(process.HasExited&&(!inputFailedWhileRunning||Volatile.Read(ref stopRequested)!=0)){}
                await Task.WhenAll(copyOutput,copyError);await output.FlushAsync();await error.FlushAsync();
                var cancelled=Volatile.Read(ref stopRequested)!=0;return new(cancelled?-1:process.ExitCode,StandardOutputPath,StandardErrorPath,began,clock.Now,cancelled);
            }
            finally {await output.DisposeAsync();await error.DisposeAsync();cancellation.Dispose();process.Dispose();}
        }
        private void RequestStop()
        {
            if(completion.IsCompleted)return;Interlocked.Exchange(ref stopRequested,1);
            try {if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}
        }
        public async Task StopAsync(CancellationToken cancellationToken=default){RequestStop();await completion.WaitAsync(cancellationToken);}
        public async ValueTask DisposeAsync(){try{await StopAsync();}finally{cancellation.Dispose();}}
    }
}
