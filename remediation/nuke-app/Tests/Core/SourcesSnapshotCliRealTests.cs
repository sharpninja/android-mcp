using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Tests.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public sealed class NativeSnapshotCliProbe:ISnapshotCliProbe
{
    public async Task<SnapshotCliObservation> RunAsync(SnapshotCliCase c)
    {
        var root=Path.Combine(Path.GetTempPath(),"snapshot-cli",Guid.NewGuid().ToString("N"),"fixture with spaces");
        var input=Path.Combine(root,"locked inputs");var output=Path.Combine(root,"raw outputs");var bundle=Path.Combine(root,"owner bundle");var runtime=Path.Combine(root,"isolated runtime");var proofRoot=c.Change=="proof-default-root"?output:Path.Combine(root,"explicit owner proofs");
        Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var inputs=Store(input);var destination=Store(output);var fixture=SourcesSnapshotCliCases.Request(c);
        await inputs.WriteAsync(fixture.Recipe);await inputs.WriteAsync(fixture.Lock);
        if(c.Existing>0)await destination.WriteAsync(new("raw/first",c.Change is "corrupt" or "fetch-existing"?new byte[]{0}:SourceSnapshotCases.First,"application/octet-stream"));
        if(c.Existing==2)await destination.WriteAsync(new("raw/second",SourceSnapshotCases.Second,"application/octet-stream"));
        if(c.Bundle){var files=Store(bundle);await files.WriteAsync(new("bundle/first",SourceSnapshotCases.First,"application/octet-stream"));if(c.Change!="bundle-missing")await files.WriteAsync(new("bundle/second",c.Change=="bundle-corrupt"?new byte[]{0}:SourceSnapshotCases.Second,"application/octet-stream"));}
        var envelope=SourcesSnapshotCliCases.Envelope(c,c.Change=="proof-default-root"?null:proofRoot);
        var envelopeHash=ContentHash.Sha256(envelope);
        if(SourcesSnapshotCliCases.Proof(c)&&c.Change is not ("proof-dry" or "proof-existing" or "proof-generic")){
            await inputs.WriteAsync(new(SourcesSnapshotCliCases.EnvelopePath,envelope,"application/json"));
            var proofs=Store(proofRoot);await proofs.WriteAsync(new(SourcesSnapshotCliCases.ProofReceipt,SourcesSnapshotCliCases.Receipt,"application/json"));await proofs.WriteAsync(new(SourcesSnapshotCliCases.ProofXml,SourcesSnapshotCliCases.Xml,"application/xml"));
        }
        if(c.Change=="proof-envelope-hash")envelopeHash=new string('0',64);
        var originalInputs=Hashes(input);var originalBundle=Hashes(bundle);var originalProofs=Hashes(proofRoot);var before=Hashes(output);
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host missing.");Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var arguments=SourcesSnapshotCliCases.Arguments(c,runtime,input,bundle,output,envelopeHash,envelope.Length).ToList();arguments[1]=typeof(Build).Assembly.Location;arguments[arguments.IndexOf("--dotnet-host")+1]=host;
        arguments.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
        var runner=new NativeProcessRunner(new SystemClock());var identity=new ToolIdentity(host,ContentHash.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString());
        async Task<(ProcessResult Process,string Text,string? Json)> Invoke(string name)
        {
            var process=await runner.RunAsync(new(identity,arguments,runtime,new Dictionary<string,string>(),Path.Combine(root,name)));
            var text=await File.ReadAllTextAsync(process.StandardOutputPath)+Environment.NewLine+await File.ReadAllTextAsync(process.StandardErrorPath);
            var json=text.Split('\n').Select(line=>line.Trim()).FirstOrDefault(line=>line.StartsWith("{\"Release\":",StringComparison.Ordinal));
            return (process,text+Environment.NewLine+"Fixture retained at "+root,json);
        }
        var first=await Invoke("first process");var after=Hashes(output);byte[]? seal=after.ContainsKey(SourcesSnapshotCliCases.SealPath)?await File.ReadAllBytesAsync(Path.Combine(output,SourcesSnapshotCliCases.SealPath)):null;
        int? repeatedExit=null;int? repeatedDownloaded=null;var repeatedRetained=true;
        if(c.Seal&&first.Process.ExitCode==0){var second=await Invoke("repeated process");repeatedExit=second.Process.ExitCode;repeatedRetained=Same(after,Hashes(output));if(second.Json is not null){using var doc=JsonDocument.Parse(second.Json);repeatedDownloaded=doc.RootElement.GetProperty("Counts").GetProperty("Downloaded").GetInt32();}}
        return new(first.Process.ExitCode,first.Json,first.Text,before,after,seal,Same(originalInputs,Hashes(input)),Same(originalBundle,Hashes(bundle)),Same(originalProofs,Hashes(proofRoot)),Directory.Exists(output),arguments,runtime,input,bundle,output,repeatedExit,repeatedDownloaded,repeatedRetained);
    }
    private static FileArtifactStore Store(string root)=>new(root,bytes=>ContentHash.Sha256(bytes.Span));
    private static Dictionary<string,string> Hashes(string root)=>!Directory.Exists(root)?new():Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToDictionary(path=>Path.GetRelativePath(root,path).Replace(Path.DirectorySeparatorChar,'/'),path=>ContentHash.Sha256(File.ReadAllBytes(path)),StringComparer.Ordinal);
    private static bool Same(IReadOnlyDictionary<string,string> first,IReadOnlyDictionary<string,string> second)=>first.OrderBy(p=>p.Key).SequenceEqual(second.OrderBy(p=>p.Key));
}
[Trait("Gate","Real")]
public sealed class SourcesSnapshotCliRealTests
{
    [Theory,MemberData(nameof(SourcesSnapshotCliCases.Cases),MemberType=typeof(SourcesSnapshotCliCases))]
    public Task SameAssertionsExecuteCompiledSingularRegistryCli(SnapshotCliCase c)=>SourcesSnapshotCliCases.AssertCase(c,new NativeSnapshotCliProbe());
}
