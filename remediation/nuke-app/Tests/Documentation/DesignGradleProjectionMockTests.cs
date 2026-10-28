using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record DesignGradleProjectionCase(string Id,DesignStageManifest Stages,OperationDefinition Operation)
{public override string ToString()=>Id;}
public static class DesignGradleProjectionCases
{
    public const string DescriptorPath="tooling/nuke-design-stages.json";
    public const string ScriptPath="build.gradle.kts";
    public static DesignGradleProjectionCase Original()
    {
        var operation=new OperationDefinition(new(Category.Design,Operation.GradleCompatibility),"Design/GradleCompatibility",
            ["project-root","output-root","cache-root","work-root","receipt-root","docker-identity","renderer-user","tool-access","design-aliases","design-registry-sha256"],
            ["generation","offline-gradle-checksum-proof","catalog-root"],[]);
        return new("CURRENT",new DesignStagePlanner().Describe(),operation);
    }
    public static IEnumerable<object[]> Cases
    {
        get
        {
            var original=Original();yield return [original];
            var reversed=original.Stages.Metadata with{Aliases=Array.AsReadOnly(original.Stages.Metadata.Aliases.Reverse().ToArray())};
            yield return [original with{Id="ALIAS-METADATA-ORDER",Stages=new(reversed,new CanonicalJson().Hash(JsonSerializer.SerializeToElement(reversed)))}];
            yield return [original with{Id="PARAMETER-METADATA-ORDER",Operation=original.Operation with{OptionalParameters=original.Operation.OptionalParameters.Reverse().ToArray()}}];
        }
    }
    public static Artifact Descriptor(DesignGradleProjectionCase test)
    {
        var parameters=test.Operation.RequiredParameters.Select(name=>(Name:name,Required:true))
            .Concat(test.Operation.OptionalParameters.Select(name=>(Name:name,Required:false)))
            .Where(parameter=>parameter.Name is not ("design-aliases" or "design-registry-sha256"))
            .Select(parameter=>new{name=parameter.Name,property="nuke"+string.Concat(parameter.Name.Split('-').Select(word=>char.ToUpperInvariant(word[0])+word[1..])),required=parameter.Required}).ToArray();
        var descriptor=new{schema_version=1,registry_sha256=test.Stages.Sha256,target=test.Operation.Key.Category.ToString(),operation=test.Operation.Key.Operation.ToString(),
            alias_parameter="design-aliases",registry_parameter="design-registry-sha256",aliases=test.Stages.Metadata.Aliases.Select(alias=>alias.Name).ToArray(),parameters};
        return new(DescriptorPath,new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(descriptor)),"application/json");
    }
    public static Artifact Script()
    {
        var path=Path.Combine(DocumentationFixture.Current.Workspace,"remediation/nuke-planning/docs/fixtures/gradle-native-delegate.txt");
        var bytes=File.ReadAllBytes(path);Assert.Equal("cf251efbbed05d0f54626a6f01063705735d1fe0447d654ddfa7653c6e95ae65",DocumentationFixture.Hash(bytes));
        return new(ScriptPath,bytes,"text/plain");
    }
    public static DesignGradleProjection Expected(DesignGradleProjectionCase test)=>new(Descriptor(test),Script());
    public static void AssertProjection(DesignGradleProjectionCase test,IDesignGradleProjection provider)
    {
        var expected=Expected(test);var actual=provider.Render(test.Stages,test.Operation);
        foreach(var pair in new[]{(expected.Descriptor,actual.Descriptor),(expected.BuildScript,actual.BuildScript)})
        {Assert.Equal(pair.Item1.RelativePath,pair.Item2.RelativePath);Assert.Equal(pair.Item1.MediaType,pair.Item2.MediaType);Assert.Equal(pair.Item1.Bytes.ToArray(),pair.Item2.Bytes.ToArray());}
    }
    public static IEnumerable<object[]> AliasIndexes=>Enumerable.Range(0,Original().Stages.Metadata.Aliases.Count).Select(index=>new object[]{index});
    public static IEnumerable<object[]> ParameterFields=>Enumerable.Range(0,11).SelectMany(index=>new[]{"name","property","required"}.Select(memberName=>new object[]{index,memberName}));
    public static DesignGradleProjection Mutated(DesignGradleProjectionCase test,string mutation,int index=0,string memberName="")
    {
        var result=Expected(test);var descriptor=result.Descriptor;var script=result.BuildScript;
        if(mutation=="descriptor-path")return result with{Descriptor=descriptor with{RelativePath="wrong"}};
        if(mutation=="descriptor-media")return result with{Descriptor=descriptor with{MediaType="wrong"}};
        if(mutation=="descriptor-bytes")return result with{Descriptor=descriptor with{Bytes=new byte[]{0}}};
        if(mutation=="script-path")return result with{BuildScript=script with{RelativePath="wrong"}};
        if(mutation=="script-media")return result with{BuildScript=script with{MediaType="wrong"}};
        if(mutation=="script-bytes")return result with{BuildScript=script with{Bytes=new byte[]{0}}};
        var json=JsonNode.Parse(descriptor.Bytes.Span)!.AsObject();
        if(mutation=="alias")json["aliases"]!.AsArray()[index]="wrong";
        else if(mutation=="parameter")
        {
            var parameter=json["parameters"]!.AsArray()[index]!.AsObject();
            if(memberName=="required")parameter[memberName]=!parameter[memberName]!.GetValue<bool>();else parameter[memberName]="wrong";
        }
        else if(mutation=="schema_version")json[mutation]=99;
        else json[mutation]="wrong";
        return result with{Descriptor=descriptor with{Bytes=new CanonicalJson().Serialize(JsonSerializer.SerializeToElement(json))}};
    }
    public static IEnumerable<object[]> CheckCases=>new[]{"CURRENT","DESCRIPTOR-MISSING","SCRIPT-MISSING","EXTRA","DUPLICATE","DESCRIPTOR-BYTES","SCRIPT-BYTES","DESCRIPTOR-MEDIA","SCRIPT-MEDIA","DESCRIPTOR-PATH","SCRIPT-PATH"}.Select(id=>new object[]{id});
    public static IReadOnlyList<Artifact> Retained(DesignGradleProjectionCase test,string id)
    {
        var expected=Expected(test);var values=new[]{expected.Descriptor,expected.BuildScript};
        return id switch
        {
            "DESCRIPTOR-MISSING"=>[expected.BuildScript],"SCRIPT-MISSING"=>[expected.Descriptor],
            "EXTRA"=>[..values,new("extra.txt",Array.Empty<byte>(),"text/plain")],"DUPLICATE"=>[..values,expected.Descriptor],
            "CURRENT"=>values,_=>AsArtifacts(Mutated(test,id.ToLowerInvariant()))
        };
    }
    public static IReadOnlyList<Artifact> AsArtifacts(DesignGradleProjection projection)=>[projection.Descriptor,projection.BuildScript];
    public static IReadOnlyList<DesignGradleProjectionIssue> ExpectedCheck(string id)=>id switch
    {
        "CURRENT"=>[],"DESCRIPTOR-MISSING" or "SCRIPT-MISSING" or "EXTRA" or "DUPLICATE" or "DESCRIPTOR-PATH" or "SCRIPT-PATH"=>[new("GRADLE_PROJECTION_SET","")],
        _=>[new("STALE_GRADLE_PROJECTION",id.StartsWith("DESCRIPTOR",StringComparison.Ordinal)?DescriptorPath:ScriptPath)]
    };
    public static void AssertCheck(DesignGradleProjectionCase test,string id,IDesignGradleProjection provider)
        =>Assert.Equal(ExpectedCheck(id),provider.Check(test.Stages,test.Operation,Retained(test,id)));
}
public sealed class ScriptedDesignGradleProjection(DesignGradleProjection response,IReadOnlyList<DesignGradleProjectionIssue>? issues=null):IDesignGradleProjection
{
    public DesignGradleProjection Render(DesignStageManifest stages,OperationDefinition operation)=>response;
    public IReadOnlyList<DesignGradleProjectionIssue> Check(DesignStageManifest stages,OperationDefinition operation,IReadOnlyList<Artifact> retainedProjection)=>issues??[];
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DesignGradleProjectionMockTests
{
    [Theory,MemberData(nameof(DesignGradleProjectionCases.Cases),MemberType=typeof(DesignGradleProjectionCases))]
    public void CorrectSingleMetadataProjectionMocksPass(DesignGradleProjectionCase test)=>DesignGradleProjectionCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleProjectionCases.Expected(test)));
    [Theory]
    [InlineData("descriptor-path")][InlineData("descriptor-media")][InlineData("descriptor-bytes")][InlineData("script-path")][InlineData("script-media")][InlineData("script-bytes")]
    [InlineData("schema_version")][InlineData("registry_sha256")][InlineData("target")][InlineData("operation")][InlineData("alias_parameter")][InlineData("registry_parameter")]
    public void EachProjectionIdentityAndPublicBindingDriftIsRejected(string mutation)
    {var test=DesignGradleProjectionCases.Original();Assert.NotNull(Record.Exception(()=>DesignGradleProjectionCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleProjectionCases.Mutated(test,mutation)))));}
    [Theory,MemberData(nameof(DesignGradleProjectionCases.AliasIndexes),MemberType=typeof(DesignGradleProjectionCases))]
    public void EveryExistingAliasDerivesFromCoreMetadata(int index)
    {var test=DesignGradleProjectionCases.Original();Assert.NotNull(Record.Exception(()=>DesignGradleProjectionCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleProjectionCases.Mutated(test,"alias",index)))));}
    [Theory,MemberData(nameof(DesignGradleProjectionCases.ParameterFields),MemberType=typeof(DesignGradleProjectionCases))]
    public void EachForwardedParameterFieldDerivesFromOperationMetadata(int index,string memberName)
    {var test=DesignGradleProjectionCases.Original();Assert.NotNull(Record.Exception(()=>DesignGradleProjectionCases.AssertProjection(test,new ScriptedDesignGradleProjection(DesignGradleProjectionCases.Mutated(test,"parameter",index,memberName)))));}
    [Theory,MemberData(nameof(DesignGradleProjectionCases.CheckCases),MemberType=typeof(DesignGradleProjectionCases))]
    public void CorrectCurrentAndStaleProjectionChecksPass(string id)
    {var test=DesignGradleProjectionCases.Original();DesignGradleProjectionCases.AssertCheck(test,id,new ScriptedDesignGradleProjection(DesignGradleProjectionCases.Expected(test),DesignGradleProjectionCases.ExpectedCheck(id)));}
    [Theory,MemberData(nameof(DesignGradleProjectionCases.CheckCases),MemberType=typeof(DesignGradleProjectionCases))]
    public void EveryWrongConsistencyOutcomeIsRejected(string id)
    {var test=DesignGradleProjectionCases.Original();IReadOnlyList<DesignGradleProjectionIssue> wrong=id=="CURRENT"?[new("WRONG","")]:[];Assert.NotNull(Record.Exception(()=>DesignGradleProjectionCases.AssertCheck(test,id,new ScriptedDesignGradleProjection(DesignGradleProjectionCases.Expected(test),wrong))));}
    [Fact]
    public void IndependentScriptUsesOneDeferredArgvInvocationAndDescriptorOnlyAliasRegistration()
    {
        var text=Encoding.UTF8.GetString(DesignGradleProjectionCases.Script().Bytes.Span);
        Assert.Equal(1,text.Split("tasks.registering(Exec::class)",StringSplitOptions.None).Length-1);
        Assert.Equal(1,text.Split("commandLine(arguments)",StringSplitOptions.None).Length-1);
        Assert.True(text.IndexOf("doFirst {",StringComparison.Ordinal)<text.IndexOf("requiredProperty(\"nukeDotnetHost\")",StringComparison.Ordinal));
        Assert.Contains("gradle.taskGraph.allTasks",text);Assert.Contains("selected.joinToString(\",\")",text);Assert.Contains("dependsOn(nukeDesignCompatibility)",text);
        foreach(var alias in new DesignStagePlanner().Describe().Metadata.Aliases)Assert.DoesNotContain(alias.Name,text);
        foreach(var prohibited in new[]{"ProcessBuilder","doLast","node","python","marp","MessageDigest"})Assert.DoesNotContain(prohibited,text,StringComparison.OrdinalIgnoreCase);
    }
}
