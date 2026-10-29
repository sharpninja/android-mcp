using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using AndroidMcp.BuildTool.Publication;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

// One variant of the existing offline Fetch CLI fixture, not a second runner.
// Native binding: pass Request() to the existing NativeSourcesFetchCliProbe
// through an optional typed request constructor; its default stays unchanged.
public static class SourcesFetchAliasCliCases
{
    public static RehydrationPlanRequest Request()
    {
        var original=SourcesFetchCliCases.Request();
        var recipe=JsonNode.Parse(original.Recipe.Bytes.Span)!.AsObject();
        var rows=recipe["inventory"]!["sources"]!.AsArray();
        var first=rows[0]!.AsObject();
        first["revision"]="first-alias"; first["url"]="https://example.invalid/first-alias";
        first["transport"]!["required_local_snapshot"]=null;
        var last=first.DeepClone().AsObject(); last["path"]="aliases/A.txt";
        last["revision"]="last-alias";last["url"]="https://example.invalid/last-alias";
        last["transport"]!["required_local_snapshot"]="snapshots/a.txt";
        rows.Add(last);
        var bytes=JsonSerializer.SerializeToUtf8Bytes(recipe);
        var recipeLock=JsonNode.Parse(original.Lock.Bytes.Span)!.AsObject();
        recipeLock["sha256"]=ContentHash.Sha256(bytes);recipeLock["source_rows"]=3;
        return original with { Recipe=original.Recipe with { Bytes=bytes },
            Lock=original.Lock with { Bytes=JsonSerializer.SerializeToUtf8Bytes(recipeLock) },
            Profile=AcquisitionCompatibilityProfile.GenericRehydrate };
    }
    public static JsonObject MockReceipt()
    {
        var receipt=SourcesFetchCliCases.MockReceipt();
        receipt["TotalRows"]=3;receipt["SelectedRows"]=3;receipt["ProcessedRows"]=3;
        var projection=receipt["Projection"]!.AsObject();
        projection["RecipeSha256"]=ContentHash.Sha256(Request().Recipe.Bytes.Span);
        projection["Profile"]=(int)AcquisitionCompatibilityProfile.GenericRehydrate;
        var physical=projection["PhysicalSources"]!.AsArray();
        var pin=physical[0]!["Pin"]!.AsObject();pin["Url"]="https://example.invalid/last-alias";pin["Revision"]="last-alias";
        pin["Sha256"]=SourcesFetchCliCases.ExpectedData["blobs/a"];pin["SizeBytes"]=3;
        physical[0]!["SelectionRowIndices"]=new JsonArray(0,2);
        physical[0]!["RequiredSnapshots"]=new JsonArray(new JsonObject { ["SourcePath"]="aliases/A.txt",["LocalPath"]="blobs/a",
            ["BundlePath"]="snapshots/a.txt",["Sha256"]=SourcesFetchCliCases.ExpectedData["blobs/a"],["Length"]=3 });
        physical[0]!["Prerequisites"]=new JsonArray();
        using var document=JsonDocument.Parse(Request().Recipe.Bytes);
        projection["Rows"]=new JsonArray(document.RootElement.GetProperty("inventory").GetProperty("sources").EnumerateArray()
            .Select((row,index)=>(JsonNode)new JsonObject { ["RowIndex"]=index,["PhysicalSourceIndex"]=index == 1 ? 1 : 0,
                ["OriginalRow"]=JsonNode.Parse(row.GetRawText()) }).ToArray());
        return receipt;
    }
    public static async Task AssertCase(ISourcesFetchCliProbe provider)
    {
        var observed=await provider.RunAsync();Assert.Equal(0,observed.FirstExit);Assert.Equal(0,observed.ResumeExit);
        Assert.True(observed.InputsRetained);Assert.True(observed.BundleRetained);
        Assert.Equal(observed.ReceiptBefore,observed.ReceiptAfter);
        Assert.Equal(observed.OutputsBefore.OrderBy(x=>x.Key),observed.OutputsAfter.OrderBy(x=>x.Key));
        Assert.Equal(SourcesFetchCliCases.ExpectedData.Keys.Append(SourcesFetchCliCases.LeasePath).Append("sources/acquisition.json").Order(),observed.OutputsBefore.Keys.Order());
        foreach(var pin in SourcesFetchCliCases.ExpectedData)Assert.Equal(pin.Value,observed.OutputsBefore[pin.Key]);
        Assert.Equal(ContentHash.Sha256(observed.ReceiptBefore),observed.OutputsBefore["sources/acquisition.json"]);
        using var document=JsonDocument.Parse(observed.ReceiptBefore);var receipt=document.RootElement;
        Assert.Equal(SourcesFetchCliCases.Generation,receipt.GetProperty("GenerationId").GetString());
        foreach(var field in new[] {"TotalRows","SelectedRows","ProcessedRows"})Assert.Equal(3,receipt.GetProperty(field).GetInt32());
        foreach(var field in new[] {"TotalPhysicalSources","SelectedPhysicalSources"})Assert.Equal(2,receipt.GetProperty(field).GetInt32());
        Assert.True(receipt.GetProperty("Complete").GetBoolean());Assert.False(receipt.GetProperty("HistoricalByteIdentityClaimed").GetBoolean());
        var projection=receipt.GetProperty("Projection");Assert.Equal((int)AcquisitionCompatibilityProfile.GenericRehydrate,projection.GetProperty("Profile").GetInt32());
        Assert.Equal(ContentHash.Sha256(Request().Recipe.Bytes.Span),projection.GetProperty("RecipeSha256").GetString());
        var physical=projection.GetProperty("PhysicalSources").EnumerateArray().ToArray();Assert.Equal(2,physical.Length);
        Assert.Equal(new[] {"blobs/a","blobs/b"},physical.Select(p=>p.GetProperty("Pin").GetProperty("LocalPath").GetString()));
        var selected=physical[0];var source=selected.GetProperty("Pin");
        Assert.Equal("https://example.invalid/last-alias",source.GetProperty("Url").GetString());
        Assert.Equal("last-alias",source.GetProperty("Revision").GetString());
        Assert.Equal("snapshots/a.txt",source.GetProperty("RequiredLocalSnapshot").GetString());
        Assert.Equal(SourcesFetchCliCases.ExpectedData["blobs/a"],source.GetProperty("Sha256").GetString());Assert.Equal(3,source.GetProperty("SizeBytes").GetInt64());
        Assert.Equal(new[] {0,2},selected.GetProperty("SelectionRowIndices").EnumerateArray().Select(x=>x.GetInt32()));
        var snapshot=Assert.Single(selected.GetProperty("RequiredSnapshots").EnumerateArray());
        Assert.Equal("aliases/A.txt",snapshot.GetProperty("SourcePath").GetString());
        Assert.Equal("snapshots/a.txt",snapshot.GetProperty("BundlePath").GetString());Assert.Empty(selected.GetProperty("Prerequisites").EnumerateArray());
        using var recipe=JsonDocument.Parse(Request().Recipe.Bytes);
        var expectedRows=recipe.RootElement.GetProperty("inventory").GetProperty("sources").EnumerateArray().ToArray();
        var actualRows=projection.GetProperty("Rows").EnumerateArray().ToArray();Assert.Equal(3,actualRows.Length);
        var canonical=new CanonicalJson();
        for(var index=0;index<3;index++)
        {
            Assert.Equal(index,actualRows[index].GetProperty("RowIndex").GetInt32());
            Assert.Equal(index == 1 ? 1 : 0,actualRows[index].GetProperty("PhysicalSourceIndex").GetInt32());
            Assert.Equal(canonical.Serialize(expectedRows[index]),canonical.Serialize(actualRows[index].GetProperty("OriginalRow")));
        }
        Assert.Equal(2,observed.Arguments.Count);
        foreach(var args in observed.Arguments)
        {
            Assert.Equal("exec",args[0]);Assert.Contains(args,x=>Path.GetFileName(x)=="AndroidMcp.Build.dll");
            Assert.DoesNotContain(args,x=>x is "build" or "run" or "--internal-process-probe");
            string Value(string name){var index=args.ToList().IndexOf(name);Assert.InRange(index,0,args.Count-2);return args[index+1];}
            Assert.Equal("Sources",Value("--target"));Assert.Equal("Fetch",Value("--operation"));
            Assert.Equal("GenericRehydrate",Value("--acquisition-profile"));Assert.Equal("2",Value("--limit"));
            Assert.Equal("PreserveAliases",Value("--cache-policy"));Assert.Equal(SourcesFetchCliCases.Generation,Value("--generation"));
            Assert.Equal(observed.RuntimeRoot,Value("--root"));Assert.Equal(observed.InputRoot,Value("--project-root"));
            Assert.Equal(observed.BundleRoot,Value("--bundle-root"));Assert.Equal(observed.OutputRoot,Value("--output-root"));
            Assert.Equal(Path.Combine(observed.InputRoot,"rehydration/fixture.json"),Value("--source-manifest"));
            Assert.Equal(Path.Combine(observed.InputRoot,"rehydration/fixture.lock.json"),Value("--recipe-lock"));
            Assert.NotEqual(observed.InputRoot,observed.RuntimeRoot);Assert.NotEqual(observed.OutputRoot,observed.RuntimeRoot);Assert.NotEqual(observed.BundleRoot,observed.RuntimeRoot);
        }
    }
}
public sealed class ScriptedSourcesFetchAliasCliProbe(string? mutation=null) : ISourcesFetchCliProbe
{
    public Task<SourcesFetchCliObservation> RunAsync()
    {
        var receipt=SourcesFetchAliasCliCases.MockReceipt();var projection=receipt["Projection"]!.AsObject();
        var first=projection["PhysicalSources"]![0]!;
        switch(mutation)
        {
            case "profile": projection["Profile"]=0;break;
            case "recipe": projection["RecipeSha256"]="wrong";break;
            case "first-url": first["Pin"]!["Url"]="https://example.invalid/first-alias";break;
            case "first-revision": first["Pin"]!["Revision"]="first-alias";break;
            case "missing-snapshot": first["Pin"]!["RequiredLocalSnapshot"]=null;break;
            case "wrong-sha": first["Pin"]!["Sha256"]="wrong";break;
            case "wrong-size": first["Pin"]!["SizeBytes"]=9;break;
            case "extra-prerequisite": first["Prerequisites"]=new JsonArray(new JsonObject());break;
            case "operational-first": first["RequiredSnapshots"]![0]!["SourcePath"]="api/A.txt";break;
            case "operational-missing": first["RequiredSnapshots"]=new JsonArray();break;
            case "row-correlation": projection["Rows"]![2]!["PhysicalSourceIndex"]=1;break;
            case "original-row": projection["Rows"]![0]!["OriginalRow"]!["transport"]!["required_local_snapshot"]="snapshots/a.txt";break;
            case "row-omitted": projection["Rows"]!.AsArray().RemoveAt(2);break;
            case "logical-count": receipt["TotalRows"]=2;break;
            case "physical-count": receipt["TotalPhysicalSources"]=3;break;
            case "complete": receipt["Complete"]=false;break;
            case "historical": receipt["HistoricalByteIdentityClaimed"]=true;break;
        }
        var bytes=JsonSerializer.SerializeToUtf8Bytes(receipt);
        var outputs=SourcesFetchCliCases.ExpectedData.ToDictionary(x=>x.Key,x=>x.Value);
        outputs[SourcesFetchCliCases.LeasePath]=ContentHash.Sha256(Array.Empty<byte>());outputs["sources/acquisition.json"]=ContentHash.Sha256(bytes);
        if(mutation == "raw-bytes")outputs["blobs/a"]="wrong";
        var after=outputs.ToDictionary(x=>x.Key,x=>x.Value);if(mutation == "replay-output")after["blobs/b"]="wrong";
        var args=SourcesFetchCliCases.Arguments("/test/runtime","/test/input","/test/bundle","/test/output");
        args[Array.IndexOf(args,"--acquisition-profile")+1]=mutation == "argument-profile" ? "CanonicalRestore" : "GenericRehydrate";
        if(mutation == "child-build")args[0]="build";
        return Task.FromResult(new SourcesFetchCliObservation(mutation == "first-exit" ? 1 : 0,mutation == "resume-exit" ? 1 : 0,
            bytes,mutation == "replay-receipt" ? [0] : bytes,outputs,after,mutation != "inputs",mutation != "bundle",[args,args],
            "/test/runtime","/test/input","/test/bundle","/test/output"));
    }
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class SourcesFetchAliasCliMockTests
{
    [Fact]public Task CorrectGenericLastAliasAndSealedReplayMock() => SourcesFetchAliasCliCases.AssertCase(new ScriptedSourcesFetchAliasCliProbe());
    [Fact]public async Task WrongAliasRowsPinsCountsAndInvocationsAreRejected()
    {
        foreach(var mutation in new[] {"profile","recipe","first-url","first-revision","missing-snapshot","wrong-sha","wrong-size",
            "extra-prerequisite","operational-first","operational-missing","row-correlation","original-row","row-omitted","logical-count",
            "physical-count","complete","historical","raw-bytes","replay-output","argument-profile","child-build","first-exit","resume-exit",
            "replay-receipt","inputs","bundle"})
            Assert.NotNull(await Record.ExceptionAsync(()=>SourcesFetchAliasCliCases.AssertCase(new ScriptedSourcesFetchAliasCliProbe(mutation))));
    }
}
