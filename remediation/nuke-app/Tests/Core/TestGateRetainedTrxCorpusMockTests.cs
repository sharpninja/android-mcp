using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record RetainedTrxCase(string Name,string File,string Sha256,bool Admitted,int Total,int Passed,int Failed);
public static class RetainedTrxCorpus
{
    public static readonly RetainedTrxCase[] Cases=
    [new("actual-18-pass","passed-18.trx","2d79b3a0603bb3ce3546cedfdfba4ed20c759b63cfbbde1764da498e4c992a92",true,18,18,0),
     new("actual-stale-host-16-pass-2-fail","failed-18.trx","cca38ebf0f9d3cd12e237d2063667668b337b289d4296bb76aa6a560db9a6e3f",false,18,16,2),
     new("actual-zero-discovered","zero-discovery.trx","09f0cc3363fcfbff181604b1257a0b1af5be5228f44b29d6fcbdb2bf9e4b54d5",false,0,0,0)];
    public static IEnumerable<object[]> Data=>Cases.Select(x=>new object[]{x.Name});
    public static RetainedTrxCase Find(string name)=>Cases.Single(x=>x.Name==name);
    public static byte[] Read(RetainedTrxCase fixture)
    {
        for(DirectoryInfo? directory=new DirectoryInfo(AppContext.BaseDirectory);directory is not null;directory=directory.Parent)
        {
            var path=Path.Combine(directory.FullName,"Tests","Core","Fixtures","TrxAdmission",fixture.File);
            if(!File.Exists(path))continue;
            var bytes=File.ReadAllBytes(path);
            Assert.Equal(fixture.Sha256,ContentHash.Sha256(bytes));return bytes;
        }
        throw new FileNotFoundException("Pinned actual retained TRX fixture is required: "+fixture.File);
    }
    public static void AssertContract(RetainedTrxCase fixture,TrxAdmissionRequest request,TrxAdmissionResult actual,int calls,IReadOnlyList<string> effects)
    {
        Assert.True(request.TrxBytes.HasValue);Assert.Equal(fixture.Sha256,ContentHash.Sha256(request.TrxBytes!.Value.Span));
        Assert.Empty(request.ExpectedClasses);Assert.Equal(1,calls);Assert.Empty(effects);
        Assert.Equal(fixture.Admitted,actual.Admitted);Assert.Equal(fixture.Sha256,actual.RawTrxSha256);Assert.Empty(actual.MissingExpectedClasses);
        if(fixture.Admitted)Assert.Equal("OK",actual.Code);else Assert.False(string.IsNullOrWhiteSpace(actual.Code)||actual.Code=="OK");
        Assert.Equal(fixture.Total,actual.ResultCount);
        if(fixture.Total>0)
        {
            Assert.NotNull(actual.Counters);Assert.Equal(fixture.Total,actual.Counters!.Total);Assert.Equal(fixture.Total,actual.Counters.Executed);
            Assert.Equal(fixture.Passed,actual.Counters.Passed);Assert.Equal(fixture.Failed,actual.Counters.Failed);Assert.Equal(0,actual.Counters.NotExecuted);
            Assert.Equal(fixture.Total,actual.ClassCounts.Values.Sum());
        }
        else
        {
            Assert.Empty(actual.ClassCounts);
            if(actual.Counters is not null)Assert.Equal(new TrxRunCounters(0,0,0,0,0),actual.Counters);
        }
    }
}
public sealed class ScriptedRetainedTrxAdmission(RetainedTrxCase fixture):ITrxResultAdmission
{
    public int Calls { get; private set; }
    public TrxAdmissionResult Evaluate(TrxAdmissionRequest request)
    {
        Calls++;
        return new(fixture.Admitted,fixture.Admitted ? "OK" : "DECLINED",fixture.Total==0 ? null : new(fixture.Total,fixture.Total,fixture.Passed,fixture.Failed,0),
            fixture.Total,fixture.Total==0 ? new Dictionary<string,int>() : new Dictionary<string,int>{{"retained-fixture",fixture.Total}},[],fixture.Sha256);
    }
}
[Trait("Gate","Mock")]
public sealed class TestGateRetainedTrxCorpusMockTests
{
    [Theory,MemberData(nameof(RetainedTrxCorpus.Data),MemberType=typeof(RetainedTrxCorpus))]
    public void CorrectActualReceiptFixtureMock(string name)
    {
        var fixture=RetainedTrxCorpus.Find(name);var request=new TrxAdmissionRequest(RetainedTrxCorpus.Read(fixture),[]);
        var service=new ScriptedRetainedTrxAdmission(fixture);var actual=service.Evaluate(request);
        RetainedTrxCorpus.AssertContract(fixture,request,actual,service.Calls,[]);
    }
    public static IEnumerable<object[]> Controls=>RetainedTrxCorpus.Cases.SelectMany(x=>Enumerable.Range(0,10).Select(memberIndex=>new object[]{x.Name,memberIndex}));
    [Theory,MemberData(nameof(Controls))]
    public void EachAdmissionHashCounterIdentityAndEffectControlRejects(string name,int field)
    {
        var fixture=RetainedTrxCorpus.Find(name);var request=new TrxAdmissionRequest(RetainedTrxCorpus.Read(fixture),[]);
        var service=new ScriptedRetainedTrxAdmission(fixture);var actual=service.Evaluate(request);var calls=service.Calls;IReadOnlyList<string> effects=[];
        switch(field)
        {
            case 0:actual=actual with { Admitted=!actual.Admitted };break;
            case 1:actual=actual with { RawTrxSha256="wrong" };break;
            case 2:actual=actual with { ResultCount=actual.ResultCount+1 };break;
            case 3:actual=actual with { Counters=actual.Counters is null ? new(1,1,1,0,0) : actual.Counters with { Total=actual.Counters.Total+1 } };break;
            case 4:actual=actual with { ClassCounts=new Dictionary<string,int>{{"wrong",999}} };break;
            case 5:actual=actual with { MissingExpectedClasses=["wrong"] };break;
            case 6:actual=actual with { Code=fixture.Admitted ? "DECLINED" : "OK" };break;
            case 7:calls++;break;
            case 8:effects=["unexpected-effect"];break;
            case 9:request=request with { ExpectedClasses=["unsupplied-class"] };break;
        }
        Assert.NotNull(Record.Exception(()=>RetainedTrxCorpus.AssertContract(fixture,request,actual,calls,effects)));
    }
}
