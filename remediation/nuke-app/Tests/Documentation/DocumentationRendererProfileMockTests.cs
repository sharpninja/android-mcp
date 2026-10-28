using System.Text;
using System.Text.Json.Nodes;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record RendererProfileCase(string Id,Artifact Toolchain,Artifact Renderer,DocumentationRendererProfile Expected,string Error)
{public override string ToString()=>Id;}

public static class RendererProfileCases
{
    public static RendererProfileCase Original()
    {
        var f=DocumentationFixture.Current;
        Artifact Read(string name)
        {
            var path="remediation/design-c4/"+name;
            var retained=name=="toolchain-lock.json"?"remediation/nuke-planning/docs/fixtures/toolchain-lock.pre-redirect-admission.json":path;
            var bytes=File.ReadAllBytes(Path.Combine(f.Workspace,retained));
            Assert.Equal(f.ExpectedHashes[path],DocumentationFixture.Hash(bytes));return new(name,bytes,"application/json");
        }
        var toolchain=Read("toolchain-lock.json");var renderer=Read("renderer-lock.json");
        var downloads=new SourcePin[]
        {
            new(".tools/gradle-8.14.3-bin.zip",new("https://services.gradle.org/distributions/gradle-8.14.3-bin.zip"),"8.14.3","bd71102213493060956ec229d946beee57158dbd89d0e62b91bca0fa2c5f3531",137393837),
            new("gradlew",new("https://raw.githubusercontent.com/gradle/gradle/v8.14.3/gradlew"),"8.14.3","fb49f8cb2e5b1d83fba3dcc2c0dd0934c5655cbaf8bd1510ada7cc02692095ca",8755),
            new("gradlew.bat",new("https://raw.githubusercontent.com/gradle/gradle/v8.14.3/gradlew.bat"),"8.14.3","49792578a7942e08b708df3d3902ddbfc49d2203775c0161ef0c24b23fe4aeae",2865),
            new("gradle/wrapper/gradle-wrapper.jar",new("https://raw.githubusercontent.com/gradle/gradle/v8.14.3/gradle/wrapper/gradle-wrapper.jar"),"8.14.3","7d3a4ac4de1c32b59bc6a4eb8ecb8e612ccd0cf1ae1e99f66902da64df296172",43764),
            new(".tools/plantuml-1.2025.10.jar",new("https://repo.maven.apache.org/maven2/net/sourceforge/plantuml/plantuml/1.2025.10/plantuml-1.2025.10.jar"),"1.2025.10","4a01ea09b317180fb8e7eef712dfdca725409d2ee1919e4b5adfe9d8362b6fe5",25281989)
        };
        var files=new RendererFilePin[]
        {
            new("structurizr.sh","2d2fc1af2a1e886d3a94004604adf9e8ce07a0d0d051bf9ec68c0d138f096f1e"),
            new("lib/structurizr-cli.jar","68b4feb3053cb138ac90ef09ede1d503f0514054694456c0c22a4c4b81129c00"),
            new("lib/structurizr-dsl-5.0.2.jar","7f1cf71a56b00c9574a6267e36af51d33d3e22500f6b1ea0c4b87fd600e2fda1"),
            new("lib/structurizr-export-5.0.2.jar","963e01fb93be3a52a50b0df446913314bdb54f0ba254b31f32989fac19a09ee5"),
            new("lib/structurizr-core-5.0.2.jar","48c974cfd72365169417cb2437a1c6964dbcfc437c705fdbe7f81e6cdadd7fd0")
        };
        return new("PROFILE-ORIGINAL",toolchain,renderer,new("structurizr/cli@sha256:0399ac8e24c16e41277cfcec22caa29e4775013e5395c0720016828426d62749","21.0.8","2025.11.09","1.2025.10","8.14.3",downloads,files),"");
    }
    public static IReadOnlyList<RendererProfileCase> All()
    {
        var original=Original();
        RendererProfileCase Mutate(string id,bool renderer,Action<JsonObject> action,string error)
        {
            var artifact=renderer?original.Renderer:original.Toolchain;var node=JsonNode.Parse(artifact.Bytes.Span)!.AsObject();action(node);
            var updated=artifact with{Bytes=Encoding.UTF8.GetBytes(node.ToJsonString())};
            return original with{Id=id,Renderer=renderer?updated:original.Renderer,Toolchain=renderer?original.Toolchain:updated,Error=error};
        }
        return
        [
            original,
            Mutate("PROFILE-MUTABLE-IMAGE",true,n=>n["image"]="structurizr/cli:latest","RENDERER_PIN"),
            Mutate("PROFILE-IMAGE-DISAGREEMENT",false,n=>n["structurizr_image"]="structurizr/cli@sha256:"+new string('a',64),"RENDERER_PIN"),
            Mutate("PROFILE-MISSING-DOWNLOAD",false,n=>n["files"]!.AsArray().RemoveAt(0),"TOOLCHAIN_PIN"),
            Mutate("PROFILE-DUPLICATE-DOWNLOAD",false,n=>n["files"]!.AsArray().Add(n["files"]![0]!.DeepClone()),"TOOLCHAIN_PIN"),
            Mutate("PROFILE-UNSAFE-DOWNLOAD",false,n=>n["files"]![0]!["file"]="../escape.zip","TOOLCHAIN_PIN"),
            Mutate("PROFILE-BAD-HASH",false,n=>n["files"]![0]!["sha256"]="bad","TOOLCHAIN_PIN"),
            Mutate("PROFILE-NONHTTPS",false,n=>n["files"]![0]!["url"]="http://example.test/file","TOOLCHAIN_PIN"),
            Mutate("PROFILE-NEGATIVE-SIZE",false,n=>n["files"]![0]!["size_bytes"]=-1,"TOOLCHAIN_PIN"),
            Mutate("PROFILE-MISSING-RENDERER",true,n=>n["renderer_files"]!.AsArray().RemoveAt(0),"RENDERER_PIN"),
            Mutate("PROFILE-DUPLICATE-RENDERER",true,n=>n["renderer_files"]!.AsArray().Add(n["renderer_files"]![0]!.DeepClone()),"RENDERER_PIN"),
            Mutate("PROFILE-UNSAFE-RENDERER",true,n=>n["renderer_files"]![0]!["path"]="../escape.jar","RENDERER_PIN"),
            Mutate("PROFILE-BAD-RENDERER-HASH",true,n=>n["renderer_files"]![0]!["sha256"]="bad","RENDERER_PIN"),
            Mutate("PROFILE-MISSING-JAVA",true,n=>n.Remove("java_version"),"RENDERER_PIN"),
            original with{Id="PROFILE-INVALID-JSON",Renderer=original.Renderer with{Bytes=Encoding.UTF8.GetBytes("{")},Error="RENDERER_PIN"}
        ];
    }
    public static IEnumerable<object[]> Data=>All().Select(c=>new object[]{c});
    public static void AssertProfile(RendererProfileCase test,IDocumentationRendererProfileReader provider)
    {
        var beforeToolchain=test.Toolchain.Bytes.ToArray();var beforeRenderer=test.Renderer.Bytes.ToArray();
        if(test.Error.Length>0)Assert.Equal(test.Error,Assert.Throws<ArchitectureAssetException>(()=>provider.Read(test.Toolchain,test.Renderer)).Code);
        else
        {
            var actual=provider.Read(test.Toolchain,test.Renderer);var expected=test.Expected;
            Assert.Equal(expected.Image,actual.Image);Assert.Equal(expected.JavaVersion,actual.JavaVersion);Assert.Equal(expected.StructurizrVersion,actual.StructurizrVersion);
            Assert.Equal(expected.PlantUmlVersion,actual.PlantUmlVersion);Assert.Equal(expected.GradleVersion,actual.GradleVersion);
            Assert.Equal(expected.Downloads,actual.Downloads);Assert.Equal(expected.RendererFiles,actual.RendererFiles);
            Assert.DoesNotContain(actual.Downloads,p=>p.LocalPath.Contains("marp",StringComparison.OrdinalIgnoreCase)||p.LocalPath.Contains("node",StringComparison.OrdinalIgnoreCase));
        }
        Assert.Equal(beforeToolchain,test.Toolchain.Bytes.ToArray());Assert.Equal(beforeRenderer,test.Renderer.Bytes.ToArray());
    }
}
public sealed class ScriptedRendererProfile(DocumentationRendererProfile result,string error="",string mutate=""):IDocumentationRendererProfileReader
{
    public (Artifact Toolchain,Artifact Renderer)? Received {get;private set;}
    public DocumentationRendererProfile Read(Artifact toolchain,Artifact renderer)
    {
        Received=(toolchain,renderer);
        if(mutate.Length>0){var target=mutate=="renderer"?renderer:toolchain;Assert.True(MemoryMarshal.TryGetArray(target.Bytes,out var bytes));bytes.Array![bytes.Offset]^=1;}
        if(error.Length>0)throw new ArchitectureAssetException(error,"Scripted pin rejection");return result;
    }
}
[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class DocumentationRendererProfileMockTests
{
    [Theory,MemberData(nameof(RendererProfileCases.Data),MemberType=typeof(RendererProfileCases))]
    public void CorrectPinnedProfileMocksPass(RendererProfileCase test)
    {var provider=new ScriptedRendererProfile(test.Expected,test.Error);RendererProfileCases.AssertProfile(test,provider);Assert.Same(test.Toolchain,provider.Received!.Value.Toolchain);Assert.Same(test.Renderer,provider.Received.Value.Renderer);}
    [Theory,MemberData(nameof(RendererProfileCases.Data),MemberType=typeof(RendererProfileCases))]
    public void WrongProfileOutcomeRejected(RendererProfileCase test)=>Assert.NotNull(Record.Exception(()=>RendererProfileCases.AssertProfile(test,new ScriptedRendererProfile(test.Expected,"WRONG"))));
    [Theory]
    [InlineData(false,"toolchain")][InlineData(false,"renderer")][InlineData(true,"toolchain")][InlineData(true,"renderer")]
    public void SourceMutationRejectedOnSuccessAndError(bool error,string source)
    {
        var test=error?RendererProfileCases.All()[1]:RendererProfileCases.Original();
        Assert.NotNull(Record.Exception(()=>RendererProfileCases.AssertProfile(test,new ScriptedRendererProfile(test.Expected,test.Error,source))));
    }
    [Theory]
    [InlineData("image")][InlineData("java")][InlineData("cli")][InlineData("puml")][InlineData("gradle")][InlineData("downloads")][InlineData("renderer")][InlineData("download-path")][InlineData("download-hash")][InlineData("download-url")][InlineData("download-size")][InlineData("download-revision")][InlineData("renderer-path")][InlineData("renderer-hash")]
    public void EveryProfilePropertyRejectsWrongResponse(string mode)
    {
        var test=RendererProfileCases.Original();var wrong=test.Expected;
        if(mode=="image")wrong=wrong with{Image="wrong"};else if(mode=="java")wrong=wrong with{JavaVersion="wrong"};else if(mode=="cli")wrong=wrong with{StructurizrVersion="wrong"};else if(mode=="puml")wrong=wrong with{PlantUmlVersion="wrong"};else if(mode=="gradle")wrong=wrong with{GradleVersion="wrong"};
        else if(mode=="downloads")wrong=wrong with{Downloads=[]};else if(mode=="renderer")wrong=wrong with{RendererFiles=[]};
        else if(mode.StartsWith("download-",StringComparison.Ordinal))
        {var values=wrong.Downloads.ToArray();values[0]=mode switch{"download-path"=>values[0] with{LocalPath="wrong"},"download-hash"=>values[0] with{Sha256="wrong"},"download-url"=>values[0] with{Url=new("https://wrong.test/")},"download-size"=>values[0] with{SizeBytes=0},_=>values[0] with{Revision="wrong"}};wrong=wrong with{Downloads=values};}
        else {var values=wrong.RendererFiles.ToArray();values[0]=mode=="renderer-path"?values[0] with{RelativePath="wrong"}:values[0] with{Sha256="wrong"};wrong=wrong with{RendererFiles=values};}
        Assert.NotNull(Record.Exception(()=>RendererProfileCases.AssertProfile(test,new ScriptedRendererProfile(wrong))));
    }
}
