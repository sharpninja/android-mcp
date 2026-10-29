using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Publication;

// Pure policy semantics. Filesystem observations come from the root adapter;
// artifact-store boundary validation still applies independently.
public sealed class PublicationChecker(ICanonicalJson canonical,IGlobMatcher? globMatcher=null) : IPublicationChecker
{
    private readonly IGlobMatcher matcher=globMatcher??new GlobMatcher();
    public PublicationCheckResult Check(ReadOnlyMemory<byte> policy, IReadOnlyList<PublicationFile> files)
    {
        var issues=new List<PublicationIssue>();
        long total=0;
        foreach(var file in files)
        {
            try { total=checked(total+Math.Max(0,file.Length)); }
            catch(OverflowException) { issues.Add(new("LENGTH_OVERFLOW",file.Path)); }
        }
        string[] allow,deny;long maximum;
        try
        {
            using var json=JsonDocument.Parse(canonical.Serialize(policy));
            var root=json.RootElement;
            maximum=root.GetProperty("maximum_file_bytes").GetInt64();
            allow=root.GetProperty("allow").EnumerateArray().Select(x=>x.GetString() ?? throw new FormatException()).ToArray();
            deny=root.GetProperty("deny").EnumerateArray().Select(x=>x.GetString() ?? throw new FormatException()).ToArray();
            if(maximum<0 || allow.Length==0 || allow.Any(string.IsNullOrEmpty) || deny.Any(string.IsNullOrEmpty))throw new FormatException();
        }
        catch(Exception e) when(e is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { issues.Add(new("INVALID_POLICY",""));return new(files.Count,total,issues); }
        var names=new HashSet<string>(StringComparer.Ordinal);
        var portable=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var file in files)
        {
            if(!names.Add(file.Path))issues.Add(new("DUPLICATE_PATH",file.Path));
            else if(!portable.Add(file.Path))issues.Add(new("PORTABLE_PATH_COLLISION",file.Path));
            if(file.Length<0)issues.Add(new("INVALID_LENGTH",file.Path));
            if(!PublicationNames.Relative(file.Path)){issues.Add(new("UNSAFE_PATH",file.Path));continue;}
            if(!allow.Any(p=>matcher.Matches(file.Path,p,GlobMatchingProfile.PublicationFilename))){issues.Add(new("NOT_ALLOWED",file.Path));continue;}
            if(deny.Any(p=>matcher.Matches(file.Path,p,GlobMatchingProfile.PublicationFilename)))issues.Add(new("DENIED",file.Path));
            if(file.Kind!=PublicationFileKind.Regular)issues.Add(new("NOT_REGULAR",file.Path));
            if(file.Length>maximum)issues.Add(new("TOO_LARGE",file.Path));
        }
        return new(files.Count,total,issues);
    }
}

internal static class PublicationNames
{
    public static bool Relative(string value)=>!string.IsNullOrEmpty(value) && !value.StartsWith('/') &&
        !value.Any(c=>c=='\\' || c==':' || char.IsControl(c)) &&
        value.Split('/').All(p=>p.Length>0 && p!="." && p!="..");
    public static bool Pin(string value)=>value.Length==64 && value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    public static bool Generation(string value)=>value.Length is >0 and <=128 &&
        value!="." && value!=".." && value.All(c=>char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}
