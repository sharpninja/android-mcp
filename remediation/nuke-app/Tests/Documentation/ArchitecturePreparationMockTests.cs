using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation;

public sealed record CodeViewCase(string Id,ArchitecturePreparationInput Input,IReadOnlyList<Artifact> Expected,string Error)
{public override string ToString()=>Id;}
public sealed record VectorPreparationCase(string Id,Artifact Input,Artifact Expected,string Error)
{public override string ToString()=>Id;}

public static class ArchitecturePreparationCases
{
    public static readonly string[] Keys=["arc-controller-classes","arc-target-classes","arc-ui-classes","server-adapters-classes","server-core-classes","server-execution-classes"];
    public static Artifact Text(string path,string text,string media="text/plain")=>new(path,Encoding.UTF8.GetBytes(text),media);
    public static IEnumerable<object[]> CodeCases=>CreateCode().Select(c=>new object[]{c});
    public static IReadOnlyList<CodeViewCase> CreateCode()
    {
        var fixture=DocumentationFixture.Current;var root=Path.Combine(fixture.Workspace,"remediation/design-c4");
        var workspace=File.ReadAllBytes(Path.Combine(root,"architecture/workspace.dsl"));var code=File.ReadAllBytes(Path.Combine(root,"architecture/code-diagrams.md"));
        Assert.Equal(fixture.ExpectedHashes["remediation/design-c4/architecture/workspace.dsl"],DocumentationFixture.Hash(workspace));
        Assert.Equal(fixture.ExpectedHashes["remediation/design-c4/architecture/code-diagrams.md"],DocumentationFixture.Hash(code));
        var input=new ArchitecturePreparationInput(new("architecture/workspace.dsl",workspace,"text/plain"),new("architecture/code-diagrams.md",code,"text/markdown"));
        var pins=JsonNode.Parse(File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/architecture-preparation-fixture-pins.json")))!["files"]!.AsArray().ToDictionary(n=>n!["path"]!.GetValue<string>(),n=>n!["sha256"]!.GetValue<string>(),StringComparer.Ordinal);
        var expected=Keys.Select(key=>{var path="build/code-views/"+key+".puml";var bytes=File.ReadAllBytes(Path.Combine(fixture.Workspace,"remediation/nuke-planning/docs/fixtures/code-views",key+".puml"));Assert.Equal(pins[path],DocumentationFixture.Hash(bytes));return new Artifact(path,bytes,"text/vnd.plantuml");}).ToArray();
        var text=Encoding.UTF8.GetString(code);var dsl=Encoding.UTF8.GetString(workspace);
        return
        [
            new("CODE-ORIGINAL-SIX",input,expected,""),
            new("CODE-MISSING-BLOCK",input with{CodeMarkdown=Text(input.CodeMarkdown.RelativePath,text.Replace("```plantuml","```text",StringComparison.Ordinal))},[],"MISSING_CODE_VIEW"),
            new("CODE-DUPLICATE-ID",input with{CodeMarkdown=Text(input.CodeMarkdown.RelativePath,text+"\n## server-core-classes duplicate\n```plantuml\nclass Duplicate\n```\n")},[],"DUPLICATE_CODE_VIEW"),
            new("CODE-UNDECLARED-VIEW",input with{CodeMarkdown=Text(input.CodeMarkdown.RelativePath,text+"\n## unexpected-classes\n```plantuml\nclass Unexpected\n```\n")},[],"UNEXPECTED_CODE_VIEW"),
            new("CODE-MISSING-IMAGE-VIEW",input with{Workspace=Text(input.Workspace.RelativePath,dsl.Replace("../docs/diagrams/server-core-classes.svg","../docs/diagrams/absent-classes.svg",StringComparison.Ordinal))},[],"MISSING_CODE_VIEW")
        ];
    }
    public const string RawSvg="<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"80\" viewBox=\"0 0 100 80\"><title>&lt;size:24&gt;Hello&lt;/size&gt;</title><!--SRC=[source-data]--><text x=\"2\" y=\"20\">vector &amp; text</text></svg>";
    public const string NormalizedSvg="<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"80\" viewBox=\"-64 -64 228 208\"><rect data-canvas=\"white\" x=\"-64\" y=\"-64\" width=\"200%\" height=\"200%\" fill=\"#ffffff\"/><title>Hello</title><text x=\"2\" y=\"20\">vector &amp; text</text></svg>";
    public static IEnumerable<object[]> VectorCases=>new[]
    {
        new VectorPreparationCase("VECTOR-RAW",Text("build/raw.svg",RawSvg,"image/svg+xml"),Text("docs/diagrams/example.svg",NormalizedSvg,"image/svg+xml"),""),
        new VectorPreparationCase("VECTOR-IDEMPOTENT",Text("build/raw.svg",NormalizedSvg,"image/svg+xml"),Text("docs/diagrams/example.svg",NormalizedSvg,"image/svg+xml"),""),
        new VectorPreparationCase("VECTOR-INVALID",Text("build/raw.svg","<svg"),Text("docs/diagrams/example.svg",""),"INVALID_SVG"),
        new VectorPreparationCase("VECTOR-RASTER",Text("build/raw.svg",RawSvg.Replace("</svg>","<image/></svg>",StringComparison.Ordinal)),Text("docs/diagrams/example.svg",""),"NON_VECTOR_CONTENT"),
        new VectorPreparationCase("VECTOR-SCRIPT",Text("build/raw.svg",RawSvg.Replace("</svg>","<script/></svg>",StringComparison.Ordinal)),Text("docs/diagrams/example.svg",""),"NON_VECTOR_CONTENT"),
        new VectorPreparationCase("VECTOR-NO-TEXT",Text("build/raw.svg","<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect/></svg>"),Text("docs/diagrams/example.svg",""),"NON_VECTOR_CONTENT")
    }.Select(c=>new object[]{c});

    public static void AssertCode(CodeViewCase test,IArchitectureAssetPreparation provider)
    {
        var beforeWorkspace=test.Input.Workspace.Bytes.ToArray();var beforeCode=test.Input.CodeMarkdown.Bytes.ToArray();
        if(test.Error.Length>0)Assert.Equal(test.Error,Assert.Throws<ArchitectureAssetException>(()=>provider.ExtractCodeViews(test.Input)).Code);
        else
        {
            var actual=provider.ExtractCodeViews(test.Input);Assert.Equal(test.Expected.Select(a=>a.RelativePath),actual.Select(a=>a.RelativePath));
            foreach(var expected in test.Expected){var artifact=actual.Single(a=>a.RelativePath==expected.RelativePath);Assert.Equal(expected.Bytes.ToArray(),artifact.Bytes.ToArray());Assert.Equal(expected.MediaType,artifact.MediaType);}
        }
        Assert.Equal(beforeWorkspace,test.Input.Workspace.Bytes.ToArray());Assert.Equal(beforeCode,test.Input.CodeMarkdown.Bytes.ToArray());
    }
    public static void AssertVector(VectorPreparationCase test,IArchitectureAssetPreparation provider)
    {
        var before=test.Input.Bytes.ToArray();
        if(test.Error.Length>0)Assert.Equal(test.Error,Assert.Throws<ArchitectureAssetException>(()=>provider.NormalizeVector(test.Input,test.Expected.RelativePath)).Code);
        else
        {
            var output=provider.NormalizeVector(test.Input,test.Expected.RelativePath);Assert.Equal(test.Expected.RelativePath,output.RelativePath);Assert.Equal("image/svg+xml",output.MediaType);
            var root=XElement.Parse(Encoding.UTF8.GetString(output.Bytes.Span));Assert.Equal("svg",root.Name.LocalName);Assert.Equal("-64 -64 228 208",(string?)root.Attribute("viewBox"));
            var canvas=root.Elements().First();Assert.Equal("rect",canvas.Name.LocalName);Assert.Equal("white",(string?)canvas.Attribute("data-canvas"));Assert.Equal("-64",(string?)canvas.Attribute("x"));Assert.Equal("-64",(string?)canvas.Attribute("y"));Assert.Equal("200%",(string?)canvas.Attribute("width"));Assert.Equal("200%",(string?)canvas.Attribute("height"));Assert.Equal("#ffffff",(string?)canvas.Attribute("fill"));
            Assert.Equal("Hello",root.Elements().Single(e=>e.Name.LocalName=="title").Value);Assert.Equal("vector & text",root.Elements().Single(e=>e.Name.LocalName=="text").Value);
            Assert.DoesNotContain(root.DescendantNodes().OfType<XComment>(),c=>c.Value.StartsWith("SRC=[",StringComparison.Ordinal));Assert.DoesNotContain(root.Descendants(),e=>e.Name.LocalName is "image" or "script");
        }
        Assert.Equal(before,test.Input.Bytes.ToArray());
    }
}

public sealed class ScriptedArchitecturePreparation(IReadOnlyList<Artifact>? code=null,Artifact? vector=null,string error=""):IArchitectureAssetPreparation
{
    public ArchitecturePreparationInput? ReceivedCode {get;private set;}
    public (Artifact Raw,string Path)? ReceivedVector {get;private set;}
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input){ReceivedCode=input;if(error.Length>0)throw new ArchitectureAssetException(error,"Scripted original design failure");return code!;}
    public Artifact NormalizeVector(Artifact raw,string path){ReceivedVector=(raw,path);if(error.Length>0)throw new ArchitectureAssetException(error,"Scripted original vector failure");return vector!;}
}

