using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using System.Text.Json.Nodes;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DesignGradleCliPlanningCase(string Id,OperationRequest Request,IReadOnlyList<PlanError> Errors)
{public override string ToString()=>Id;}
public static class DesignGradleCliPlanningCases
{
    public static readonly string[] Required=["project-root","output-root","cache-root","work-root","receipt-root","docker-identity","renderer-user","tool-access","design-aliases","design-registry-sha256"];
    public static readonly string[] Optional=["generation","offline-gradle-checksum-proof","catalog-root","policy-relative-path"];
    public static readonly PlannedStep[] Steps=[new("design-stage-union",[])];
    public static DesignGradleProjectionCase ProjectionCase()=>DesignGradleProjectionCases.Original() with
    {Operation=new(new(Category.Design,Operation.GradleCompatibility),"Design/GradleCompatibility",Required,Optional,Steps)};
    public static Dictionary<string,string> Parameters()=>new(StringComparer.Ordinal)
    {
        ["project-root"]="/fixture/design",["output-root"]="/fixture/final",["cache-root"]="/fixture/cache",
        ["work-root"]="/fixture/work",["receipt-root"]="/fixture/receipts",["docker-identity"]="/fixture/docker.json",
        ["renderer-user"]="1000:1000",["tool-access"]="Online",["design-aliases"]="extractCodeViews",
        ["design-registry-sha256"]=new DesignStagePlanner().Describe().Sha256
    };
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var rows=new List<DesignGradleCliPlanningCase>{new("DESIGN-GRADLE-REQUIRED",new("Design","GradleCompatibility",Parameters()),[])};
            var optional=Parameters();optional["design-aliases"]="exportArchitecture,packageDesignDocs";optional["tool-access"]="Offline";
            optional.Add("generation","fixture-union");optional.Add("offline-gradle-checksum-proof","proof/gradle.json");optional.Add("catalog-root","/fixture/catalog");optional.Add("policy-relative-path","tooling/design-source-policy.json");
            rows.Add(new("DESIGN-GRADLE-OPTIONAL",new("Design","GradleCompatibility",optional),[]));
            rows.Add(new("DESIGN-GRADLE-ALL-MISSING",new("Design","GradleCompatibility",new Dictionary<string,string>()),Required.Select(key=>new PlanError("MISSING_PARAMETER",key,"Supply --"+key+".")).ToArray()));
            foreach(var key in Required)
            {var input=Parameters();input.Remove(key);rows.Add(new("DESIGN-GRADLE-MISSING-"+key,new("Design","GradleCompatibility",input),[new("MISSING_PARAMETER",key,"Supply --"+key+".")]));}
            foreach(var key in new[]{"design-aliases","design-registry-sha256"})
            {var input=Parameters();input[key]=" ";rows.Add(new("DESIGN-GRADLE-EMPTY-"+key,new("Design","GradleCompatibility",input),[new("MISSING_PARAMETER",key,"Supply --"+key+".")]));}
            foreach(var key in new[]{"operations","compiler-identity","unknown-option"})
            {var input=Parameters();input.Add(key,"unexpected");rows.Add(new("DESIGN-GRADLE-UNSUPPORTED-"+key,new("Design","GradleCompatibility",input),[new("UNSUPPORTED_PARAMETER",key,"Not accepted by Design/GradleCompatibility.")]));}
            foreach(var category in new[]{"Sources","Validate"})rows.Add(new("DESIGN-GRADLE-ILLEGAL-"+category,new(category,"GradleCompatibility",Parameters()),[new("ILLEGAL_CATEGORY_OPERATION","",category+"/GradleCompatibility")]));
            return rows.Select(test=>new object[]{test});
        }
    }
    public static void AssertPlan(DesignGradleCliPlanningCase test,IOperationPlanner provider)
    {
        var before=test.Request.Parameters.OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToArray();var result=provider.Plan(test.Request);
        Assert.Empty(provider.ObservedEffects);Assert.Equal(before,test.Request.Parameters.OrderBy(pair=>pair.Key,StringComparer.Ordinal));
        Assert.Equal(test.Errors.Count==0,result.IsValid);Assert.Equal(test.Errors,result.Errors);
        if(test.Errors.Count>0){Assert.Null(result.Plan);return;}
        Assert.NotNull(result.Plan);Assert.Equal(new OperationKey(Category.Design,Operation.GradleCompatibility),result.Plan.Key);
        Assert.Equal(Steps.Select(step=>step.Id),result.Plan.Steps.Select(step=>step.Id));Assert.Empty(Assert.Single(result.Plan.Steps).DependsOn);
    }
}
public sealed class ScriptedDesignGradleCliPlanner(DesignGradleCliPlanningCase test,string mutation=""):IOperationPlanner
{
    public IReadOnlyList<string> ObservedEffects=>mutation=="effects"?["read-file"]:[];
    public PlanningResult Plan(OperationRequest request)
    {
        Assert.Same(test.Request,request);
        if(mutation=="outcome")return test.Errors.Count==0?new(null,[new("WRONG","","")]):new(new(new(Category.Design,Operation.GradleCompatibility),DesignGradleCliPlanningCases.Steps),[]);
        if(test.Errors.Count>0)
        {
            var errors=test.Errors.ToArray();if(mutation=="code")errors[0]=errors[0] with{Code="WRONG"};if(mutation=="parameter")errors[0]=errors[0] with{Parameter="wrong"};if(mutation=="message")errors[0]=errors[0] with{Message="wrong"};return new(null,errors);
        }
        PlannedStep[] steps=[new(mutation=="step"?"wrong":"design-stage-union",mutation=="dependency"?["invented-second-graph"]:[])];
        return new(new(new(Category.Design,mutation=="operation"?Operation.Architecture:Operation.GradleCompatibility),steps),[]);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleCliPlanningMockTests
{
    [Fact]
    public void GeneratedDelegateForwardsPolicyOnlyAsOptionalMetadata()
    {
        var test=DesignGradleCliPlanningCases.ProjectionCase();var expected=DesignGradleProjectionCases.Expected(test);
        DesignGradleProjectionCases.AssertProjection(test,new ScriptedDesignGradleProjection(expected));
        var descriptor=JsonNode.Parse(expected.Descriptor.Bytes.Span)!.AsObject();
        var policy=Assert.Single(descriptor["parameters"]!.AsArray(),node=>node!["name"]!.GetValue<string>()=="policy-relative-path")!;
        Assert.Equal("nukePolicyRelativePath",policy["property"]!.GetValue<string>());Assert.False(policy["required"]!.GetValue<bool>());
        Assert.Equal(new DesignStagePlanner().Describe().Metadata.Aliases.Select(alias=>alias.Name),descriptor["aliases"]!.AsArray().Select(node=>node!.GetValue<string>()));
        Assert.Equal(DesignGradleProjectionCases.Script().Bytes.ToArray(),expected.BuildScript.Bytes.ToArray());
    }
    [Theory,MemberData(nameof(DesignGradleCliPlanningCases.Cases),MemberType=typeof(DesignGradleCliPlanningCases))]
    public void CorrectCategoryParameterPlansPass(DesignGradleCliPlanningCase test)=>DesignGradleCliPlanningCases.AssertPlan(test,new ScriptedDesignGradleCliPlanner(test));
    [Theory,MemberData(nameof(DesignGradleCliPlanningCases.Cases),MemberType=typeof(DesignGradleCliPlanningCases))]
    public void EveryWrongPlanningOutcomeIsRejected(DesignGradleCliPlanningCase test)=>Assert.NotNull(Record.Exception(()=>DesignGradleCliPlanningCases.AssertPlan(test,new ScriptedDesignGradleCliPlanner(test,"outcome"))));
    [Theory]
    [InlineData("operation")][InlineData("step")][InlineData("dependency")][InlineData("effects")]
    public void EverySuccessFieldAndPrematureEffectIsRejected(string mutation)
    {var test=(DesignGradleCliPlanningCase)DesignGradleCliPlanningCases.Cases.First()[0];Assert.NotNull(Record.Exception(()=>DesignGradleCliPlanningCases.AssertPlan(test,new ScriptedDesignGradleCliPlanner(test,mutation))));}
    [Theory]
    [InlineData("code")][InlineData("parameter")][InlineData("message")][InlineData("effects")]
    public void EveryFailureFieldAndPrematureEffectIsRejected(string mutation)
    {var test=DesignGradleCliPlanningCases.Cases.Select(row=>(DesignGradleCliPlanningCase)row[0]).First(value=>value.Errors.Count>0);Assert.NotNull(Record.Exception(()=>DesignGradleCliPlanningCases.AssertPlan(test,new ScriptedDesignGradleCliPlanner(test,mutation))));}
}
