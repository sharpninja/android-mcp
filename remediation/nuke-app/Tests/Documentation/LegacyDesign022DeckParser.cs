using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Tests.Documentation;

// Test-only oracle for the original DeckParser's two callbacks. The production
// renderer remains the existing projector. Decoding reuses the shared Python
// html.unescape-compatible authority; no second production parser is added.
internal static class LegacyDesign022DeckParser
{
    private static readonly Regex Attribute=new(@"(?<name>[^\s=/>]+)(?:\s*=\s*(?:""(?<double>[^""]*)""|'(?<single>[^']*)'|(?<bare>[^\s>]+)))?",RegexOptions.CultureInvariant);
    internal static Design022DeckObservation Read(string html)
    {
        var images=new List<string?>();var literals=new List<string>();var decoder=new SourceHtmlDecoder();var position=0;
        void Data(string text,bool decode=true)
        {var value=decode?decoder.Decode(text):text;if(value.Contains("![",StringComparison.Ordinal)&&value.Contains("](",StringComparison.Ordinal))literals.Add(string.Concat(value.EnumerateRunes().Take(80).Select(rune=>rune.ToString())));}
        while(position<html.Length)
        {
            var open=html.IndexOf('<',position);
            if(open<0){Data(html[position..]);break;}
            if(open>position)Data(html[position..open]);
            if(html.AsSpan(open).StartsWith("<!--",StringComparison.Ordinal))
            {var commentEnd=html.IndexOf("-->",open+4,StringComparison.Ordinal);if(commentEnd<0)break;position=commentEnd+3;continue;}
            var end=open+1;char quote='\0';
            for(;end<html.Length;end++){
                var character=html[end];if(quote!='\0'){if(character==quote)quote='\0';}
                else if(character is '\'' or '"')quote=character;
                else if(character=='>')break;
            }
            if(end==html.Length){Data(html[open..]);break;}
            position=end+1;var body=html[(open+1)..end];
            if(body.Length==0||!char.IsAsciiLetter(body[0]))continue;
            var nameEnd=0;while(nameEnd<body.Length&&!char.IsWhiteSpace(body[nameEnd])&&body[nameEnd]!='/')nameEnd++;
            var name=body[..nameEnd].ToLowerInvariant();var attributes=new Dictionary<string,string?>(StringComparer.Ordinal);
            foreach(Match attribute in Attribute.Matches(body[nameEnd..])){
                var raw=attribute.Groups["double"].Success?attribute.Groups["double"]:attribute.Groups["single"].Success?attribute.Groups["single"]:attribute.Groups["bare"];
                attributes[attribute.Groups["name"].Value.ToLowerInvariant()]=raw.Success?decoder.Decode(raw.Value):null;
            }
            if(name=="img"&&attributes.GetValueOrDefault("alt")=="Screen")images.Add(attributes.TryGetValue("src",out var image)?image:"");
            if((name is "script" or "style")&&!body.TrimEnd().EndsWith('/')){
                var close=Regex.Match(html[position..],@"</\s*"+name+@"\s*>",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
                if(!close.Success)break; // HTMLParser.feed buffers unfinished CDATA until its closing tag.
                Data(html.Substring(position,close.Index),false);position+=close.Index+close.Length;
            }
        }
        return new(images,literals);
    }
}
