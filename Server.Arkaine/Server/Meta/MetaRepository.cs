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
            var result = await _context.Ratings.Where(r => r.FileName == rating.FileName && r.Bucket == rating.Bucket).FirstOrDefaultAsync();

            // Update
            if (result != null)
            {
                result.Value = rating.Value;
                _context.Ratings.Update(result);
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
