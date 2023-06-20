namespace Server.Arkaine.Tags
{
    public interface ITagService
    {
        Task<IEnumerable<Tag>> AddTag(AddTagRequest request);
        Task<IEnumerable<string>> GetFileNamesForTag(string name);
        Task<IDictionary<string, IEnumerable<Tag>>> GetTagsForFile(IEnumerable<string> files);
        Task<IEnumerable<Tag>> DeleteTag(int id);
    }
}