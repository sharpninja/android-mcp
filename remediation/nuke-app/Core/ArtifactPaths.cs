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
        // The selected root may be an ordinary directory reached through a
        // reparse point above it. Inspect that chain before creating anything.
        var ancestors=new Stack<string>();
        for(DirectoryInfo? ancestor=new(path);ancestor is not null;ancestor=ancestor.Parent)
            ancestors.Push(ancestor.FullName);
        foreach(var ancestor in ancestors)RejectReparse(ancestor);
        foreach(var part in parts){path=Path.Combine(path,part);RejectReparse(path);}
        return path;
    }
    private static void RejectReparse(string path)
    {
        FileAttributes attributes;
        try { attributes=File.GetAttributes(path); }
        catch(FileNotFoundException) { return; }
        catch(DirectoryNotFoundException) { return; }
        // Exists() deliberately hides dangling links; GetAttributes does not.
        if((attributes&FileAttributes.ReparsePoint)!=0)
            throw new ArgumentException("Artifact paths cannot traverse reparse points.",nameof(path));
    }
}
