using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Tests.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Catalog;

/// <summary>One Catalog fixture preparation over the shared native producer/cache services.</summary>
public static class NativeCatalogSourceCacheInput
{
    public static async Task<(SourceExtractGenerationRequest Request,FileArtifactStore Generations,FileArtifactLeaseProvider Leases)> CreateAsync(
        CatalogIntakeFixture fixture,SourceJavaAdmission? java,ISourcesRuntimeFactory factory,IReadOnlyList<int> cached,
        Func<SourceExtractIndex,SourceExtractIndex>? indexMutation=null,SourceExtractionProfile? profile=null)
    {
        var canonical=new CanonicalJson();var producer=java is null ? new SourceProducerIdentityBuilder().Create().Identity : NativeSourceCacheGenerationFixture.Producer(java);
        var files=new JsonObject();foreach(var file in producer.ProducerFiles)files[file.Key]=file.Value;
        var recipe=new JsonObject{["recipe_version"]=1,["release"]="native-catalog-intake-fixture",["inventory"]=fixture.Inventory.DeepClone(),
            ["producer"]=new JsonObject{["path"]="native-fixture-producer",["files"]=files}};
        var recipeBytes=canonical.Serialize(JsonSerializer.SerializeToElement(recipe));
        var locked=new JsonObject{["recipe_file"]="recipe.json",["sha256"]=ContentHash.Sha256(recipeBytes),["source_rows"]=fixture.Inventory["sources"]!.AsArray().Count,["producer_files"]=producer.ProducerFiles.Count};
        var planned=new RehydrationPlanner(canonical).Plan(new(new("recipe.json",recipeBytes,"application/json"),new("recipe.lock.json",canonical.Serialize(JsonSerializer.SerializeToElement(locked)),"application/json")));
        Assert.True(planned.IsValid,JsonSerializer.Serialize(planned.Issues));var projection=planned.Plan!.SourceProjection!;
        var request=new SourceExtractGenerationRequest(projection,profile ?? SourceExtractionProfile.CurrentCandidate,producer,Java:java);
        var generations=new FileArtifactStore(Path.Combine(fixture.Root,"source generation"),Hash);
        var leases=new FileArtifactLeaseProvider(Path.Combine(fixture.Root,"source generation locks"));
        var raw=new FileArtifactStore(Path.Combine(fixture.Root,"producer raw originals"),Hash);
        foreach(var physical in projection.PhysicalSources)
        {
            var source=projection.Rows.First(row=>row.PhysicalSourceIndex==physical.PhysicalSourceIndex);
            await raw.WriteAsync(new(physical.Pin.LocalPath,fixture.Originals[source.OriginalRow.GetProperty("path").GetString()!],"application/octet-stream"));
        }
        if(cached.Count!=0)
            request=(await NativeSourceCacheGenerationFixture.CreateAsync(request,raw,generations,leases,factory,cached,"prepared-native-input",indexMutation:indexMutation)).Request;
        // Prepared cache bytes are input originals for the Catalog consumer,
        // just as parser-produced gzip files were in the original fixture.
        foreach(var directory in new[]{"source generation","producer raw originals"})
        {
            var retained=Path.Combine(fixture.Root,directory);
            if(!Directory.Exists(retained))continue; // Explicit no-index fixture has no cache generation.
            foreach(var path in Directory.EnumerateFiles(retained,"*",SearchOption.AllDirectories))
                fixture.Originals.Add(Path.GetRelativePath(fixture.Root,path),await File.ReadAllBytesAsync(path));
        }
        return(request,generations,leases);
    }
    private static string Hash(ReadOnlyMemory<byte> bytes)=>ContentHash.Sha256(bytes.Span);
}
