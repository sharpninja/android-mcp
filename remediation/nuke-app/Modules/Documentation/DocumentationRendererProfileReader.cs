using System.Text.Json;
using System.Text.RegularExpressions;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Modules.Sources.Acquisition;

namespace AndroidMcp.BuildTool.Documentation;

/// <summary>Interprets retained lock data without acquisition, process execution or host changes.</summary>
public sealed class DocumentationRendererProfileReader:IDocumentationRendererProfileReader
{
    private readonly IStrictJsonInspector strict;
    public DocumentationRendererProfileReader():this(new StrictJsonInspector()) { }
    public DocumentationRendererProfileReader(IStrictJsonInspector inspector){strict=inspector;}
    public DocumentationRendererProfile Read(Artifact toolchainLock,Artifact rendererLock)
    {
        string gradle,plantuml,cli,image;SourcePin[] downloads;var redirects=new Dictionary<string,IReadOnlyList<Uri>>(StringComparer.Ordinal);
        try
        {
            var inspection=strict.Inspect(toolchainLock.Bytes);
            if(inspection.CanonicalDiagnostics.Count>0||inspection.Value is null)throw new FormatException("Strict JSON inspection rejected toolchain lock");
            var root=inspection.Value.Value;
            gradle=Version(root,"gradle");plantuml=Version(root,"plantuml");cli=Version(root,"structurizr_cli");image=Text(root,"structurizr_image");
            var expected=new HashSet<string>(StringComparer.Ordinal){".tools/gradle-"+gradle+"-bin.zip","gradlew","gradlew.bat","gradle/wrapper/gradle-wrapper.jar",".tools/plantuml-"+plantuml+".jar"};
            downloads=root.GetProperty("files").EnumerateArray().Select(file=>
            {
                var path=Text(file,"file");var hash=Text(file,"sha256");var size=file.GetProperty("size_bytes").GetInt64();var url=new Uri(Text(file,"url"),UriKind.Absolute);
                if(!expected.Contains(path)||!ArtifactPaths.IsRelative(path)||!Hash(hash)||size<0||url.Scheme!=Uri.UriSchemeHttps||url.UserInfo.Length>0)throw new FormatException("Invalid pinned tool artifact");
                if(file.TryGetProperty("allowed_redirects",out var declared))
                {
                    var urls=declared.EnumerateArray().Select(value=>new Uri(value.GetString()??throw new FormatException("Null redirect URL"),UriKind.Absolute)).ToArray();
                    if(urls.Any(value=>value.Scheme!=Uri.UriSchemeHttps||value.UserInfo.Length>0)||urls.Distinct().Count()!=urls.Length)throw new FormatException("Invalid or duplicate declared redirect URL");
                    redirects.Add(path,urls);
                }
                return new SourcePin(path,url,path==".tools/plantuml-"+plantuml+".jar"?plantuml:gradle,hash,size);
            }).ToArray();
            if(downloads.Length!=expected.Count||!expected.SetEquals(downloads.Select(p=>p.LocalPath)))throw new FormatException("Missing, duplicate or unexpected tool artifacts");
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {throw new ArchitectureAssetException("TOOLCHAIN_PIN","Invalid committed toolchain lock: "+error.Message);}
        try
        {
            var inspection=strict.Inspect(rendererLock.Bytes);
            if(inspection.CanonicalDiagnostics.Count>0||inspection.Value is null)throw new FormatException("Strict JSON inspection rejected renderer lock");
            var root=inspection.Value.Value;var selectedImage=Text(root,"image");var java=Version(root,"java_version");
            if(!Regex.IsMatch(selectedImage,@"^[a-z0-9./_-]+@sha256:[a-f0-9]{64}$")||selectedImage!=image)throw new FormatException("Image identity is mutable or differs between locks");
            var files=root.GetProperty("renderer_files").EnumerateArray().Select(file=>new RendererFilePin(Text(file,"path"),Text(file,"sha256"))).ToArray();
            if(files.Length!=5||files.Select(p=>p.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=5||files.Any(p=>!ArtifactPaths.IsRelative(p.RelativePath)||!Hash(p.Sha256))||!files.Any(p=>p.RelativePath=="structurizr.sh")||!files.Any(p=>p.RelativePath=="lib/structurizr-cli.jar")||new[]{"dsl","export","core"}.Any(kind=>files.Count(p=>Regex.IsMatch(p.RelativePath,@"^lib/structurizr-"+kind+@"-[0-9.]+\.jar$"))!=1))
                throw new FormatException("Missing, duplicate or invalid renderer identity pins");
            return new(selectedImage,java,cli,plantuml,gradle,downloads,files,redirects.Count==0?null:redirects);
        }
        catch(Exception error) when(error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        {throw new ArchitectureAssetException("RENDERER_PIN","Invalid committed renderer lock: "+error.Message);}
    }
    private static bool Hash(string value)=>Regex.IsMatch(value,"^[a-f0-9]{64}$");
    private static string Text(JsonElement parent,string name)=>parent.GetProperty(name).GetString()??throw new FormatException("Null "+name);
    private static string Version(JsonElement parent,string name)
    {var value=Text(parent,name);return Regex.IsMatch(value,@"^[0-9]+(?:\.[0-9]+){1,3}$")?value:throw new FormatException("Invalid "+name);}
}
