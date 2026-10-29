using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

public sealed record RuleCase(string Effect, string Schema, string[] Targets, int MaxItems);
public sealed record BindingCase(string Binding, string Schema, string Target, object Items);
public sealed record PolicyInput(BindingCase[] Plan, IReadOnlyDictionary<string,RuleCase> Rules, RuleCase? Entry,
    string EntrySchema, bool Authority, bool Emergency = false);
public sealed record PolicyCase(string Id, PolicyInput Input, string Decision);
public interface IPolicyCaseProvider { string Evaluate(PolicyInput input); }

public static class PolicyCases
{
    public static PolicyInput Input(string effect="allow", string entry="allow", bool authority=true, bool emergency=false, object? items=null) =>
        new([new("binding","s","t",items ?? 2)],new Dictionary<string,RuleCase>{["binding"]=new(effect,"s",["t"],3)},new(entry,"tool",["t"],3),"tool",authority,emergency);
    public static IEnumerable<object[]> All
    {
        get
        {
            foreach(var effect in new[]{"deny","ask","allow"})
            foreach(var entry in new[]{"deny","ask","allow"})
            foreach(var authority in new[]{false,true})
            foreach(var emergency in new[]{false,true})
            {
                var expected=emergency || effect=="deny" || entry=="deny" ? "deny" : !authority ? "os-unavailable" : effect=="ask" || entry=="ask" ? "ask" : "admit-candidate";
                yield return [new PolicyCase($"POL-{effect}-{entry}-{authority}-{emergency}",Input(effect,entry,authority,emergency),expected)];
            }
            foreach(var value in new object[]{-1,true,2.0,4,"2"})
                yield return [new PolicyCase($"POL-bad-items-{value.GetType().Name}-{value}",Input(items:value),"deny")];
            var input=Input();
            yield return [new PolicyCase("POL-empty-plan",input with {Plan=[]},"deny")];
            yield return [new PolicyCase("POL-missing-entry",input with {Entry=null},"deny")];
            yield return [new PolicyCase("POL-stale-entry",input with {EntrySchema="stale"},"deny")];
            yield return [new PolicyCase("POL-missing-binding",input with {Rules=new Dictionary<string,RuleCase>()},"deny")];
            yield return [new PolicyCase("POL-stale-binding",input with {Plan=[new("binding","stale","t",1)]},"deny")];
            yield return [new PolicyCase("POL-outside-target",input with {Plan=[new("binding","s","other",1)]},"deny")];
            yield return [new PolicyCase("POL-entry-max",input with {Entry=new("allow","tool",["t"],1)},"deny")];
            yield return [new PolicyCase("POL-unknown-effect",input with {Entry=new("future","tool",["t"],3)},"deny")];
        }
    }
    public static void AssertCase(PolicyCase test,IPolicyCaseProvider provider) => Assert.Equal(test.Decision,provider.Evaluate(test.Input));
}
public sealed class ScriptedPolicy(PolicyInput expected,string decision):IPolicyCaseProvider
{
    public bool Called { get; private set; }
    public string Evaluate(PolicyInput input) { Assert.Same(expected,input);Called=true;return decision; }
}
[Trait("Gate","Mock")]
public sealed class PolicyMockGateTests
{
    [Theory,MemberData(nameof(PolicyCases.All),MemberType=typeof(PolicyCases))]
    public void CorrectResponsesPass(PolicyCase test)
    {
        var fake=new ScriptedPolicy(test.Input,test.Decision);PolicyCases.AssertCase(test,fake);Assert.True(fake.Called);
    }
    [Theory,MemberData(nameof(PolicyCases.All),MemberType=typeof(PolicyCases))]
    public void EveryWrongDecisionRejected(PolicyCase test)
    {
        foreach(var wrong in new[]{"deny","ask","admit-candidate","os-unavailable","allow"}.Where(x=>x!=test.Decision))
            Assert.NotNull(Record.Exception(()=>PolicyCases.AssertCase(test,new ScriptedPolicy(test.Input,wrong))));
    }
}
