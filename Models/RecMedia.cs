using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Models
{
    public class RecMedia: TMDbMedia
    {
        public string type { get; set; }
        public int score { get; set; }
    }
}
