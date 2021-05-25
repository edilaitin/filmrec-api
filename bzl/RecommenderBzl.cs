using FilmrecAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.bzl
{
    public class RecommenderBzl : IRecommenderBzl
    {
        public Task<RecommenderResult> recommendMedia()
        {
            var list = new List<RecMedia>();
            list.Add(new RecMedia()
            {
                id = "13",
                averageRating = 8.5,
                imageSource = "https://image.tmdb.org/t/p/w780/h5J4W4veyxMXDMjeNxZI46TsHOb.jpg",
                tagline = "Life is like a box of chocolates...you never know what you're gonna get.",
                title = "Forrest Gump",
                type = "movie"
            });

            var result = new RecommenderResult()
            {
                results = list
            }; 
            return Task.FromResult(result);
        }
    }
}
