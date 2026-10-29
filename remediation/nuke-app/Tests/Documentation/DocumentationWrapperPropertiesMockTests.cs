using System.Text;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record WrapperPropertiesCase(string Id,DocumentationRendererProfile Profile,Artifact Properties,bool Rejected)
{public override string ToString()=>Id;}
public static class WrapperPropertiesCases
{
    public const string Path="gradle/wrapper/gradle-wrapper.properties";
    // Literal independent oracle copied from the retained original bootstrap
    // configuration contract, not from the implementation being introduced.
    public const string Expected="distributionBase=GRADLE_USER_HOME\ndistributionPath=wrapper/dists\ndistributionUrl=https\\://services.gradle.org/distributions/gradle-8.14.3-bin.zip\ndistributionSha256Sum=bd71102213493060956ec229d946beee57158dbd89d0e62b91bca0fa2c5f3531\nnetworkTimeout=10000\nvalidateDistributionUrl=true\nzipStoreBase=GRADLE_USER_HOME\nzipStorePath=wrapper/dists\n";
    private static DocumentationRendererProfile Profile=>RendererProfileCases.Original().Expected;
    private static Artifact Text(string text)=>new(Path,Encoding.UTF8.GetBytes(text),"application/octet-stream");
    public static IEnumerable<object[]> Cases
    {
        get {
            yield return new object[]{new WrapperPropertiesCase("LF",Profile,Text(Expected),false)};
            yield return new object[]{new WrapperPropertiesCase("CRLF",Profile,Text(Expected.Replace("\n","\r\n",StringComparison.Ordinal)),false)};
            yield return new object[]{new WrapperPropertiesCase("CR",Profile,Text(Expected.Replace('\n','\r')),false)};
            foreach(var (name,oldValue,newValue) in new[]{
                ("base","distributionBase=GRADLE_USER_HOME","distributionBase=OTHER"),("distribution-path","distributionPath=wrapper/dists","distributionPath=other"),
                ("url","https\\://services.gradle.org/","https\\://unapproved.example/"),("version","gradle-8.14.3-bin.zip","gradle-8.14.2-bin.zip"),
                ("hash","bd71102213493060956ec229d946beee57158dbd89d0e62b91bca0fa2c5f3531",new string('0',64)),
                ("timeout","networkTimeout=10000","networkTimeout=60"),("url-validation","validateDistributionUrl=true","validateDistributionUrl=false"),
                ("zip-base","zipStoreBase=GRADLE_USER_HOME","zipStoreBase=OTHER"),("zip-path","zipStorePath=wrapper/dists","zipStorePath=other")})
                yield return new object[]{new WrapperPropertiesCase(name,Profile,Text(Expected.Replace(oldValue,newValue,StringComparison.Ordinal)),true)};
            foreach(var (name,text) in new[]{("missing-newline",Expected.TrimEnd('\n')),("extra-line",Expected+"extra=true\n"),
                ("order",Expected.Replace("distributionBase=GRADLE_USER_HOME\ndistributionPath=wrapper/dists","distributionPath=wrapper/dists\ndistributionBase=GRADLE_USER_HOME",StringComparison.Ordinal)),
                ("whitespace",Expected.Replace("networkTimeout=10000","networkTimeout =10000",StringComparison.Ordinal)),("bom","\ufeff"+Expected),
                ("missing-property",Expected.Replace("validateDistributionUrl=true\n","",StringComparison.Ordinal))})
                yield return new object[]{new WrapperPropertiesCase(name,Profile,Text(text),true)};
            yield return new object[]{new WrapperPropertiesCase("invalid-utf8",Profile,new(Path,new byte[]{0xff,0xfe,0xfd},"application/octet-stream"),true)};
            yield return new object[]{new WrapperPropertiesCase("wrong-source",Profile,Text(Expected) with{RelativePath="wrong.properties"},true)};
            var profile=Profile with{GradleVersion="9.1.0",Downloads=Profile.Downloads.Select(pin=>pin.LocalPath==".tools/gradle-8.14.3-bin.zip"?pin with{LocalPath=".tools/gradle-9.1.0-bin.zip",Sha256=new string('a',64)}:pin).ToArray()};
            var changed=Expected.Replace("8.14.3","9.1.0",StringComparison.Ordinal).Replace("bd71102213493060956ec229d946beee57158dbd89d0e62b91bca0fa2c5f3531",new string('a',64),StringComparison.Ordinal);
            yield return new object[]{new WrapperPropertiesCase("profile-bound",profile,Text(changed),false)};
            yield return new object[]{new WrapperPropertiesCase("old-properties-new-profile",profile,Text(Expected),true)};
        }
    }
    public static void AssertContract(WrapperPropertiesCase test,IDocumentationWrapperPropertiesValidator provider)
    {
        var bytes=test.Properties.Bytes.ToArray();var error=Record.Exception(()=>provider.Validate(test.Profile,test.Properties));
        if(test.Rejected){var typed=Assert.IsType<ArchitectureAssetException>(error);Assert.Equal("WRAPPER_PROPERTIES",typed.Code);Assert.Equal("Committed wrapper properties differ from pinned expected configuration",typed.Message);}
        else Assert.Null(error);
        Assert.Equal(bytes,test.Properties.Bytes.ToArray());
    }
}
public sealed class ScriptedWrapperPropertiesValidator(WrapperPropertiesCase test,string mutation=""):IDocumentationWrapperPropertiesValidator
{
    public void Validate(DocumentationRendererProfile profile,Artifact properties)
    {
        Assert.Same(test.Profile,profile);Assert.Same(test.Properties,properties);
        if(mutation=="bytes"){Assert.True(MemoryMarshal.TryGetArray(properties.Bytes,out var owned));owned.Array![owned.Offset]^=1;return;}
        if(test.Rejected&&mutation!="accept"||!test.Rejected&&mutation=="reject")throw new ArchitectureAssetException(mutation=="code"?"WRONG":"WRAPPER_PROPERTIES",mutation=="message"?"WRONG":"Committed wrapper properties differ from pinned expected configuration");
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationWrapperPropertiesMockTests
{
    [Theory,MemberData(nameof(WrapperPropertiesCases.Cases),MemberType=typeof(WrapperPropertiesCases))]
    public void CorrectIndependentWrapperResponsesPass(WrapperPropertiesCase test)=>WrapperPropertiesCases.AssertContract(test,new ScriptedWrapperPropertiesValidator(test));
    [Theory,MemberData(nameof(WrapperPropertiesCases.Cases),MemberType=typeof(WrapperPropertiesCases))]
    public void EachOriginalPropertyBoundaryRejectsWrongOutcome(WrapperPropertiesCase test)=>Assert.NotNull(Record.Exception(()=>WrapperPropertiesCases.AssertContract(test,new ScriptedWrapperPropertiesValidator(test,test.Rejected?"accept":"reject"))));
    [Theory,InlineData("code"),InlineData("message")]
    public void ExactOriginalFailureAndTypedCodeAreIndependentlyChecked(string mutation)
    {var test=(WrapperPropertiesCase)WrapperPropertiesCases.Cases.First(row=>((WrapperPropertiesCase)row[0]).Rejected)[0];Assert.NotNull(Record.Exception(()=>WrapperPropertiesCases.AssertContract(test,new ScriptedWrapperPropertiesValidator(test,mutation))));}
    [Fact]
    public void InputMutationIsRejectedByTheOriginalReadOnlySideEffectCheck()
    {var test=(WrapperPropertiesCase)WrapperPropertiesCases.Cases.First()[0];Assert.NotNull(Record.Exception(()=>WrapperPropertiesCases.AssertContract(test,new ScriptedWrapperPropertiesValidator(test,"bytes"))));}
}
