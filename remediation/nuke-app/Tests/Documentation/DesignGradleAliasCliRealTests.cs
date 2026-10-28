using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

internal static class NativeDesignAliasCli
{
    internal static async Task<DesignAliasCliObservation> Run(DesignAliasCliCall call,CancellationToken token)
    {
        var hashes=new NativeContentHash();var sources=DesignAliasCliCases.SourceArtifacts(call.Case);
        var project=new FileArtifactStore(call.ProjectRoot,bytes=>hashes.Sha256(bytes.Span));
        foreach(var artifact in sources)await project.WriteAsync(artifact,token);
        Directory.CreateDirectory(Path.Combine(call.RuntimeRoot,".nuke"));
        var sourcePins=sources.ToDictionary(artifact=>artifact.RelativePath,artifact=>hashes.Sha256(artifact.Bytes.Span),StringComparer.Ordinal);
        var hostPin=hashes.Sha256(await File.ReadAllBytesAsync(call.Host,token));
        var dllPin=hashes.Sha256(await File.ReadAllBytesAsync(call.Dll,token));
        var argv=DesignAliasCliCases.Arguments(call).ToList();
        argv.InsertRange(1,["--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
            "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json")]);
        var processRequest=new ProcessRequest(new(call.Host,hostPin,Environment.Version.ToString()),argv,call.RuntimeRoot,
            new Dictionary<string,string>(),Path.Combine(call.Root,"cli-process"));
        var process=await new NativeProcessRunner(new SystemClock()).RunAsync(processRequest,token);
        var stdoutRetained=File.Exists(process.StandardOutputPath);var stderrRetained=File.Exists(process.StandardErrorPath);
        var diagnostic=(stdoutRetained?await File.ReadAllTextAsync(process.StandardOutputPath,token):"")+Environment.NewLine+
            (stderrRetained?await File.ReadAllTextAsync(process.StandardErrorPath,token):"");
        var evidence=new FileArtifactStore(call.Root,bytes=>hashes.Sha256(bytes.Span));
        await evidence.WriteAsync(new("cli-invocation.json",JsonSerializer.SerializeToUtf8Bytes(new{request=processRequest,process,dll_sha256=dllPin,
            expected_source_pins=sourcePins,scope="Current compiled monolith GradleCompatibility dispatch; no source build or renderer requested"}),"application/json"),token);
        var output=new FileArtifactStore(call.OutputRoot,bytes=>hashes.Sha256(bytes.Span));
        var artifacts=new List<Artifact>();
        foreach(var expected in call.Case.Code.Expected)
            if(File.Exists(ArtifactPaths.Resolve(call.OutputRoot,expected.RelativePath)))
                artifacts.Add(new(expected.RelativePath,await output.ReadAsync(expected.RelativePath,token),expected.MediaType));
        const string manifestPath="generation-manifest.json";
        Artifact? manifest=File.Exists(ArtifactPaths.Resolve(call.OutputRoot,manifestPath))
            ?new(manifestPath,await output.ReadAsync(manifestPath,token),"application/json"):null;
        var leaseScope="design-assets/"+hashes.Sha256(Encoding.UTF8.GetBytes(call.OutputRoot));
        var internalLease=".leases/"+hashes.Sha256(Encoding.UTF8.GetBytes(leaseScope))+".lock";
        var allowed=call.Case.Code.Expected.Select(artifact=>artifact.RelativePath).Append(manifestPath).Append(internalLease).ToHashSet(StringComparer.Ordinal);
        var outputPaths=Directory.Exists(call.OutputRoot)?Directory.EnumerateFiles(call.OutputRoot,"*",SearchOption.AllDirectories)
            .Select(path=>Path.GetRelativePath(call.OutputRoot,path).Replace('\\','/')).ToArray():Array.Empty<string>();
        var currentSources=Directory.EnumerateFiles(call.ProjectRoot,"*",SearchOption.AllDirectories)
            .Select(path=>Path.GetRelativePath(call.ProjectRoot,path).Replace('\\','/')).Order(StringComparer.Ordinal).ToArray();
        var unchanged=sourcePins.Keys.Order(StringComparer.Ordinal).SequenceEqual(currentSources,StringComparer.Ordinal);
        foreach(var pair in sourcePins)
            unchanged&=hashes.Sha256((await project.ReadAsync(pair.Key,token)).Span)==pair.Value;
        var noToolEffects=new[]{"cache","work","tool-receipts","not-a-catalog"}.All(name=>!Directory.Exists(Path.Combine(call.Root,name)))
            &&!File.Exists(Path.Combine(call.Root,"absent-docker.json"));
        return new(process.ExitCode,diagnostic,stdoutRetained,stderrRetained,
            hashes.Sha256(await File.ReadAllBytesAsync(call.Host,token))==hostPin,hashes.Sha256(await File.ReadAllBytesAsync(call.Dll,token))==dllPin,
            unchanged,noToolEffects,argv[0]=="exec"&&!argv.Contains("build",StringComparer.Ordinal)&&!argv.Contains("restore",StringComparer.Ordinal)&&!argv.Contains("run",StringComparer.Ordinal),
            argv,artifacts,manifest,outputPaths.Where(path=>!allowed.Contains(path)).Order(StringComparer.Ordinal).ToArray());
    }
}

[Trait("Gate","Real")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleAliasCliRealTests
{
    [Theory,MemberData(nameof(DesignAliasCliCases.Cases),MemberType=typeof(DesignAliasCliCases))]
    public async Task SameFiniteCasesBindActualCurrentMonolithDispatch(DesignAliasCliCase test)
    {
        // Retain all output and subprocess evidence in the calling workspace;
        // do not depend on quota-limited machine-private temporary directories.
        var root=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/nuke-app/artifacts/gradle-alias-cli-tests",Guid.NewGuid().ToString("N"));
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host unavailable");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        await DesignAliasCliCases.AssertContract(new(test,root,host,typeof(Build).Assembly.Location),NativeDesignAliasCli.Run);
    }
}
