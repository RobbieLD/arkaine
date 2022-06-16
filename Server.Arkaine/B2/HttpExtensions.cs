namespace Server.Arkaine.B2
{
    public static class HttpExtensions
    {
        public static async Task<SeekableB2Stream> GetSeekableStreamAsync(this HttpClient client, string token, string url, CancellationToken cancellationToken)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
            var stream = new SeekableB2Stream(client, cancellationToken);
            await stream.Open(url);
            return stream;
        }
    }
}
