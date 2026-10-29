using System.IO.Compression;
using System.Text.Json;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Publication;

[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-007")]
public sealed class RehydrationMetadataReferenceRealTests
{
    [Theory,MemberData(nameof(RehydrationMetadataReferenceCases.Cases),MemberType=typeof(RehydrationMetadataReferenceCases))]
    public void SameCasesUseSoleNativeRecipeWalk(MetadataReferenceProjectionCase c)=>
        RehydrationMetadataReferenceCases.AssertCase(c,new NativeMetadataHarness(c));

    private sealed class NativeMetadataHarness:ISnapshotProjectionHarness
    {
        private readonly CountingCanonical canonical;
        public NativeMetadataHarness(MetadataReferenceProjectionCase c)
        {
            var recipe=RehydrationMetadataReferenceCases.Request(c).Recipe.Bytes.ToArray();
            if(c.GZip){
                using var input=new MemoryStream(recipe,false);
                using var gzip=new GZipStream(input,CompressionMode.Decompress);
                using var output=new MemoryStream();gzip.CopyTo(output);recipe=output.ToArray();
            }
            canonical=new(recipe);var planner=new RehydrationPlanner(canonical);
            Planner=planner;SeedPlanner=planner;
        }
        public IRehydrationSnapshotPlanner Planner {get;}
        public IRehydrationSeedPlanner SeedPlanner {get;}
        public int RecipeParseCalls=>canonical.RecipeParseCalls;
    }
    private sealed class CountingCanonical(byte[] recipe):ICanonicalJson
    {
        private readonly ICanonicalJson inner=new CanonicalJson();
        public int RecipeParseCalls {get;private set;}
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
