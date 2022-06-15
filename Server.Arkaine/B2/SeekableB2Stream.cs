namespace Server.Arkaine.B2
{
    public class SeekableB2Stream : Stream
    {
        public string ContentType => _contentType;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _contentLength;
        public override long Position
        {
            get
            {
                return _stream?.Position ?? 0;
            }
            set
            {
                if (_stream == null)
                {
                    throw new InvalidOperationException("Stream not open");
                }

                _stream.Position = value;
            }
        }

        public override void Flush() => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_stream == null)
            {
                throw new InvalidOperationException("Stream not open");
            }

            return _stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            // We can ignore the initial seek
            if (origin == SeekOrigin.Begin && offset == 0)
            {
                return 0;
            }

            // Not sure if there is a way to make this async?
            return HttpSeek(offset).GetAwaiter().GetResult();
        }

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (_stream != null)
            {
                _stream.Dispose();
            }

            base.Dispose(disposing);
        }

        private readonly HttpClient _client;
        private readonly CancellationToken _cancellationToken;
        private Stream? _stream;
        private string _contentType = string.Empty;
        private string _url = string.Empty;
        private long _contentLength;

        public SeekableB2Stream(HttpClient client, CancellationToken cancellationToken)
        {
            _client = client;
            _cancellationToken = cancellationToken;
        }

        public async Task Open(string url)
        {
            var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _cancellationToken);
            _contentLength = response.Content.Headers.ContentLength ?? 0;
            _contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            _stream = await response.Content.ReadAsStreamAsync(_cancellationToken);
            _url = url;
        }

        private async Task<long> HttpSeek(long offset)
        {
            _client.DefaultRequestHeaders.Clear();
            _client.DefaultRequestHeaders.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, null);
            _stream = await _client.GetStreamAsync(_url, _cancellationToken);
            return offset;
        }
    }
}
