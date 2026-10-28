using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record ArchitectureCliPlanningCase(string Id,OperationRequest Request,IReadOnlyList<PlanError> Errors)
{public override string ToString()=>Id;}
public static class ArchitectureCliPlanningCases
{
    public static readonly string[] Required=["project-root","output-root","cache-root","work-root","receipt-root","docker-identity","renderer-user","tool-access"];
    public static readonly PlannedStep[] Steps=[new("validate-architecture-inputs",[]),new("preflight-native-context",["validate-architecture-inputs"]),new("verify-pinned-tools",["preflight-native-context"]),new("generate-architecture",["verify-pinned-tools"]),new("seal-generation",["generate-architecture"])];
    public static Dictionary<string,string> Parameters()=>new(StringComparer.Ordinal)
    {
        ["project-root"]="/fixture/design",["output-root"]="/fixture/output",["cache-root"]="/fixture/design",
        ["work-root"]="/fixture/work",["receipt-root"]="/fixture/receipts",["docker-identity"]="/fixture/docker-tool.json",
        ["renderer-user"]="1000:1000",["tool-access"]="Online"
    };
    public static IEnumerable<object[]> Data
    {
        get
        {
            var result=new List<ArchitectureCliPlanningCase>{new("ARCH-CLI-REQUIRED",new("Design","Architecture",Parameters()),[])};
            var optional=Parameters();optional["tool-access"]="Offline";optional.Add("generation","architecture-fixture");optional.Add("offline-gradle-checksum-proof","proof/gradle.json");
            result.Add(new("ARCH-CLI-OPTIONAL",new("Design","Architecture",optional),[]));
            result.Add(new("ARCH-CLI-ALL-MISSING",new("Design","Architecture",new Dictionary<string,string>()),Required.Select(key=>new PlanError("MISSING_PARAMETER",key,"Supply --"+key+".")).ToArray()));
            foreach(var key in Required)
            {var parameters=Parameters();parameters.Remove(key);result.Add(new("ARCH-CLI-MISSING-"+key,new("Design","Architecture",parameters),[new("MISSING_PARAMETER",key,"Supply --"+key+".")]));}
            foreach(var key in new[]{"docker-identity","tool-access"})
            {var parameters=Parameters();parameters[key]=" ";result.Add(new("ARCH-CLI-EMPTY-"+key,new("Design","Architecture",parameters),[new("MISSING_PARAMETER",key,"Supply --"+key+".")]));}
            foreach(var key in new[]{"compiler-identity","operations","unknown-option"})
            {var parameters=Parameters();parameters.Add(key,"unexpected");result.Add(new("ARCH-CLI-UNSUPPORTED-"+key,new("Design","Architecture",parameters),[new("UNSUPPORTED_PARAMETER",key,"Not accepted by Design/Architecture.")]));}
            foreach(var category in new[]{"Validate","Sources"})
                result.Add(new("ARCH-CLI-ILLEGAL-"+category,new(category,"Architecture",Parameters()),[new("ILLEGAL_CATEGORY_OPERATION","",category+"/Architecture")]));
            return result.Select(test=>new object[]{test});
        }
    }
    public static void AssertPlan(ArchitectureCliPlanningCase test,IOperationPlanner provider)
    {
        var before=test.Request.Parameters.OrderBy(p=>p.Key,StringComparer.Ordinal).ToArray();var result=provider.Plan(test.Request);
        Assert.Empty(provider.ObservedEffects);Assert.Equal(before,test.Request.Parameters.OrderBy(p=>p.Key,StringComparer.Ordinal));
        Assert.Equal(test.Errors.Count==0,result.IsValid);Assert.Equal(test.Errors,result.Errors);
        if(test.Errors.Count>0){Assert.Null(result.Plan);return;}
        Assert.NotNull(result.Plan);Assert.Equal(new OperationKey(Category.Design,Operation.Architecture),result.Plan.Key);
        Assert.Equal(Steps.Select(s=>s.Id),result.Plan.Steps.Select(s=>s.Id));
        for(var index=0;index<Steps.Length;index++)Assert.Equal(Steps[index].DependsOn,result.Plan.Steps[index].DependsOn);
    }
}
public sealed class ScriptedArchitectureCliPlanner(ArchitectureCliPlanningCase test,string mutation=""):IOperationPlanner
{
    public IReadOnlyList<string> ObservedEffects=>mutation=="effects"?["read-file"]:[];
    public PlanningResult Plan(OperationRequest request)
    {
        Assert.Same(test.Request,request);var errors=test.Errors.ToArray();
        if(mutation=="outcome")return errors.Length==0?new(null,[new("WRONG","","")]):new(new(new(Category.Design,Operation.Architecture),ArchitectureCliPlanningCases.Steps),[]);
        if(errors.Length>0)
        {
            if(mutation=="error-code")errors[0]=errors[0] with{Code="WRONG"};
            if(mutation=="error-parameter")errors[0]=errors[0] with{Parameter="wrong"};
            if(mutation=="error-message")errors[0]=errors[0] with{Message="wrong"};
            return new(null,errors);
        }
        var steps=ArchitectureCliPlanningCases.Steps.Select(s=>new PlannedStep(s.Id,s.DependsOn.ToArray())).ToArray();
        if(mutation=="step-id")steps[0]=steps[0] with{Id="wrong"};
        if(mutation=="step-dependency")steps[1]=steps[1] with{DependsOn=[]};
        if(mutation=="step-count")steps=steps.Skip(1).ToArray();
        return new(new(new(Category.Design,mutation=="operation"?Operation.Assets:Operation.Architecture),steps),[]);
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitectureCliPlanningMockTests
{
    [Theory,MemberData(nameof(ArchitectureCliPlanningCases.Data),MemberType=typeof(ArchitectureCliPlanningCases))]
    public void CorrectArchitectureCliPlanningMocksPass(ArchitectureCliPlanningCase test)=>ArchitectureCliPlanningCases.AssertPlan(test,new ScriptedArchitectureCliPlanner(test));
    [Theory,MemberData(nameof(ArchitectureCliPlanningCases.Data),MemberType=typeof(ArchitectureCliPlanningCases))]
    public void EveryWrongPlanningOutcomeIsRejected(ArchitectureCliPlanningCase test)=>Assert.NotNull(Record.Exception(()=>ArchitectureCliPlanningCases.AssertPlan(test,new ScriptedArchitectureCliPlanner(test,"outcome"))));
    [Theory]
    [InlineData("operation")][InlineData("step-id")][InlineData("step-dependency")][InlineData("step-count")][InlineData("effects")]
    public void EachSuccessFieldRejectsItsOwnWrongResponse(string mutation){var test=(ArchitectureCliPlanningCase)ArchitectureCliPlanningCases.Data.First()[0];Assert.NotNull(Record.Exception(()=>ArchitectureCliPlanningCases.AssertPlan(test,new ScriptedArchitectureCliPlanner(test,mutation))));}
    [Theory]
    [InlineData("error-code")][InlineData("error-parameter")][InlineData("error-message")][InlineData("effects")]
    public void EachFailureFieldRejectsItsOwnWrongResponse(string mutation){var test=ArchitectureCliPlanningCases.Data.Select(row=>(ArchitectureCliPlanningCase)row[0]).First(test=>test.Errors.Count>0);Assert.NotNull(Record.Exception(()=>ArchitectureCliPlanningCases.AssertPlan(test,new ScriptedArchitectureCliPlanner(test,mutation))));}
}
