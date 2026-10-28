using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Pure editable code extraction and final vector presentation profile.</summary>
public sealed class ArchitectureAssetPreparation : IArchitectureAssetPreparation
{
    public IReadOnlyList<Artifact> ExtractCodeViews(ArchitecturePreparationInput input)
    {
        var dsl = Encoding.UTF8.GetString(input.Workspace.Bytes.Span);
        var code = Encoding.UTF8.GetString(input.CodeMarkdown.Bytes.Span);
        var expected = Regex.Matches(dsl,@"(?m)^\s*image\s+\.\./docs/diagrams/([a-z-]+)\.svg\s*$")
            .Select(m=>m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        if(expected.Count==0)throw new ArchitectureAssetException("MISSING_CODE_VIEW","Workspace declares no editable code-view images");
        var blocks = Regex.Matches(code,@"(?ms)^## ([a-z-]+)[^\n]*\r?\n.*?```plantuml\r?\n(.*?)\r?\n```");
        var extracted = new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(Match match in blocks)
        {
            var key=match.Groups[1].Value;
            if(!extracted.TryAdd(key,match.Groups[2].Value+"\n"))
                throw new ArchitectureAssetException("DUPLICATE_CODE_VIEW","Duplicate editable code view: "+key);
        }
        var missing=expected.Except(extracted.Keys,StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(missing.Length>0)throw new ArchitectureAssetException("MISSING_CODE_VIEW","Missing editable code view: "+string.Join(", ",missing));
        var unexpected=extracted.Keys.Except(expected,StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if(unexpected.Length>0)throw new ArchitectureAssetException("UNEXPECTED_CODE_VIEW","Unexpected editable code view: "+string.Join(", ",unexpected));
        return extracted.OrderBy(p=>p.Key,StringComparer.Ordinal)
            .Select(p=>new Artifact("build/code-views/"+p.Key+".puml",Encoding.UTF8.GetBytes(p.Value),"text/vnd.plantuml")).ToArray();
    }

    public Artifact NormalizeVector(Artifact rawSvg,string outputPath)
    {
        XElement root;
        try
        {
            using var input=new StringReader(Encoding.UTF8.GetString(rawSvg.Bytes.Span));
            using var reader=XmlReader.Create(input,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null });
            root=XElement.Load(reader,LoadOptions.PreserveWhitespace);
            if(root.Name.LocalName!="svg" || root.Name.NamespaceName!="http://www.w3.org/2000/svg")
                throw new XmlException("Expected SVG namespace and root");
        }
        catch(XmlException error){throw new ArchitectureAssetException("INVALID_SVG","Invalid generated SVG: "+error.Message);}
        if(root.Descendants().Any(e=>e.Name.LocalName is "image" or "script") || !root.Descendants().Any(e=>e.Name.LocalName=="text"))
            throw new ArchitectureAssetException("NON_VECTOR_CONTENT","SVG requires vector text and no raster/script content");
        foreach(var metadata in root.DescendantNodes().OfType<XComment>().Where(c=>c.Value.StartsWith("SRC=[",StringComparison.Ordinal)).ToArray())metadata.Remove();
        foreach(var title in root.Descendants().Where(e=>e.Name.LocalName=="title"))
            title.Value=Regex.Replace(title.Value,@"</?size(?::[^>]+)?>","");
        var canvases=root.Elements().Where(e=>e.Name.LocalName=="rect" && (string?)e.Attribute("data-canvas")=="white").ToArray();
        if(canvases.Length==0)
        {
            var dimensions=Regex.Match((string?)root.Attribute("viewBox")??"",@"^0 0 ([0-9.]+) ([0-9.]+)$");
            if(!dimensions.Success || !decimal.TryParse(dimensions.Groups[1].Value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var width) || !decimal.TryParse(dimensions.Groups[2].Value,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var height))
                throw new ArchitectureAssetException("INVALID_SVG","Generated SVG requires a zero-origin numeric viewBox");
            root.SetAttributeValue("viewBox","-64 -64 "+(width+128).ToString(CultureInfo.InvariantCulture)+" "+(height+128).ToString(CultureInfo.InvariantCulture));
            root.AddFirst(new XElement(root.Name.Namespace+"rect",new XAttribute("data-canvas","white"),new XAttribute("x","-64"),new XAttribute("y","-64"),new XAttribute("width","200%"),new XAttribute("height","200%"),new XAttribute("fill","#ffffff")));
        }
        return new Artifact(outputPath,Encoding.UTF8.GetBytes(root.ToString(SaveOptions.DisableFormatting)+"\n"),"image/svg+xml");
    }
}
