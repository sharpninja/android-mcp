using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public static class ReleaseCodecPlannerCases
{
    public static IEnumerable<object[]> Cases
    {
        get
        {
            (string,string)[] split=[("project-root","artifacts"),("source-artifact","sealed.bin"),("generation","parts"),("artifact-sha256",new string('a',64))];
            (string,string)[] assemble=[("project-root","artifacts"),("parts-manifest","parts/manifest.json"),("generation","assembly"),("destination-artifact","new.bin"),("manifest-sha256",new string('b',64))];
            var cases=new List<PlannerCase>();
            void Add(Operation operation,(string Key,string Value)[] parameters,string[] steps)
            {
                var edges=steps.Skip(1).Select((step,index)=>step+"<-"+steps[index]).ToArray();
                var key=new OperationKey(Category.Publish,operation);
                cases.Add(new(operation+"-required",PlannerContractCases.Request("Publish",operation.ToString(),parameters),key,steps,[],edges));
                cases.Add(new(operation+"-case",PlannerContractCases.Request("publish",operation.ToString().ToLowerInvariant(),parameters),key,steps,[],edges));
                foreach(var parameter in parameters)
                    cases.Add(new(operation+"-missing-"+parameter.Key,PlannerContractCases.Request("Publish",operation.ToString(),parameters.Where(value=>value.Key!=parameter.Key).ToArray()),null,[],["MISSING_PARAMETER:"+parameter.Key]));
                foreach(var parameter in new[]{"output-root","pipeline-request"})
                    cases.Add(new(operation+"-unsupported-"+parameter,PlannerContractCases.Request("Publish",operation.ToString(),parameters.Append((parameter,"unexpected")).ToArray()),null,[],["UNSUPPORTED_PARAMETER:"+parameter]));
            }
            Add(Operation.Split,split,["admit-release-input","split-parts","seal-parts-manifest"]);
            Add(Operation.Reassemble,assemble,["admit-parts-manifest","verify-parts","reassemble-artifact","seal-reassembly-receipt"]);
            cases.Add(new("Split-explicit-size",PlannerContractCases.Request("Publish","Split",split.Append(("part-bytes","377")).ToArray()),new(Category.Publish,Operation.Split),
                ["admit-release-input","split-parts","seal-parts-manifest"],[],["split-parts<-admit-release-input","seal-parts-manifest<-split-parts"]));
            return cases.Select(test=>new object[]{test});
        }
    }
}

[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-007")]
public sealed class ReleaseCodecPlannerMockTests
{
    [Theory,MemberData(nameof(ReleaseCodecPlannerCases.Cases),MemberType=typeof(ReleaseCodecPlannerCases))]
    public void CorrectIndependentPlansPassSharedAssertions(PlannerCase test)=>new PlannerMockGateTests().CorrectMocksPassEveryAssertion(test);
    [Theory,MemberData(nameof(ReleaseCodecPlannerCases.Cases),MemberType=typeof(ReleaseCodecPlannerCases))]
    public void EachPlanErrorAndEffectControlIsRejected(PlannerCase test)
    {
        var harness=new PlannerMockGateTests();harness.WrongEffectsAreRejectedInsidePassingHarness(test);
        harness.WrongKeysPlansAndErrorsAreRejected(test);harness.IndividualKeyStepAndErrorMutationsAreRejected(test);
    }
}
