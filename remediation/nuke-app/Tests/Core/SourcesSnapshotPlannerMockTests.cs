using AndroidMcp.BuildTool.Core;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;

public static class SourcesSnapshotPlannerCases
{
    public static readonly string[] PinGroup=["pipeline-request","pipeline-request-sha256","pipeline-request-length"];
    public static readonly (string Key,string Value)[] Required=[("project-root","inputs"),("source-manifest","recipe.json"),("recipe-lock","recipe.lock.json"),("output-root","raw")];
    public static IEnumerable<object[]> Cases
    {
        get
        {
            PlannerCase Case(string id,IEnumerable<(string Key,string Value)> args,string[] errors,string category="Sources",string operation="Snapshot")=>
                new(id,PlannerContractCases.Request(category,operation,args.ToArray()),errors.Length==0?new(Category.Sources,Operation.Snapshot):null,errors.Length==0?["source-snapshot"]:[],errors);
            yield return [Case("default",Required,[])];
            yield return [Case("case",Required,[],"sources","snapshot")];
            yield return [Case("missing-all",[],Required.Select(p=>"MISSING_PARAMETER:"+p.Key).ToArray())];
            foreach(var missing in Required)yield return [Case("missing-"+missing.Key,Required.Where(p=>p.Key!=missing.Key),["MISSING_PARAMETER:"+missing.Key])];
            yield return [Case("all-options",Required.Concat(new[]{("generation","snapshot-1"),("release","release-011"),("snapshot-mode","Fetch"),("download","True"),("bundle-root","bundle"),("acquisition-profile","GenericRehydrate")}),[])];
            foreach(var unsupported in new[]{"limit","cache-policy","fetch-delay-seconds"})yield return [Case("unsupported-"+unsupported,Required.Append((unsupported,"1")),["UNSUPPORTED_PARAMETER:"+unsupported])];
            for(var mask=0;mask<8;mask++)
            {
                var selected=PinGroup.Where((_,i)=>(mask&(1<<i))!=0).Select(key=>(key,"pin"));
                var missing=mask is 0 or 7?[]:PinGroup.Where((_,i)=>(mask&(1<<i))==0).Select(key=>"MISSING_PARAMETER:"+key).ToArray();
                yield return [Case("trio-mask-"+mask,Required.Concat(selected),missing)];
            }
            foreach(var blank in PinGroup)yield return [Case("trio-blank-"+blank,Required.Concat(PinGroup.Select(key=>(key,key==blank?" ":"pin"))),["MISSING_PARAMETER:"+blank])];
        }
    }
}
[Trait("Gate","Mock")]
public sealed class SourcesSnapshotPlannerMockTests
{
    [Theory,MemberData(nameof(SourcesSnapshotPlannerCases.Cases),MemberType=typeof(SourcesSnapshotPlannerCases))]
    public void CorrectDeclaredRouteAndGroupCases(PlannerCase test)=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(PlannerContractCases.Correct(test)));
    [Theory,MemberData(nameof(SourcesSnapshotPlannerCases.Cases),MemberType=typeof(SourcesSnapshotPlannerCases))]
    public void IndependentPlanErrorsAndEffectsDiscriminate(PlannerCase test)
    {
        var expected=PlannerContractCases.Correct(test);
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected,"premature-effect"))));
        Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Errors=[new("WRONG","","wrong")]}))));
        if(expected.Plan is not null){Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=expected.Plan with{Steps=[]}}))));Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(expected with{Plan=expected.Plan with{Key=new(Category.Publish,Operation.Check)}}))));}
        else Assert.NotNull(Record.Exception(()=>PlannerContractCases.AssertContract(test,new ScriptedPlanner(new(new(new(Category.Sources,Operation.Snapshot),[new("source-snapshot",[])]),[])))));
    }
}
