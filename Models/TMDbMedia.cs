using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Models
{
    public class TMDbMedia
    {
        public string id { get; set; }
        public string title { get; set; }
        public string imageSource { get; set; }
        public double averageRating { get; set; }
        public string tagline { get; set; }
    }
}
