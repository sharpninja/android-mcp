using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record SdkNamedTargetObservation(string Code,IReadOnlyList<string> Calls);
public sealed class NamedSdkLocation(List<string> calls,string file)
{public string File {get{calls.Add("Location.File");return file;}}}
public sealed class NamedSdkTarget(List<string> calls,string file)
{public NamedSdkLocation Location {get{calls.Add("Target.Location");return new(calls,file);}}}
public sealed class NamedSdkProject
{
    public IDictionary<string,NamedSdkTarget> Targets {get;}
    public NamedSdkProject(IDictionary<string,NamedSdkTarget> targets)=>Targets=targets;
}
// Deliberately exposes only the declared dictionary's named read API. Either
// concrete/generic enumeration is a test failure, even when the key exists.
public sealed class NamedOnlySdkTargets(List<string> calls,NamedSdkTarget? selected):IDictionary<string,NamedSdkTarget>
{
    public const string Required="ReferenceCopyLocalPathsOutputGroup";
    public bool ContainsKey(string key){calls.Add("ContainsKey:"+key);return key==Required&&selected is not null;}
    public NamedSdkTarget this[string key]
    {get{calls.Add("Item:"+key);return key==Required&&selected is not null ? selected : throw new KeyNotFoundException();}set=>throw new NotSupportedException();}
    public IEnumerator<KeyValuePair<string,NamedSdkTarget>> GetEnumerator(){calls.Add("ENUMERATION_FORBIDDEN");throw new InvalidOperationException("Use the declared named dictionary API.");}
    IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    public ICollection<string> Keys=>throw new NotSupportedException();public ICollection<NamedSdkTarget> Values=>throw new NotSupportedException();
    public int Count=>selected is null ? 0 : 1;public bool IsReadOnly=>true;
    public void Add(string key,NamedSdkTarget value)=>throw new NotSupportedException();
    public bool Remove(string key)=>throw new NotSupportedException();
    public bool TryGetValue(string key,out NamedSdkTarget value)=>throw new NotSupportedException();
    public void Add(KeyValuePair<string,NamedSdkTarget> item)=>throw new NotSupportedException();
    public void Clear()=>throw new NotSupportedException();
    public bool Contains(KeyValuePair<string,NamedSdkTarget> item)=>throw new NotSupportedException();
    public void CopyTo(KeyValuePair<string,NamedSdkTarget>[] array,int arrayIndex)=>throw new NotSupportedException();
    public bool Remove(KeyValuePair<string,NamedSdkTarget> item)=>throw new NotSupportedException();
}
public sealed class SdkNamedTargetFixture
{
    public static readonly string[] Names=["named-target","missing-target","wrong-origin"];
    public const string ExpectedOrigin="/fixture/sdk/Microsoft.Common.CurrentVersion.targets";
    public string Name {get;}
    public List<string> Calls {get;}=[];
    public NamedSdkProject Project {get;}
    public SdkNamedTargetFixture(string name)
    {
        Name=name;var file=name=="wrong-origin" ? "/fixture/app/Override.targets" : ExpectedOrigin;
        Project=new(new NamedOnlySdkTargets(Calls,name=="missing-target" ? null : new(Calls,file)));
    }
    public SdkNamedTargetObservation Expected=>new(Name=="named-target" ? "OK" : "SDK_RESOLUTION_TARGET_UNSUPPORTED",
        Name=="missing-target" ? ["ContainsKey:"+NamedOnlySdkTargets.Required]
            : ["ContainsKey:"+NamedOnlySdkTargets.Required,"Item:"+NamedOnlySdkTargets.Required,"Target.Location","Location.File"]);
    public SdkNamedTargetObservation Observe(string code)=>new(code,Calls);
}
public static class SdkNamedTargetCases
{
    public static IEnumerable<object[]> Data=>SdkNamedTargetFixture.Names.Select(name=>new object[]{name});
    public static void AssertContract(SdkNamedTargetFixture fixture,JsonNode actual)=>Assert.Equal(
        NativeFactsFixture.Canonical(fixture.Expected),new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(actual)));
    public static void ScriptedVerify(NamedSdkProject project,string expected)
    {
        var targets=project.Targets;
        if(!targets.ContainsKey(NamedOnlySdkTargets.Required))throw new TestGateFactsException("SDK_RESOLUTION_TARGET_UNSUPPORTED","Required target is absent.");
        var selected=targets[NamedOnlySdkTargets.Required];
        if(Path.GetFullPath(selected.Location.File)!=Path.GetFullPath(expected))throw new TestGateFactsException("SDK_RESOLUTION_TARGET_UNSUPPORTED","Selected target has another origin.");
    }
    public static SdkNamedTargetObservation Execute(SdkNamedTargetFixture fixture,Action<object,string> verify)
    {
        var code="OK";try{verify(fixture.Project,SdkNamedTargetFixture.ExpectedOrigin);}catch(TestGateFactsException error){code=error.Code;}
        return fixture.Observe(code);
    }
}
public sealed class SdkNamedTargetLookupMockTests
{
    [Theory,MemberData(nameof(SdkNamedTargetCases.Data),MemberType=typeof(SdkNamedTargetCases))]
    public void Declared_named_dictionary_API_preserves_exact_target_provenance(string name)
    {
        var fixture=new SdkNamedTargetFixture(name);
        var actual=SdkNamedTargetCases.Execute(fixture,(project,expected)=>SdkNamedTargetCases.ScriptedVerify((NamedSdkProject)project,expected));
        SdkNamedTargetCases.AssertContract(fixture,JsonSerializer.SerializeToNode(actual)!);
    }
    [Theory,MemberData(nameof(SdkNamedTargetCases.Data),MemberType=typeof(SdkNamedTargetCases))]
    public void Wrong_code_key_order_or_call_coverage_is_rejected(string name)
    {
        var fixture=new SdkNamedTargetFixture(name);var original=JsonSerializer.SerializeToNode(fixture.Expected)!;
        var members=ReviewOfficialTransportCases.Leaves(original).ToArray();
        foreach(var member in members){var actual=original.DeepClone();ReviewOfficialTransportCases.Mutate(actual,member.Path);Assert.ThrowsAny<Exception>(()=>SdkNamedTargetCases.AssertContract(fixture,actual));}
    }
}
