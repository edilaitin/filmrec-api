using FilmrecAPI.Models;
using Nancy.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FilmrecAPI.bzl
{
    public class RecommenderBzl : IRecommenderBzl
    {
        private const string baseUrl = "https://api.themoviedb.org/3";
        private const string baseImageUrl = "https://image.tmdb.org/t/p";
        private const string baseTMDbUrl = "https://www.themoviedb.org";
        private const string API_KEY = "59011b4c0a38b967321110cf98cf6cf5";
        private const string language = "en - US";

        public async Task<RecommenderResult> recommendMedia(RecommenderContext recommenderContext)
        {
            if(recommenderContext.userPick.MediaType == Media.Film)
            {
                var moviesByUserPick = await getMoviesByUserPicks(recommenderContext);

                var tasks = new List<Task<List<RecMedia>>>();
                foreach (string similarMovies in recommenderContext.userPick.SimilarFilms)
                {
                    var movieId = await getIdByName(similarMovies, "movie");
                    var taskRecommend = getRecommendedMediaById(movieId, "movie", "recommendations");
                    var taskSimilar = getRecommendedMediaById(movieId, "movie", "similar");
                    tasks.Add(taskRecommend);
                    tasks.Add(taskSimilar);
                }

                var moviesBySimilar = (await Task.WhenAll(tasks)).SelectMany(x => x).Distinct().ToList();

                var moviesIntersection = moviesByUserPick.Intersect(moviesBySimilar).ToList();
                var finalMovies = moviesIntersection;
                if (finalMovies.Count == 0)
                {
                    finalMovies = moviesByUserPick.Union(moviesBySimilar).ToList();
                }

                var alreadySeenMoviesIds = recommenderContext.userMedias
                    .FindAll(media => media.mediaData.type == "movie")
                    .Select(media => media.mediaData.mediaId)
                    .ToList();

                var filteredMovies = finalMovies
                    .FindAll(movie => !alreadySeenMoviesIds.Contains(movie.id));

                return new RecommenderResult()
                {
                    results = filteredMovies
                };
            }
            else
            {
                var result = new List<RecMedia>();
                result.Add(new RecMedia()
                {
                    id = "13",
                    averageRating = 8.5,
                    imageSource = "https://image.tmdb.org/t/p/w780/h5J4W4veyxMXDMjeNxZI46TsHOb.jpg",
                    tagline = "Life is like a box of chocolates...you never know what you're gonna get.",
                    title = "Forrest Gump",
                    type = "movie"
                });
                return new RecommenderResult()
                {
                    results = result
                };
            }
        }

        private async Task<List<RecMedia>> getMoviesByUserPicks(RecommenderContext recommenderContext)
        {
            int maxNrPages = 20;
            var (people, genres) = await getPeopleAndGenres(recommenderContext);
            var (runTimeGte, runtimeLte) = durationsToIntPair(recommenderContext.userPick.Durations);
            
            var tasks = new List<Task<List<RecMedia>>>();
            var client = new HttpClient();
            
            for(int page = 1; page <= maxNrPages; page++)
            {
                string urlString = string.Format(baseUrl + "/discover/movie?api_key={0}&language={1}&with_people={2}&with_genres={3}&with_runtime.gte={4}&with_runtime.lte={5}&sort_by=popularity.desc&page={6}",
                                                                            API_KEY, language, people, genres, runTimeGte, runtimeLte, page);
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(urlString),
                    Method = HttpMethod.Get,
                };
                var task = client.SendAsync(request).ContinueWith(result =>
                {
                    return processMediaResponseMessage(result, "movie", 5);
                }).Unwrap();
                tasks.Add(task);
            }
            return (await Task.WhenAll(tasks)).SelectMany(x => x).Distinct().ToList();
        }

        private async Task<Tuple<string, string>> getPeopleAndGenres(RecommenderContext recommenderContext)
        {
            string people = "";
            string genres = "";

            foreach (string actorName in recommenderContext.userPick.Actors)
            {
                people = people + await getIdByName(actorName, "person") + ",";
            }
            foreach (string directorName in recommenderContext.userPick.Directors)
            {
                people = people + await getIdByName(directorName, "person") + ",";
            }
            foreach (string genre in recommenderContext.userPick.Genres)
            {
                genres = genres + mapGenreToTMDbId(genre, "movie") + ",";
            }
            return new Tuple<string, string>(people, genres);
        }

        private Tuple<int, int> durationsToIntPair(List<string> durations)
        {
            int runTimeGte = 0;
            int runtimeLte = 999;
            foreach (string duration in durations)
            {
                if (duration == "1h - 1h30")
                {
                    if (runtimeLte < 90 || runtimeLte == 999) runtimeLte = 90;
                    if (runTimeGte > 60 || runTimeGte == 0) runTimeGte = 60;
                }
                if (duration == "1h30 - 2h")
                {
                    if (runtimeLte < 120 || runtimeLte == 999) runtimeLte = 120;
                    if (runTimeGte > 90 || runTimeGte == 0) runTimeGte = 90;
                }
                if (duration == "2h - 2h30")
                {
                    if (runtimeLte < 150 || runtimeLte == 999) runtimeLte = 150;
                    if (runTimeGte > 120 || runTimeGte == 0) runTimeGte = 120;
                }
                if (duration == "2h30+")
                {
                    if (runTimeGte == 0) runTimeGte = 150;
                }
            }
            return new Tuple<int, int>(runTimeGte, runtimeLte);
        }

        private string mapGenreToTMDbId(string genre, string type)
        {
            string result;
            switch(genre)
            {
                case "Drama":       result = type == "tv" ? "18"    : "18";     break;
                case "Comedy":      result = type == "tv" ? "35"    : "35";     break;
                case "Romantic":    result = type == "tv" ? "10766" : "10749";  break;
                case "Documentary": result = type == "tv" ? "99"    : "99";     break;
                case "Action":      result = type == "tv" ? "10759" : "28";     break;
                case "Horror":      result = type == "tv" ? "9648"  : "27";     break;
                case "Thriller":    result = type == "tv" ? "80"    : "53";     break;
                case "Fantasy":     result = type == "tv" ? "10765" : "14";     break;

                default: result = ""; break;
            }
            return result; 
        }

        private Task<string> getIdByName(string name, string type)
        {
            var client = new HttpClient();
            string urlString = string.Format(baseUrl + "/search/{0}?api_key={1}&language={2}&query={3}", type, API_KEY, language, name);

            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(urlString),
                Method = HttpMethod.Get,
            };

            return client.SendAsync(request).ContinueWith(async result =>
            {
                var response = result.Result;

                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string content = await response.Content.ReadAsStringAsync();

                    JavaScriptSerializer js = new JavaScriptSerializer();
                    dynamic resultJSON = js.DeserializeObject(content);

                    var mediaJSON = resultJSON["results"][0];
                    return ((int) mediaJSON["id"]).ToString();
                }
                else
                {
                    return "invalid";
                }
            }).Unwrap();
        }

        private async Task<List<RecMedia>> getRecommendedMediaById(string id, string type, string apiType, int score = 0)
        {
            int maxNrPages = 10;

            var tasks = new List<Task<List<RecMedia>>>();
            var client = new HttpClient();

            for (int page = 1; page <= maxNrPages; page++)
            {
                string urlString = string.Format(baseUrl + "/{0}/{1}/{2}?api_key={3}&language={4}&page={5}", type, id, apiType, API_KEY, language, page);
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(urlString),
                    Method = HttpMethod.Get,
                };
                var task = client.SendAsync(request).ContinueWith(result =>
                {
                    return processMediaResponseMessage(result, type, score);
                }).Unwrap();
                tasks.Add(task);
            }
            return (await Task.WhenAll(tasks)).SelectMany(x => x).Distinct().ToList();
        }

        private async Task<List<RecMedia>> processMediaResponseMessage(Task<HttpResponseMessage> result, string type, int score)
        {
            var response = result.Result;

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string content = await response.Content.ReadAsStringAsync();

                JavaScriptSerializer js = new JavaScriptSerializer();
                dynamic resultJSON = js.DeserializeObject(content);

                var mediaJSONs = resultJSON["results"];
                var listResult = new List<RecMedia>();

                foreach (dynamic mediaJSON in mediaJSONs)
                {
                    if (mediaJSON["poster_path"] != null)
                    {
                        var recMedia = new RecMedia
                        {
                            id = mediaJSON["id"].ToString(),
                            title = type == "tv" ? mediaJSON["name"] : mediaJSON["title"],
                            tagline = "No Tagline",
                            averageRating = mediaJSON["vote_average"],
                            imageSource = string.Format(baseImageUrl + "/w780/{0}", mediaJSON["poster_path"]),
                            type = type,
                            score = score
                        };
                        listResult.Add(recMedia);
                    }
                }
                return listResult;
            }
            else
            {
                return new List<RecMedia>();
            }
        }
    }
}
