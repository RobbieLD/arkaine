using Server.Arkaine.Favourites;

namespace Server.Arkaine.Tags
{
    public class TagService : ITagService
    {
        private readonly ITagRepository _repository;

        public TagService(ITagRepository repository)
        {
            _repository = repository;
        }

        public async Task AddTag(AddTagRequest request)
        {
            await _repository.Add(request.Name, request.FileName);
        }

        public async Task<IEnumerable<Tag>> GetTagsForFile(string fileName)
        {
            return await _repository.GetTags(fileName);
        }

        public async Task<IEnumerable<string>> GetFileNamesForTag(string name)
        {
            return await _repository.GetFiles(name);
        }
    }
}
