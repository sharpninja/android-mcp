using System.Text.Json;
using AndroidMcp.BuildTool.Core;

namespace AndroidMcp.BuildTool.Sources.Java;

// Local admission only. Native byte/host/export/ABI validation is delegated to
// the existing JavaNativeParser, with no compiler, transport or system fallback.
public sealed class JavaSourceRuntimeLoader(IStrictJsonInspector? inspector=null):IJavaSourceRuntimeLoader
{
    private readonly IStrictJsonInspector strict=inspector??new StrictJsonInspector();
    public IJavaSourceRuntime Open(SourceJavaAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if(string.IsNullOrWhiteSpace(admission.ManifestPath))throw new ArgumentException("Select a Java native source-lock manifest.",nameof(admission));
        var selected=Path.GetFullPath(admission.ManifestPath);var directory=Path.GetDirectoryName(selected)!;
        var path=ArtifactPaths.Resolve(directory,Path.GetFileName(selected));
        var inspected=strict.Inspect(File.ReadAllBytes(path));var diagnostics=inspected.CanonicalDiagnostics;
        if(diagnostics.Count!=0)throw new InvalidDataException("Java native manifest failed strict JSON admission: "+diagnostics[0].Code+" "+diagnostics[0].Message);
        var manifest=inspected.Value??throw new InvalidDataException("Java native manifest has no JSON value.");
        if(manifest.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Java native manifest must be an object.");
        Require(manifest,"schema",JavaNativeWorkflow.ManifestSchema);
        Require(manifest,"runtime_version",JavaNativeWorkflow.RuntimeVersion);
        Require(manifest,"runtime_commit",JavaNativeBuilder.RuntimeCommit);
        Require(manifest,"runtime_tag_object",JavaNativeBuilder.RuntimeTagObject);
        Require(manifest,"grammar_version",JavaNativeWorkflow.GrammarVersion);
        Require(manifest,"grammar_commit",JavaNativeBuilder.GrammarCommit);
        var rid=Text(manifest,"rid");
        if(!manifest.TryGetProperty("native_artifact",out var artifact)||artifact.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Java native artifact descriptor is absent.");
        var native=ArtifactPaths.Resolve(directory,Text(artifact,"relative_path"));var sha=Text(artifact,"sha256");
        if(!artifact.TryGetProperty("length",out var length)||!length.TryGetInt64(out var expectedLength)||expectedLength<=0||new FileInfo(native).Length!=expectedLength)throw new InvalidDataException("Java native artifact length differs from its descriptor.");
        if(!artifact.TryGetProperty("language_abi",out var abi)||!abi.TryGetInt32(out var claimedAbi))throw new InvalidDataException("Java native artifact ABI descriptor is absent or not an integer.");
        if(!manifest.TryGetProperty("required_exports",out var exports)||exports.ValueKind!=JsonValueKind.Array)throw new InvalidDataException("Java native export contract is absent.");
        var symbols=new HashSet<string>(StringComparer.Ordinal);
        foreach(var symbol in exports.EnumerateArray())
            if(symbol.ValueKind!=JsonValueKind.String||symbol.GetString() is not { } name||!symbols.Add(name))throw new InvalidDataException("Java native export contract contains a non-string or duplicate symbol.");
        if(!symbols.SetEquals(JavaNativeParser.RequiredSymbols))throw new InvalidDataException("Java native export contract differs from the pinned parser contract.");
        JavaNativeParser? parser=null;
        try
        {
            parser=new JavaNativeParser(new(native,sha,rid));
            if(parser.LanguageVersion!=claimedAbi)throw new InvalidDataException("Java native ABI differs from the manifest descriptor.");
            var runtime=new OwnedJavaRuntime(parser);parser=null;return runtime;
        }
        finally{parser?.Dispose();}
    }
    private static string Text(JsonElement value,string property)
    {
        if(!value.TryGetProperty(property,out var field)||field.ValueKind!=JsonValueKind.String||field.GetString() is not {Length:>0} text)
            throw new InvalidDataException("Java native manifest requires string field "+property+".");
        return text;
    }
    private static void Require(JsonElement value,string property,string expected)
    {if(Text(value,property)!=expected)throw new InvalidDataException("Java native manifest has an unapproved "+property+".");}
    private sealed class OwnedJavaRuntime:IJavaSourceRuntime
    {
        private readonly JavaNativeParser parser;private readonly ISourceExtractor extractor;private int disposed;
        internal OwnedJavaRuntime(JavaNativeParser parser){this.parser=parser;extractor=new JavaSourceExtractor(parser);}
        public ISourceExtractor Extractor{get{ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);return extractor;}}
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)==0)parser.Dispose();}
    }
}
