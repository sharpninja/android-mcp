using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record GradleProjectionParameterCase(string Id,OperationRequest Request,IReadOnlyList<PlanError> Errors)
{public override string ToString()=>Id;}
public static class GradleProjectionParameterCases
{
    // Root appends this already-approved enum declaration before mock discovery.
    public static OperationKey Key=>new(Category.Design,Enum.Parse<Operation>("GradleProjection"));
    public static PlannedStep[] Steps=>[new("emit-gradle-projection",[])];
    public static IEnumerable<object[]> Cases
    {
        get
        {
            Dictionary<string,string> Args()=>new(StringComparer.Ordinal){["output-root"]="/fixture/gradle-projection"};
            var rows=new List<GradleProjectionParameterCase>{new("EMIT-REQUIRED",new("Design","GradleProjection",Args()),[])};
            var optional=Args();optional.Add("generation","fixture-projection");rows.Add(new("EMIT-OPTIONAL",new("Design","GradleProjection",optional),[]));
            rows.Add(new("EMIT-MISSING",new("Design","GradleProjection",new Dictionary<string,string>()),[new("MISSING_PARAMETER","output-root","Supply --output-root.")]));
            var empty=Args();empty["output-root"]=" ";rows.Add(new("EMIT-EMPTY",new("Design","GradleProjection",empty),[new("MISSING_PARAMETER","output-root","Supply --output-root.")]));
            foreach(var key in new[]{"project-root","tool-access","design-aliases"})
            {var args=Args();args.Add(key,"unneeded");rows.Add(new("EMIT-UNSUPPORTED-"+key,new("Design","GradleProjection",args),[new("UNSUPPORTED_PARAMETER",key,"Not accepted by Design/GradleProjection.")]));}
            foreach(var category in new[]{"Sources","Validate"})rows.Add(new("EMIT-ILLEGAL-"+category,new(category,"GradleProjection",Args()),[new("ILLEGAL_CATEGORY_OPERATION","",category+"/GradleProjection")]));
            rows.Add(new("EMIT-UNKNOWN",new("Design","UnknownGradleProjection",Args()),[new("UNKNOWN_OPERATION","","UnknownGradleProjection")]));
            return rows.Select(test=>new object[]{test});
        }
    }
    public static void AssertPlan(GradleProjectionParameterCase test,IOperationPlanner provider)
    {
        var before=test.Request.Parameters.OrderBy(pair=>pair.Key,StringComparer.Ordinal).ToArray();var result=provider.Plan(test.Request);
        Assert.Empty(provider.ObservedEffects);Assert.Equal(before,test.Request.Parameters.OrderBy(pair=>pair.Key,StringComparer.Ordinal));
        Assert.Equal(test.Errors,result.Errors);Assert.Equal(test.Errors.Count==0,result.IsValid);
        if(test.Errors.Count!=0){Assert.Null(result.Plan);return;}
        Assert.NotNull(result.Plan);Assert.Equal(Key,result.Plan.Key);var step=Assert.Single(result.Plan.Steps);Assert.Equal("emit-gradle-projection",step.Id);Assert.Empty(step.DependsOn);
    }
}
public sealed class ScriptedGradleProjectionPlanner(GradleProjectionParameterCase test,string mutation=""):IOperationPlanner
{
    public IReadOnlyList<string> ObservedEffects=>mutation=="effects"?["write-file"]:[];
    public PlanningResult Plan(OperationRequest request)
    {
        Assert.Same(test.Request,request);
        if(mutation=="outcome")return test.Errors.Count==0?new(null,[new("WRONG","","")]):new(new(GradleProjectionParameterCases.Key,GradleProjectionParameterCases.Steps),[]);
        if(test.Errors.Count!=0)
        {
            var errors=test.Errors.ToArray();if(mutation=="code")errors[0]=errors[0] with{Code="WRONG"};if(mutation=="parameter")errors[0]=errors[0] with{Parameter="wrong"};if(mutation=="message")errors[0]=errors[0] with{Message="wrong"};return new(null,errors);
        }
        return new(new(mutation=="operation"?new(Category.Design,Operation.Assets):GradleProjectionParameterCases.Key,
            [new(mutation=="step"?"wrong":"emit-gradle-projection",mutation=="dependency"?["invented-graph"]:[])]),[]);
    }
}
public sealed class GradleProjectionRecipeEnvironment(bool cancelled=false)
{
    public DesignGradleProjectionCase Input {get;}=DesignGradleCliPlanningCases.ProjectionCase();
    public List<string> Trace {get;}=[];
    public CancellationTokenSource Cancellation {get;}=new();
    public bool Cancelled {get;}=cancelled;
    public IReadOnlyList<Artifact> Expected=>DesignGradleProjectionCases.AsArtifacts(DesignGradleProjectionCases.Expected(Input));
}
public static class GradleProjectionRecipeCases
{
    public static IEnumerable<object[]> Cases=>new[]{false,true}.Select(cancelled=>new object[]{cancelled});
    public static async Task AssertRecipe(GradleProjectionRecipeEnvironment e,Func<ArtifactGenerationRecipe> provider)
    {
        if(e.Cancelled)e.Cancellation.Cancel();
        var recipe=provider();Assert.Empty(recipe.Sources);Assert.Empty(e.Trace);
        var prepared=recipe.Prepare(Array.Empty<Artifact>());Assert.Equal(new[]{"projection"},e.Trace);
        Assert.Equal(e.Expected.Select(artifact=>artifact.RelativePath),prepared.OutputPaths);Assert.Null(prepared.VerifyBeforeCommitAsync);
        IReadOnlyList<Artifact>? result=null;var error=await Record.ExceptionAsync(async()=>result=await prepared.MaterializeAsync(e.Cancellation.Token));
        if(e.Cancelled){Assert.IsAssignableFrom<OperationCanceledException>(error);Assert.Null(result);}
        else {Assert.Null(error);Assert.NotNull(result);DesignDocumentationStageCases.AssertArtifacts(e.Expected,result);}
        Assert.Equal(new[]{"projection"},e.Trace);
    }
}
public sealed class ScriptedGradleProjectionRecipe(GradleProjectionRecipeEnvironment e,string mutation="")
{
    public ArtifactGenerationRecipe Create()
    {
        if(mutation=="early")e.Trace.Add("projection");
        return new(mutation=="sources"?[new("unneeded-input","text/plain")]:[],inputs=>
        {
            Assert.Empty(inputs);e.Trace.Add("projection");var expected=e.Expected;
            return new(mutation=="paths"?[]:expected.Select(a=>a.RelativePath).ToArray(),token=>
            {
                if(mutation!="cancel")token.ThrowIfCancellationRequested();
                var result=expected.ToArray();
                if(mutation=="media")result[0]=result[0] with{MediaType="wrong"};
                if(mutation=="bytes")result[0]=result[0] with{Bytes=new byte[]{0}};
                if(mutation=="missing")result=result.Skip(1).ToArray();
                if(mutation=="extra")result=result.Append(new Artifact("unneeded.txt",Array.Empty<byte>(),"text/plain")).ToArray();
                return Task.FromResult<IReadOnlyList<Artifact>>(result);
            });
        });
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class GradleProjectionEmissionMockTests
{
    [Theory,MemberData(nameof(GradleProjectionParameterCases.Cases),MemberType=typeof(GradleProjectionParameterCases))]
    public void CorrectNoInputProjectionParameterPlansPass(GradleProjectionParameterCase test)=>GradleProjectionParameterCases.AssertPlan(test,new ScriptedGradleProjectionPlanner(test));
    [Theory,MemberData(nameof(GradleProjectionParameterCases.Cases),MemberType=typeof(GradleProjectionParameterCases))]
    public void EveryWrongParameterOutcomeIsRejected(GradleProjectionParameterCase test)=>Assert.NotNull(Record.Exception(()=>GradleProjectionParameterCases.AssertPlan(test,new ScriptedGradleProjectionPlanner(test,"outcome"))));
    [Theory]
    [InlineData("operation")][InlineData("step")][InlineData("dependency")][InlineData("effects")]
    public void EverySuccessAndEarlyEffectFaultIsRejected(string mutation)
    {var test=(GradleProjectionParameterCase)GradleProjectionParameterCases.Cases.First()[0];Assert.NotNull(Record.Exception(()=>GradleProjectionParameterCases.AssertPlan(test,new ScriptedGradleProjectionPlanner(test,mutation))));}
    [Theory]
    [InlineData("code")][InlineData("parameter")][InlineData("message")][InlineData("effects")]
    public void EveryFailureAndEarlyEffectFaultIsRejected(string mutation)
    {var test=GradleProjectionParameterCases.Cases.Select(row=>(GradleProjectionParameterCase)row[0]).First(test=>test.Errors.Count!=0);Assert.NotNull(Record.Exception(()=>GradleProjectionParameterCases.AssertPlan(test,new ScriptedGradleProjectionPlanner(test,mutation))));}
    [Theory,MemberData(nameof(GradleProjectionRecipeCases.Cases),MemberType=typeof(GradleProjectionRecipeCases))]
    public Task CorrectTwoArtifactNoInputRecipeAndCancellationPass(bool cancelled)
    {var e=new GradleProjectionRecipeEnvironment(cancelled);return GradleProjectionRecipeCases.AssertRecipe(e,new ScriptedGradleProjectionRecipe(e).Create);}
    [Theory]
    [InlineData("sources")][InlineData("paths")][InlineData("media")][InlineData("bytes")][InlineData("missing")][InlineData("extra")][InlineData("early")][InlineData("cancel")]
    public async Task EachProjectionRecipeFaultIsRejected(string mutation)
    {var e=new GradleProjectionRecipeEnvironment(mutation=="cancel");Assert.NotNull(await Record.ExceptionAsync(()=>GradleProjectionRecipeCases.AssertRecipe(e,new ScriptedGradleProjectionRecipe(e,mutation).Create)));}
}
