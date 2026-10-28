using System.Reflection;
using System.Runtime.CompilerServices;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

// Narrow diagnostic contract only. No SDK assembly is loaded by these cases.
public static class SdkCausalDiagnosticsCases
{
    public const string Framework="Microsoft.Build.Framework, Version=15.1.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
    public static Exception NativeFailure()
    {
        try{ThrowAtSdkResolutionBoundary();throw new InvalidOperationException("Unreachable.");}
        catch(FileNotFoundException cause){return new TargetInvocationException(cause);}
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowAtSdkResolutionBoundary()=>throw new FileNotFoundException("Could not load "+Framework,Framework);
    public static void AssertNativeCause(Func<Exception,TestGateFactsException> translate)
    {
        var error=translate(NativeFailure());
        Assert.Equal("SDK_EVALUATION_FAILED",error.Code);
        Assert.Contains(nameof(FileNotFoundException),error.Message);
        Assert.Contains(Framework,error.Message);
        Assert.Contains(nameof(ThrowAtSdkResolutionBoundary),error.Message);
    }
    public static void AssertKnownCause(Func<Exception,TestGateFactsException> translate)
    {
        var known=new TestGateFactsException("SDK_PIN_MISSING","Exact admitted dependency is missing.");
        Assert.Same(known,translate(new TargetInvocationException(known)));
    }
    public static TestGateFactsException ScriptedCorrect(Exception error)
    {
        for(Exception? current=error;current is not null;current=current.InnerException)
            if(current is TestGateFactsException known)return known;
        var inner=error is TargetInvocationException {InnerException:{} cause} ? cause : error;
        return new("SDK_EVALUATION_FAILED","Native SDK session rejected: "+inner);
    }
}
public sealed class SdkCausalDiagnosticsMockTests
{
    [Fact] public void Correct_mock_preserves_native_identity_and_original_resolution_stack()
        =>SdkCausalDiagnosticsCases.AssertNativeCause(SdkCausalDiagnosticsCases.ScriptedCorrect);
    [Fact] public void Known_admission_error_object_and_code_are_preserved()
        =>SdkCausalDiagnosticsCases.AssertKnownCause(SdkCausalDiagnosticsCases.ScriptedCorrect);
    [Fact] public void Message_only_translation_is_rejected()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertNativeCause(error=>
            new("SDK_EVALUATION_FAILED",error.InnerException!.Message)));
    [Fact] public void Wrong_error_code_is_rejected()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertNativeCause(error=>
            new("SDK_PIN_MISSING",error.InnerException!.ToString())));
    [Fact] public void Lost_framework_identity_is_rejected()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertNativeCause(error=>
            new("SDK_EVALUATION_FAILED",error.InnerException!.ToString().Replace(SdkCausalDiagnosticsCases.Framework,"redacted-wrong-identity",StringComparison.Ordinal))));
    [Fact] public void Replacing_the_existing_known_error_is_rejected()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertKnownCause(error=>
            new("SDK_PIN_MISSING",error.InnerException!.Message)));
}
