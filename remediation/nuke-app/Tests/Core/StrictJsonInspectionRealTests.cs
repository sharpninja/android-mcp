using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

[Trait("Gate", "Real")]
[Trait("Requirement", "TEST-BUILD-004")]
[Trait("FunctionalRequirement", "FR-BUILD-004")]
public sealed class StrictJsonInspectionRealTests
{
    [Theory, MemberData(nameof(StrictJsonInspectionCases.ByteCases), MemberType = typeof(StrictJsonInspectionCases))]
    public void NativeBytesRetainTheSameRawValuesDiagnosticsAndCanonicalRanks(string id) =>
        StrictJsonInspectionCases.AssertByte(StrictJsonInspectionCases.Get(id), new StrictJsonInspector());

    [Theory, MemberData(nameof(StrictJsonInspectionCases.ElementCases), MemberType = typeof(StrictJsonInspectionCases))]
    public void NativeElementsOwnTheirValueOutsideTheCallerLifetime(string id) =>
        StrictJsonInspectionCases.AssertElement(StrictJsonInspectionCases.Get(id), new StrictJsonInspector());
}
