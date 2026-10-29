using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public static class SdkScopeDiagnosticsCases
{
    public static IEnumerable<object[]> Data=>[
        ["Native path is outside the admitted participating roots.","/unadmitted/selected/SDK.props","ImportedProject","/fixture/app/Host.csproj"],
        ["Native path traverses a linked or escaped input.","/fixture/app/link/Shared.props","ImportingProject","/fixture/app/Tests/Tests.csproj"],
        ["Native path is not absolute.","relative.csproj","Project","relative.csproj"],
        ["Native path is outside the admitted participating roots.","/unadmitted/selected/rules.ruleset","CodeAnalysisRuleSet","/fixture/app/Host.csproj"]
    ];
    public static string Message(string reason,string path,string role,string project)
        =>"Native SDK session rejected: "+reason+" EvaluatingProject="+project+"; InputRole="+role+"; InputPath="+path;
    public static void AssertDiagnostic(TestGateFactsException actual,string reason,string path,string role,string project)
    {Assert.Equal("INPUT_SCOPE_VIOLATION",actual.Code);Assert.Equal(Message(reason,path,role,project),actual.Message);}
}
public sealed class SdkScopeDiagnosticsMockTests
{
    [Theory,MemberData(nameof(SdkScopeDiagnosticsCases.Data),MemberType=typeof(SdkScopeDiagnosticsCases))]
    public void Private_scope_diagnostic_preserves_original_code_and_exact_causal_input(string reason,string path,string role,string project)
    {
        var mock=new TestGateFactsException("INPUT_SCOPE_VIOLATION",SdkScopeDiagnosticsCases.Message(reason,path,role,project));
        SdkScopeDiagnosticsCases.AssertDiagnostic(mock,reason,path,role,project);
    }
    [Theory,MemberData(nameof(SdkScopeDiagnosticsCases.Data),MemberType=typeof(SdkScopeDiagnosticsCases))]
    public void Wrong_code_and_each_missing_causal_field_are_rejected(string reason,string path,string role,string project)
    {
        var invalid=new[]{new TestGateFactsException("SOURCE_INPUTS_UNRESOLVED",SdkScopeDiagnosticsCases.Message(reason,path,role,project)),
            new TestGateFactsException("INPUT_SCOPE_VIOLATION",SdkScopeDiagnosticsCases.Message(reason,"",role,project)),
            new TestGateFactsException("INPUT_SCOPE_VIOLATION",SdkScopeDiagnosticsCases.Message(reason,path,"",project)),
            new TestGateFactsException("INPUT_SCOPE_VIOLATION",SdkScopeDiagnosticsCases.Message(reason,path,role,""))};
        foreach(var actual in invalid)Assert.ThrowsAny<Exception>(()=>SdkScopeDiagnosticsCases.AssertDiagnostic(actual,reason,path,role,project));
    }
}
