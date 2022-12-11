namespace Server.Arkaine.Meta
{
    public interface IMetaRepository
    {
        Task SetRating(Rating rating);
        Task<IDictionary<string, Rating>> GetRatings(string bucket);
    }
}