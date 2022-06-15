namespace Server.Arkaine.B2
{
    public static class HttpExtensions
    {
        public static async Task<SeekableB2Stream> GetSeekableStreamAsync(this HttpClient client, string url, CancellationToken cancellationToken)
        {
            var stream = new SeekableB2Stream(client, cancellationToken);
            await stream.Open(url);
            return stream;
        }
    }
}
