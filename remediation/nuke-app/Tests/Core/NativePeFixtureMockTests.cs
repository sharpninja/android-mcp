using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Review.Staging;

public sealed record PeFixtureObservation(string Hash,string RepeatedHash,Guid Module,Guid RepeatedModule,
    int Stamp,int RepeatedStamp,int Length,int RepeatedLength);
public static class PeFixtureCases
{
    public static readonly Guid Module=new("33333333-3333-3333-3333-333333333333");
    public static PeFixtureObservation Correct=>new(new string('e',64),new string('e',64),Module,Module,0,0,512,512);
    public static void AssertContract(PeFixtureObservation actual)
    {
        Assert.Matches("^[0-9a-f]{64}$",actual.Hash);Assert.Equal(actual.Hash,actual.RepeatedHash);
        Assert.Equal(Module,actual.Module);Assert.Equal(Module,actual.RepeatedModule);
        Assert.Equal(0,actual.Stamp);Assert.Equal(0,actual.RepeatedStamp);
        Assert.True(actual.Length>0);Assert.Equal(actual.Length,actual.RepeatedLength);
    }
    public static IEnumerable<object[]> Mutations()
    {
        yield return [Correct with{Hash="invalid"}];
        yield return [Correct with{RepeatedHash=new string('f',64)}];
        yield return [Correct with{Module=Guid.Empty}];
        yield return [Correct with{RepeatedModule=Guid.Empty}];
        yield return [Correct with{Stamp=1}];
        yield return [Correct with{RepeatedStamp=1}];
        yield return [Correct with{Length=0}];
        yield return [Correct with{RepeatedLength=511}];
    }
}
public sealed class NativePeFixtureMockTests
{
    [Fact]public void Independently_declared_stable_PE_contract_passes()=>PeFixtureCases.AssertContract(PeFixtureCases.Correct);
    [Theory,MemberData(nameof(PeFixtureCases.Mutations),MemberType=typeof(PeFixtureCases))]
    public void Every_identity_timestamp_hash_or_size_mutation_is_rejected(PeFixtureObservation wrong)
        =>Assert.ThrowsAny<Exception>(()=>PeFixtureCases.AssertContract(wrong));
}
