using System.Globalization;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources;

// The same invocation guard binds original function bodies to scripted facts
// first, then to the ONE existing native producer after the mock gate.
public sealed class CallbackFunctionalOriginalReplay:ICallbackSourceFacts,IFunctionalTaxonomySourceFacts
{
    private readonly CallbackFunctionalOriginalCase test;
    private readonly OriginalFactMutation? mutation;
    private readonly ICallbackSourceFacts? callbacks;
    private readonly IFunctionalTaxonomySourceFacts? functional;
    private int index;
    private CallbackFunctionalOriginalReplay(CallbackFunctionalOriginalCase test,OriginalFactMutation? mutation,ICallbackSourceFacts? callbacks,IFunctionalTaxonomySourceFacts? functional)
    {this.test=test;this.mutation=mutation;this.callbacks=callbacks;this.functional=functional;}
    public static CallbackFunctionalOriginalRig Mock(CallbackFunctionalOriginalCase test,OriginalFactMutation? mutation=null)
    {var replay=new CallbackFunctionalOriginalReplay(test,mutation,null,null);return new(replay,replay,replay.Complete);}
    public static CallbackFunctionalOriginalRig Native(CallbackFunctionalOriginalCase test,ICallbackSourceFacts callbacks,IFunctionalTaxonomySourceFacts functional)
    {var replay=new CallbackFunctionalOriginalReplay(test,null,callbacks,functional);return new(replay,replay,replay.Complete);}
    private void Complete()=>Assert.Equal(test.Steps.Count,index);
    private static JsonObject View(JsonObject input,JsonObject? owner,IReadOnlyDictionary<string,JsonObject>? types,JsonArray? additional)=>new()
    {
        ["input"]=input.DeepClone(),["owner"]=owner?.DeepClone(),["additional"]=additional?.DeepClone(),
        ["types"]=new JsonObject((types??new Dictionary<string,JsonObject>()).Select(pair=>new KeyValuePair<string,JsonNode?>(pair.Key,pair.Value.DeepClone())))
    };
    private JsonNode Take(FactOperation operation,JsonObject input,Func<JsonNode>? native,JsonObject? owner=null,IReadOnlyDictionary<string,JsonObject>? types=null,JsonArray? additional=null)
    {
        Assert.True(index<test.Steps.Count,"Unexpected source-fact invocation");var step=index++;var expected=SourceFactContractCases.Get(test.Steps[step]);Assert.Equal(expected.Operation,operation);
        var before=View(input,owner,types,additional);Assert.True(JsonNode.DeepEquals(View(expected.Input,expected.Owner,expected.Types,expected.Additional),before),"Original function fixture/input drift");
        var result=native is null?expected.Expected.DeepClone():native();Assert.True(JsonNode.DeepEquals(before,View(input,owner,types,additional)),"Source producer mutated original function input/context");
        if(mutation?.Step==step)result=Mutate(result,mutation);return result;
    }
    private static JsonNode Mutate(JsonNode result,OriginalFactMutation mutation)
    {
        if(mutation.Path.Length==0)return mutation.Value!.DeepClone();var clone=result.DeepClone();var parts=mutation.Path.Split('.');JsonNode parent=clone;
        foreach(var part in parts.SkipLast(1))parent=parent is JsonArray array?array[int.Parse(part,CultureInfo.InvariantCulture)]!:parent[part]!;
        if(parent is JsonArray list)list[int.Parse(parts[^1],CultureInfo.InvariantCulture)]=mutation.Value?.DeepClone();else parent[parts[^1]]=mutation.Value?.DeepClone();return clone;
    }
    public JsonObject TypeCandidate(JsonObject declaration)=>Take(FactOperation.TypeCandidate,declaration,callbacks is null?null:()=>callbacks.TypeCandidate(declaration)).AsObject();
    public JsonObject EndpointMethod(JsonObject declaration,JsonObject? owner=null)=>Take(FactOperation.Endpoint,declaration,callbacks is null?null:()=>callbacks.EndpointMethod(declaration,owner),owner:owner).AsObject();
    public JsonArray RegistrationCandidates(JsonObject declaration,IReadOnlyDictionary<string,JsonObject> types)=>Take(FactOperation.Registration,declaration,callbacks is null?null:()=>callbacks.RegistrationCandidates(declaration,types),types:types).AsArray();
    public JsonArray PossiblePairs(JsonArray candidates)=>Take(FactOperation.Pairs,new JsonObject(),callbacks is null?null:()=>callbacks.PossiblePairs(candidates),additional:candidates).AsArray();
    public JsonObject Taxonomy(JsonObject declaration)=>Take(FactOperation.Taxonomy,declaration,functional is null?null:()=>functional.Taxonomy(declaration)).AsObject();
}

[Trait("Gate","Mock")]
[Trait("Requirement","TEST-BUILD-004")]
public sealed class CallbackFunctionalOriginalMockTests
{
    [Theory,MemberData(nameof(CallbackFunctionalOriginalCases.Cases),MemberType=typeof(CallbackFunctionalOriginalCases))]
    public void ExactOriginalFunctionInputsAssertionsAndCompositionsPassCorrectMocks(string id)
    {var test=CallbackFunctionalOriginalCases.Get(id);CallbackFunctionalOriginalCases.AssertCase(test,CallbackFunctionalOriginalReplay.Mock(test));}
    [Theory,MemberData(nameof(CallbackFunctionalOriginalCases.Cases),MemberType=typeof(CallbackFunctionalOriginalCases))]
    public void EachOriginalAssertionRejectsIndependentWrongFact(string id)
    {var test=CallbackFunctionalOriginalCases.Get(id);foreach(var mutation in test.Mutations)Assert.NotNull(Record.Exception(()=>CallbackFunctionalOriginalCases.AssertCase(test,CallbackFunctionalOriginalReplay.Mock(test,mutation))));}
}
