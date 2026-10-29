using AndroidMcp.BuildTool.Core;
using AndroidMcp.BuildTool.Sources;
using AndroidMcp.BuildTool.Sources.Java;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

internal sealed record JavaProducerIdentityCase(string Id,bool Accepted);
internal sealed record JavaProducerIdentityFixture(JavaRuntimeAdmissionFixture Input,JavaSourceProducerIdentity Expected,SourceJavaAdmission Admission);
internal sealed record JavaProducerIdentityRig(IJavaSourceRuntimeLoader Loader,Func<int> StrictInspectionCalls);
internal static class JavaProducerIdentityCases
{
    internal static readonly IReadOnlyList<JavaProducerIdentityCase> All=[
        new("identity-unpinned",true),new("identity-pinned",true),new("identity-extra-metadata",true),new("identity-relative",true),new("identity-unicode",true),
        new("pin-hash-mismatch",false),new("pin-length-mismatch",false),new("pin-uppercase",false),new("pin-short",false),new("pin-negative-length",false),
        new("pin-hash-only",false),new("pin-length-only",false),new("pin-zero-length",false),new("pin-replaced-same-length",false),new("pin-replaced-length",false)];
    public static IEnumerable<object[]> Cases=>All.Select(x=>new object[]{x.Id});
    internal static JavaProducerIdentityCase Get(string id)=>All.Single(x=>x.Id==id);
    internal static JavaProducerIdentityFixture Create(JavaProducerIdentityCase test,byte[] nativeBytes,string fixtureParent)
    {
        var legacy=test.Id switch{"identity-extra-metadata"=>"extra-metadata","identity-relative"=>"relative-admission","identity-unicode"=>"unicode-asset-path",_=>"valid"};
        var input=JavaRuntimeAdmissionCases.Create(JavaRuntimeAdmissionCases.Get(legacy),nativeBytes,fixtureParent);
        var path=Path.GetFullPath(input.Admission.ManifestPath);var bytes=File.ReadAllBytes(path);
        var expected=new JavaSourceProducerIdentity(path,ContentHash.Sha256(bytes),bytes.LongLength,
            legacy=="unicode-asset-path"?"native/lib java 雪.so":"native/parser.so",ContentHash.Sha256(nativeBytes),nativeBytes.LongLength,
            "linux-x64",14,"0.25.2","6e0618704ad758ba2ea5822faa80bcd36fbeba3d","695d1530e59804279d9c739335b73b882d9f1caf","0.23.5","94703d5a6bed02b98e438d7cad1136c01a60ba2c");
        string? hash=test.Id=="identity-unpinned"?null:expected.ManifestSha256;
        long? length=test.Id=="identity-unpinned"?null:expected.ManifestLength;
        switch(test.Id)
        {
            case "pin-hash-mismatch":hash=new string('0',64);break;
            case "pin-length-mismatch":length++;break;
            case "pin-uppercase":hash=hash!.ToUpperInvariant();break;
            case "pin-short":hash="abc";break;
            case "pin-negative-length":length=-1;break;
            case "pin-hash-only":length=null;break;
            case "pin-length-only":hash=null;break;
            case "pin-zero-length":length=0;break;
            case "pin-replaced-same-length":var replacement=bytes.ToArray();replacement[0]=(byte)'[';File.WriteAllBytes(path,replacement);break;
            case "pin-replaced-length":File.AppendAllText(path," ");break;
        }
        return new(input,expected,new(input.Admission.ManifestPath,hash,length));
    }
    internal static void AssertContract(JavaProducerIdentityCase test,JavaProducerIdentityFixture fixture,JavaProducerIdentityRig rig)
    {
        var before=Directory.EnumerateFiles(fixture.Input.Root,"*",SearchOption.AllDirectories).ToDictionary(x=>x,x=>ContentHash.Sha256(File.ReadAllBytes(x)),StringComparer.Ordinal);
        var original=fixture.Admission;IJavaSourceRuntime? opened=null;
        try
        {
            var error=Record.Exception(()=>opened=rig.Loader.Open(fixture.Admission));
            if(!test.Accepted){Assert.IsType<InvalidDataException>(error);Assert.Null(opened);Assert.Equal(0,rig.StrictInspectionCalls());}
            else
            {
                Assert.Null(error);var runtime=Assert.IsAssignableFrom<IJavaSourceProducerIdentityRuntime>(opened);
                Assert.Equal(1,rig.StrictInspectionCalls());Assert.Equal(fixture.Expected,runtime.Identity);Assert.Same(runtime.Identity,runtime.Identity);
                JavaContractCases.AssertContract(JavaContractCases.Get("JAVA-07"),runtime.Extractor);
                runtime.Dispose();Assert.Throws<ObjectDisposedException>(()=>_=runtime.Identity);Assert.Throws<ObjectDisposedException>(()=>_=runtime.Extractor);runtime.Dispose();
            }
            Assert.Equal(original,fixture.Admission);
            Assert.Equal(before.Keys.Order(StringComparer.Ordinal),Directory.EnumerateFiles(fixture.Input.Root,"*",SearchOption.AllDirectories).Order(StringComparer.Ordinal));
            foreach(var file in before)Assert.Equal(file.Value,ContentHash.Sha256(File.ReadAllBytes(file.Key)));
        }
        finally{opened?.Dispose();}
    }
    internal static JavaSourceProducerIdentity Wrong(JavaSourceProducerIdentity value,int field)=>field switch
    {
        0=>value with{ManifestPath=value.ManifestPath+"wrong"},1=>value with{ManifestSha256=new string('0',64)},2=>value with{ManifestLength=value.ManifestLength+1},
        3=>value with{NativeRelativePath="wrong.so"},4=>value with{NativeSha256=new string('0',64)},5=>value with{NativeLength=value.NativeLength+1},
        6=>value with{Rid="wrong-rid"},7=>value with{LanguageAbi=15},8=>value with{RuntimeVersion="wrong"},9=>value with{RuntimeCommit="wrong"},
        10=>value with{RuntimeTagObject="wrong"},11=>value with{GrammarVersion="wrong"},12=>value with{GrammarCommit="wrong"},_=>value
    };
}
