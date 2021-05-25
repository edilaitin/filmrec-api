using FilmrecAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.bzl
{
    public class RecommenderBzl : IRecommenderBzl
    {
        public Task<TMDbMedia> recommendMedia()
        {
            var result = new TMDbMedia()
            {
                id = "13",
                averageRating = 8.5,
                imageSource = "https://image.tmdb.org/t/p/w500/h5J4W4veyxMXDMjeNxZI46TsHOb.jpg",
                tagline = "Life is like a box of chocolates...you never know what you're gonna get.",
                title = "TITLE"
            };
            return Task.FromResult(result);
        }
    }
}
