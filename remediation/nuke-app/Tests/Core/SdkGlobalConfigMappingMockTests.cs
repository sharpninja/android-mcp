using System.Text;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class SdkGlobalConfigFixture
{
    public const string Candidate="/.globalconfig";
    public const string Actual="/fixture/app/.globalconfig";
    public SdkSourceEvaluationFixture Source { get; }=new("source-graph");
    public NativeFactsFixture Facts { get; }=new("sdk-source-graph");
    public TestGateSdkEvaluation Expected { get; }
    public SdkGlobalConfigFixture()
    {
        var host=Source.Request.Run.HostProject;
        var project=Source.Projects[host];
        Source.Projects[host]=project with {Items=[..project.Items,
            new("GlobalAnalyzerConfigFiles",Candidate,Candidate,host),
            new("EditorConfigFiles",Actual,Actual,host)]};
        Expected=Source.Expected with {Projects=Source.Expected.Projects.Select(value=>
        {
            if(value.ProjectPath!=host)return value;
            var propertyBoundary=value.Inputs.TakeWhile(input=>!project.ParticipatingFileProperties.ContainsKey(input.Kind)).Count();
            return value with {Inputs=[..value.Inputs.Take(propertyBoundary),new("EditorConfigFiles",Actual,host),..value.Inputs.Skip(propertyBoundary)]};
        }).ToArray()};
        foreach(var input in Expected.Projects.SelectMany(value=>value.Inputs))
            Facts.Files[input.AbsolutePath]=Encoding.UTF8.GetBytes("input:"+input.AbsolutePath);
        foreach(var import in Expected.Projects.SelectMany(value=>value.Imports))
            Facts.Files[import]=Encoding.UTF8.GetBytes("import:"+import);
        Facts.Files[Actual]=Encoding.UTF8.GetBytes("is_global = true\ndotnet_diagnostic.CA1822.severity = warning\n");
        Assert.False(Facts.Files.ContainsKey(Candidate));
    }
}
public sealed class ScriptedSdkGlobalConfigProjection(SdkGlobalConfigFixture fixture,SdkSourceEvaluationDependencies io):ITestGateSdkEvaluator
{
    public async Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,CancellationToken cancellationToken=default)
    {
        await using var session=await io.OpenAsync(fixture.Source.OpenRequest,cancellationToken);
        foreach(var project in fixture.Source.ProjectOrder)await session.LoadProjectAsync(project,cancellationToken);
        return fixture.Expected;
    }
}
public sealed class RecordedSdkGlobalConfigProjection(ITestGateSdkEvaluator inner):ITestGateSdkEvaluator
{
    public List<TestGateSdkEvaluation> Results { get; }=[];
    public async Task<TestGateSdkEvaluation> EvaluateAsync(TestGateSdkEvaluationRequest request,CancellationToken cancellationToken=default)
    {var result=await inner.EvaluateAsync(request,cancellationToken);Results.Add(result);return result;}
}
public static class SdkGlobalConfigCases
{
    public static void AssertProjection(SdkGlobalConfigFixture fixture,TestGateSdkEvaluation actual)
        =>Assert.Equal(NativeFactsFixture.Canonical(fixture.Expected),NativeFactsFixture.Canonical(actual));
    public static async Task AssertMappingAndManifestAsync(Func<SdkGlobalConfigFixture,SdkSourceEvaluationDependencies,ITestGateSdkEvaluator> create)
    {
        var fixture=new SdkGlobalConfigFixture();var source=new SdkSourceEvaluationDependencies(fixture.Source);
        var files=new NativeFactsDependencies(fixture.Facts);var projection=new RecordedSdkGlobalConfigProjection(create(fixture,source));
        // Existing proven collector is unchanged. Only its injected native
        // projection is scripted for the mock gate and rebound after green.
        var collector=new SdkEvaluatedTestGateNativeFacts(fixture.Source.Configuration,projection,files,files,files,files,files);
        var initialBytes=fixture.Facts.Files[SdkGlobalConfigFixture.Actual];
        var first=await collector.CaptureSourceAsync(fixture.Source.Request.Run,TestGateSnapshotPoint.BeforeBuild);
        fixture.Facts.Files[SdkGlobalConfigFixture.Actual]=Encoding.UTF8.GetBytes("is_global = true\ndotnet_diagnostic.CA1822.severity = error\n");
        var second=await collector.CaptureSourceAsync(fixture.Source.Request.Run,TestGateSnapshotPoint.AfterHostBuild);
        Assert.Equal(2,projection.Results.Count);
        foreach(var actual in projection.Results)AssertProjection(fixture,actual);
        Assert.Equal(new[]{fixture.Source.OpenRequest,fixture.Source.OpenRequest}.Select(NativeFactsFixture.Canonical),source.Opens.Select(NativeFactsFixture.Canonical));
        Assert.Equal(fixture.Source.ProjectOrder.Concat(fixture.Source.ProjectOrder),source.Loads);Assert.Equal(2,source.Disposals);Assert.Empty(source.Queries);
        Assert.DoesNotContain(files.Files,read=>read.Request.AbsolutePath==SdkGlobalConfigFixture.Candidate);
        var reads=files.Files.Where(read=>read.Request.AbsolutePath==SdkGlobalConfigFixture.Actual).ToArray();
        Assert.Equal(4,reads.Length);
        Assert.Equal(new[]{"SourceInput","SourceInputRecheck","SourceInput","SourceInputRecheck"},reads.Select(read=>read.Request.Role));
        Assert.Equal(new[]{Convert.ToBase64String(initialBytes),Convert.ToBase64String(initialBytes),
            Convert.ToBase64String(fixture.Facts.Files[SdkGlobalConfigFixture.Actual]),Convert.ToBase64String(fixture.Facts.Files[SdkGlobalConfigFixture.Actual])},reads.Select(read=>read.BytesBase64));
        var snapshots=new[]{first,second};var contents=new[]{initialBytes,fixture.Facts.Files[SdkGlobalConfigFixture.Actual]};
        for(var index=0;index<snapshots.Length;index++)
        {
            var saved=Assert.Single(files.Writes,write=>write.Path==snapshots[index].Manifest.RelativePath);
            var bytes=Convert.FromBase64String(saved.BytesBase64);
            Assert.Equal(ContentHash.Sha256(bytes),snapshots[index].ContentFingerprint);
            var manifest=JsonSerializer.Deserialize<TestGateSourceManifest>(bytes)!;
            Assert.DoesNotContain(manifest.Inputs,input=>input.AbsolutePath==SdkGlobalConfigFixture.Candidate);
            var input=Assert.Single(manifest.Inputs,input=>input.AbsolutePath==SdkGlobalConfigFixture.Actual);
            Assert.Equal("EditorConfigFiles",input.Kind);Assert.Equal(fixture.Source.Request.Run.HostProject,input.DefiningProject);
            Assert.Equal(ContentHash.Sha256(contents[index]),input.Sha256);Assert.Equal(contents[index].LongLength,input.Length);
        }
        Assert.NotEqual(first.ContentFingerprint,second.ContentFingerprint);
    }
}
public sealed class SdkGlobalConfigMappingMockTests
{
    [Fact]
    public Task SDK_candidates_are_ignored_and_actual_editor_config_bytes_change_the_manifest()
        =>SdkGlobalConfigCases.AssertMappingAndManifestAsync((fixture,io)=>new ScriptedSdkGlobalConfigProjection(fixture,io));
    [Fact]
    public void Candidate_leak_and_dropped_actual_editor_config_are_rejected()
    {
        var fixture=new SdkGlobalConfigFixture();var host=fixture.Expected.Projects[0];
        var leaked=fixture.Expected with {Projects=[host with {Inputs=[..host.Inputs,new("GlobalAnalyzerConfigFiles",SdkGlobalConfigFixture.Candidate,host.ProjectPath)]},..fixture.Expected.Projects.Skip(1)]};
        var dropped=fixture.Expected with {Projects=[host with {Inputs=host.Inputs.Where(input=>input.AbsolutePath!=SdkGlobalConfigFixture.Actual).ToArray()},..fixture.Expected.Projects.Skip(1)]};
        Assert.ThrowsAny<Exception>(()=>SdkGlobalConfigCases.AssertProjection(fixture,leaked));
        Assert.ThrowsAny<Exception>(()=>SdkGlobalConfigCases.AssertProjection(fixture,dropped));
    }
}
