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
            await _repository.Add(request.Name, request.FileName, request.TimeStamp);
        }

        public async Task<IDictionary<string, IEnumerable<Tag>>> GetTagsForFile(IEnumerable<string> files)
        {
            return await _repository.GetTags(files);
        }

        public async Task<IEnumerable<string>> GetFileNamesForTag(string name)
        {
            return await _repository.GetFiles(name);
        }
    }
}
