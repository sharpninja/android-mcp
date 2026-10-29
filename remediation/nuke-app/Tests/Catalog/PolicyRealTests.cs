using AndroidMcp.BuildTool.Catalog;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed class NativePolicyProvider:IPolicyCaseProvider
{
    private static PolicyRule Rule(RuleCase rule)=>new(rule.Effect,rule.Schema,new HashSet<string>(rule.Targets,StringComparer.Ordinal),rule.MaxItems);
    public string Evaluate(PolicyInput input)=>PolicyReference.Evaluate(
        input.Plan.Select(x=>new CanonicalBinding(x.Binding,x.Schema,x.Target,x.Items)).ToArray(),
        input.Rules.ToDictionary(x=>x.Key,x=>Rule(x.Value),StringComparer.Ordinal),
        input.Entry is null ? null : Rule(input.Entry),input.EntrySchema,input.Authority,input.Emergency);
}

[Trait("Gate","Real")]
public sealed class PolicyRealTests
{
    [Theory,MemberData(nameof(PolicyCases.All),MemberType=typeof(PolicyCases))]
    public void NativePolicyUsesSameCasesAndAssertions(PolicyCase test)=>PolicyCases.AssertCase(test,new NativePolicyProvider());
}
