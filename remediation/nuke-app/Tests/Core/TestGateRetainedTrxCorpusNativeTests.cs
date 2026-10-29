using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateRetainedTrxCorpusNativeTests
{
    [Theory,MemberData(nameof(RetainedTrxCorpus.Data),MemberType=typeof(RetainedTrxCorpus))]
    public void SameActualRetainedTrxBytesBindToSharedAdmission(string name)
    {
        var fixture=RetainedTrxCorpus.Find(name);var request=new TrxAdmissionRequest(RetainedTrxCorpus.Read(fixture),[]);
        var calls=new List<TrxAdmissionRequest>();ITrxResultAdmission service=new RecordingNativeTrxAdmission(new TrxResultAdmission(),calls);
        var actual=service.Evaluate(request);RetainedTrxCorpus.AssertContract(fixture,request,actual,calls.Count,[]);
    }
}
