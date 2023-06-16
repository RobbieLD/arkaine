namespace Server.Arkaine.Tags
{
    public interface ITagRepository
    {
        Task Add(string name, string fileName, int timeStamp);
        Task<IEnumerable<string>> GetFiles(string name);
        Task<IDictionary<string, IEnumerable<Tag>>> GetTags(IEnumerable<string> files);
    }
}