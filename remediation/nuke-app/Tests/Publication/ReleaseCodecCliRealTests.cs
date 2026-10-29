using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real"),Trait("Platform","Linux"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseCodecCliRealTests
{
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task ActualCategoryCallersRetainSourceAndRoundTripOriginalPayload(bool empty)
    {
        var payload=empty?Array.Empty<byte>():ReleaseCodecCases.Payload;
        var fixture=await Fixture.Create(payload);
        var split=await fixture.Call("Split",["--source-artifact","sealed.bin","--artifact-sha256",ContentHash.Sha256(payload),"--generation","parts","--part-bytes","377"]);
        Assert.True(split.ExitCode==0,fixture.Diagnostic(split));
        using var splitReport=JsonDocument.Parse(await fixture.Report(split,"Split"));
        var manifestReceipt=splitReport.RootElement.GetProperty("ManifestArtifact").Deserialize<StoredArtifact>()!;
        Assert.Equal("parts/manifest.json",manifestReceipt.RelativePath);
        var manifestBytes=await fixture.Read(manifestReceipt.RelativePath);
        Assert.Equal(manifestReceipt.Sha256,ContentHash.Sha256(manifestBytes));Assert.Equal(manifestReceipt.Length,manifestBytes.LongLength);
        using var manifest=JsonDocument.Parse(manifestBytes);var data=manifest.RootElement;
        Assert.Equal("android-mcp-release-parts-v1",data.GetProperty("format").GetString());
        Assert.Equal("sealed.bin",data.GetProperty("artifact_name").GetString());
        Assert.Equal(payload.LongLength,data.GetProperty("bytes").GetInt64());Assert.Equal(ContentHash.Sha256(payload),data.GetProperty("sha256").GetString());
        Assert.Equal(377,data.GetProperty("part_bytes").GetInt64());
        var parts=data.GetProperty("parts").EnumerateArray().ToArray();Assert.Equal((payload.Length+376)/377,parts.Length);
        for(var index=0;index<parts.Length;index++){
            var name="artifact.part-"+(index+1).ToString("D4",System.Globalization.CultureInfo.InvariantCulture);
            var expected=payload.AsSpan(index*377,Math.Min(377,payload.Length-index*377)).ToArray();
            Assert.Equal(name,parts[index].GetProperty("name").GetString());Assert.Equal(expected.LongLength,parts[index].GetProperty("bytes").GetInt64());
            Assert.Equal(ContentHash.Sha256(expected),parts[index].GetProperty("sha256").GetString());Assert.Equal(expected,await fixture.Read("parts/"+name));
        }
        var assembled=await fixture.Call("Reassemble",["--parts-manifest",manifestReceipt.RelativePath,"--manifest-sha256",manifestReceipt.Sha256,"--generation","assembly","--destination-artifact","restored.bin"]);
        Assert.True(assembled.ExitCode==0,fixture.Diagnostic(assembled));
        using var assemblyReport=JsonDocument.Parse(await fixture.Report(assembled,"Reassemble"));
        var artifact=assemblyReport.RootElement.GetProperty("Artifact").Deserialize<StoredArtifact>()!;
        Assert.Equal("restored.bin",artifact.RelativePath);Assert.Equal(payload.LongLength,artifact.Length);Assert.Equal(ContentHash.Sha256(payload),artifact.Sha256);
        Assert.Equal(payload,await fixture.Read(artifact.RelativePath));Assert.Equal(payload,await fixture.Read("sealed.bin"));
        var markerReceipt=assemblyReport.RootElement.GetProperty("SuccessMarker").Deserialize<StoredArtifact>()!;
        Assert.Equal("assembly/success.json",markerReceipt.RelativePath);
        var markerBytes=await fixture.Read(markerReceipt.RelativePath);Assert.Equal(markerReceipt.Sha256,ContentHash.Sha256(markerBytes));
        using var marker=JsonDocument.Parse(markerBytes);Assert.Equal("restored.bin",marker.RootElement.GetProperty("artifact").GetString());
        Assert.Equal(manifestReceipt.Sha256,marker.RootElement.GetProperty("manifest_sha256").GetString());
        // Existing final output must survive a new-generation retry unchanged.
        var retry=await fixture.Call("Reassemble",["--parts-manifest",manifestReceipt.RelativePath,"--manifest-sha256",manifestReceipt.Sha256,"--generation","assembly-retry","--destination-artifact","restored.bin"]);
        Assert.NotEqual(0,retry.ExitCode);Assert.Equal(payload,await fixture.Read("restored.bin"));Assert.False(File.Exists(Path.Combine(fixture.Root,"assembly-retry/success.json")));
        fixture.AssertPinnedRuntime();
    }

    [Fact]
    public async Task WrongTrustedWholePinCannotEmitACommitOrChangeTheSealedInput()
    {
        var fixture=await Fixture.Create(ReleaseCodecCases.Payload);
        var result=await fixture.Call("Split",["--source-artifact","sealed.bin","--artifact-sha256",new string('0',64),"--generation","parts"]);
        Assert.NotEqual(0,result.ExitCode);Assert.False(File.Exists(Path.Combine(fixture.Root,"parts/manifest.json")));
        Assert.Equal(ReleaseCodecCases.Payload,await fixture.Read("sealed.bin"));fixture.AssertPinnedRuntime();
    }

    private sealed class Fixture(string root,string runtime,ToolIdentity host,string dll,string dllHash)
    {
        public string Root=>root;
        public static async Task<Fixture> Create(byte[] payload)
        {
            var root=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"android-mcp-codec-cli-"+Guid.NewGuid().ToString("N"))).FullName;
            await File.WriteAllBytesAsync(Path.Combine(root,"sealed.bin"),payload);
            var runtime=Path.Combine(root,"isolated runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
            var path=Environment.ProcessPath!;Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(path));
            var dll=typeof(AndroidMcp.BuildTool.Build).Assembly.Location;
            return new(root,runtime,new(path,Hash(path),Environment.Version.ToString()),dll,Hash(dll));
        }
        public Task<byte[]> Read(string path)=>File.ReadAllBytesAsync(ArtifactPaths.Resolve(root,path));
        public Task<ProcessResult> Call(string operation,string[] parameters)
        {
            string[] arguments=["exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
                "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json"),dll,
                "--root",runtime,"--target","Publish","--operation",operation,"--dotnet-host",host.ExecutablePath,"--project-root",root,..parameters];
            return new NativeProcessRunner(new SystemClock()).RunAsync(new(host,arguments,runtime,new Dictionary<string,string>(),Path.Combine(root,"receipts-"+Guid.NewGuid().ToString("N"))));
        }
        public async Task<string> Report(ProcessResult result,string operation)=>Assert.Single((await File.ReadAllTextAsync(result.StandardOutputPath)).Split('\n').Select(line=>line.Trim()),
            line=>line.StartsWith("{\"Operation\":\""+operation+"\"",StringComparison.Ordinal));
        public string Diagnostic(ProcessResult result)=>root+Environment.NewLine+File.ReadAllText(result.StandardOutputPath)+Environment.NewLine+File.ReadAllText(result.StandardErrorPath);
        public void AssertPinnedRuntime(){Assert.Equal(host.Sha256,Hash(host.ExecutablePath));Assert.Equal(dllHash,Hash(dll));}
        private static string Hash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
    }
}
