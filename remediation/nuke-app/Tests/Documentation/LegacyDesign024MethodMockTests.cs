using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record Design024Evidence(string Code,string Dsl,JsonObject Contract);

// Literal original predicates, including the original declaration regex and
// existence-only class ownership assertions. No stronger substitute is used.
public static class LegacyDesign024Methods
{
    public static readonly string[] Names=["test_revocation_fanout_has_callback_and_lease_sinks","test_ask_is_exact_protected_owner_path",
        "test_concrete_codec_proxy_and_client_factory","test_interfaces_are_domain_ports","test_component_paths_rendered",
        "test_host_direct_route_and_ownership","test_explicit_close_resume_and_root_generations"];
    public static readonly string[][] CodeTokens=[
        ["CallbackBroker ..|> PrincipalSessionSink","ArcInputLeaseRegistry ..|> PrincipalSessionSink","BridgeService \"1\" *-- \"1\" SessionInvalidationFanout"],
        ["AdmissionGate ..> OwnerAuthenticationService : exact prepared Ask consent","+observePendingExecutionChallenges()","+resolveExecutionConsent(challengeRef, decision)","+reviewExactExecutionConsent(challengeRef, decision)"],
        ["VerifiedCodecRegistry ..|> CodecRegistry","VerifiedCallbackProxyFactory ..|> CallbackProxyFactory","ProtocolArcClientFactory ..|> ArcClientFactory","ArcSessionManager ..> ArcClientFactory","ArcClientAdapter \"1\" *-- \"1\" ArcFrameCodec"],
        [],[],["ProtocolHttpRouter ..> DirectEventEmitter : negotiated direct-event route","BridgeService \"1\" *-- \"1\" ClientCredentialService"],
        ["+registerRoot(root, classification)","+selectRegisteredRoot(rootId)","Node IDs are never reused within the process boot epoch"]];
    public static readonly string[] CleanupTokens=["Close admitted SDK/Binder backend resources","Close ADB collectors and owned connection resources","Reopen only after last challenge reaches terminal disposition"];
    public static readonly string[] ConcreteClasses=["VerifiedCodecRegistry","VerifiedCallbackProxyFactory","ProtocolArcClientFactory"];
    public static Design024Evidence Correct=>new(string.Join('\n',CodeTokens.SelectMany(value=>value))+"\ninterface IndependentPort\n",
        "component bridge.app ExecutionComponents \"Execution\" {\n bridge.app.arcController\n autoLayout\n}\n"+
        string.Join('\n',new[]{"callbacks","transport","device"}.Select(value=>"bridge.app.storage -> bridge.app."+value))+"\n"+string.Join('\n',CleanupTokens),
        new JsonObject{["classes"]=new JsonObject{["IndependentPort"]="bridge.domain",["VerifiedCodecRegistry"]="bridge.platform",["VerifiedCallbackProxyFactory"]="bridge.platform",["ProtocolArcClientFactory"]="bridge.platform"}});
    public static void AssertOriginal(string method,Design024Evidence evidence)
    {
        var index=Array.IndexOf(Names,method);Assert.InRange(index,0,Names.Length-1);
        foreach(var token in CodeTokens[index])Assert.Contains(token,evidence.Code,StringComparison.Ordinal);
        if(index==2)foreach(var name in ConcreteClasses)Assert.True(evidence.Contract["classes"]!.AsObject().ContainsKey(name),name);
        if(index==3)foreach(var name in Regex.Matches(evidence.Code,@"^interface (\w+)",RegexOptions.Multiline).Select(match=>match.Groups[1].Value).Distinct(StringComparer.Ordinal))
            Assert.Equal("bridge.domain",evidence.Contract["classes"]![name]!.GetValue<string>());
        if(index==4){
            var execution=Regex.Match(evidence.Dsl,@"component bridge.app ExecutionComponents[^\n]+\{(.*?)autoLayout",RegexOptions.Singleline);
            Assert.True(execution.Success);Assert.Contains("bridge.app.arcController",execution.Groups[1].Value,StringComparison.Ordinal);
            foreach(var target in new[]{"callbacks","transport","device"})Assert.Contains("bridge.app.storage -> bridge.app."+target,evidence.Dsl,StringComparison.Ordinal);
        }
        if(index==6)foreach(var token in CleanupTokens)Assert.Contains(token,evidence.Dsl,StringComparison.Ordinal);
    }
    public static IEnumerable<object[]> Mutations
    {
        get {
            for(var index=0;index<Names.Length;index++)foreach(var token in CodeTokens[index])yield return new object[]{index,"code",token};
            foreach(var name in ConcreteClasses)yield return new object[]{2,"class",name};
            yield return new object[]{3,"owner","IndependentPort"};yield return new object[]{3,"missing-owner","IndependentPort"};
            yield return new object[]{4,"dsl","bridge.app.arcController"};yield return new object[]{4,"dsl","component bridge.app ExecutionComponents"};
            foreach(var target in new[]{"callbacks","transport","device"})yield return new object[]{4,"dsl","bridge.app.storage -> bridge.app."+target};
            foreach(var token in CleanupTokens)yield return new object[]{6,"dsl",token};
        }
    }
    public static Design024Evidence Wrong(int index,string field,string token)
    {
        var value=Correct;
        if(field=="code")return value with{Code=value.Code.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(field=="dsl")return value with{Dsl=value.Dsl.Replace(token,"REMOVED",StringComparison.Ordinal)};
        if(field is "class" or "missing-owner")Assert.True(value.Contract["classes"]!.AsObject().Remove(token));
        else value.Contract["classes"]![token]="bridge.platform";
        return value;
    }
}

[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class LegacyDesign024MethodMockTests
{
    public static IEnumerable<object[]> Methods=>LegacyDesign024Methods.Names.Select(value=>new object[]{value});
    [Theory,MemberData(nameof(Methods))]
    public void CorrectIndependentMockPassesEveryOriginalMethod(string method)=>LegacyDesign024Methods.AssertOriginal(method,LegacyDesign024Methods.Correct);
    [Theory,MemberData(nameof(LegacyDesign024Methods.Mutations),MemberType=typeof(LegacyDesign024Methods))]
    public void EachOriginalPredicateRejectsItsOwnWrongMock(int index,string field,string token)=>
        Assert.NotNull(Record.Exception(()=>LegacyDesign024Methods.AssertOriginal(LegacyDesign024Methods.Names[index],LegacyDesign024Methods.Wrong(index,field,token))));
    [Fact]
    public void OriginalOwnerPredicateDoesNotInventAnOwnerValueForConcreteClasses()
    {var value=LegacyDesign024Methods.Correct;foreach(var name in LegacyDesign024Methods.ConcreteClasses)value.Contract["classes"]![name]="other";LegacyDesign024Methods.AssertOriginal(LegacyDesign024Methods.Names[2],value);}
}
