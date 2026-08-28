using Microsoft.EntityFrameworkCore;

namespace Server.Arkaine.Admin
{
    public interface IMediaLibraryReferenceService
    {
        Task RenameFileReferencesAsync(string sourceFile, string targetFile, CancellationToken cancellationToken);
    }

    public class MediaLibraryReferenceService : IMediaLibraryReferenceService
    {
        private readonly ArkaineDbContext _context;

        public MediaLibraryReferenceService(ArkaineDbContext context)
        {
            _context = context;
        }

        public async Task RenameFileReferencesAsync(string sourceFile, string targetFile, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(sourceFile) ||
                string.IsNullOrWhiteSpace(targetFile) ||
                string.Equals(sourceFile, targetFile, StringComparison.Ordinal))
            {
                return;
            }

            var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(cancellationToken)
                : null;

            try
            {
                var favourites = await _context.Favourites
                    .Where(favourite => favourite.Name == sourceFile || favourite.Name == targetFile)
                    .OrderBy(favourite => favourite.Id)
                    .ToListAsync(cancellationToken);

                foreach (var favourite in favourites.Where(favourite => favourite.Name == sourceFile))
                {
                    favourite.Name = targetFile;
                }

                foreach (var duplicate in favourites
                    .GroupBy(favourite => new { favourite.UserName, favourite.Name })
                    .SelectMany(group => group.Skip(1))
                    .ToList())
                {
                    _context.Favourites.Remove(duplicate);
                }

                var tags = await _context.Tags
                    .Where(tag => tag.FileName == sourceFile || tag.FileName == targetFile)
                    .OrderBy(tag => tag.Id)
                    .ToListAsync(cancellationToken);

                foreach (var tag in tags.Where(tag => tag.FileName == sourceFile))
                {
                    tag.FileName = targetFile;
                }

                foreach (var duplicate in tags
                    .GroupBy(tag => new { tag.FileName, tag.Name, tag.Timestamp })
                    .SelectMany(group => group.Skip(1))
                    .ToList())
                {
                    _context.Tags.Remove(duplicate);
                }

                await _context.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
    }
}
