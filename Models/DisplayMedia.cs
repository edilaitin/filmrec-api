using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Models
{
    public class DisplayMedia : TMDbMedia
    {
        public MediaData mediaData { get; set; }
    }
}
