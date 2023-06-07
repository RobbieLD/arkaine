namespace Server.Arkaine.Tags
{
    public interface ITagRepository
    {
        Task Add(string name, string fileName);
        Task<IEnumerable<string>> GetFiles(string name);
        Task<IEnumerable<Tag>> GetTags(string fileName);
    }
}