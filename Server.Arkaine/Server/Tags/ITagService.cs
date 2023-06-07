namespace Server.Arkaine.Tags
{
    public interface ITagService
    {
        Task AddTag(AddTagRequest request);
        Task<IEnumerable<string>> GetFileNamesForTag(string name);
        Task<IEnumerable<Tag>> GetTagsForFile(string fileName);
    }
}