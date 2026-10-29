using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Core;

// Proposed shared root-owned seam, not a parser/runner implementation.
public sealed record TrxAdmissionRequest(ReadOnlyMemory<byte>? TrxBytes,IReadOnlyList<string> ExpectedClasses);
public sealed record TrxRunCounters(int Total,int Executed,int Passed,int Failed,int NotExecuted);
public sealed record TrxAdmissionResult(bool Admitted,string Code,TrxRunCounters? Counters,int ResultCount,
    IReadOnlyDictionary<string,int> ClassCounts,IReadOnlyList<string> MissingExpectedClasses,string? RawTrxSha256);
public interface ITrxResultAdmission { TrxAdmissionResult Evaluate(TrxAdmissionRequest request); }

public enum TestBuildStepKind { ForceHostBuild,ForceTestsBuild,TestWithoutBuild }
public sealed record TestBuildStep(TestBuildStepKind Kind,string Project,bool NoIncremental,
    bool NoRestore,bool NoBuild,bool BuildProjectReferences);
public sealed record TestBuildPlanRequest(string HostProject,string TestsProject,ToolIdentity DotNet);
public sealed record TestBuildPlan(ToolIdentity DotNet,IReadOnlyList<TestBuildStep> Steps);
public sealed record TestBuildEvidence(IReadOnlyList<TestBuildStep> ExecutedSteps,string CurrentSourceFingerprint,
    string BuiltSourceFingerprint,string FinalSourceFingerprint,string FreshHostSha256,Guid FreshHostModuleId,
    Guid RunningHostModuleId,string TestsHostReferenceSha256,string FreshTestsSha256,string ExecutedTestsSha256);
public sealed record TestBuildAdmission(bool Admitted,string Code);
public interface ITestBuildPlanAdmission
{
    TestBuildPlan Plan(TestBuildPlanRequest request);
    TestBuildAdmission Evaluate(TestBuildPlan plan,TestBuildEvidence evidence);
}
