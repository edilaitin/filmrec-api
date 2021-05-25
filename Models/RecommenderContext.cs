using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Models
{
    public class RecommenderContext
    {
        public UserPick userPick { get; set; }
        public List<DisplayMedia> userMedias { get; set; }
    }
}
