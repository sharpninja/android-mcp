using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record SdkPackageChoiceCase(string Id,string Xml,bool Present,bool IsString,string? Package,
    long? Size,string? Error=null,string Platform="37.2",long? ExpectedSize=null);
public sealed record SdkPackageChoiceObservation(long? Size,string? Error,bool InputPreserved);
public interface ISdkPackageChoiceProbe { SdkPackageChoiceObservation Run(SdkPackageChoiceCase test); }
public static class SdkPackageChoiceCases
{
    public static IEnumerable<object[]> All=>new[]{
        new SdkPackageChoiceCase("absent-default",SdkVersionCases.Valid,false,false,null,170000),
        new SdkPackageChoiceCase("explicit-default",SdkVersionCases.Valid,true,true,"platforms;android-37.2",170000),
        new SdkPackageChoiceCase("explicit-null-not-default",SdkVersionCases.Valid,true,false,null,null,"SDK_PACKAGE"),
        new SdkPackageChoiceCase("explicit-number-not-default",SdkVersionCases.Valid,true,false,null,null,"SDK_PACKAGE"),
        new SdkPackageChoiceCase("null-after-malformed-xml","<broken",true,false,null,null,"SDK_XML"),
        new SdkPackageChoiceCase("null-before-bad-size",SdkVersionCases.Valid.Replace("170000","bad"),true,false,null,null,"SDK_PACKAGE"),
        new SdkPackageChoiceCase("explicit-empty-matches-only-empty",SdkVersionCases.Valid.Replace("platforms;android-37.2",""),true,true,"",170000),
        new SdkPackageChoiceCase("explicit-custom",SdkVersionCases.Valid.Replace("platforms;android-37.2","custom"),true,true,"custom",170000),
        new SdkPackageChoiceCase("explicit-wrong",SdkVersionCases.Valid,true,true,"wrong",null,"SDK_PACKAGE"),
        new SdkPackageChoiceCase("missing-platform-first","<broken",true,false,null,null,"SDK_PLATFORM",Platform:""),
        new SdkPackageChoiceCase("size-pin",SdkVersionCases.Valid,false,false,null,null,"SDK_SIZE",ExpectedSize:1),
        new SdkPackageChoiceCase("dtd-before-null","<!DOCTYPE x [<!ENTITY p SYSTEM 'file:///denied'>]>"+SdkVersionCases.Valid,true,false,null,null,"SDK_XML")
    }.Select(value=>new object[]{value});
    public static void AssertCase(SdkPackageChoiceCase test,ISdkPackageChoiceProbe probe)
    {
        var actual=probe.Run(test);Assert.Equal(test.Error,actual.Error);Assert.Equal(test.Size,actual.Size);Assert.True(actual.InputPreserved);
    }
}
public sealed class ScriptedSdkPackageChoiceProbe(string? mutation=null):ISdkPackageChoiceProbe
{
    public SdkPackageChoiceObservation Run(SdkPackageChoiceCase test)=>new(mutation=="size"?99:test.Size,mutation=="error"?"WRONG":test.Error,mutation!="input");
}
[Trait("Gate","Mock")]
public sealed class SdkPackageChoiceMockTests
{
    [Theory,MemberData(nameof(SdkPackageChoiceCases.All),MemberType=typeof(SdkPackageChoiceCases))]
    public void CorrectOriginalPackagePresenceCases(SdkPackageChoiceCase test)=>SdkPackageChoiceCases.AssertCase(test,new ScriptedSdkPackageChoiceProbe());
    [Theory,MemberData(nameof(SdkPackageChoiceCases.All),MemberType=typeof(SdkPackageChoiceCases))]
    public void IndependentOutcomesAndInputControlsDiscriminate(SdkPackageChoiceCase test)
    {foreach(var mutation in new[]{"size","error","input"})Assert.NotNull(Record.Exception(()=>SdkPackageChoiceCases.AssertCase(test,new ScriptedSdkPackageChoiceProbe(mutation))));}
}
