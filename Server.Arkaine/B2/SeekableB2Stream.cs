namespace Server.Arkaine.B2
{
    public class SeekableB2Stream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => _position;
            set => Seek(value, value >= 0 ? SeekOrigin.Begin : SeekOrigin.End);
        }

        public override void Flush() => throw new NotImplementedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newpos = _position;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newpos = offset;
                    break;
                case SeekOrigin.Current:
                    newpos += offset;
                    break;
                case SeekOrigin.End:
                    newpos = _length - Math.Abs(offset);
                    break;
            }
            if (newpos < 0 || newpos > _length)
                throw new InvalidOperationException("Stream position is invalid.");
            return _position = newpos;
        }

        public override void SetLength(long value) => throw new NotImplementedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();

        protected override void Dispose(bool disposing) => base.Dispose(disposing);

        private long _position;
        private long _length;
        private readonly IHttpClientFactory _httpClientFactory;

        public SeekableB2Stream(IHttpClientFactory httpClientFactory, string token, string url)
        {
            // https://github.com/mlhpdx/seekable-s3-stream/blob/master/SeekableS3Stream/SeekableS3Stream.cs
            _httpClientFactory = httpClientFactory;
        }
    }
}
