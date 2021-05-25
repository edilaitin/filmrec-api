using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Models
{
    public class MediaData
    {
        public string mediaId { get; set; }
        public bool liked { get; set; }
        public bool disliked { get; set; }
        public string type { get; set; }
        public bool viewed { get; set; }
    }
}
