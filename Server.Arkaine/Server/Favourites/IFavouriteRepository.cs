using Server.Arkaine.B2;

namespace Server.Arkaine.Favourites
{
    public interface IFavouriteRepository
    {
        Task Add(string name, string user);
        Task<HashSet<string>> All(string user);
        Task<FilesResponse> Page(string user, string? start, int count);
    }
}