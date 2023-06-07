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

        public async Task Add(string name, string fileName)
        {
            _context.Tags.Add(new Tag
            {
                Name = name,
                FileName = fileName
            });

            await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<Tag>> GetTags(string fileName)
        {
            return await _context.Tags.Where(t => t.FileName.Equals(fileName)).ToListAsync();
        }

        public async Task<IEnumerable<string>> GetFiles(string name)
        {
            return await _context.Tags.Where(t => t.Name.Equals(name)).Select(t => t.Name).ToListAsync();
        }
    }
}
