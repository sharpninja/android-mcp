using System.IO.Compression;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationSeedRealTests
{
    [Theory,MemberData(nameof(RehydrationSeedCases.Cases),MemberType=typeof(RehydrationSeedCases))]
    public void SameCasesUseSingleNativeRecipeWalk(SeedProjectionCase c) =>
        RehydrationSeedCases.AssertCase(c,new NativeSeedHarness(c));

    private sealed class NativeSeedHarness : ISeedProjectionHarness
    {
        private readonly CountingCanonical canonical;
        public NativeSeedHarness(SeedProjectionCase c)
        {
            var recipe=RehydrationSeedCases.Request(c).Recipe.Bytes.ToArray();
            if(c.GZip) {
                using var input=new MemoryStream(recipe,false);
                using var gzip=new GZipStream(input,CompressionMode.Decompress);
                using var output=new MemoryStream();gzip.CopyTo(output);recipe=output.ToArray();
            }
            canonical=new(recipe);
            Planner=new RehydrationPlanner(canonical);
        }
        public IRehydrationSeedPlanner Planner { get; }
        public int RecipeParseCalls=>canonical.RecipeParseCalls;
    }
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
