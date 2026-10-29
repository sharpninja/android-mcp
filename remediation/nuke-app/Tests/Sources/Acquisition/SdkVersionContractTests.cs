using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed record SdkVersionCase(string Id,string Xml,string Platform,string? Package,long? Expected,long? Size,string? Error);
public static class SdkVersionCases
{
    public const string Valid="<sdk-repository><remotePackage path=\"platforms;android-37.2\"><archives><archive><complete><size>170000</size></complete></archive></archives></remotePackage></sdk-repository>";
    public static IEnumerable<object[]> All=>new[]{
        new SdkVersionCase("exact",Valid,"37.2",null,170000,170000,null),
        new SdkVersionCase("explicit-path",Valid,"37.2","platforms;android-37.2",170000,170000,null),
        new SdkVersionCase("missing-platform",Valid,"",null,null,null,"SDK_PLATFORM"),
        new SdkVersionCase("missing-package",Valid,"38",null,null,null,"SDK_PACKAGE"),
        new SdkVersionCase("duplicate-package",Valid.Replace("</sdk-repository>",Valid[16..^17]+"</sdk-repository>"),"37.2",null,null,null,"SDK_PACKAGE"),
        new SdkVersionCase("size-pin",Valid,"37.2",null,1,null,"SDK_SIZE"),
        new SdkVersionCase("bad-size",Valid.Replace("170000","not-a-size"),"37.2",null,null,null,"SDK_SIZE"),
        new SdkVersionCase("doctype","<!DOCTYPE sdk-repository [<!ENTITY x SYSTEM 'file:///etc/passwd'>]>"+Valid,"37.2",null,null,null,"SDK_XML")
    }.Select(x=>new object[]{x});
    public static void AssertCase(SdkVersionCase test,ISdkArchiveVersionValidator validator)
    {
        if(test.Error is not null){var e=Record.Exception(()=>validator.ResolveSize(Encoding.UTF8.GetBytes(test.Xml),test.Platform,test.Package,test.Expected));Assert.IsType<TransportException>(e);Assert.Equal(test.Error,((TransportException)e!).Code);return;}
        Assert.Equal(test.Size,validator.ResolveSize(Encoding.UTF8.GetBytes(test.Xml),test.Platform,test.Package,test.Expected));
    }
}
public sealed class ScriptedSdkVersionValidator(SdkVersionCase test,string? mutation=null):ISdkArchiveVersionValidator
{
    public long ResolveSize(ReadOnlyMemory<byte> xml,string platform,string? package=null,long? expected=null)
    {
        Assert.Equal(test.Xml,Encoding.UTF8.GetString(xml.Span));Assert.Equal(test.Platform,platform);Assert.Equal(test.Package,package);Assert.Equal(test.Expected,expected);
        if(test.Error is not null&&mutation!="error")throw new TransportException(mutation=="code"?"wrong":test.Error,"Scripted SDK rejection");
        return mutation=="size"?1:test.Size??170000;
    }
}
[Trait("Gate","Mock")]
public sealed class SdkVersionMockTests
{
    [Theory,MemberData(nameof(SdkVersionCases.All),MemberType=typeof(SdkVersionCases))]
    public void CorrectMock(SdkVersionCase test)=>SdkVersionCases.AssertCase(test,new ScriptedSdkVersionValidator(test));
    [Theory,MemberData(nameof(SdkVersionCases.All),MemberType=typeof(SdkVersionCases))]
    public void EveryWrongOutcomeRejected(SdkVersionCase test)
    {foreach(var mutation in test.Error is null?new[]{"size"}:new[]{"error","code"})Assert.NotNull(Record.Exception(()=>SdkVersionCases.AssertCase(test,new ScriptedSdkVersionValidator(test,mutation))));}
}
