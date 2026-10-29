using System.Text;
using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ContentHashCase(string Id,byte[] Bytes,string Sha256,string AidlSha1,string GitBlobSha1);
public static class ContentHashCases
{
    // Git vectors independently checked with git hash-object over the exact raw
    // fixture bytes. Git framing uses byte length, not UTF16 character count.
    public static IEnumerable<object[]> All=>new[]
    {
        new ContentHashCase("HASH-empty",[],"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855","da39a3ee5e6b4b0d3255bfef95601890afd80709","e69de29bb2d1d6434b8b29ae775ad8c2e48c5391"),
        new ContentHashCase("HASH-abc",Encoding.UTF8.GetBytes("abc"),"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","a9993e364706816aba3e25717850c26c9cd0d89d","f2ba8f84ab5c1bce84a7b441cb1959cfc7093b7f"),
        new ContentHashCase("HASH-binary",[0,1,2,127,128,255],"da2cb6ad175bc966de5e79c6e16777f8a98b610c2424a894132df2815be50677","9782e2ee27939aacd5486e263ab51ab07de1ffec","ac0deae0c6de979e3136dcc6bdb1d07c58d37107"),
        new ContentHashCase("HASH-utf8-byte-length",Encoding.UTF8.GetBytes("é"),"4a99557e4033c3539de2eb65472017cad5f9557f7a0625a09f1c3f6e2ba69c4c","bf15be717ac1b080b4f1c456692825891ff5073d","4b04fff51468d8ab5201ab02b725dc477bc7cb45")
    }.Select(x=>new object[]{x});
    public static void AssertCase(ContentHashCase test,IContentHash provider)
    {
        Assert.Equal(test.Sha256,provider.Sha256(test.Bytes));
        Assert.Equal(test.AidlSha1,provider.AidlSha1Legacy(test.Bytes));
        Assert.Equal(test.GitBlobSha1,provider.GitBlobSha1Legacy(test.Bytes));
    }
}
public sealed class ScriptedContentHash(ContentHashCase expected,string? sha256=null,string? aidl=null,string? git=null):IContentHash
{
    public int Calls { get; private set; }
    private void Input(ReadOnlySpan<byte> bytes) { Assert.Equal(expected.Bytes,bytes.ToArray());Calls++; }
    public string Sha256(ReadOnlySpan<byte> bytes) { Input(bytes);return sha256 ?? expected.Sha256; }
    public string AidlSha1Legacy(ReadOnlySpan<byte> bytes) { Input(bytes);return aidl ?? expected.AidlSha1; }
    public string GitBlobSha1Legacy(ReadOnlySpan<byte> payload) { Input(payload);return git ?? expected.GitBlobSha1; }
}
[Trait("Gate","Mock")]
public sealed class ContentHashMockTests
{
    [Theory,MemberData(nameof(ContentHashCases.All),MemberType=typeof(ContentHashCases))]
    public void CorrectMocksPass(ContentHashCase test)
    { var fake=new ScriptedContentHash(test);ContentHashCases.AssertCase(test,fake);Assert.Equal(3,fake.Calls); }
    [Theory,MemberData(nameof(ContentHashCases.All),MemberType=typeof(ContentHashCases))]
    public void EveryIndividualDigestCharacterMutationRejected(ContentHashCase test)
    {
        foreach(var (expected,kind) in new[]{(test.Sha256,0),(test.AidlSha1,1),(test.GitBlobSha1,2)})
        for(var i=0;i<expected.Length;i++)
        {
            var bytes=expected.ToCharArray();bytes[i]=bytes[i]=='0' ? '1' : '0';var wrong=new string(bytes);
            var fake=new ScriptedContentHash(test,kind==0 ? wrong : null,kind==1 ? wrong : null,kind==2 ? wrong : null);
            Assert.NotNull(Record.Exception(()=>ContentHashCases.AssertCase(test,fake)));
        }
    }
    [Theory,MemberData(nameof(ContentHashCases.All),MemberType=typeof(ContentHashCases))]
    public void WrongCaseLengthAndUnframedGitIdentityRejected(ContentHashCase test)
    {
        Assert.NotNull(Record.Exception(()=>ContentHashCases.AssertCase(test,new ScriptedContentHash(test,sha256:test.Sha256.ToUpperInvariant()))));
        Assert.NotNull(Record.Exception(()=>ContentHashCases.AssertCase(test,new ScriptedContentHash(test,aidl:test.AidlSha1[..^1]))));
        Assert.NotNull(Record.Exception(()=>ContentHashCases.AssertCase(test,new ScriptedContentHash(test,git:test.AidlSha1))));
    }
}

[Trait("Gate","Real")]
public sealed class ContentHashRealTests
{
    [Theory,MemberData(nameof(ContentHashCases.All),MemberType=typeof(ContentHashCases))]
    public void NativeHashesUseSameVectorsAndAssertions(ContentHashCase test)=>ContentHashCases.AssertCase(test,new NativeContentHash());
}
