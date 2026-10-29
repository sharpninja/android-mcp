using System.Runtime.InteropServices;
using System.Text;
using AndroidMcp.BuildTool.Core;
using Microsoft.Win32.SafeHandles;

namespace AndroidMcp.BuildTool.Sources.Java;

public sealed record JavaNativeAsset(string AbsolutePath,string Sha256,string Rid="linux-x64");

// Layout and bool marshalling are pinned to runtime0.25.2 api.h.
// Every parse owns its parser/tree; no static mutable native parser exists.
public sealed class JavaNativeParser : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] internal readonly struct Point { public readonly uint Row; public readonly uint Column; }
    [StructLayout(LayoutKind.Sequential)] internal readonly struct Node
    { private readonly uint c0,c1,c2,c3; internal readonly IntPtr Id,Tree; internal bool IsNull=>Id==IntPtr.Zero; }
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Create();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Delete(IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] private delegate bool SetLanguage(IntPtr parser,IntPtr language);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint LanguageAbi(IntPtr language);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr ParseString(IntPtr parser,IntPtr oldTree,[In]byte[] data,uint length);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Node RootNode(IntPtr tree);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr NodeType(Node node);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint NodeNumber(Node node);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Point NodePoint(Node node);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] private delegate bool NodeBool(Node node);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Node NodeChild(Node node,uint index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate Node NodeField(Node node,[MarshalAs(UnmanagedType.LPUTF8Str)]string field,uint length);
    private sealed class OwnedHandle(Delete delete) : SafeHandleZeroOrMinusOneIsInvalid(true)
    {
        private readonly Delete release=delete;
        public static OwnedHandle Create(IntPtr pointer,Delete delete) { var result=new OwnedHandle(delete);result.SetHandle(pointer);return result; }
        protected override bool ReleaseHandle(){release(handle);return true;}
    }
    private readonly object sync=new();private IntPtr library;
    private readonly Create parserNew,language;private readonly Delete parserDelete,treeDelete;
    private readonly SetLanguage setLanguage;private readonly LanguageAbi languageAbi;private readonly ParseString parse;
    private readonly RootNode root;private readonly NodeType nodeType;private readonly NodeNumber start,end,namedCount,childCount;
    private readonly NodePoint startPoint;private readonly NodeBool missing,hasError;private readonly NodeChild namedChild,child;private readonly NodeField field;
    public int LanguageVersion { get; }
    public string ArtifactSha256 { get; }
    public static IReadOnlyList<string> RequiredSymbols { get; }=Array.AsReadOnly(new[]{"tree_sitter_java","ts_parser_new","ts_parser_delete","ts_parser_set_language","ts_language_abi_version",
        "ts_parser_parse_string","ts_tree_delete","ts_tree_root_node","ts_node_type","ts_node_start_byte","ts_node_end_byte","ts_node_start_point",
        "ts_node_named_child_count","ts_node_child_count","ts_node_named_child","ts_node_child","ts_node_child_by_field_name","ts_node_is_missing","ts_node_has_error"});
    public JavaNativeParser(JavaNativeAsset asset)
    {
        if(asset.Rid!="linux-x64" || !OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture!=Architecture.X64)
            throw new PlatformNotSupportedException("Java native parser currently validated only for linux-x64.");
        if(!Path.IsPathFullyQualified(asset.AbsolutePath) || !File.Exists(asset.AbsolutePath))throw new FileNotFoundException("Pinned project Java parser asset is absent.",asset.AbsolutePath);
        ArtifactSha256=ContentHash.Sha256(File.ReadAllBytes(asset.AbsolutePath));
        if(asset.Sha256.Length!=64 || ArtifactSha256!=asset.Sha256)throw new InvalidDataException("Java native parser content pin mismatch.");
        library=NativeLibrary.Load(asset.AbsolutePath);
        try
        {
            foreach(var symbol in RequiredSymbols)NativeLibrary.GetExport(library,symbol);
            parserNew=Bind<Create>("ts_parser_new");language=Bind<Create>("tree_sitter_java");parserDelete=Bind<Delete>("ts_parser_delete");treeDelete=Bind<Delete>("ts_tree_delete");
            setLanguage=Bind<SetLanguage>("ts_parser_set_language");languageAbi=Bind<LanguageAbi>("ts_language_abi_version");parse=Bind<ParseString>("ts_parser_parse_string");root=Bind<RootNode>("ts_tree_root_node");
            nodeType=Bind<NodeType>("ts_node_type");start=Bind<NodeNumber>("ts_node_start_byte");end=Bind<NodeNumber>("ts_node_end_byte");startPoint=Bind<NodePoint>("ts_node_start_point");
            namedCount=Bind<NodeNumber>("ts_node_named_child_count");childCount=Bind<NodeNumber>("ts_node_child_count");namedChild=Bind<NodeChild>("ts_node_named_child");child=Bind<NodeChild>("ts_node_child");
            field=Bind<NodeField>("ts_node_child_by_field_name");missing=Bind<NodeBool>("ts_node_is_missing");hasError=Bind<NodeBool>("ts_node_has_error");
            LanguageVersion=checked((int)languageAbi(language()));if(LanguageVersion!=14)throw new InvalidDataException("Pinned Java grammar must expose ABI14.");
            if(Marshal.SizeOf<Point>()!=8 || Marshal.SizeOf<Node>()!=16+2*IntPtr.Size)throw new InvalidDataException("Native Java node/point layout mismatch.");
        }
        catch {NativeLibrary.Free(library);library=IntPtr.Zero;throw;}
    }
    private T Bind<T>(string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
    internal T WithTree<T>(ReadOnlyMemory<byte> data,Func<SyntaxTree,T> visitor)
    {
        lock(sync)
        {
            ObjectDisposedException.ThrowIf(library==IntPtr.Zero,this);
            var bytes=data.ToArray();
            using var parser=OwnedHandle.Create(parserNew(),parserDelete);if(parser.IsInvalid)throw new InvalidDataException("Native Java parser allocation failed.");
            if(!setLanguage(parser.DangerousGetHandle(),language()))throw new InvalidDataException("Native Java parser rejected pinned grammar ABI.");
            using var tree=OwnedHandle.Create(parse(parser.DangerousGetHandle(),IntPtr.Zero,bytes,checked((uint)bytes.Length)),treeDelete);
            if(tree.IsInvalid)throw new InvalidDataException("Native Java parse returned no tree.");
            return visitor(new SyntaxTree(this,bytes,root(tree.DangerousGetHandle())));
        }
    }
    internal sealed class SyntaxTree(JavaNativeParser api,byte[] data,Node root)
    {
        internal Node Root { get; }=root;
        internal string Kind(Node node)=>node.IsNull?"":Marshal.PtrToStringUTF8(api.nodeType(node))!;
        internal int Start(Node node)=>checked((int)api.start(node));internal int End(Node node)=>checked((int)api.end(node));
        internal int Line(Node node)=>checked((int)api.startPoint(node).Row+1);
        internal string Text(Node node)=>node.IsNull?"":Encoding.UTF8.GetString(data.AsSpan(Start(node),End(node)-Start(node)));
        internal string Slice(int start,int end)=>Encoding.UTF8.GetString(data.AsSpan(start,end-start));
        internal Node Field(Node node,string name)=>node.IsNull?default:api.field(node,name,checked((uint)Encoding.UTF8.GetByteCount(name)));
        internal bool Missing(Node node)=>!node.IsNull&&api.missing(node);internal bool HasError(Node node)=>!node.IsNull&&api.hasError(node);
        internal IEnumerable<Node> Children(Node node,bool named=true)
        {if(node.IsNull)yield break;var count=named?api.namedCount(node):api.childCount(node);for(uint i=0;i<count;i++)yield return named?api.namedChild(node,i):api.child(node,i);}
    }
    public void Dispose(){lock(sync){if(library!=IntPtr.Zero){NativeLibrary.Free(library);library=IntPtr.Zero;}}GC.SuppressFinalize(this);}
    ~JavaNativeParser(){Dispose();}
}
