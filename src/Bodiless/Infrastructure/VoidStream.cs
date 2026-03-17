using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Bodiless.Infrastructure;

internal sealed class VoidStream(Stream baseStream) : Stream
{
    private readonly Stream baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));

    public override bool CanRead => baseStream.CanRead;

    public override bool CanSeek => baseStream.CanSeek;

    public override bool CanWrite => baseStream.CanWrite;

    public override long Length => baseStream.Length;

    public override long Position
    {
        get => baseStream.Position;
        set => baseStream.Position = value;
    }

    public override void Flush()
    {
        baseStream.Flush();
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return baseStream.FlushAsync(cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return baseStream.Read(buffer, offset, count);
    }

    public override int Read(Span<byte> buffer)
    {
        return baseStream.Read(buffer);
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return baseStream.ReadAsync(buffer, offset, count, cancellationToken);
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return baseStream.ReadAsync(buffer, cancellationToken);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return baseStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        baseStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
