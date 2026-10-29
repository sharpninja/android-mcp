using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Documentation;
using AndroidMcp.BuildTool.Documentation.Validation;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Documentation.Validation;

public sealed class RepositoryDocumentationCase
{
    public const string Design="remediation/design-c4/",Checksum=Design+"docs/checksums.sha256",Html=Design+"docs/storyboards.html";
    public string Id{get;}
    public Dictionary<string,byte[]> Files{get;}=new(StringComparer.Ordinal);
    public List<PublicationFile> Entries{get;}=[];
    public RepositoryDocumentationCheckResult? Expected{get;private set;}=new(1,4,2,1,[]);
    public string Exception{get;private set;}="";
    public List<string> Reads{get;}=[];
    public List<ArtifactReferenceRequest> References{get;}=[];
    public RepositoryDocumentationCase(string id)
    {
        Id=id;
        Add(Checksum,"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad  README.md\n");
        Add(Design+"README.md","abc");
        Add("README.md","[directory](emptydir) [space](guides/a%20b.md#part) [plus](guides/a+b.md) [remote](https://example.test/) [mail](mailto:owner@example.test) [anchor](#part)");
        Add("guides/a b.md","space target");Add("guides/a+b.md","plus target");Add("nested/untracked.md","[target](../guides/a%20b.md)");
        Add("nested/.git/ignored.md","[ignored](missing.md)");
        Add(Design+"docs/screens/SCR01.svg","<svg xmlns='http://www.w3.org/2000/svg'/>");
        Add(Design+"docs/other.svg","<any-xml-root/>");
        Add(Design+"ui-svg/SCR01.svg","<svg xmlns='http://www.w3.org/2000/svg'/>");
        Add(Html,"<script src='local.js'></script><img src='data:image/svg+xml;base64,AA=='>");
        foreach(var directory in new[]{"emptydir","guides","nested","nested/.git",Design.TrimEnd('/'),Design+"docs",Design+"docs/screens",Design+"ui-svg"})Entries.Add(new(directory,0,PublicationFileKind.Directory));
        switch(id){
            case "GOOD":break;
            case "CHANGED-CHECKSUM":Replace(Design+"README.md","changed");Errors("Missing or changed design checksum: README.md");break;
            case "MISSING-CHECKSUM-TARGET":Remove(Design+"README.md");Errors("Missing or changed design checksum: README.md");break;
            case "CHECKSUM-UPPERCASE":Replace(Checksum,Text(Checksum).Replace("ba7816bf","BA7816BF",StringComparison.Ordinal));Errors("Missing or changed design checksum: README.md");break;
            case "MISSING-LINK":Replace("README.md",Text("README.md")+" [missing](guides/missing%20x.md#part)");Expected=Expected! with{MarkdownLinksChecked=5,Errors=["Missing link in README.md: guides/missing%20x.md"]};break;
            case "MISSING-DIRECTORY":Entries.RemoveAll(entry=>entry.Path=="emptydir");Errors("Missing link in README.md: emptydir");break;
            case "ENCODED-FRAGMENT":Replace("README.md",Text("README.md")+" [literal](guides/a%23b.md)");Add("guides/a#b.md","literal hash");Expected=Expected! with{MarkdownLinksChecked=5};break;
            case "GIT-SEGMENT":Add(".git/top.md","[ignored](absent)");Add("docs/not.git.md","no links");break;
            case "MISSING-COPY":Remove(Design+"docs/screens/SCR01.svg");Expected=Expected! with{RenderedSvgCount=1,Errors=["Different UI copy: SCR01.svg"]};break;
            case "DIFFERENT-COPY":Replace(Design+"ui-svg/SCR01.svg","<svg/>");Errors("Different UI copy: SCR01.svg");break;
            case "EXTRA-EDITABLE":Add(Design+"ui-svg/extra.svg","<svg/>");Expected=Expected! with{EditableUiCopies=2,Errors=["Different UI copy: extra.svg"]};break;
            case "NESTED-EDITABLE-IGNORED":Add(Design+"ui-svg/nested/ignore.svg","not parsed by original UI-copy loop");break;
            case "PLAIN-XML-ALLOWED":Replace(Design+"docs/other.svg","<root><text>not an invented SVG namespace contract</text></root>");break;
            case "CSS-AND-UPPERCASE-REMOTE-ALLOWED":Replace(Html,"<style>url(https://external.test/font)</style><IMG SRC='HTTP://example.test/x'><a href='https://example.test/x'>a</a>");break;
            case "REMOTE-DUPLICATE-ATTRIBUTES":Replace(Html,"<img src='https://one.test/x' src='//two.test/y'>");Errors("Remote presentation asset: img: https://one.test/x","Remote presentation asset: img: //two.test/y");break;
            case "REMOTE-ENTITY":Replace(Html,"<img src='h&#116;tps://example.test/a&amp;b'>");Errors("Remote presentation asset: img: https://example.test/a&b");break;
            case "COMMENTS-AND-SCRIPT-DATA":Replace(Html,"<!-- <img src='https://ignored.test/x'> --><script>const x=\"<img src='https://ignored.test/x'>\";</script>");break;
            case "MULTIPLE-ERRORS":
                Replace(Design+"README.md","changed");Replace("README.md",Text("README.md")+" [missing](absent.md)");Replace(Design+"ui-svg/SCR01.svg","<svg/>");
                Replace(Html,"<img src='https://one.test/x' src='//two.test/y'>");
                Expected=Expected! with{MarkdownLinksChecked=5,Errors=["Missing or changed design checksum: README.md","Missing link in README.md: absent.md","Different UI copy: SCR01.svg","Remote presentation asset: img: https://one.test/x","Remote presentation asset: img: //two.test/y"]};break;
            case "BAD-CHECKSUM-LINE":Replace(Checksum,"missing two spaces\n");Failure("format");break;
            case "BAD-XML":Replace(Design+"docs/other.svg","<svg");Failure("xml");break;
            case "MISSING-MANIFEST":Remove(Checksum);Failure("missing");break;
            case "UNADMITTED-ESCAPE":Replace("README.md","[external](../../outside.md)");Failure("scope");break;
            case "CANCELLED":Failure("cancelled");break;
            default:
                if(!id.StartsWith("REMOTE-",StringComparison.Ordinal))throw new ArgumentException("Unknown repository case");
                var tag=id[7..];Replace(Html,"<"+tag+" HREF='//example.test/resource'>");Errors("Remote presentation asset: "+tag+": //example.test/resource");break;
        }
        BuildTrace();
    }
    private string Text(string path)=>Encoding.UTF8.GetString(Files[path]);
    private void Add(string path,string text){var bytes=Encoding.UTF8.GetBytes(text);Files.Add(path,bytes);Entries.Add(new(path,bytes.Length));}
    private void Replace(string path,string text){Files[path]=Encoding.UTF8.GetBytes(text);var index=Entries.FindIndex(entry=>entry.Path==path);Entries[index]=Entries[index] with{Length=Files[path].Length};}
    private void Remove(string path){Files.Remove(path);Entries.RemoveAll(entry=>entry.Path==path);}
    private void Errors(params string[] errors)=>Expected=Expected! with{Errors=errors};
    private void Failure(string kind){Expected=null;Exception=kind;}
    private void BuildTrace()
    {
        if(Exception=="cancelled")return;
        Reads.Add(Checksum);if(Exception is "missing" or "format")return;
        if(Files.ContainsKey(Design+"README.md"))Reads.Add(Design+"README.md");
        foreach(var entry in Entries.Where(entry=>entry.Kind==PublicationFileKind.Regular&&entry.Path.EndsWith(".md",StringComparison.Ordinal)&&!entry.Path.Split('/').Contains(".git",StringComparer.Ordinal))){
            Reads.Add(entry.Path);
            foreach(Match match in Regex.Matches(Text(entry.Path),@"\]\(([^)]+)\)"))References.Add(new(entry.Path,match.Groups[1].Value,ArtifactReferenceProfile.RepositoryMarkdown));
            if(Exception=="scope"&&entry.Path=="README.md")return;
        }
        foreach(var entry in Entries.Where(entry=>entry.Kind==PublicationFileKind.Regular&&entry.Path.StartsWith(Design+"docs/",StringComparison.Ordinal)&&entry.Path.EndsWith(".svg",StringComparison.Ordinal))){Reads.Add(entry.Path);if(Exception=="xml"&&entry.Path==Design+"docs/other.svg")return;}
        foreach(var entry in Entries.Where(entry=>entry.Kind==PublicationFileKind.Regular&&entry.Path.StartsWith(Design+"ui-svg/",StringComparison.Ordinal)&&!entry.Path[(Design+"ui-svg/").Length..].Contains('/')&&entry.Path.EndsWith(".svg",StringComparison.Ordinal))){
            var copy=Design+"docs/screens/"+System.IO.Path.GetFileName(entry.Path);if(Files.ContainsKey(copy)){Reads.Add(entry.Path);Reads.Add(copy);}
        }
        Reads.Add(Html);
        // Independent HTML observations for these source-derived fixtures.
        if(Id=="GOOD"||!Id.StartsWith("REMOTE-",StringComparison.Ordinal)&&Id is not ("COMMENTS-AND-SCRIPT-DATA" or "CSS-AND-UPPERCASE-REMOTE-ALLOWED" or "MULTIPLE-ERRORS")){
            References.Add(new(Html,"local.js",ArtifactReferenceProfile.RepositoryOfflineAsset));References.Add(new(Html,"data:image/svg+xml;base64,AA==",ArtifactReferenceProfile.RepositoryOfflineAsset));
        }else if(Id is "REMOTE-DUPLICATE-ATTRIBUTES" or "MULTIPLE-ERRORS"){
            References.Add(new(Html,"https://one.test/x",ArtifactReferenceProfile.RepositoryOfflineAsset));References.Add(new(Html,"//two.test/y",ArtifactReferenceProfile.RepositoryOfflineAsset));
        }else if(Id=="REMOTE-ENTITY")References.Add(new(Html,"h&#116;tps://example.test/a&amp;b",ArtifactReferenceProfile.RepositoryOfflineAsset));
        else if(Id=="CSS-AND-UPPERCASE-REMOTE-ALLOWED")References.Add(new(Html,"HTTP://example.test/x",ArtifactReferenceProfile.RepositoryOfflineAsset));
        else if(Id.StartsWith("REMOTE-",StringComparison.Ordinal))References.Add(new(Html,"//example.test/resource",ArtifactReferenceProfile.RepositoryOfflineAsset));
    }
}
public sealed class RepositoryDocumentationEnvironment
{
    public RepositoryDocumentationCase Test{get;}
    public List<string> Reads{get;}=[];
    public List<ArtifactReferenceRequest> References{get;}=[];
    public int Writes{get;set;}
    public CancellationTokenSource Cancellation{get;}=new();
    public RepositoryDocumentationEnvironment(RepositoryDocumentationCase test){Test=test;if(test.Exception=="cancelled")Cancellation.Cancel();}
    public IArtifactStore Store(IArtifactStore? native=null)=>new RecordingStore(this,native);
    public IArtifactReferenceResolver Resolver=>new RecordingResolver(this);
    private sealed class RecordingStore(RepositoryDocumentationEnvironment e,IArtifactStore? native):IArtifactStore
    {
        public Task<StoredArtifact> WriteAsync(Artifact artifact,CancellationToken token=default){e.Writes++;throw new IOException("Repository checker cannot write");}
        public Task<ReadOnlyMemory<byte>> ReadAsync(string path,CancellationToken token=default){token.ThrowIfCancellationRequested();e.Reads.Add(path);return native?.ReadAsync(path,token)??Task.FromResult<ReadOnlyMemory<byte>>(e.Test.Files.TryGetValue(path,out var bytes)?bytes:throw new FileNotFoundException("fixture",path));}
    }
    private sealed class RecordingResolver(RepositoryDocumentationEnvironment e):IArtifactReferenceResolver
    {public ArtifactReferenceResolution Resolve(ArtifactReferenceRequest request){e.References.Add(request);return new ArtifactReferenceResolver(new SourceHtmlDecoder().Decode).Resolve(request);}}
}
public static class RepositoryDocumentationCases
{
    public static IEnumerable<object[]> Cases=>new[]{"GOOD","CHANGED-CHECKSUM","MISSING-CHECKSUM-TARGET","CHECKSUM-UPPERCASE","MISSING-LINK","MISSING-DIRECTORY","ENCODED-FRAGMENT","GIT-SEGMENT","MISSING-COPY","DIFFERENT-COPY","EXTRA-EDITABLE","NESTED-EDITABLE-IGNORED","PLAIN-XML-ALLOWED","CSS-AND-UPPERCASE-REMOTE-ALLOWED","REMOTE-DUPLICATE-ATTRIBUTES","REMOTE-ENTITY","COMMENTS-AND-SCRIPT-DATA","MULTIPLE-ERRORS","BAD-CHECKSUM-LINE","BAD-XML","MISSING-MANIFEST","UNADMITTED-ESCAPE","CANCELLED"}
        .Concat(new[]{"script","img","link","iframe","video","audio"}.Select(tag=>"REMOTE-"+tag)).Select(id=>new object[]{id});
    public static async Task AssertContract(RepositoryDocumentationEnvironment e,IRepositoryDocumentationValidator provider)
    {
        RepositoryDocumentationCheckResult? actual=null;var error=await Record.ExceptionAsync(async()=>actual=await provider.CheckAsync(e.Test.Entries,e.Cancellation.Token));
        switch(e.Test.Exception){
            case "":Assert.Null(error);Assert.NotNull(actual);var expected=e.Test.Expected!;Assert.Equal(expected.ChecksumsChecked,actual.ChecksumsChecked);Assert.Equal(expected.MarkdownLinksChecked,actual.MarkdownLinksChecked);Assert.Equal(expected.RenderedSvgCount,actual.RenderedSvgCount);Assert.Equal(expected.EditableUiCopies,actual.EditableUiCopies);Assert.Equal(expected.Errors,actual.Errors);Assert.Equal(expected.ExitCode,actual.ExitCode);break;
            case "format":Assert.IsType<FormatException>(error);break;
            case "xml":Assert.IsType<XmlException>(error);break;
            case "missing":Assert.IsType<FileNotFoundException>(error);break;
            case "scope":Assert.Equal("DOCS_REPOSITORY_REFERENCE_SCOPE",Assert.IsType<ArchitectureAssetException>(error).Code);break;
            case "cancelled":Assert.IsAssignableFrom<OperationCanceledException>(error);break;
        }
        if(e.Test.Exception.Length>0)Assert.Null(actual);
        Assert.Equal(e.Test.Reads,e.Reads);Assert.Equal(e.Test.References,e.References);Assert.Equal(0,e.Writes);
    }
}
public sealed class ScriptedRepositoryDocumentationValidator(RepositoryDocumentationEnvironment e,string mutation=""):IRepositoryDocumentationValidator
{
    public async Task<RepositoryDocumentationCheckResult> CheckAsync(IReadOnlyList<PublicationFile> entries,CancellationToken token=default)
    {
        Assert.Same(e.Test.Entries,entries);
        if(mutation=="outcome"){
            if(e.Test.Exception=="cancelled")return new(0,0,0,0,[]);
            throw new InvalidOperationException("Wrong outcome");
        }
        token.ThrowIfCancellationRequested();
        foreach(var path in mutation=="reads"?e.Test.Reads.Skip(1):e.Test.Reads)await e.Store().ReadAsync(path,token);
        foreach(var request in mutation=="references"?e.Test.References.Skip(1):e.Test.References)e.Resolver.Resolve(mutation=="profile"?request with{Profile=ArtifactReferenceProfile.GradleMarkdown}:request);
        if(mutation=="write")await e.Store().WriteAsync(new("unexpected",new byte[]{1},"application/octet-stream"),token);
        switch(e.Test.Exception){case "format":throw new FormatException();case "xml":throw new XmlException();case "missing":throw new FileNotFoundException();case "scope":throw new ArchitectureAssetException("DOCS_REPOSITORY_REFERENCE_SCOPE","Unsupported reference scope");}
        var value=e.Test.Expected!;return mutation switch{"checksums"=>value with{ChecksumsChecked=value.ChecksumsChecked+1},"links"=>value with{MarkdownLinksChecked=value.MarkdownLinksChecked+1},"vectors"=>value with{RenderedSvgCount=value.RenderedSvgCount+1},"copies"=>value with{EditableUiCopies=value.EditableUiCopies+1},"errors"=>value with{Errors=["wrong"]},_=>value};
    }
}
[Trait("Gate","Mock"),Trait("Requirement","FR-BUILD-005"),Trait("Requirement","TEST-BUILD-005")]
public sealed class RepositoryDocumentationMockTests
{
    [Theory,MemberData(nameof(RepositoryDocumentationCases.Cases),MemberType=typeof(RepositoryDocumentationCases))]
    public Task OriginalRepositoryCasesPassCorrectIndependentMocks(string id){var e=new RepositoryDocumentationEnvironment(new(id));return RepositoryDocumentationCases.AssertContract(e,new ScriptedRepositoryDocumentationValidator(e));}
    [Theory,MemberData(nameof(RepositoryDocumentationCases.Cases),MemberType=typeof(RepositoryDocumentationCases))]
    public async Task EachOriginalOutcomeAndExceptionIsDiscriminating(string id)
    {var e=new RepositoryDocumentationEnvironment(new(id));Assert.NotNull(await Record.ExceptionAsync(()=>RepositoryDocumentationCases.AssertContract(e,new ScriptedRepositoryDocumentationValidator(e,"outcome"))));}
    [Theory,InlineData("checksums"),InlineData("links"),InlineData("vectors"),InlineData("copies"),InlineData("errors"),InlineData("reads"),InlineData("references"),InlineData("profile"),InlineData("write")]
    public async Task EveryCounterErrorAndRequiredReadOnlyInteractionRejectsWrongResponses(string mutation)
    {var e=new RepositoryDocumentationEnvironment(new("GOOD"));Assert.NotNull(await Record.ExceptionAsync(()=>RepositoryDocumentationCases.AssertContract(e,new ScriptedRepositoryDocumentationValidator(e,mutation))));}
    [Fact]
    public void ResultRetainsTheExactOriginalJsonFieldsAndExitRule()
    {using var json=JsonDocument.Parse(JsonSerializer.Serialize(new RepositoryDocumentationCheckResult(1,2,3,4,[])));Assert.Equal(new[]{"checksums_checked","markdown_links_checked","rendered_svg_count","editable_ui_copies","errors"},json.RootElement.EnumerateObject().Select(property=>property.Name));Assert.Equal(0,new RepositoryDocumentationCheckResult(0,0,0,0,[]).ExitCode);Assert.Equal(1,new RepositoryDocumentationCheckResult(0,0,0,0,["error"]).ExitCode);}
}
