using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidMcp.BuildTool.Core;
using static AndroidMcp.BuildTool.Modules.Review.ReviewNormalization;
namespace AndroidMcp.BuildTool.Modules.Review;

public sealed record ReviewProjectDefinition(string Path,string Sha256);
public sealed record ReviewNativeNormalizationRequest(string Head,TestGateProducerRequest Run,TestGateProducerResult Gate,
    StoredArtifact Evaluation,StoredArtifact SourceManifest,StoredArtifact ClassificationPolicy,
    IReadOnlyList<ReviewProjectDefinition> Definitions,StoredArtifact Trx,ReviewEvidence TrxOrigin);

/// <summary>Projects an already completed native gate. It never starts builds/tests, evaluates SDK projects,
/// or mints a native authority label. Trusted caller composition must bind the producer and Git observations.</summary>
public sealed class ReviewNativeProjectNormalizer(IPinnedArtifactReader pins,ICanonicalJson canonical,
    ITestBuildPlanAdmission build,ITrxResultAdmission trx)
{
    private static readonly JsonSerializerOptions Strict=new(){UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public async Task<ReviewNormalizationProjection> NormalizeAsync(ReviewNativeNormalizationRequest request,CancellationToken token=default)
    {
        const string metadata="PROJECT_METADATA_UNRESOLVED";const string native="NATIVE_GATE_UNVERIFIED";
        Need(string.IsNullOrWhiteSpace(request.Run.Filter),"UNFILTERED_RUN_REQUIRED");
        var gate=request.Gate;
        Need(gate.Admitted&&gate.Code=="OK"&&gate.BuildEvidence is not null&&gate.TrxAdmission?.Admitted==true,native);
        Need(build.Evaluate(build.Plan(new(request.Run.HostProject,request.Run.TestsProject,request.Run.DotNet)),gate.BuildEvidence!).Admitted,native);
        Need(gate.Evidence.Contains(request.Evaluation)&&gate.Evidence.Contains(request.SourceManifest),native);
        Need(request.SourceManifest.Sha256==gate.BuildEvidence!.CurrentSourceFingerprint&&
            request.SourceManifest.Sha256==gate.BuildEvidence.BuiltSourceFingerprint&&request.SourceManifest.Sha256==gate.BuildEvidence.FinalSourceFingerprint,native);
        Need(request.Trx.Sha256==gate.TrxAdmission!.RawTrxSha256,native);
        var origin=request.TrxOrigin;
        Need(origin.Head==request.Head&&origin.Path==request.Trx.RelativePath&&origin.Sha256==request.Trx.Sha256&&
            !string.IsNullOrWhiteSpace(origin.RunId)&&!string.IsNullOrWhiteSpace(origin.Host)&&!string.IsNullOrWhiteSpace(origin.Command),native);
        Need(Path.GetRelativePath(request.Run.WorkspaceRoot,Path.Combine(request.Run.ReceiptDirectory,"tests.trx")).Replace('\\','/')==request.Trx.RelativePath,native);
        var captures=new Dictionary<string,StoredArtifact>(StringComparer.Ordinal);
        foreach(var pin in gate.Evidence)
        {await ReadBytesAsync(pins,pin,token);Need(!captures.TryGetValue(pin.RelativePath,out var prior)||prior==pin,native);captures[pin.RelativePath]=pin;}
        var evaluationObject=await ReadAsync(pins,canonical,request.Evaluation,token);
        var manifestObject=await ReadAsync(pins,canonical,request.SourceManifest,token);
        var evaluation=JsonSerializer.Deserialize<TestGateSdkEvaluation>(evaluationObject.ToJsonString(),Strict);
        var manifest=JsonSerializer.Deserialize<TestGateSourceManifest>(manifestObject.ToJsonString(),Strict);
        Need(evaluation is not null&&manifest is not null,metadata);
        Need(manifest.HostProject==request.Run.HostProject&&manifest.TestsProject==request.Run.TestsProject,native);
        var projects=evaluation.Projects;
        Need(projects.Count>0&&projects.Select(x=>x.ProjectPath).Distinct(StringComparer.Ordinal).Count()==projects.Count,metadata);
        var definitions=request.Definitions.ToDictionary(x=>x.Path,x=>x.Sha256,StringComparer.Ordinal);
        Need(definitions.Count==request.Definitions.Count&&definitions.Count>0,metadata);
        string Relative(string path)
        {var relative=Path.GetRelativePath(request.Run.WorkspaceRoot,path).Replace('\\','/');Need(ArtifactPaths.IsRelative(relative),metadata);return relative;}
        var sdkPaths=projects.Select(x=>Relative(x.ProjectPath)).ToHashSet(StringComparer.Ordinal);
        var sourceProjects=manifest.Inputs.Where(x=>x.Kind=="Project").ToArray();
        Need(sourceProjects.Select(x=>Relative(x.AbsolutePath)).ToHashSet(StringComparer.Ordinal).SetEquals(sdkPaths),metadata);
        var rows=new List<object>();
        foreach(var project in projects)
        {
            var path=Relative(project.ProjectPath);
            Need(definitions.TryGetValue(path,out var definition),metadata);
            var source=sourceProjects.Where(x=>x.AbsolutePath==project.ProjectPath).ToArray();
            Need(source.Length==1&&source[0].DefiningProject==project.ProjectPath&&source[0].Sha256==definition,"EVIDENCE_HASH_MISMATCH");
            var facts=project.TestMetadata;Need(facts is not null,metadata);
            Need(bool.TryParse(facts.IsTestProject,out var isTest),metadata);
            Need(isTest?facts.TestKind is "unit" or "integration" or "mixed":facts.TestKind=="none",metadata);
            rows.Add(new{path,definitionSha256=definition,isTestProject=isTest,kind=facts.TestKind});
        }
        var policy=await ReadAsync(pins,canonical,request.ClassificationPolicy,token);captures[request.ClassificationPolicy.RelativePath]=request.ClassificationPolicy;
        Need(Text(policy,"head")==request.Head,"HEAD_MISMATCH");
        var declared=Array(policy,"projects",metadata);var nonSdk=definitions.Keys.Where(x=>!sdkPaths.Contains(x)).ToHashSet(StringComparer.Ordinal);
        Need(declared.Count==nonSdk.Count&&declared.Select(x=>Text(x,"path")).ToHashSet(StringComparer.Ordinal).SetEquals(nonSdk),metadata);
        foreach(var project in declared)
        {
            var path=Text(project,"path");Need(Text(project,"definitionSha256")==definitions[path],"EVIDENCE_HASH_MISMATCH");
            Need(project?["isTestProject"]?.GetValue<bool>()==false&&Text(project,"kind")=="none",metadata);
            rows.Add(new{path,definitionSha256=definitions[path],isTestProject=false,kind="none"});
        }
        Need(rows.Count==definitions.Count,metadata);
        var testProject=projects.SingleOrDefault(x=>x.ProjectPath==request.Run.TestsProject);
        Need(testProject?.TestMetadata?.IsTestProject.Equals("true",StringComparison.OrdinalIgnoreCase)==true,metadata);
        var trxBytes=await ReadBytesAsync(pins,request.Trx,token);Need(trx.Evaluate(new(trxBytes,request.Run.ExpectedClasses)).Admitted,native);
        captures[request.Trx.RelativePath]=request.Trx;
        var result=JsonSerializer.SerializeToElement(new{head=request.Head,toolSha256=request.Run.DotNet.Sha256,projects=rows,
            fullUnfilteredTrx=new[]{new{project=Relative(request.Run.TestsProject),path=request.Trx.RelativePath,sha256=request.Trx.Sha256,
                head=request.Head,runId=origin.RunId,host=origin.Host,command=origin.Command,filter=(string?)null}},
            nativeEvidence=captures.Values.OrderBy(x=>x.RelativePath,StringComparer.Ordinal)});
        return new(canonical.Serialize(result),captures.Values.OrderBy(x=>x.RelativePath,StringComparer.Ordinal).ToArray());
    }
}
