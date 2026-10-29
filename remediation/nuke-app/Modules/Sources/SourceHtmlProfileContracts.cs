namespace AndroidMcp.BuildTool.Sources;

public enum SourceHtmlDecodingProfile { Text, Attribute }
// Additive seam. The existing one-argument Decode keeps its text semantics.
public interface IProfiledSourceHtmlDecoder : ISourceHtmlDecoder
{
    string Decode(string text, SourceHtmlDecodingProfile profile);
}
