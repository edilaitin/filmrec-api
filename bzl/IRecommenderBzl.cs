using FilmrecAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.bzl
{
    public interface IRecommenderBzl
    {
        public Task<TMDbMedia> recommendMedia();
    }
}
