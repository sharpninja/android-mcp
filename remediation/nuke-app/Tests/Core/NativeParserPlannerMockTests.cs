using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
public static class NativeParserPlannerCases
{
    public static IEnumerable<object[]> Cases=>new[]{
        new PlannerCase("REG-NATIVE-01",PlannerContractCases.Request("Sources","NativeParser",("output-root","fresh"),("compiler-identity","compiler.json")),
            new(Category.Sources,Operation.NativeParser),["verify-compiler-identity","acquire-native-sources","build-native-parser","verify-native-parser"],[],
            ["acquire-native-sources<-verify-compiler-identity","build-native-parser<-acquire-native-sources","verify-native-parser<-build-native-parser"]),
        new PlannerCase("REG-NATIVE-02",PlannerContractCases.Request("Sources","NativeParser"),null,[],["MISSING_PARAMETER:compiler-identity","MISSING_PARAMETER:output-root"]),
        new PlannerCase("REG-NATIVE-03",PlannerContractCases.Request("Sources","NativeParser",("output-root","fresh"),("compiler-identity","compiler.json"),("shell-command","unsafe")),null,[],["UNSUPPORTED_PARAMETER:shell-command"])
    }.Select(x=>new object[]{x});
}
[Trait("Gate","Mock")]
public sealed class NativeParserPlannerMockTests
{
    [Theory,MemberData(nameof(NativeParserPlannerCases.Cases),MemberType=typeof(NativeParserPlannerCases))]
    public void CorrectMock(PlannerCase test)=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(NativeParserPlannerCases.Cases),MemberType=typeof(NativeParserPlannerCases))]
    public void PrematureEffectRejected(PlannerCase test)=>Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test),"network-before-validation"))));
}
