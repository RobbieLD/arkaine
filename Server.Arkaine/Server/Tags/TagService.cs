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

        public async Task<IEnumerable<Tag>> AddTag(AddTagRequest request)
        {
            await _repository.Add(request.Name, request.FileName, request.TimeStamp);
            return await _repository.GetTags(request.FileName);
        }

        public async Task<IDictionary<string, IEnumerable<Tag>>> GetTagsForFile(IEnumerable<string> files)
        {
            return await _repository.GetTags(files);
        }

        public async Task<IEnumerable<string>> GetFileNamesForTag(string name)
        {
            return await _repository.GetFiles(name);
        }

        public async Task<IEnumerable<Tag>> DeleteTag(int id)
        {
            var fileName = await _repository.Delete(id);
            return await _repository.GetTags(fileName);
        }
    }
}
