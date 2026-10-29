using System.Text.RegularExpressions;

namespace AndroidMcp.BuildTool.Core;

/// <summary>Pure contract over native build facts. The root producer must establish their authority.</summary>
public sealed class TestBuildPlanAdmission:ITestBuildPlanAdmission
{
    public TestBuildPlan Plan(TestBuildPlanRequest request)=>new(request.DotNet,
    [new(TestBuildStepKind.ForceHostBuild,request.HostProject,true,true,false,true),
     new(TestBuildStepKind.ForceTestsBuild,request.TestsProject,true,true,false,false),
     new(TestBuildStepKind.TestWithoutBuild,request.TestsProject,false,true,true,false)]);
    public TestBuildAdmission Evaluate(TestBuildPlan plan,TestBuildEvidence evidence)
    {
        if(plan.Steps.Count!=3)return new(false,"BUILD_PLAN_MISMATCH");
        var required=Plan(new(plan.Steps[0].Project,plan.Steps[1].Project,plan.DotNet));
        if(!required.Steps.SequenceEqual(plan.Steps))return new(false,"BUILD_PLAN_MISMATCH");
        if(!plan.Steps.SequenceEqual(evidence.ExecutedSteps))return new(false,"BUILD_PLAN_MISMATCH");
        if(!new[]{evidence.CurrentSourceFingerprint,evidence.BuiltSourceFingerprint,evidence.FinalSourceFingerprint,
                evidence.FreshHostSha256,evidence.TestsHostReferenceSha256,evidence.FreshTestsSha256,evidence.ExecutedTestsSha256}.All(Hash)||
            evidence.FreshHostModuleId==Guid.Empty||evidence.RunningHostModuleId==Guid.Empty)return new(false,"INVALID_BUILD_EVIDENCE");
        if(evidence.CurrentSourceFingerprint!=evidence.BuiltSourceFingerprint||evidence.CurrentSourceFingerprint!=evidence.FinalSourceFingerprint)
            return new(false,"SOURCE_CHANGED");
        if(evidence.FreshHostModuleId!=evidence.RunningHostModuleId)return new(false,"STALE_HOST");
        if(evidence.FreshHostSha256!=evidence.TestsHostReferenceSha256)return new(false,"STALE_TEST_REFERENCE");
        if(evidence.FreshTestsSha256!=evidence.ExecutedTestsSha256)return new(false,"STALE_TEST_BINARY");
        return new(true,"OK");
    }
    private static bool Hash(string value)=>!string.IsNullOrEmpty(value)&&Regex.IsMatch(value,"^[0-9a-fA-F]{64}$");
}
