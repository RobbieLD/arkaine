using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Server.Arkaine.Favourites
{
    [Index(nameof(UserName), IsUnique = false)]
    [Index(nameof(Name), IsUnique = false)]
    public class Favourite
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
    }
}
