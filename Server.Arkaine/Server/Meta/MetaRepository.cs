using Microsoft.EntityFrameworkCore;

namespace Server.Arkaine.Meta
{
    public class MetaRepository : IMetaRepository
    {
        private readonly ArkaineDbContext _context;
        public MetaRepository(ArkaineDbContext context)
        {
            _context = context;
        }

        public async Task SetRating(Rating rating)
        {
            // Update
            if (rating.Id > 0)
            {
                _context.Ratings.Update(rating);
            }
            // New
            else
            {
                await _context.AddAsync(rating);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IDictionary<string, Rating>> GetRatings(string bucket)
        {
            return await _context.Ratings.Where(r => r.Bucket == bucket).ToDictionaryAsync(r => r.FileName, r => r);
        }
    }
}
