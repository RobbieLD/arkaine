namespace Server.Arkaine.Admin
{
    public class GenerationReport
    {
        public int Generated { get; set; }
        public int Failed { get; set; }
        public int Scanned { get; set; }
        public bool Finished { get; set; }
    }

}
