using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class SdkCausalDiagnosticsNativeTests
{
    [Fact] public void Same_native_cause_assertions_bind_existing_translator()
        =>SdkCausalDiagnosticsCases.AssertNativeCause(NativeTestGateSdkSessionFactory.Translate);
    [Fact] public void Same_known_admission_identity_assertions_bind_existing_translator()
        =>SdkCausalDiagnosticsCases.AssertKnownCause(NativeTestGateSdkSessionFactory.Translate);
    [Fact] public void Same_missing_stack_control_rejects_reduced_native_output()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertNativeCause(error=>
        {
            var native=NativeTestGateSdkSessionFactory.Translate(error);
            return new(native.Code,error.InnerException!.Message);
        }));
    [Fact] public void Same_wrong_code_control_rejects_changed_native_output()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertNativeCause(error=>
            new("SDK_PIN_MISSING",NativeTestGateSdkSessionFactory.Translate(error).Message)));
    [Fact] public void Same_lost_identity_control_rejects_changed_native_output()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertNativeCause(error=>
        {
            var native=NativeTestGateSdkSessionFactory.Translate(error);
            return new(native.Code,native.Message.Replace(SdkCausalDiagnosticsCases.Framework,"redacted-wrong-identity",StringComparison.Ordinal));
        }));
    [Fact] public void Same_replaced_known_error_control_rejects_changed_native_object()
        =>Assert.ThrowsAny<Exception>(()=>SdkCausalDiagnosticsCases.AssertKnownCause(error=>
        {
            var native=NativeTestGateSdkSessionFactory.Translate(error);return new(native.Code,native.Message);
        }));
}
