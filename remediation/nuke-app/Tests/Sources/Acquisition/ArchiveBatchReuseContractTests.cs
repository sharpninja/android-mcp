using System.Text;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

public sealed record ArchiveBatchReuseCase(string Id,bool Full=false,string? Change=null,string? Error=null);
public sealed record ArchiveBatchReuseObservation(string? Error,int FirstReads,int TotalReads,int ArchiveReads,
    int SourceWrites,bool Sealed,bool Retained,IReadOnlyList<byte[]> Bytes,bool RequestPins);
public interface IArchiveBatchReuseProbe { Task<ArchiveBatchReuseObservation> RunAsync(ArchiveBatchReuseCase test); }
public static class ArchiveBatchReuseCases
{
    public static IEnumerable<object[]> All=>new[]{
        new ArchiveBatchReuseCase("metadata-same-version"),
        new ArchiveBatchReuseCase("metadata-changed-etag",Change:"etag"),
        new ArchiveBatchReuseCase("metadata-changed-revision",Change:"revision"),
        new ArchiveBatchReuseCase("metadata-changed-profile",Change:"profile",Error:"BATCH_PROFILE"),
        new ArchiveBatchReuseCase("metadata-missing-etag",Change:"missing-etag",Error:"ETAG"),
        new ArchiveBatchReuseCase("metadata-missing-platform",Change:"missing-platform",Error:"SDK_PLATFORM"),
        new ArchiveBatchReuseCase("metadata-changed-repository",Change:"repository",Error:"SDK_PACKAGE"),
        new ArchiveBatchReuseCase("metadata-source-hash",Change:"source-hash",Error:"SOURCE_HASH"),
        new ArchiveBatchReuseCase("metadata-source-size",Change:"source-size",Error:"SOURCE_SIZE"),
        new ArchiveBatchReuseCase("full-same-version",Full:true),
        new ArchiveBatchReuseCase("full-changed-hash",Full:true,Change:"archive-hash",Error:"ARCHIVE_HASH"),
        new ArchiveBatchReuseCase("full-tampered-retained",Full:true,Change:"tampered",Error:"ARCHIVE_HASH"),
        new ArchiveBatchReuseCase("full-source-hash",Full:true,Change:"source-hash",Error:"SOURCE_HASH"),
        new ArchiveBatchReuseCase("metadata-cancelled",Change:"cancel",Error:"CANCELLED")
    }.Select(x=>new object[]{x});
    public static byte[] First(ArchiveBatchReuseCase test)=>test.Full?LegacyAcquisitionCases.Java:LegacyAcquisitionCases.Xml;
    public static byte[] Second(ArchiveBatchReuseCase test)=>test.Full?Encoding.UTF8.GetBytes("package b; class D{}"):LegacyAcquisitionCases.Properties;
    public static bool Separate(ArchiveBatchReuseCase test)=>test.Change is "etag" or "revision";
    public static bool FirstFails(ArchiveBatchReuseCase test)=>test.Change is "tampered" or "cancel" or "profile";
    public static ArchiveBatchReuseObservation Expected(ArchiveBatchReuseCase test)
    {
        var count=FirstFails(test)?0:test.Error is null?2:1;
        return new(test.Error,test.Full?0:FirstFails(test)?0:12,test.Full?0:FirstFails(test)?0:Separate(test)?24:12,
            test.Full?(test.Change=="archive-hash"?2:1):0,count,test.Error is null,true,
            count==0?[]:count==1?[First(test)]:[First(test),Second(test)],true);
    }
    public static async Task AssertCase(ArchiveBatchReuseCase test,IArchiveBatchReuseProbe probe)
    {
        var actual=await probe.RunAsync(test);var expected=Expected(test);
        Assert.Equal(expected.Error,actual.Error);Assert.Equal(expected.ArchiveReads,actual.ArchiveReads);
        Assert.Equal(expected.SourceWrites,actual.SourceWrites);Assert.Equal(expected.Sealed,actual.Sealed);
        Assert.True(actual.Retained);Assert.True(actual.RequestPins);
        Assert.Equal(expected.Bytes.Count,actual.Bytes.Count);
        foreach(var pair in expected.Bytes.Zip(actual.Bytes))Assert.Equal(pair.First,pair.Second);
        if(test.Full||FirstFails(test)){Assert.Equal(0,actual.FirstReads);Assert.Equal(0,actual.TotalReads);}
        else {Assert.True(actual.FirstReads>0);Assert.Equal(actual.FirstReads*(Separate(test)?2:1),actual.TotalReads);}
    }
}
public sealed class ScriptedArchiveBatchReuseProbe(string? mutation=null):IArchiveBatchReuseProbe
{
    public Task<ArchiveBatchReuseObservation> RunAsync(ArchiveBatchReuseCase test)
    {
        var value=ArchiveBatchReuseCases.Expected(test);
        return Task.FromResult(mutation switch{
            "error"=>value with{Error="WRONG"},"archive"=>value with{ArchiveReads=value.ArchiveReads+1},
            "writes"=>value with{SourceWrites=value.SourceWrites+1},"seal"=>value with{Sealed=!value.Sealed},
            "retained"=>value with{Retained=false},"pins"=>value with{RequestPins=false},
            "bytes"=>value with{Bytes=value.Bytes.Count==0?new[]{new byte[]{0}}:value.Bytes.Select(_=>new byte[]{0}).ToArray()},
            "reads"=>value with{TotalReads=value.TotalReads+1},"first"=>value with{FirstReads=0,TotalReads=0},_=>value});
    }
}
[Trait("Gate","Mock")]
public sealed class ArchiveBatchReuseMockTests
{
    [Theory,MemberData(nameof(ArchiveBatchReuseCases.All),MemberType=typeof(ArchiveBatchReuseCases))]
    public Task CorrectOriginalInteractionContract(ArchiveBatchReuseCase test)=>ArchiveBatchReuseCases.AssertCase(test,new ScriptedArchiveBatchReuseProbe());
    [Theory,MemberData(nameof(ArchiveBatchReuseCases.All),MemberType=typeof(ArchiveBatchReuseCases))]
    public async Task EveryOutputAndInteractionRejectsIndependentMutation(ArchiveBatchReuseCase test)
    {
        foreach(var mutation in new[]{"error","archive","writes","seal","retained","pins","bytes","reads"})
            Assert.NotNull(await Record.ExceptionAsync(()=>ArchiveBatchReuseCases.AssertCase(test,new ScriptedArchiveBatchReuseProbe(mutation))));
        if(!test.Full&&!ArchiveBatchReuseCases.FirstFails(test))
            Assert.NotNull(await Record.ExceptionAsync(()=>ArchiveBatchReuseCases.AssertCase(test,new ScriptedArchiveBatchReuseProbe("first"))));
    }
}
