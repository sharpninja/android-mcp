namespace AndroidMcp.BuildTool.Core;

/// <summary>One root-relative path policy for stores, recipes, archives and manifests.</summary>
public static class ArtifactPaths
{
    public static string[] Segments(string relativePath)
    {
        if(string.IsNullOrWhiteSpace(relativePath) || relativePath.Contains('\\') || relativePath.Contains(':') || Path.IsPathRooted(relativePath))
            throw new ArgumentException("Use a nonempty portable root-relative artifact path.",nameof(relativePath));
        var parts=relativePath.Split('/');
        if(parts.Any(x=>x is "" or "." or ".." || x.IndexOfAny(Path.GetInvalidFileNameChars())>=0))
            throw new ArgumentException("Artifact path contains unsafe components.",nameof(relativePath));
        return parts;
    }
    public static bool IsRelative(string? relativePath)
    {
        try { Segments(relativePath!);return true; } catch(ArgumentException) { return false; }
    }
    public static string Resolve(string rootDirectory,string relativePath)
    {
        var parts=Segments(relativePath);
        var path=Path.GetFullPath(rootDirectory);
        RejectReparse(path);
        foreach(var part in parts){path=Path.Combine(path,part);RejectReparse(path);}
        return path;
    }
    private static void RejectReparse(string path)
    {
        if((Directory.Exists(path)||File.Exists(path)) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
            throw new ArgumentException("Artifact paths cannot traverse reparse points.",nameof(path));
    }
}
