using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Acquisition;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationAliasPrecedenceRealTests
{
    [Theory,MemberData(nameof(RehydrationAliasPrecedenceCases.Cases),MemberType=typeof(RehydrationAliasPrecedenceCases))]
    public void SameCasesUseSingleNativePlanner(AliasPrecedenceCase c) =>
        RehydrationAliasPrecedenceCases.AssertCase(c,new NativeAliasHarness(c));

    private sealed class NativeAliasHarness : IAliasPrecedenceHarness
    {
        private readonly CountingCanonical canonical;
        public NativeAliasHarness(AliasPrecedenceCase c)
        {
            canonical=new(RehydrationAliasPrecedenceCases.Request(c).Recipe.Bytes.ToArray());
            Planner=new RehydrationPlanner(canonical);
        }
        public IRehydrationPlanner Planner { get; }
        public int RecipeParseCalls=>canonical.RecipeParseCalls;
    }
    // Same observation seam as Publication's established native Seed cases.
    // Only the dependency binding changes; assertions/fixtures stay unchanged.
    private sealed class CountingCanonical(byte[] recipe) : ICanonicalJson
    {
        private readonly ICanonicalJson inner=new CanonicalJson();
        public int RecipeParseCalls { get;private set; }
        public byte[] Serialize(ReadOnlyMemory<byte> rawUtf8)
        {
            if(rawUtf8.Span.SequenceEqual(recipe))RecipeParseCalls++;
            return inner.Serialize(rawUtf8);
        }
        public byte[] Serialize(JsonElement value)=>inner.Serialize(value);
        public string Hash(ReadOnlyMemory<byte> rawUtf8)=>inner.Hash(rawUtf8);
        public string Hash(JsonElement value)=>inner.Hash(value);
    }
}
