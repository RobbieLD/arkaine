namespace Server.Arkaine.Tags
{
    public interface ITagService
    {
        Task AddTag(AddTagRequest request);
        Task<IEnumerable<string>> GetFileNamesForTag(string name);
        Task<IDictionary<string, IEnumerable<string>>> GetTagsForFile(IEnumerable<string> files);
    }
}