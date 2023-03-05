using Server.Arkaine.B2;

namespace Server.Arkaine.Favourites
{
    public interface IFavouritesService
    {
        Task AddFavourite(string fileName, string userName);
        Task<IEnumerable<string>> GetAllFavourites(string user);
        Task<FilesResponse> GetAllFavouritesPage(string user, int pageSize, string? startFile);
    }
}