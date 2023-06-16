namespace Server.Arkaine.Tags
{
    public class AddTagRequest
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public int TimeStamp { get; set; }
    }
}