[Trait("Gate","Mock")]
[Trait("Requirement","FR-BUILD-005")]
[Trait("Requirement","TEST-BUILD-005")]
public sealed class ArchitecturePreparationMockTests
{
    [Theory,MemberData(nameof(ArchitecturePreparationCases.CodeCases),MemberType=typeof(ArchitecturePreparationCases))]
    public void CorrectEditableCodeViewMocksPass(CodeViewCase test){var mock=new ScriptedArchitecturePreparation(code:test.Expected,error:test.Error);ArchitecturePreparationCases.AssertCode(test,mock);Assert.Same(test.Input,mock.ReceivedCode);}
    [Theory,MemberData(nameof(ArchitecturePreparationCases.VectorCases),MemberType=typeof(ArchitecturePreparationCases))]
    public void CorrectVectorMocksPass(VectorPreparationCase test){var mock=new ScriptedArchitecturePreparation(vector:test.Expected,error:test.Error);ArchitecturePreparationCases.AssertVector(test,mock);Assert.Same(test.Input,mock.ReceivedVector!.Value.Raw);Assert.Equal(test.Expected.RelativePath,mock.ReceivedVector.Value.Path);}
    [Theory,MemberData(nameof(ArchitecturePreparationCases.CodeCases),MemberType=typeof(ArchitecturePreparationCases))]
    public void WrongCodeOutcomeIsRejected(CodeViewCase test)=>Assert.NotNull(Record.Exception(()=>ArchitecturePreparationCases.AssertCode(test,new ScriptedArchitecturePreparation(error:"WRONG"))));
    [Theory,MemberData(nameof(ArchitecturePreparationCases.VectorCases),MemberType=typeof(ArchitecturePreparationCases))]
    public void WrongVectorOutcomeIsRejected(VectorPreparationCase test)=>Assert.NotNull(Record.Exception(()=>ArchitecturePreparationCases.AssertVector(test,new ScriptedArchitecturePreparation(error:"WRONG"))));
    [Fact]
    public void EveryCodeArtifactPropertyRejectsWrongResponse()
    {
        var test=ArchitecturePreparationCases.CreateCode()[0];
        foreach(var mode in new[]{"missing","path","bytes","media"})
        {
            var wrong=test.Expected.ToArray();if(mode=="missing")wrong=wrong.Skip(1).ToArray();else wrong[0]=mode switch{"path"=>wrong[0] with{RelativePath="wrong"},"bytes"=>wrong[0] with{Bytes=Encoding.UTF8.GetBytes("wrong")},_=>wrong[0] with{MediaType="wrong"}};
            Assert.NotNull(Record.Exception(()=>ArchitecturePreparationCases.AssertCode(test,new ScriptedArchitecturePreparation(code:wrong))));
        }
    }
    [Theory]
    [InlineData("viewBox")][InlineData("x")][InlineData("y")][InlineData("width")][InlineData("height")][InlineData("fill")][InlineData("canvas")][InlineData("title")][InlineData("text")][InlineData("metadata")][InlineData("raster")][InlineData("script")][InlineData("path")][InlineData("media")]
    public void EachVectorAssertionRejectsItsOwnWrongResponse(string mode)
    {
        var test=(VectorPreparationCase)ArchitecturePreparationCases.VectorCases.First()[0];var root=XElement.Parse(ArchitecturePreparationCases.NormalizedSvg);
        if(mode=="viewBox")root.SetAttributeValue("viewBox","0 0 100 80");
        else if(mode is "x" or "y" or "width" or "height" or "fill")root.Elements().First().SetAttributeValue(mode,"wrong");
        else if(mode=="canvas")root.Elements().First().Remove();
        else if(mode is "title" or "text")root.Elements().Single(e=>e.Name.LocalName==mode).Value="wrong";
        else if(mode=="metadata")root.Add(new XComment("SRC=[unremoved]"));
        else if(mode is "raster" or "script")root.Add(new XElement(root.Name.Namespace+(mode=="raster"?"image":"script")));
        var wrong=test.Expected with{Bytes=Encoding.UTF8.GetBytes(root.ToString())};if(mode=="path")wrong=wrong with{RelativePath="wrong"};if(mode=="media")wrong=wrong with{MediaType="wrong"};
        Assert.NotNull(Record.Exception(()=>ArchitecturePreparationCases.AssertVector(test,new ScriptedArchitecturePreparation(vector:wrong))));
    }
}
