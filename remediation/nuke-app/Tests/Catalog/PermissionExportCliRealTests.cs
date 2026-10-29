using System.Text.Json;
using AndroidMcp.BuildTool.Catalog;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class PermissionExportCliRealTests
{
    [Theory]
    [InlineData("union")][InlineData("unicode")][InlineData("samples")]
    public async Task RegisteredMonolithExportsIndependentFixtureFactsWithoutChangingSource(string mode)
    {
        var test=new PermissionExportCase("PERMISSION-CLI-"+mode,mode);
        var fixture=await new NativePermissionExportProbe().RunAsync(test);Assert.Null(fixture.Error);
        var source=fixture.SourceCatalog;var root=Path.GetDirectoryName(source)!;var output=Path.Combine(root,"cli export");
        var runtime=Path.Combine(root,"isolated runtime");Directory.CreateDirectory(Path.Combine(runtime,".nuke"));
        var host=Environment.ProcessPath??throw new InvalidOperationException("Native dotnet host required");
        Assert.Equal("dotnet",Path.GetFileNameWithoutExtension(host));
        var dll=typeof(AndroidMcp.BuildTool.Build).Assembly.Location;
        string Hash(string path){using var stream=File.OpenRead(path);return ContentHash.Sha256(stream);}
        var tool=new ToolIdentity(host,Hash(host),Environment.Version.ToString());var assemblyHash=Hash(dll);var sourceHash=Hash(Path.Combine(source,"catalog.sqlite"));
        string[] args=["exec","--runtimeconfig",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.runtimeconfig.json"),
            "--depsfile",Path.Combine(AppContext.BaseDirectory,"AndroidMcp.Build.Tests.deps.json"),dll,
            "--root",runtime,"--target","Catalog","--operation","Export","--dotnet-host",host,
            "--project-root",NativeSchemaProvider.Root(),"--source-catalog",source,"--output-root",output];
        var result=await new NativeProcessRunner(new SystemClock()).RunAsync(new(tool,args,runtime,new Dictionary<string,string>(),Path.Combine(root,"cli receipts")));
        var stdout=await File.ReadAllTextAsync(result.StandardOutputPath);var stderr=await File.ReadAllTextAsync(result.StandardErrorPath);
        Assert.True(result.ExitCode==0,"Retained CLI fixture: "+root+Environment.NewLine+stdout+Environment.NewLine+stderr);
        Assert.Equal(tool.Sha256,Hash(host));Assert.Equal(assemblyHash,Hash(dll));Assert.Equal(sourceHash,Hash(Path.Combine(source,"catalog.sqlite")));
        var line=Assert.Single(stdout.Split('\n').Select(value=>value.Trim()),value=>value.StartsWith("{\"permission_names\":",StringComparison.Ordinal));
        using var doc=JsonDocument.Parse(line);var report=doc.RootElement;
        var summary=new PermissionExportSummary(report.GetProperty("permission_names").GetInt64(),report.GetProperty("manifest_definition_records").GetInt64(),
            report.GetProperty("source_catalog").GetString()!,report.GetProperty("complete").GetBoolean());
        var artifacts=report.GetProperty("artifacts").Deserialize<StoredArtifact[]>()!;
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var artifact in artifacts)files.Add(artifact.RelativePath,await File.ReadAllBytesAsync(ArtifactPaths.Resolve(output,artifact.RelativePath)));
        PermissionExportCases.AssertContents(test,new(summary,artifacts),files,source);
        // Read-only opening itself is observed by the in-process same-case
        // lane. This external lane proves route/output and source retention.
    }
}
