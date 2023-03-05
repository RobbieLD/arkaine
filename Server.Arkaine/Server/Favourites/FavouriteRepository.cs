using Microsoft.EntityFrameworkCore;
using Server.Arkaine.B2;

namespace Server.Arkaine.Favourites
{
    public class FavouriteRepository : IFavouriteRepository
    {
        private readonly ArkaineDbContext _context;
        public FavouriteRepository(ArkaineDbContext context)
        {
            _context = context;
        }

        public async Task Add(string name, string user)
        {
            _context.Favourites.Add(new Favourite
            {
                Name = name,
                UserName = user
            });

            await _context.SaveChangesAsync();
        }

        public async Task<HashSet<string>> All(string user)
        {
            return (await _context.Favourites.Where(f => f.UserName == user).Select(f => f.Name).ToListAsync()).ToHashSet();
        }

        public async Task<FilesResponse> Page(string user, string? start, int count)
        {
            var startId = (await _context.Favourites.FirstOrDefaultAsync(f => f.Name == start))?.Id;
            List<Favourite> favs;

            if (startId != null)
            {
                favs = await _context.Favourites.Where(f => f.Id > startId).Take(count).ToListAsync();
            } else
            {
                favs = await _context.Favourites.Take(count + 1).ToListAsync();
            }

            // TODO: Make favourite handle other types of media
            return new FilesResponse
            {
                Files = favs.Take(count).Select(f => new B2File
                {
                    IsFavoureite = true,
                    FileName = f.Name,
                    ContentType = "image",

                }).ToList(),
                NextFileName = favs.Count > count ? favs.Last().Name : string.Empty
            };
        }
    }
}
