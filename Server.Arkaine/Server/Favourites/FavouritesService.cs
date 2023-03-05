using Server.Arkaine.B2;

namespace Server.Arkaine.Favourites
{
    public class FavouritesService : IFavouritesService
    {
        private readonly IFavouriteRepository _repository;

        public FavouritesService(IFavouriteRepository repository)
        {
            _repository = repository;
        }

        public async Task AddFavourite(string fileName, string userName)
        {
            await _repository.Add(fileName, userName);
        }

        public async Task<IEnumerable<string>> GetAllFavourites(string user)
        {
            return await _repository.All(user);
        }

        public async Task<FilesResponse> GetAllFavouritesPage(string user, int pageSize, string? startFile)
        {
            return await _repository.Page(user, startFile, pageSize);
        }
    }
}
