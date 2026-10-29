using Xunit;
namespace AndroidMcp.BuildTool.Tests.Core;
public sealed record RemovalRuleFixture(string Prefix,IReadOnlyList<string> ContainsAny);
public sealed record RemovalInput(IReadOnlyList<string>? Keys,IReadOnlyList<RemovalRuleFixture>? Rules,bool Clear,bool PublicOverride);
public sealed record RemovalObservation(string Code,string? ChildValue,bool ParentPreserved,bool RequestPreserved,int Starts,bool ReceiptCreated);
public static class ProcessEnvironmentRemovalCases
{
    public const string Name="ANDROIDMCP_DUMMY_ENV",Ambient="owned environment value",Public="owned public override";
    public static string[] Names=>["default-preserves","empty-preserves","exact-removal","absent-key-preserves","public-override","exact-public-conflict","invalid-empty-key","invalid-nul-key","invalid-equals-key","duplicate-key","rule-removal","rule-prefix-boundary","rule-prefix-case","rule-token-boundary","rule-token-case","rule-public-conflict","invalid-empty-prefix","invalid-empty-token-list","invalid-empty-token","clear-plus-removal","multiple-rules","exact-key-case"];
    public static IEnumerable<object[]> Data=>Names.Select(x=>new object[]{x});
    public static RemovalInput Input(string name)=>name switch
    {
        "empty-preserves"=>new([],[],false,false),"exact-removal"=>new([Name],null,false,false),"absent-key-preserves"=>new(["ANDROIDMCP_ABSENT_ENV"],null,false,false),
        "public-override"=>new(null,null,false,true),"exact-public-conflict"=>new([Name],null,false,true),
        "invalid-empty-key"=>new([""],null,false,false),"invalid-nul-key"=>new(["bad\0key"],null,false,false),"invalid-equals-key"=>new(["bad=key"],null,false,false),"duplicate-key"=>new([Name,Name],null,false,false),
        "rule-removal"=>new(null,[new("ANDROIDMCP_",["DUMMY"])],false,false),"rule-prefix-boundary"=>new(null,[new("OTHER_",["DUMMY"])],false,false),
        "rule-prefix-case"=>new(null,[new("androidmcp_",["DUMMY"])],false,false),"rule-token-boundary"=>new(null,[new("ANDROIDMCP_",["TIMEOUT"])],false,false),"rule-token-case"=>new(null,[new("ANDROIDMCP_",["dummy"])],false,false),
        "rule-public-conflict"=>new(null,[new("ANDROIDMCP_",["DUMMY"])],false,true),"invalid-empty-prefix"=>new(null,[new("",["DUMMY"])],false,false),
        "invalid-empty-token-list"=>new(null,[new("ANDROIDMCP_",[])],false,false),"invalid-empty-token"=>new(null,[new("ANDROIDMCP_",[""])],false,false),
        "clear-plus-removal"=>new([Name],null,true,false),"multiple-rules"=>new(null,[new("OTHER_",["OTHER"]),new("ANDROIDMCP_",["UNUSED","DUMMY"])],false,false),
        "exact-key-case"=>new([Name.ToLowerInvariant()],null,false,false),_=>new(null,null,false,false)
    };
    public static RemovalObservation Expected(string name)
    {
        var error=name.StartsWith("invalid-",StringComparison.Ordinal)||name=="duplicate-key"?"ENVIRONMENT_REMOVAL_INVALID":name.EndsWith("public-conflict",StringComparison.Ordinal)?"ENVIRONMENT_REMOVAL_CONFLICT":"OK";
        var value=name=="public-override"?Public:name is "exact-removal" or "rule-removal" or "clear-plus-removal" or "multiple-rules"?null:name=="exact-key-case"&&OperatingSystem.IsWindows()?null:Ambient;
        return new(error,error=="OK"?value:null,true,true,error=="OK"?1:0,error=="OK");
    }
    public static void AssertContract(string name,RemovalObservation actual)
    {var expected=Expected(name);Assert.Equal(expected.Code,actual.Code);Assert.Equal(expected.ChildValue,actual.ChildValue);Assert.True(actual.ParentPreserved);Assert.True(actual.RequestPreserved);Assert.Equal(expected.Starts,actual.Starts);Assert.Equal(expected.ReceiptCreated,actual.ReceiptCreated);}
}
public sealed class ProcessEnvironmentRemovalMockTests
{
    [Theory,MemberData(nameof(ProcessEnvironmentRemovalCases.Data),MemberType=typeof(ProcessEnvironmentRemovalCases)),Trait("Gate","Mock")]
    public void Every_removal_preservation_conflict_and_admission_effect_is_discriminated(string name)
    {
        var packet=ProcessEnvironmentRemovalCases.Input(name);Assert.NotNull(packet);
        var actual=ProcessEnvironmentRemovalCases.Expected(name);ProcessEnvironmentRemovalCases.AssertContract(name,actual);
        foreach(var wrong in new[]{actual with{Code="wrong"},actual with{ChildValue=actual.ChildValue is null?"wrong":null},actual with{ParentPreserved=false},actual with{RequestPreserved=false},actual with{Starts=actual.Starts+1},actual with{ReceiptCreated=!actual.ReceiptCreated}})
            Assert.ThrowsAny<Exception>(()=>ProcessEnvironmentRemovalCases.AssertContract(name,wrong));
    }
}
