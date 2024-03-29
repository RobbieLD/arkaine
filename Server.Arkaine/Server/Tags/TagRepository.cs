﻿using Microsoft.EntityFrameworkCore;

namespace Server.Arkaine.Tags
{
    public class TagRepository : ITagRepository
    {
        private readonly ArkaineDbContext _context;
        public TagRepository(ArkaineDbContext context)
        {
            _context = context;
        }

        public async Task Add(string name, string fileName, int timeStamp)
        {
            _context.Tags.Add(new Tag
            {
                Name = name,
                FileName = fileName,
                Timestamp = timeStamp
            });

            await _context.SaveChangesAsync();
        }

        public async Task<IDictionary<string, IEnumerable<Tag>>> GetTags(IEnumerable<string> files)
        {
            return await _context.Tags
                .Where(t => files.Contains(t.FileName))
                .GroupBy(t => t.FileName)
                .ToDictionaryAsync(t => t.Key, k => k.Select(t => t));
        }

        public async Task<IEnumerable<string>> GetFiles(string name)
        {
            return await _context.Tags.Where(t => t.Name.Equals(name)).Select(t => t.Name).ToListAsync();
        }

        public async Task<string> Delete(int id)
        {
            var tag = await _context.Tags.FirstAsync(t => t.Id == id);
            var fileName = tag.FileName;
            _context.Tags.Remove(tag);
            await _context.SaveChangesAsync();

            return fileName;
            
        }

        public async Task<IEnumerable<Tag>> GetTags(string fileName)
        {
            return await _context.Tags.Where(t => t.FileName == fileName).ToListAsync();
        }
    }
}
