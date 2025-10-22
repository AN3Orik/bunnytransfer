using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BunnyCDN.Net.Storage
{
    /// <summary>
    /// HttpContent wrapper that reports upload progress
    /// </summary>
    internal class ProgressHttpContent : HttpContent
    {
        private readonly Stream _content;
        private readonly int _bufferSize;
        private readonly Action<long> _progressCallback;

        public ProgressHttpContent(Stream content, Action<long>? progressCallback, int bufferSize = 64 * 1024)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _progressCallback = progressCallback ?? (_ => { });
            _bufferSize = bufferSize;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await _content.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                await stream.FlushAsync();
                
                totalBytesRead += bytesRead;
                _progressCallback?.Invoke(totalBytesRead);
            }
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
