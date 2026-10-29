using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;
namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;
public sealed class NativeSdkPackageChoiceProbe:ISdkPackageChoiceProbe
{
    public SdkPackageChoiceObservation Run(SdkPackageChoiceCase test)
    {
        var bytes=Encoding.UTF8.GetBytes(test.Xml);var before=bytes.ToArray();long? size=null;string? error=null;
        try{size=new SdkArchiveVersionValidator().ResolvePackageSize(bytes,test.Platform,new(test.Present,test.IsString,test.Package),test.ExpectedSize);}
        catch(TransportException problem){error=problem.Code;}
        return new(size,error,before.AsSpan().SequenceEqual(bytes));
    }
}
[Trait("Gate","Real")]
public sealed class SdkPackageChoiceRealTests
{
    [Theory,MemberData(nameof(SdkPackageChoiceCases.All),MemberType=typeof(SdkPackageChoiceCases))]
    public void SameOriginalPackagePresenceCases(SdkPackageChoiceCase test)=>SdkPackageChoiceCases.AssertCase(test,new NativeSdkPackageChoiceProbe());
}
