using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

[Trait("Gate","Real"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationBootstrapCliRealTests
{
    [Theory,MemberData(nameof(BootstrapCliCases.Cases),MemberType=typeof(BootstrapCliCases))]
    public Task SameThreeAliasPropertiesAndModeCasesBindActualCurrentMonolith(BootstrapCliCase test)=>BootstrapCliCases.AssertContract(test,Probe);
    private static async Task<BootstrapCliObservation> Probe(BootstrapCliCase test)
    {
        if(!OperatingSystem.IsLinux())throw new PlatformNotSupportedException("Executable-bit caller proof requires the admitted Linux adapter");
        var root=Path.Combine(Path.GetTempPath(),"documentation-bootstrap-cli",Guid.NewGuid().ToString("N"));
        var projectRoot=Path.Combine(root,"project");var cacheRoot=Path.Combine(root,"cache");var runtimeRoot=Path.Combine(root,"runtime");
        var hashes=new NativeContentHash();FileArtifactStore Store(string selected)=>new(selected,value=>hashes.Sha256(value.Span));
        var project=Store(projectRoot);var cache=Store(cacheRoot);var evidence=Store(root);
        var profile=RendererProfileCases.Original();var lockJson=JsonNode.Parse(profile.Toolchain.Bytes.Span)!;
        byte[] Zip()
        {
            using var stream=new MemoryStream();using(var archive=new ZipArchive(stream,ZipArchiveMode.Create,true)){
                var entry=archive.CreateEntry("gradle-8.14.3/bin/gradle",CompressionLevel.NoCompression);using var output=entry.Open();output.Write("fixture gradle data"u8);}
            return stream.ToArray();
        }
        // These admitted fixture bytes are never executed. Real native image
        // identity admission still uses the existing pinned Docker/image path.
        var toolBytes=new Dictionary<string,byte[]>(StringComparer.Ordinal){[".tools/gradle-8.14.3-bin.zip"]=Zip(),["gradlew"]="fixture wrapper data"u8.ToArray(),["gradlew.bat"]="fixture windows wrapper data"u8.ToArray(),["gradle/wrapper/gradle-wrapper.jar"]="fixture wrapper jar data"u8.ToArray(),[".tools/plantuml-1.2025.10.jar"]="fixture plantuml data"u8.ToArray()};
        foreach(var file in lockJson["files"]!.AsArray()){
            var bytes=toolBytes[file!["file"]!.GetValue<string>()];file["sha256"]=hashes.Sha256(bytes);file["size_bytes"]=bytes.Length;}
        var toolchain=profile.Toolchain with{Bytes=JsonSerializer.SerializeToUtf8Bytes(lockJson)};
        var sources=new List<Artifact>();var projection=DesignGradleProjectionCases.Expected(DesignGradleCliPlanningCases.ProjectionCase());sources.Add(projection.Descriptor);sources.Add(projection.BuildScript);sources.Add(toolchain);sources.Add(profile.Renderer);
        if(test.Alias!="verifyToolchain"){sources.Add(DocumentationFixture.Current.Input.BlueprintMarkdown);sources.Add(DocumentationFixture.Current.Input.DesignMarkdown);}
        var zipHash=hashes.Sha256(toolBytes[".tools/gradle-8.14.3-bin.zip"]);
        var properties=WrapperPropertiesCases.Expected.Replace("bd71102213493060956ec229d946beee57158dbd89d0e62b91bca0fa2c5f3531",zipHash,StringComparison.Ordinal);
        if(test.Failure=="BAD-PROPERTIES")properties=properties.Replace("networkTimeout=10000","networkTimeout=1",StringComparison.Ordinal);
        if(test.Failure!="MISSING-PROPERTIES")sources.Add(new(BootstrapCliCases.PropertiesPath,Encoding.UTF8.GetBytes(properties),"application/octet-stream"));
        foreach(var artifact in sources)await project.WriteAsync(artifact);
        foreach(var pair in toolBytes)await cache.WriteAsync(new(pair.Key,pair.Value,"application/octet-stream"));
        var extra=new Dictionary<string,byte[]>(StringComparer.Ordinal){[".tools/gradle-8.14.3/bin/gradle"]="fixture gradle data"u8.ToArray(),[".tools/gradle-8.14.3/bin/extra"]="retained extra"u8.ToArray(),["unrelated"]="retained unrelated"u8.ToArray()};
        foreach(var pair in extra)await cache.WriteAsync(new(pair.Key,pair.Value,"application/octet-stream"));
        Directory.CreateDirectory(ArtifactPaths.Resolve(cacheRoot,".tools/gradle-8.14.3/bin/retained-directory"));
        var modes=new Dictionary<string,int>(StringComparer.Ordinal){[".tools/gradle-8.14.3/bin/gradle"]=416,[".tools/gradle-8.14.3/bin/extra"]=384,[".tools/gradle-8.14.3/bin/retained-directory"]=448,["gradlew"]=438,["unrelated"]=438};
        foreach(var mode in modes)File.SetUnixFileMode(ArtifactPaths.Resolve(cacheRoot,mode.Key),(UnixFileMode)mode.Value);
        var gradle=lockJson["files"]![0]!;var checksum=Encoding.ASCII.GetBytes(zipHash+"  gradle-8.14.3-bin.zip\n");const string proofPath="proofs/gradle.json";
        var canonical=new CanonicalJson();var proof=new ToolChecksumProof(1,gradle["url"]!.GetValue<string>(),zipHash,toolBytes[".tools/gradle-8.14.3-bin.zip"].Length,
            gradle["url"]!.GetValue<string>()+".sha256",hashes.Sha256(checksum),Convert.ToBase64String(checksum));
        await cache.WriteAsync(new(proofPath,canonical.Serialize(JsonSerializer.SerializeToElement(proof)),"application/json"));
        Directory.CreateDirectory(Path.Combine(runtimeRoot,".nuke"));
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host missing");Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var dll=typeof(Build).Assembly.Location;var outputRoot=Path.Combine(root,"final");
        var args=new[]{"exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),"--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json"),dll,
            "--root",runtimeRoot,"--target","Design","--operation","GradleCompatibility","--dotnet-host",host,"--project-root",projectRoot,"--output-root",outputRoot,
            "--cache-root",cacheRoot,"--work-root",Path.Combine(root,"work"),"--receipt-root",Path.Combine(root,"renderer-receipts"),"--docker-identity",Path.Combine(DocumentationFixture.Current.Workspace,"remediation/evidence/docker-admission-001.json"),
            "--renderer-user","1000:1000","--tool-access","Offline","--offline-gradle-checksum-proof",proofPath,"--design-aliases",test.Alias,"--design-registry-sha256",new DesignStagePlanner().Describe().Sha256,"--generation","bootstrap-caller"};
        var request=new ProcessRequest(new(host,hashes.Sha256(await File.ReadAllBytesAsync(host)),Environment.Version.ToString()),args,runtimeRoot,new Dictionary<string,string>(),Path.Combine(root,"cli-process"));
        var process=await new NativeProcessRunner(new SystemClock()).RunAsync(request);
        var diagnostic=await File.ReadAllTextAsync(process.StandardOutputPath)+Environment.NewLine+await File.ReadAllTextAsync(process.StandardErrorPath);
        await evidence.WriteAsync(new("invocation.json",JsonSerializer.SerializeToUtf8Bytes(new{request,process,test,scope="Actual private compiled monolith, synthetic admitted tool data never executed, original caller properties/mode effects; existing pinned native renderer image identity admission"}),"application/json"));
        foreach(var path in modes.Keys.ToArray())modes[path]=(int)File.GetUnixFileMode(ArtifactPaths.Resolve(cacheRoot,path));
        var sourceUnchanged=true;foreach(var source in sources)sourceUnchanged&=(await project.ReadAsync(source.RelativePath)).Span.SequenceEqual(source.Bytes.Span);
        var toolsUnchanged=true;foreach(var pair in toolBytes.Concat(extra))toolsUnchanged&=(await cache.ReadAsync(pair.Key)).Span.SequenceEqual(pair.Value);
        var output=Store(outputRoot);var tree=Directory.Exists(outputRoot)?await new NativeArtifactWorkspace(outputRoot).EnumerateAsync():[];
        var paths=tree.Where(entry=>entry.Kind==NativeArtifactKind.RegularFile&&!entry.RelativePath.StartsWith(".leases/",StringComparison.Ordinal)).Select(entry=>entry.RelativePath).Order(StringComparer.Ordinal).ToArray();
        Artifact? manifest=paths.Contains("generation-manifest.json",StringComparer.Ordinal)?new("generation-manifest.json",await output.ReadAsync("generation-manifest.json"),"application/json"):null;
        var bootstrapStore=Store(Path.Combine(root,"work","bootstrap"));
        Artifact? receipt=paths.Contains("toolchain-bootstrap-receipt.json",StringComparer.Ordinal)?new("toolchain-bootstrap-receipt.json",await output.ReadAsync("toolchain-bootstrap-receipt.json"),"application/json"):null;
        Artifact? child=receipt is null?null:new("generation-manifest.json",await bootstrapStore.ReadAsync("generation-manifest.json"),"application/json");
        if(receipt is not null)Assert.Equal(receipt.Bytes.ToArray(),(await bootstrapStore.ReadAsync(receipt.RelativePath)).ToArray());
        return new(process.ExitCode,diagnostic,manifest,modes,sourceUnchanged,toolsUnchanged,File.Exists(process.StandardOutputPath)&&File.Exists(process.StandardErrorPath),args[0]=="exec"&&!args.Contains("build",StringComparer.Ordinal)&&!args.Contains("restore",StringComparer.Ordinal),paths,sources,receipt,child);
    }
}
