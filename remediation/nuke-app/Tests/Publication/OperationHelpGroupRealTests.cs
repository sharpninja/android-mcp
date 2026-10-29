using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Publication;
[Trait("Gate","Real")]
public sealed class OperationHelpGroupRealTests
{
    [Theory,MemberData(nameof(OperationHelpGroupCases.Cases),MemberType=typeof(OperationHelpGroupCases))]
    public void SameDeclaredGroupAndOmission(HelpGroupCase c)=>OperationHelpGroupCases.AssertCase(c,new OperationHelpBuilder(new CanonicalJson()));
}
