namespace AndroidMcp.BuildTool.Core;

public enum GlobMatchingProfile { PublicationFilename, PosixFullPath }

// Pure matching only. Filesystem containment and selector admission are separate callers.
public interface IGlobMatcher
{
    bool Matches(string text, string pattern, GlobMatchingProfile profile);
}
