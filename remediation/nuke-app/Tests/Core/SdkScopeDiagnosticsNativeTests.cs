using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class SdkScopeDiagnosticsNativeTests
{
    [Theory,MemberData(nameof(SdkScopeDiagnosticsCases.Data),MemberType=typeof(SdkScopeDiagnosticsCases))]
    public void Same_private_refusal_cases_bind_to_native_diagnostic(string reason,string path,string role,string project)
        =>SdkScopeDiagnosticsCases.AssertDiagnostic(NativeTestGateSdkSessionFactory.ScopeFailure(reason,path,role,project),reason,path,role,project);
}
