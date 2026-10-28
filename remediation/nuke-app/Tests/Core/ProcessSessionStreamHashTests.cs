using AndroidMcp.BuildTool.Core;
using Xunit;

namespace AndroidMcp.BuildTool.Tests.Core;

public sealed record ProcessStreamHashCase(string Id,ContentHashCase Vector,string Mode);
public static class ProcessStreamHashCases
{
    public static IEnumerable<object[]> Cases=>ContentHashCases.All.Select(x=>(ContentHashCase)x[0]).SelectMany(vector=>new[]{"seekable","nonseekable","chunk1","position"}.Select(mode=>new object[]{new ProcessStreamHashCase(vector.Id+"-"+mode,vector,mode)}));
    public static void AssertContract(ProcessStreamHashCase test,Func<Stream,string> provider)
    {
        using var stream=new ProcessHashFixtureStream(test.Vector.Bytes,test.Mode);var hash=provider(stream);
        Assert.Equal(test.Vector.Sha256,hash);Assert.Equal(test.Vector.Bytes.Length,stream.BytesRead);Assert.False(stream.WasDisposed);
        if(stream.CanSeek)Assert.Equal(stream.InitialPosition+test.Vector.Bytes.Length,stream.Position);
    }
    public static void AssertInvalid(bool missing,Func<Stream?,string> provider)
    {
        using var closed=new MemoryStream();closed.Dispose();var error=Record.Exception(()=>provider(missing?null:closed));
        Assert.NotNull(error);Assert.Equal(missing?typeof(ArgumentNullException):typeof(ArgumentException),error!.GetType());
    }
}
public sealed class ProcessHashFixtureStream:Stream
{
    private readonly byte[] data;private readonly int chunk;private long position;private readonly bool seekable;
    public int InitialPosition {get;}
    public int BytesRead {get;private set;}
    public bool WasDisposed {get;private set;}
    public ProcessHashFixtureStream(byte[] bytes,string mode)
    {
        InitialPosition=mode=="position"?2:0;data=InitialPosition>0?new byte[]{255,0}.Concat(bytes).ToArray():bytes.ToArray();position=InitialPosition;chunk=mode=="chunk1"?1:7;seekable=mode is "seekable" or "position";
    }
    public override bool CanRead=>!WasDisposed;public override bool CanSeek=>seekable;public override bool CanWrite=>false;
    public override long Length=>seekable?data.Length:throw new NotSupportedException();
    public override long Position {get=>seekable?position:throw new NotSupportedException();set{if(!seekable)throw new NotSupportedException();if(value<0||value>data.Length)throw new ArgumentOutOfRangeException(nameof(value));position=value;}}
    public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(WasDisposed,this);var length=Math.Min(Math.Min(buffer.Length,chunk),data.Length-checked((int)position));data.AsSpan(checked((int)position),length).CopyTo(buffer);position+=length;BytesRead+=length;return length;
    }
    public override long Seek(long offset,SeekOrigin origin){Position=(origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>position,SeekOrigin.End=>data.Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset;return position;}
    public override void Flush(){}public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    protected override void Dispose(bool disposing){WasDisposed=true;base.Dispose(disposing);}
}
[Trait("Gate","Mock"),Trait("Requirement","TEST-BUILD-003")]
public sealed class ProcessSessionStreamHashMockTests
{
    [Theory,InlineData(true),InlineData(false)]
    public void NullAndUnreadableStreamContractsRejectWrongSuccessAndException(bool missing)
    {
        ProcessStreamHashCases.AssertInvalid(missing,stream=>{if(stream is null)throw new ArgumentNullException(nameof(stream));throw new ArgumentException("Unreadable fixture stream");});
        Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertInvalid(missing,_=>new string('0',64))));
        Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertInvalid(missing,_=>throw new InvalidOperationException("wrong failure"))));
    }
    private static string Correct(ProcessStreamHashCase test,Stream stream)
    {
        using var received=new MemoryStream();stream.CopyTo(received);Assert.Equal(test.Vector.Bytes,received.ToArray());return test.Vector.Sha256;
    }
    [Theory,MemberData(nameof(ProcessStreamHashCases.Cases),MemberType=typeof(ProcessStreamHashCases))]
    public void CorrectMocksReadFromCurrentPositionWithoutSeekingOrDisposing(ProcessStreamHashCase test)=>ProcessStreamHashCases.AssertContract(test,stream=>Correct(test,stream));
    [Theory,MemberData(nameof(ProcessStreamHashCases.Cases),MemberType=typeof(ProcessStreamHashCases))]
    public void EveryDigestCharacterWrongCaseMissingReadsAndInputDisposalAreRejected(ProcessStreamHashCase test)
    {
        for(var i=0;i<test.Vector.Sha256.Length;i++)
        {
            var wrong=test.Vector.Sha256.ToCharArray();wrong[i]=wrong[i]=='0'?'1':'0';
            Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertContract(test,stream=>{Correct(test,stream);return new string(wrong);})));
        }
        Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertContract(test,stream=>Correct(test,stream).ToUpperInvariant())));
        Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertContract(test,stream=>{var value=Correct(test,stream);stream.Dispose();return value;})));
        if(test.Vector.Bytes.Length>0)Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertContract(test,_=>test.Vector.Sha256)));
        if(test.Mode=="position")Assert.NotNull(Record.Exception(()=>ProcessStreamHashCases.AssertContract(test,stream=>{stream.Position=0;return Correct(test,stream);}))); 
    }
}
