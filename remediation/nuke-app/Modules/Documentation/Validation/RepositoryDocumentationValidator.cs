using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Publication;
using AndroidMcp.BuildTool.Sources;

namespace AndroidMcp.BuildTool.Documentation.Validation;

// Original repository checker rules over the complete admitted entry snapshot.
// No enumeration, filesystem implementation, hash implementation or second HTML
// parser lives here. Public root/JSON/exit caller binding remains outside.
public sealed class RepositoryDocumentationValidator:IRepositoryDocumentationValidator
{
    private readonly IArtifactStore repository;
    private readonly IArtifactReferenceResolver references;
    private readonly IArtifactReceiptVerifier receipts;
    private readonly ISourceHtmlDecoder html;
    private readonly IArtifactTreeReader? metadata;
    public RepositoryDocumentationValidator(IArtifactStore repository,IArtifactReferenceResolver references,IArtifactReceiptVerifier receipts,ISourceHtmlDecoder html)
        :this(repository,references,receipts,html,null){ }
    public RepositoryDocumentationValidator(IArtifactStore repository,IArtifactReferenceResolver references,IArtifactReceiptVerifier receipts,ISourceHtmlDecoder html,IArtifactTreeReader? metadata)
    {this.repository=repository;this.references=references;this.receipts=receipts;this.html=html;this.metadata=metadata;}
    private const string Design="remediation/design-c4/",Checksum=Design+"docs/checksums.sha256",Presentation=Design+"docs/storyboards.html";
    private static readonly Regex MarkdownLink=new(@"\]\(([^)]+)\)",RegexOptions.CultureInvariant);
    public async Task<RepositoryDocumentationCheckResult> CheckAsync(IReadOnlyList<PublicationFile> entries,CancellationToken cancellationToken=default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var byPath=entries.ToDictionary(entry=>entry.Path,StringComparer.Ordinal);
        var errors=new List<string>();var checksumCount=0;var linkCount=0;
        async Task<ReadOnlyMemory<byte>> Read(string path)=>await repository.ReadAsync(path,cancellationToken);
        async Task<string> Text(string path)=>new UTF8Encoding(false,true).GetString((await Read(path)).Span).Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n');
        bool IsFile(string path)=>byPath.TryGetValue(path,out var item)&&item.Kind==PublicationFileKind.Regular;
        async Task<bool> Exists(string path)
        {
            if(path.Length==0)return true; // The selected repository root exists.
            if(byPath.TryGetValue(path,out var item)){
                if(item.Kind==PublicationFileKind.SymbolicLink)throw Scope("symbolic-link");
                return item.Kind is PublicationFileKind.Regular or PublicationFileKind.Directory;
            }
            // Root enumeration deliberately never descends .git. Query only
            // the referenced parent, with the SAME bound metadata authority.
            var parts=path.Split('/');
            if(metadata is null||!parts.Take(parts.Length-1).Contains(".git",StringComparer.Ordinal))return false;
            if(!ArtifactPaths.IsRelative(path))throw Scope("metadata-path");
            var parent=string.Join('/',parts.Take(parts.Length-1));IReadOnlyList<ArtifactTreeEntry> children;
            try{children=await metadata.EnumerateAsync(parent,false,cancellationToken);}
            catch(FileNotFoundException){return false;}
            catch(DirectoryNotFoundException){return false;}
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var child in children)
                if(!ArtifactPaths.IsRelative(child.RelativePath)||Path.GetDirectoryName(child.RelativePath)?.Replace('\\','/')!=parent||!seen.Add(child.RelativePath))throw Scope("metadata-scope");
            var match=children.SingleOrDefault(child=>child.RelativePath==path);
            if(match is null)return false;
            if(match.Kind is not(NativeArtifactKind.RegularFile or NativeArtifactKind.Directory)||match.Native is null||match.Native.Kind!=match.Kind||match.Native.Length!=match.Length)throw Scope("metadata-kind-or-identity");
            return true;
        }
        using(var lines=new StringReader(await Text(Checksum)))while(lines.ReadLine() is {} line)
        {
            var separator=line.IndexOf("  ",StringComparison.Ordinal);
            if(separator<0)throw new FormatException("Checksum row requires a two-space separator");
            var expected=line[..separator];var relative=line[(separator+2)..];checksumCount++;
            var path=Design+relative;
            if(!ArtifactPaths.IsRelative(path))throw Scope("checksum-path");
            if(!IsFile(path)||receipts.Describe(new(path,await Read(path),"application/octet-stream")).Sha256!=expected)
                errors.Add("Missing or changed design checksum: "+relative);
        }
        foreach(var entry in entries.Where(entry=>entry.Kind==PublicationFileKind.Regular&&entry.Path.EndsWith(".md",StringComparison.Ordinal)&&!entry.Path.Split('/').Contains(".git",StringComparer.Ordinal)))
        {
            foreach(Match match in MarkdownLink.Matches(await Text(entry.Path)))
            {
                var raw=match.Groups[1].Value;var resolved=references.Resolve(new(entry.Path,raw,ArtifactReferenceProfile.RepositoryMarkdown));
                if(resolved.Disposition==ArtifactReferenceDisposition.Ignored)continue;
                linkCount++;
                if(resolved.Disposition!=ArtifactReferenceDisposition.Local||resolved.RootRelativePath is null)throw Scope(resolved.Reason??"reference");
                if(!await Exists(resolved.RootRelativePath))errors.Add("Missing link in "+entry.Path+": "+raw.Trim('<','>').Split('#')[0]);
            }
        }
        var vectors=entries.Where(entry=>entry.Kind==PublicationFileKind.Regular&&entry.Path.StartsWith(Design+"docs/",StringComparison.Ordinal)&&entry.Path.EndsWith(".svg",StringComparison.Ordinal)).ToArray();
        foreach(var vector in vectors)
        {
            using var input=new MemoryStream((await Read(vector.Path)).ToArray(),false);
            // Original ET.parse checks XML, not an invented SVG root/namespace
            // or expected view set. Reuse the BCL XML parser with no resolver.
            using var reader=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Parse,XmlResolver=null});
            _=XDocument.Load(reader);
        }
        var editable=entries.Where(entry=>entry.Kind==PublicationFileKind.Regular&&entry.Path.StartsWith(Design+"ui-svg/",StringComparison.Ordinal)&&
            !entry.Path[(Design+"ui-svg/").Length..].Contains('/')&&entry.Path.EndsWith(".svg",StringComparison.Ordinal)).ToArray();
        foreach(var entry in editable)
        {
            var copy=Design+"docs/screens/"+Path.GetFileName(entry.Path);
            // The original copy loop reads the editable bytes only after its
            // destination exists. Retain that short-circuit IO ordering.
            if(!IsFile(copy))errors.Add("Different UI copy: "+Path.GetFileName(entry.Path));
            else {
                var editableBytes=await Read(entry.Path);var copyBytes=await Read(copy);
                if(!editableBytes.Span.SequenceEqual(copyBytes.Span))errors.Add("Different UI copy: "+Path.GetFileName(entry.Path));
            }
        }
        foreach(var attribute in GeneratedDesignValidator.AssetAttributes(await Text(Presentation)))
            if(references.Resolve(new(Presentation,attribute.Value,ArtifactReferenceProfile.RepositoryOfflineAsset)).Disposition==ArtifactReferenceDisposition.Remote)
                errors.Add("Remote presentation asset: "+attribute.Tag+": "+html.Decode(attribute.Value));
        return new(checksumCount,linkCount,vectors.Length,editable.Length,errors.ToArray());
    }
    private static ArchitectureAssetException Scope(string reason)=>new("DOCS_REPOSITORY_REFERENCE_SCOPE","Repository reference requires shared path/entry admission: "+reason);
}
