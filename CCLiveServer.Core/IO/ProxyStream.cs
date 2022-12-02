namespace CCLiveServer.Core.IO;

public sealed class ProxyStream : Stream
{
    private readonly Stream _stream;
    private long _position;

    public ProxyStream(Stream stream)
    {
        _stream = stream;
    }

    public override bool CanRead => _stream?.CanRead ?? true;

    public override bool CanSeek => _stream?.CanSeek ?? true;

    public override bool CanWrite => _stream?.CanWrite ?? true;

    public override long Length => _stream?.Length ?? _position;

    public override long Position
    {
        get => _position;
        set => _stream.Seek(value, SeekOrigin.Current);
    }

    public override void Flush()
    {
        _stream?.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var result = _stream?.Read(buffer, offset, count) ?? count;
        _position += count;

        return result;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (_stream == null)
            throw new NotSupportedException();

        return _position = _stream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _stream?.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _stream?.Write(buffer, offset, count);
        _position += count;
    }

    protected override void Dispose(bool disposing)
    {
        _stream?.Dispose();
    }
}
