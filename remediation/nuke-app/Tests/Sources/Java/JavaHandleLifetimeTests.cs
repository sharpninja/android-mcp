using System.Reflection;
using System.Runtime.InteropServices;
using AndroidMcp.BuildTool.Sources.Java;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Sources.Java;

public static class JavaHandleContract
{
    public static void AssertLifetime(Func<IntPtr,Action<IntPtr>,SafeHandle> factory)
    {
        foreach(var pointer in new[]{IntPtr.Zero,new IntPtr(-1),new IntPtr(0x1234)})
        {
            var released=new List<IntPtr>();var handle=factory(pointer,released.Add);
            Assert.Equal(pointer,handle.DangerousGetHandle());Assert.Equal(pointer==IntPtr.Zero||pointer==new IntPtr(-1),handle.IsInvalid);
            Assert.Empty(released);handle.Dispose();handle.Dispose();Assert.True(handle.IsClosed);
            Assert.Equal(handle.IsInvalid?Array.Empty<IntPtr>():new[]{pointer},released);
        }
    }
}
internal sealed class MockJavaHandle:SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly Action<IntPtr> release;private readonly bool twice;
    public MockJavaHandle(IntPtr pointer,Action<IntPtr> release,bool twice=false):base(true){this.release=release;this.twice=twice;SetHandle(pointer);}
    protected override bool ReleaseHandle(){release(handle);if(twice)release(handle);return true;}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaHandleLifetimeMockTests
{
    [Fact]
    public void CorrectPointerAndLifetimeMocksAndIndependentControls()
    {
        JavaHandleContract.AssertLifetime((pointer,release)=>new MockJavaHandle(pointer,release));
        Assert.NotNull(Record.Exception(()=>JavaHandleContract.AssertLifetime((_,release)=>new MockJavaHandle(IntPtr.Zero,release))));
        Assert.NotNull(Record.Exception(()=>JavaHandleContract.AssertLifetime((pointer,release)=>new MockJavaHandle(pointer,release,true))));
        Assert.NotNull(Record.Exception(()=>JavaHandleContract.AssertLifetime((pointer,_)=>new MockJavaHandle(pointer,_=>{}))));
    }
}
[Trait("Gate","Real"),Trait("Requirement","TEST-BUILD-004")]
public sealed class JavaHandleLifetimeRealTests
{
    [Fact]
    public void IdenticalPointerAndLifetimeContractBindsToActualNativeHandleFactory()
    {
        JavaHandleContract.AssertLifetime((pointer,release)=>
        {
            var parser=typeof(JavaNativeParser);var handle=parser.GetNestedType("OwnedHandle",BindingFlags.NonPublic)!;var delete=parser.GetNestedType("Delete",BindingFlags.NonPublic)!;
            var callback=Delegate.CreateDelegate(delete,release.Target,release.Method);
            return (SafeHandle)handle.GetMethod("Create",BindingFlags.Public|BindingFlags.Static)!.Invoke(null,[pointer,callback])!;
        });
    }
}
