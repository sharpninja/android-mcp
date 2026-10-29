using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed class TestGateProducerOriginNativeTests
{
    [Theory,MemberData(nameof(ProducerOriginCases.Data),MemberType=typeof(ProducerOriginCases))]
    public async Task Same_seven_origin_handoff_cases_bind_the_native_producer(string name)
    {
        var fixture=new ProducerOriginFixture(name);var io=new ProducerMockDependencies(fixture.Original);
        var facts=new OriginAwareProducerFacts(fixture,io);
        var native=new NativeTestGateEvidenceProducer(io,io,facts,new RecordingNativeTestBuildAdmission(io),
            new RecordingNativeProducerTrxAdmission(io),io);
        TestGateProducerResult? completed=null;var code="OK";
        try{completed=await native.ProduceAsync(fixture.Original.Request);code=completed.Code;}
        catch(TestGateFactsException error){code=error.Code;}
        var observed=new ProducerOriginObservation(code,completed is null ? null : io.Observe(completed),
            io.Processes,facts.OriginCalls,io.Order);
        ProducerOriginCases.AssertContract(fixture,JsonSerializer.SerializeToNode(observed)!);
    }
}
