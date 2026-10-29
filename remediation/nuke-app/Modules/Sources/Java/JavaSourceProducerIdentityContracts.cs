namespace AndroidMcp.BuildTool.Sources.Java;

// Captured by the sole loader from its admitted bytes and loaded parser.
// This is evidence for a later pinned admission, not a second manifest reader.
public sealed record JavaSourceProducerIdentity(
    string ManifestPath,string ManifestSha256,long ManifestLength,
    string NativeRelativePath,string NativeSha256,long NativeLength,
    string Rid,int LanguageAbi,string RuntimeVersion,string RuntimeCommit,
    string RuntimeTagObject,string GrammarVersion,string GrammarCommit);

public interface IJavaSourceProducerIdentityRuntime : IJavaSourceRuntime
{
    JavaSourceProducerIdentity Identity { get; }
}
