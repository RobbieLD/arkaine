using Server.Arkaine.B2;

namespace Server.Arkaine.Ingest
{
    public abstract class BaseExtractor
    {
        protected readonly HttpClient _httpClient;

        public BaseExtractor(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
    }
}
