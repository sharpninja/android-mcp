using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Documentation.Validation;

// Pure validation over the caller's admitted union. All source selection,
// filesystem admission, capture, writing and recipe execution remain outside.
public sealed class GeneratedDesignValidator(IArchitectureViewIndexReader views,IArtifactReferenceResolver references,
    IStrictJsonInspector strict,IArtifactReceiptVerifier receipts,ISourceHtmlDecoder htmlDecoder):IGeneratedDesignValidator
{
    private const string ChecksumPath="docs/checksums.sha256",HtmlPath="docs/storyboards.html",DeckPath="storyboards/storyboard.md";
    private static readonly string[] RequiredMarkdown=["README.md","architecture/code-diagrams.md",DeckPath];
    private static readonly Regex MarkdownLink=new(@"!?\[[^]]*]\(([^)]+)\)",RegexOptions.CultureInvariant);
    private static readonly Regex StoryImage=new(@"!\[[^]]*]\(([^)]+\.svg)\)",RegexOptions.CultureInvariant);
    private static readonly Regex ImageTag=new(@"<img\s",RegexOptions.CultureInvariant);
    private static readonly Regex SvgDataImage=new("src=\"data:image/svg\\+xml;base64,",RegexOptions.CultureInvariant);
    // This raw legacy scan intentionally sees comments and script text. The
    // separate start-tag pass below retains the other checker's HTML semantics.
    private static readonly Regex RawAsset=new("(?:src|href)=[\"'](?<attribute>[^\"']*)|url\\(\\s*[\"']?(?<css>[^\\s)\"']*)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex Sha256Line=new("^[a-f0-9]{64}$",RegexOptions.CultureInvariant);
    private static readonly Regex ScriptEnd=new(@"</\s*script\s*>",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    private static readonly Regex StyleEnd=new(@"</\s*style\s*>",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);

    public GeneratedDesignValidationResult Validate(GeneratedDesignValidationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.Artifacts);ArgumentNullException.ThrowIfNull(input.Model);
        var issues=new List<DesignIssue>();var diagnostics=new List<DocumentationParseDiagnostic>();
        void Issue(string code,string path,string subject)=>issues.Add(new(code,path,subject));
        GeneratedDesignValidationResult AdmissionFailure()=>new(input.Phase,new(0,0,0,0),[],[],issues.ToArray(),[]);
        if(!Enum.IsDefined(input.Phase)){Issue("DOCS_PHASE","","unsupported");return AdmissionFailure();}
        var artifacts=new Dictionary<string,Artifact>(StringComparer.Ordinal);
        foreach(var artifact in input.Artifacts)
        {
            if(!ArtifactPaths.IsRelative(artifact.RelativePath)){Issue("DOCS_INPUT_PATH",artifact.RelativePath,"unsafe");return AdmissionFailure();}
            if(!artifacts.TryAdd(artifact.RelativePath,artifact)){Issue("DOCS_INPUT_DUPLICATE",artifact.RelativePath,"duplicate");return AdmissionFailure();}
        }
        foreach(var member in new[]{input.WorkspaceDsl,input.UiManifest})
        {
            if(!artifacts.TryGetValue(member.RelativePath,out var admitted)){Issue("DOCS_INPUT_MEMBER",member.RelativePath,"missing");return AdmissionFailure();}
            if(admitted.MediaType!=member.MediaType||!admitted.Bytes.Span.SequenceEqual(member.Bytes.Span)){Issue("DOCS_INPUT_MEMBER",member.RelativePath,"different");return AdmissionFailure();}
        }
        if(input.Phase==DocumentationValidationPhase.BeforeManifest&&artifacts.ContainsKey(ChecksumPath))Issue("DOCS_PHASE_CHECKSUM",ChecksumPath,"BeforeManifest");
        var vectors=artifacts.Values.Where(a=>a.RelativePath.StartsWith("docs/",StringComparison.Ordinal)&&a.RelativePath.EndsWith(".svg",StringComparison.Ordinal)).OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        var editable=artifacts.Values.Where(a=>TopLevelEditable(a.RelativePath)).OrderBy(a=>a.RelativePath,StringComparer.Ordinal).ToArray();
        var actualPaths=vectors.Select(a=>a.RelativePath).ToArray();var modelIds=ModelIds(input.Model);
        var expectedPaths=Array.Empty<string>();
        if(AdmitUiManifest(input,out var variants,out var manifestProblem))
        {
            var index=views.Read(input.WorkspaceDsl);
            foreach(var group in index.NativeKeys.GroupBy(key=>key,StringComparer.Ordinal).Where(group=>group.Count()>1).OrderBy(group=>group.Key,StringComparer.Ordinal))Issue("DOCS_VIEW_DUPLICATE",input.WorkspaceDsl.RelativePath,group.Key);
            expectedPaths=index.NativeKeys.Select(key=>"docs/diagrams/structurizr-"+key+".svg").Concat(index.ImageSvgPaths)
                .Concat(modelIds.Select(id=>"docs/screens/"+id+".svg")).Concat(variants.Select(path=>"docs/"+path)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            foreach(var missing in expectedPaths.Except(actualPaths,StringComparer.Ordinal))Issue("DOCS_SVG_MISSING",missing,"expected");
            foreach(var extra in actualPaths.Except(expectedPaths,StringComparer.Ordinal))Issue("DOCS_SVG_UNEXPECTED",extra,"unexpected");
        }
        else Issue("DOCS_UI_MANIFEST",input.UiManifest.RelativePath,manifestProblem);
        foreach(var vector in vectors)ValidateVector(vector,issues,diagnostics);

        foreach(var required in RequiredMarkdown.Where(path=>!artifacts.ContainsKey(path)).Order(StringComparer.Ordinal))Issue("DOCS_REQUIRED_SOURCE",required,"missing");
        var checkedLinks=0;
        foreach(var markdown in artifacts.Values.Where(a=>SelectedMarkdown(a.RelativePath)).OrderBy(a=>a.RelativePath,StringComparer.Ordinal))
        {
            var text=Utf8(markdown);
            if(text.Contains("```mermaid",StringComparison.OrdinalIgnoreCase))Issue("DOCS_MERMAID",markdown.RelativePath,"current-source");
            foreach(Match match in MarkdownLink.Matches(text))
            {
                var raw=match.Groups[1].Value;var resolution=references.Resolve(new(markdown.RelativePath,raw,ArtifactReferenceProfile.GradleMarkdown));
                if(resolution.Disposition==ArtifactReferenceDisposition.Ignored)continue;
                checkedLinks++;
                if(resolution.Disposition!=ArtifactReferenceDisposition.Local)Issue("DOCS_LINK_INVALID",markdown.RelativePath,resolution.Reason??"invalid");
                else if(resolution.RootRelativePath is not { } target||!artifacts.ContainsKey(target))Issue("DOCS_LINK_MISSING",markdown.RelativePath,raw);
            }
        }
        var expectedCopies=modelIds.Select(id=>(Editable:"ui-svg/"+id+".svg",Document:"docs/screens/"+id+".svg")).OrderBy(pair=>pair.Editable,StringComparer.Ordinal).ToArray();
        foreach(var pair in expectedCopies)
        {
            if(!artifacts.TryGetValue(pair.Editable,out var copy)||!artifacts.TryGetValue(pair.Document,out var document))Issue("DOCS_COPY_MISSING",pair.Editable,pair.Document);
            else if(!copy.Bytes.Span.SequenceEqual(document.Bytes.Span))Issue("DOCS_COPY_DIFFERENT",pair.Editable,pair.Document);
        }
        var copyPaths=expectedCopies.Select(pair=>pair.Editable).ToHashSet(StringComparer.Ordinal);
        foreach(var extra in editable.Where(a=>!copyPaths.Contains(a.RelativePath)))Issue("DOCS_COPY_UNEXPECTED",extra.RelativePath,"unexpected");

        if(!artifacts.TryGetValue(HtmlPath,out var html))Issue("DOCS_REQUIRED_HTML",HtmlPath,"missing");
        else
        {
            var text=Utf8(html);
            foreach(Match match in RawAsset.Matches(text))
            {
                var css=match.Groups["css"].Success;var value=match.Groups[css?"css":"attribute"].Value;
                if(references.Resolve(new(HtmlPath,value,ArtifactReferenceProfile.GradleOfflineAsset)).Disposition==ArtifactReferenceDisposition.Remote)
                    Issue("DOCS_HTML_GRADLE_EXTERNAL",HtmlPath,(css?"css:":"attribute:")+value);
            }
            foreach(var attribute in AssetAttributes(text))
            {
                var resolution=references.Resolve(new(HtmlPath,attribute.Value,ArtifactReferenceProfile.RepositoryOfflineAsset));
                if(resolution.Disposition==ArtifactReferenceDisposition.Remote)
                    Issue("DOCS_HTML_ASSET_EXTERNAL",HtmlPath,attribute.Tag+":"+attribute.Name+":"+htmlDecoder.Decode(attribute.Value));
            }
            if(artifacts.TryGetValue(DeckPath,out var deck))
            {
                var images=StoryImage.Matches(Utf8(deck));
                foreach(Match image in images)
                {
                    var raw=image.Groups[1].Value;var target=references.Resolve(new(DeckPath,raw,ArtifactReferenceProfile.GradleMarkdown));
                    if(target.Disposition!=ArtifactReferenceDisposition.Local||target.RootRelativePath is null||!artifacts.ContainsKey(target.RootRelativePath))Issue("DOCS_EMBED_REFERENCE",DeckPath,raw);
                }
                var imageCount=ImageTag.Matches(text).Count;var dataCount=SvgDataImage.Matches(text).Count;
                if(imageCount!=images.Count)Issue("DOCS_EMBED_IMAGE_COUNT",HtmlPath,CountSubject(images.Count,imageCount));
                if(dataCount!=images.Count)Issue("DOCS_EMBED_DATA_COUNT",HtmlPath,CountSubject(images.Count,dataCount));
                if(text.Contains("![Screen]",StringComparison.Ordinal))Issue("DOCS_EMBED_MARKDOWN",HtmlPath,"![Screen]");
            }
        }
        var checkedChecksums=input.Phase==DocumentationValidationPhase.AfterManifest?ValidateChecksums(artifacts,issues):0;
        return new(input.Phase,new(checkedChecksums,checkedLinks,vectors.Length,editable.Length),expectedPaths,actualPaths,issues.ToArray(),diagnostics.ToArray());
    }
    private bool AdmitUiManifest(GeneratedDesignValidationInput input,out string[] variants,out string problem)
    {
        variants=[];problem="strict-json";var inspected=strict.Inspect(input.UiManifest.Bytes);
        if(inspected.CanonicalDiagnostics.Count!=0||inspected.Value is null||inspected.Value.Value.ValueKind!=JsonValueKind.Object)return false;
        var manifest=inspected.Value.Value;
        problem="model-identity";
        try
        {
            static string[] Ids(JsonElement value,string property)=>value.GetProperty(property).EnumerateArray().Select(node=>node.GetProperty("id").GetString()??"").Order(StringComparer.Ordinal).ToArray();
            var screens=input.Model.Screens.Select(node=>node["id"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
            var stories=input.Model.Storyboards.Select(node=>node["id"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
            if(!Ids(manifest,"screens").SequenceEqual(screens)||!Ids(manifest,"storyboards").SequenceEqual(stories)||screens.Concat(stories).Distinct(StringComparer.Ordinal).Count()!=screens.Length+stories.Length)return false;
            problem="model-variants";
            var modelVariants=input.Model.Screens.SelectMany(screen=>screen["state_specs"]!.AsObject().Select(pair=>"screens/states/"+pair.Key+".svg"))
                .Concat(input.Model.Storyboards.SelectMany(story=>story["steps"]!.AsArray().Select(step=>"screens/steps/"+step!["step_id"]!.GetValue<string>()+".svg"))).Order(StringComparer.Ordinal).ToArray();
            var declared=manifest.GetProperty("state_svg_files").EnumerateArray().Select(node=>node.GetString()??"").ToArray();
            if(declared.Any(path=>!ArtifactPaths.IsRelative("docs/"+path))||declared.Distinct(StringComparer.Ordinal).Count()!=declared.Length||!declared.Order(StringComparer.Ordinal).SequenceEqual(modelVariants))return false;
            variants=modelVariants;return true;
        }
        catch(Exception error) when(error is InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException){return false;}
    }
    private static string[] ModelIds(DocumentationModel model)=>model.Screens.Concat(model.Storyboards).Select(node=>node["id"]!.GetValue<string>()).Order(StringComparer.Ordinal).ToArray();
    private static bool TopLevelEditable(string path)=>path.StartsWith("ui-svg/",StringComparison.Ordinal)&&path.EndsWith(".svg",StringComparison.Ordinal)&&!path["ui-svg/".Length..].Contains('/');
    private static bool SelectedMarkdown(string path)=>path.EndsWith(".md",StringComparison.Ordinal)&&(path.StartsWith("docs/",StringComparison.Ordinal)||path.StartsWith("contracts/",StringComparison.Ordinal)||RequiredMarkdown.Contains(path,StringComparer.Ordinal));
    private static string Utf8(Artifact artifact)=>Encoding.UTF8.GetString(artifact.Bytes.Span);
    private static string CountSubject(int expected,int actual)=>"expected="+expected.ToString(CultureInfo.InvariantCulture)+";actual="+actual.ToString(CultureInfo.InvariantCulture);
    private static void ValidateVector(Artifact artifact,List<DesignIssue> issues,List<DocumentationParseDiagnostic> diagnostics)
    {
        var text=Utf8(artifact);XDocument? document=null;
        try
        {
            using var stream=new MemoryStream(artifact.Bytes.ToArray(),false);
            using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null});
            document=XDocument.Load(reader,LoadOptions.SetLineInfo);
        }
        catch(XmlException error)
        {
            issues.Add(new("DOCS_SVG_XML",artifact.RelativePath,"xml"));
            diagnostics.Add(new(artifact.RelativePath,Math.Max(1,error.LineNumber),Math.Max(1,error.LinePosition),error.Message));
        }
        if(document is not null)
        {
            var root=document.Root;
            if(root is null||root.Name.LocalName!="svg")issues.Add(new("DOCS_SVG_ROOT",artifact.RelativePath,"svg"));
            else if(root.Name.NamespaceName!="http://www.w3.org/2000/svg")issues.Add(new("DOCS_SVG_NAMESPACE",artifact.RelativePath,"http://www.w3.org/2000/svg"));
            else if(!text.Contains("<svg",StringComparison.Ordinal)||!text.Contains("<text",StringComparison.Ordinal)||!root.Descendants(XName.Get("text","http://www.w3.org/2000/svg")).Any())issues.Add(new("DOCS_SVG_TEXT",artifact.RelativePath,"text"));
        }
        if(text.Contains("<!--SRC=[",StringComparison.Ordinal))issues.Add(new("DOCS_SVG_METADATA",artifact.RelativePath,"SRC"));
        if(text.Contains("<image",StringComparison.OrdinalIgnoreCase))issues.Add(new("DOCS_SVG_RASTER",artifact.RelativePath,"image"));
        if(text.Contains("<script",StringComparison.OrdinalIgnoreCase))issues.Add(new("DOCS_SVG_SCRIPT",artifact.RelativePath,"script"));
    }
    private int ValidateChecksums(IReadOnlyDictionary<string,Artifact> artifacts,List<DesignIssue> issues)
    {
        if(!artifacts.TryGetValue(ChecksumPath,out var manifest)){issues.Add(new("DOCS_CHECKSUM_REQUIRED",ChecksumPath,"missing"));return 0;}
        var count=0;var seen=new HashSet<string>(StringComparer.Ordinal);
        using var reader=new StringReader(Utf8(manifest));
        while(reader.ReadLine() is { } line)
        {
            count++;var separator=line.IndexOf("  ",StringComparison.Ordinal);
            if(separator!=64||!Sha256Line.IsMatch(line[..64])){issues.Add(new("DOCS_CHECKSUM_LINE",ChecksumPath,count.ToString(CultureInfo.InvariantCulture)));continue;}
            var target=line[(separator+2)..];
            if(!ArtifactPaths.IsRelative(target)){issues.Add(new("DOCS_CHECKSUM_PATH",ChecksumPath,target));continue;}
            if(string.Equals(target,ChecksumPath,StringComparison.OrdinalIgnoreCase)){issues.Add(new("DOCS_CHECKSUM_SELF",ChecksumPath,target));continue;}
            if(!seen.Add(target)){issues.Add(new("DOCS_CHECKSUM_DUPLICATE",ChecksumPath,target));continue;}
            if(!artifacts.TryGetValue(target,out var artifact)){issues.Add(new("DOCS_CHECKSUM_MISSING",ChecksumPath,target));continue;}
            if(receipts.Describe(artifact).Sha256!=line[..separator])issues.Add(new("DOCS_CHECKSUM_HASH",ChecksumPath,target));
        }
        return count;
    }

    // A bounded start-tag reader for the six legacy asset tags. It preserves
    // duplicate attributes and raw entity text, skips comments and script/style
    // character data, and delegates decoding/classification to shared services.
    private static IEnumerable<(string Tag,string Name,string Value)> AssetAttributes(string text)
    {
        var position=0;
        while(position<text.Length)
        {
            var open=text.IndexOf('<',position);if(open<0)yield break;
            if(text.AsSpan(open).StartsWith("<!--",StringComparison.Ordinal))
            {var end=text.IndexOf("-->",open+4,StringComparison.Ordinal);if(end<0)yield break;position=end+3;continue;}
            position=open+1;if(position>=text.Length)yield break;
            if(!char.IsAsciiLetter(text[position]))continue;
            var begin=position;
            while(position<text.Length&&!char.IsWhiteSpace(text[position])&&text[position] is not ('/' or '>'))position++;
            var tag=text[begin..position].ToLowerInvariant();var asset=tag is "script" or "img" or "link" or "iframe" or "video" or "audio";var selfClosing=false;
            while(position<text.Length)
            {
                while(position<text.Length&&char.IsWhiteSpace(text[position]))position++;
                if(position>=text.Length)break;
                if(text[position]=='>'){position++;break;}
                if(text[position]=='/'){position++;if(position<text.Length&&text[position]=='>'){position++;selfClosing=true;break;}continue;}
                begin=position;
                while(position<text.Length&&!char.IsWhiteSpace(text[position])&&text[position] is not ('=' or '/' or '>'))position++;
                if(position==begin){position++;continue;}
                var name=text[begin..position].ToLowerInvariant();
                while(position<text.Length&&char.IsWhiteSpace(text[position]))position++;
                if(position>=text.Length||text[position]!='=')continue;
                position++;while(position<text.Length&&char.IsWhiteSpace(text[position]))position++;
                string value;
                if(position<text.Length&&(text[position] is '\'' or '"'))
                {
                    var quote=text[position++];begin=position;var end=text.IndexOf(quote,position);
                    if(end<0){value=text[begin..];position=text.Length;}else{value=text[begin..end];position=end+1;}
                }
                else
                {begin=position;while(position<text.Length&&!char.IsWhiteSpace(text[position])&&text[position]!='>')position++;value=text[begin..position];}
                if(asset&&(name is "src" or "href"))yield return(tag,name,value);
            }
            if(!selfClosing&&(tag is "script" or "style"))
            {var close=(tag=="script"?ScriptEnd:StyleEnd).Match(text,position);if(!close.Success)yield break;position=close.Index+close.Length;}
        }
    }
}
