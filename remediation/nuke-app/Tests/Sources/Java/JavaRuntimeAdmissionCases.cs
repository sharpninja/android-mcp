using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using AndroidMcp.BuildTool.Tests.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

internal sealed record JavaRuntimeAdmissionCase(string Id,bool Accepted=false);
internal sealed record JavaRuntimeAdmissionFixture(string Root,SourceJavaAdmission Admission);
internal static class JavaRuntimeAdmissionCases
{
    internal static readonly IReadOnlyList<JavaRuntimeAdmissionCase> All=[
        new("valid",true),new("extra-metadata",true),new("exports-reordered",true),new("relative-admission",true),new("unicode-asset-path",true),
        new("schema"),new("runtime-version"),new("runtime-commit"),new("runtime-tag"),new("grammar-version"),new("grammar-commit"),
        new("rid"),new("abi"),new("length"),new("hash"),new("hash-uppercase"),new("hash-short"),new("path-parent"),new("path-absolute"),new("path-backslash"),
        new("exports-missing"),new("exports-extra"),new("exports-duplicate"),new("exports-absent"),new("artifact-missing"),new("pin-missing"),new("root-array"),
        new("invalid-json"),new("duplicate-hidden"),new("unicode-hidden"),new("nonfinite-hidden"),new("invalid-utf8"),new("manifest-missing"),new("library-missing"),
        new("library-link"),new("manifest-link"),new("bad-native-image")];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    internal static JavaRuntimeAdmissionCase Get(string id)=>All.Single(x=>x.Id==id);
    internal static readonly byte[] MockLibrary="opaque fixture native image"u8.ToArray();
    private static byte[] JsonVector(string id){var test=CanonicalJsonCases.Values.Single(x=>x.Id==id);return test.RawBytes??Encoding.UTF8.GetBytes(test.Raw);}
    private static byte[] WithHiddenVector(JsonObject manifest,byte[] vector)
    {
        using var stream=new MemoryStream();using(var writer=new Utf8JsonWriter(stream))
        {writer.WriteStartObject();foreach(var property in JsonSerializer.SerializeToElement(manifest).EnumerateObject())property.WriteTo(writer);writer.WritePropertyName("extra_metadata");writer.WriteRawValue(vector,skipInputValidation:true);writer.WriteEndObject();writer.Flush();}
        return stream.ToArray();
    }
    internal static JavaRuntimeAdmissionFixture Create(JavaRuntimeAdmissionCase test,byte[] material,string? fixtureParent=null)
    {
        var root=Path.Combine(fixtureParent??Path.GetTempPath(),"androidmcp-java-runtime-admission",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"native"));
        var relative=test.Id=="unicode-asset-path"?"native/lib java 雪.so":"native/parser.so";
        var bytes=test.Id=="bad-native-image"?"not a native shared library"u8.ToArray():material.ToArray();
        var library=Path.Combine(root,relative);File.WriteAllBytes(library,bytes);
        var artifact=new JsonObject{["relative_path"]=relative,["sha256"]=ContentHash.Sha256(bytes),["length"]=bytes.LongLength,["language_abi"]=14};
        var exports=new JsonArray(JavaNativeParser.RequiredSymbols.Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());
        var manifest=new JsonObject{["schema"]="androidmcp-java-native-source-lock-v1",["runtime_version"]="0.25.2",["runtime_commit"]="6e0618704ad758ba2ea5822faa80bcd36fbeba3d",["runtime_tag_object"]="695d1530e59804279d9c739335b73b882d9f1caf",["grammar_version"]="0.23.5",["grammar_commit"]="94703d5a6bed02b98e438d7cad1136c01a60ba2c",["rid"]="linux-x64",["native_artifact"]=artifact,["required_exports"]=exports};
        switch(test.Id)
        {
            case "extra-metadata":manifest["extra_metadata"]=new JsonObject{["retained"]=true};break;
            case "exports-reordered":manifest["required_exports"]=new JsonArray(JavaNativeParser.RequiredSymbols.Reverse().Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray());break;
            case "schema":manifest["schema"]="unknown-native-lock-v2";break;
            case "runtime-version":manifest["runtime_version"]="0.26.0";break;
            case "runtime-commit":manifest["runtime_commit"]=new string('0',40);break;
            case "runtime-tag":manifest["runtime_tag_object"]="6e0618704ad758ba2ea5822faa80bcd36fbeba3d";break;
            case "grammar-version":manifest["grammar_version"]="0.23.4";break;
            case "grammar-commit":manifest["grammar_commit"]=new string('0',40);break;
            case "rid":manifest["rid"]="win-x64";break;
            case "abi":artifact["language_abi"]=15;break;
            case "length":artifact["length"]=bytes.LongLength+1;break;
            case "hash":artifact["sha256"]=new string('0',64);break;
            case "hash-uppercase":artifact["sha256"]=ContentHash.Sha256(bytes).ToUpperInvariant();break;
            case "hash-short":artifact["sha256"]="abc";break;
            case "path-parent":artifact["relative_path"]="../outside.so";break;
            case "path-absolute":artifact["relative_path"]=library;break;
            case "path-backslash":artifact["relative_path"]="native\\parser.so";break;
            case "exports-missing":exports.RemoveAt(0);break;
            case "exports-extra":exports.Add("not-an-approved-export");break;
            case "exports-duplicate":exports.Add(JavaNativeParser.RequiredSymbols[0]);break;
            case "exports-absent":manifest.Remove("required_exports");break;
            case "artifact-missing":manifest.Remove("native_artifact");break;
            case "pin-missing":manifest.Remove("runtime_commit");break;
            case "library-missing":File.Delete(library);break;
            case "library-link":var alias=Path.Combine(root,"native/link.so");File.CreateSymbolicLink(alias,library);artifact["relative_path"]="native/link.so";break;
        }
        var raw=JsonSerializer.SerializeToUtf8Bytes(manifest);
        raw=test.Id switch{
            "root-array"=>JsonSerializer.SerializeToUtf8Bytes(Array.Empty<string>()),
            "invalid-json"=>JsonVector("JSON-33-two-values"),
            "invalid-utf8"=>JsonVector("JSON-36-invalid-utf8"),
            "duplicate-hidden"=>WithHiddenVector(manifest,JsonVector("JSON-25-escaped-duplicate")),
            "unicode-hidden"=>WithHiddenVector(manifest,JsonVector("JSON-28-unpaired-surrogate")),
            "nonfinite-hidden"=>WithHiddenVector(manifest,JsonVector("JSON-27-overflow")),_=>raw};
        var path=Path.Combine(root,"native-source-lock.json");File.WriteAllBytes(path,raw);
        if(test.Id=="manifest-missing")File.Delete(path);
        if(test.Id=="manifest-link"){var alias=Path.Combine(root,"linked-manifest.json");File.CreateSymbolicLink(alias,path);path=alias;}
        return new(root,new(test.Id=="relative-admission"?Path.GetRelativePath(Environment.CurrentDirectory,path):path));
    }
    internal static void AssertContract(JavaRuntimeAdmissionCase test,IJavaSourceRuntimeLoader loader,JavaRuntimeAdmissionFixture fixture)
    {
        var before=Directory.EnumerateFiles(fixture.Root,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>ContentHash.Sha256(File.ReadAllBytes(x)),StringComparer.Ordinal);
        var original=fixture.Admission.ManifestPath;IJavaSourceRuntime? runtime=null;
        try
        {
            var error=Record.Exception(()=>runtime=loader.Open(fixture.Admission));
            if(!test.Accepted){Assert.NotNull(error);Assert.Null(runtime);}
            else
            {
                Assert.Null(error);Assert.NotNull(runtime);var extractor=runtime!.Extractor;Assert.Same(extractor,runtime.Extractor);
                JavaContractCases.AssertContract(JavaContractCases.Get("JAVA-07"),extractor);
                runtime.Dispose();Assert.Throws<ObjectDisposedException>(()=>_ = runtime.Extractor);
                Assert.Throws<ObjectDisposedException>(()=>extractor.Extract(JavaContractCases.Get("JAVA-07").Input()));runtime.Dispose();
            }
            Assert.Equal(original,fixture.Admission.ManifestPath);
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal),Directory.EnumerateFiles(fixture.Root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach(var file in before)Assert.Equal(file.Value,ContentHash.Sha256(File.ReadAllBytes(file.Key)));
        }
        finally{runtime?.Dispose();}
    }
}
