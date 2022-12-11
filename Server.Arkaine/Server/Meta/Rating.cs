using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Arkaine.Meta
{
    [Index(nameof(Bucket), IsUnique = false)]
    public class Rating
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int Value { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
    }
}
