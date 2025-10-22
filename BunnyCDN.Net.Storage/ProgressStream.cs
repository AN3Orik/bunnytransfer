using System;
using System.IO;
using System.Threading.Tasks;

namespace BunnyCDN.Net.Storage
{
    /// <summary>
    /// Stream wrapper that reports progress during read operations
    /// </summary>
    internal class ProgressStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _length;
        private readonly Action<long> _progressCallback;
        private long _totalBytesRead;

        public ProgressStream(Stream baseStream, Action<long> progressCallback)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _progressCallback = progressCallback ?? throw new ArgumentNullException(nameof(progressCallback));
            _length = baseStream.Length;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => false;
        public override long Length => _length;

        public override long Position
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = _baseStream.Read(buffer, offset, count);
            if (bytesRead > 0)
            {
                _totalBytesRead += bytesRead;
                _progressCallback?.Invoke(_totalBytesRead);
            }
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            int bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            if (bytesRead > 0)
            {
                _totalBytesRead += bytesRead;
                _progressCallback?.Invoke(_totalBytesRead);
            }
            return bytesRead;
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
