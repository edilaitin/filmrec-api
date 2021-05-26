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
        private const int MAX_NR_MEDIAS_RETURNED = 20;

        private const string baseUrl = "https://api.themoviedb.org/3";
        private const string baseImageUrl = "https://image.tmdb.org/t/p";
        private const string baseTMDbUrl = "https://www.themoviedb.org";
        private const string API_KEY = "59011b4c0a38b967321110cf98cf6cf5";
        private const string language = "en - US";

        // SCORE RECEIVED FOR EACH RESPECTED USER PICK

        // General
        private const int Genre_Present_Score    = 2;
        private const int Actor_Present_Score    = 5;
        private const int Director_Present_Score = 5;

        // TV Series
        private const int Still_Running_Score    = 2;
        private const int Similar_Series_Score   = 5;
        private const int Series_Duration_Score  = 2;
        private const int Season_Duration_Score  = 2;
        private const int Episode_Duration_Score = 2;

        // Movies
        private const int Similar_Movies_Score  = 5;
        private const int Movie_Duration_Score  = 2;

        public async Task<RecommenderResult> recommendMedia(RecommenderContext recommenderContext)
        {
            var medias = new List<RecMedia>();
            var similarMoviesIds = new List<string>();
            var similarTvsIds = new List<string>();
            var filteredMovies = new List<RecMedia>();
            var filteredTvSeries = new List<RecMedia>();

            if(recommenderContext.userPick.MediaType == Media.Film || recommenderContext.userPick.MediaType == Media.None)
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
                similarMoviesIds = moviesBySimilar
                    .Select(movie => movie.id)
                    .ToList();

                var finalMovies = moviesByUserPick.Union(moviesBySimilar).ToList();

                var alreadySeenMoviesIds = recommenderContext.userMedias
                    .FindAll(media => media.mediaData.type == "movie")
                    .Select(media => media.mediaData.mediaId)
                    .ToList();

                filteredMovies = finalMovies
                    .FindAll(movie => !alreadySeenMoviesIds.Contains(movie.id));
            }
            if(recommenderContext.userPick.MediaType == Media.Series || recommenderContext.userPick.MediaType == Media.None)
            {
                var tasksByPeople = new List<Task<List<RecMedia>>>();
                foreach (string actorName in recommenderContext.userPick.Actors)
                {
                    var actorId = await getIdByName(actorName, "person");
                    tasksByPeople.Add(getMediaWithPerson(actorId, "tv", "actor"));
                }
                foreach (string directorName in recommenderContext.userPick.Directors)
                {
                    var directorId = await getIdByName(directorName, "person");
                    tasksByPeople.Add(getMediaWithPerson(directorId, "tv", "director"));
                }
                var tvSeriesByPeople = (await Task.WhenAll(tasksByPeople)).SelectMany(x => x).Distinct().ToList();

                var tasksSimilar = new List<Task<List<RecMedia>>>();
                foreach (string similarSeries in recommenderContext.userPick.SimilarSeries)
                {
                    var seriesId = await getIdByName(similarSeries, "tv");
                    var taskRecommend = getRecommendedMediaById(seriesId, "tv", "recommendations");
                    var taskSimilar = getRecommendedMediaById(seriesId, "tv", "similar");
                    tasksSimilar.Add(taskRecommend);
                    tasksSimilar.Add(taskSimilar);
                }

                var tvSeriesBySimilar = (await Task.WhenAll(tasksSimilar)).SelectMany(x => x).Distinct().ToList();
                similarTvsIds = tvSeriesBySimilar
                    .Select(tv => tv.id)
                    .ToList();

                var tvSeriesPopular = await getPopularTvSeries();

                var finalTvSeries = tvSeriesByPeople.Union(tvSeriesPopular).ToList().Union(tvSeriesBySimilar).ToList();

                var alreadySeenTvsIds = recommenderContext.userMedias
                    .FindAll(media => media.mediaData.type == "tv")
                    .Select(media => media.mediaData.mediaId)
                    .ToList();

                filteredTvSeries = finalTvSeries
                    .FindAll(tv => !alreadySeenTvsIds.Contains(tv.id));                
            }
            
            medias = filteredMovies.Union(filteredTvSeries).ToList();
            var mediasWithCost = await applyScore(medias, recommenderContext, similarMoviesIds, similarTvsIds);
            return new RecommenderResult()
            {
                // order the medias descending by their score
                results = mediasWithCost.OrderByDescending(media => media.score).Take(MAX_NR_MEDIAS_RETURNED).ToList()
            };
        }

        private async Task<List<RecMedia>> getMoviesByUserPicks(RecommenderContext recommenderContext)
        {
            int maxNrPages = 100;
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
                    return processMediaResponseMessage(result, "movie", 0);
                }).Unwrap();
                tasks.Add(task);
            }
            return (await Task.WhenAll(tasks)).SelectMany(x => x).Distinct().ToList();
        }

        private async Task<List<RecMedia>> getPopularTvSeries()
        {
            int maxNrPages = 100;            
            var tasks = new List<Task<List<RecMedia>>>();
            var client = new HttpClient();
            
            for(int page = 1; page <= maxNrPages; page++)
            {
                string urlString = string.Format(baseUrl + "/tv/popular?api_key={0}&language={1}&page={2}", API_KEY, language, page);
                
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(urlString),
                    Method = HttpMethod.Get,
                };
                var task = client.SendAsync(request).ContinueWith(result =>
                {
                    return processMediaResponseMessage(result, "tv", 0);
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
                people = people + await getIdByName(actorName, "person") + "|";
            }
            foreach (string directorName in recommenderContext.userPick.Directors)
            {
                people = people + await getIdByName(directorName, "person") + "|";
            }
            foreach (string genre in recommenderContext.userPick.Genres)
            {
                genres = genres + mapGenreToTMDbId(genre, "movie") + "|";
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
                case "Drama":       result = type == "tv" ? "18"    : "18getRecommendedMediaById";     break;
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
            int maxNrPages = 100;

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

        private async Task<List<RecMedia>> getMediaWithPerson(string personId, string type, string role)
        {
          int maxNrPages = 100;

            var tasks = new List<Task<List<RecMedia>>>();
            var client = new HttpClient();

            for (int page = 1; page <= maxNrPages; page++)
            {
                string urlString = string.Format(baseUrl + "/person/{0}/{1}_credits?api_key={2}&language={3}&page={4}", personId, type, API_KEY, language, page);
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(urlString),
                    Method = HttpMethod.Get,
                };
                var task = client.SendAsync(request).ContinueWith(async result =>
                {
                    var response = result.Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();

                        JavaScriptSerializer js = new JavaScriptSerializer();
                        dynamic resultJSON = js.DeserializeObject(content);
                        
                        dynamic mediaJSONs;
                        if (role == "director")
                        {
                            mediaJSONs = resultJSON["crew"];
                        }
                        else {
                            mediaJSONs = resultJSON["cast"];
                        }
                        var listResult = new List<RecMedia>();
                        foreach (dynamic mediaJSON in mediaJSONs)
                        {
                            if (mediaJSON["poster_path"] != null)
                            {
                                var recMedia = getMediaFromJson(mediaJSON, type, 0);
                                listResult.Add(recMedia);
                            }
                        }
                        return listResult;
                    }
                    else
                    {
                        return new List<RecMedia>();
                    }
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
                        var recMedia = getMediaFromJson(mediaJSON, type, score);
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

        private RecMedia getMediaFromJson(dynamic jsonMedia, string type, int score)
        {
            var recMedia = new RecMedia
            {
                id = jsonMedia["id"].ToString(),
                title = type == "tv" ? jsonMedia["name"] : jsonMedia["title"],
                tagline = "No Tagline",
                averageRating = jsonMedia["vote_average"],
                imageSource = string.Format(baseImageUrl + "/w780/{0}", jsonMedia["poster_path"]),
                type = type,
                score = score
            };
            return recMedia;
        }

        private async Task<List<RecMedia>> applyScore(List<RecMedia> medias, RecommenderContext recommenderContext, List<string> similarMoviesIds, List<string> similarTVsIds)
        {
            foreach(RecMedia media in medias)
            {
                var client = new HttpClient();
                string urlStringDetails = string.Format(baseUrl + "/{0}/{1}?api_key={2}&language={3}", media.type, media.id, API_KEY, language);
                string urlStringCredits = string.Format(baseUrl + "/{0}/{1}/credits?api_key={2}&language={3}", media.type, media.id, API_KEY, language);

                var requestDetails = new HttpRequestMessage()
                {
                    RequestUri = new Uri(urlStringDetails),
                    Method = HttpMethod.Get,
                };
                var requestCredits = new HttpRequestMessage()
                {
                    RequestUri = new Uri(urlStringCredits),
                    Method = HttpMethod.Get,
                };

                dynamic mediaDetails = await client.SendAsync(requestDetails).ContinueWith(async result =>
                {
                    var response = result.Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        return js.DeserializeObject(content);
                    }
                    else { return "invalid";}
                }).Unwrap();
                
                dynamic mediaCredits = await client.SendAsync(requestCredits).ContinueWith(async result =>
                {
                    var response = result.Result;

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string content = await response.Content.ReadAsStringAsync();
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        return js.DeserializeObject(content);
                    }
                    else { return "invalid";}
                }).Unwrap();

                if (mediaDetails != "invalid" && mediaCredits != "invalid")
                {
                    applyRatingScore(media);
                    applyGenreScore(media, mediaDetails, recommenderContext);
                    applyActorPresentScore(media, mediaCredits, recommenderContext);
                    applyDirectorPresentScore(media, mediaCredits, recommenderContext);

                    if (media.type == "tv")
                    {
                        applyStillRunningScore(media, mediaDetails, recommenderContext);
                        applySimilarSeriesScore(media, similarTVsIds);
                        applySeriesDurationScore(media, mediaDetails, recommenderContext);
                        applySeasonDurationScore(media, mediaDetails, recommenderContext);
                        applyEpisodeDurationScore(media, mediaDetails, recommenderContext);
                    }
                    else if (media.type == "movie")
                    {
                        applySimilarMoviesScore(media, similarMoviesIds);
                        applyMovieDurationScore(media, mediaDetails, recommenderContext);
                    }
                }
            }
            return medias;
        }


        private void applyRatingScore(RecMedia media)
        {
            media.score = media.score + media.averageRating / 2;
        }
        
        private void applyGenreScore(RecMedia media, dynamic mediaDetails, RecommenderContext recommenderContext)
        {
            var mediaGenres = mediaDetails["genres"];
            foreach (string genre in recommenderContext.userPick.Genres)
            {
                var genreId = mapGenreToTMDbId(genre, "movie");
                foreach (string mediaGenre in mediaGenres)
                {
                    if (genre == mediaGenre)
                    {
                        media.score = media.score + Genre_Present_Score;
                    }
                }
            }
        }

        private void applyActorPresentScore(RecMedia media, dynamic mediaCredits, RecommenderContext recommenderContext)
        {
            var mediaCast = mediaCredits["cast"];
            foreach (string actorName in recommenderContext.userPick.Actors)
            {
                foreach (dynamic castMember in mediaCast)
                {
                    if (actorName == castMember["name"])
                    {
                        media.score = media.score + Actor_Present_Score;
                    }
                }
            }
        }

        private void applyDirectorPresentScore(RecMedia media, dynamic mediaCredits, RecommenderContext recommenderContext)
        {
            var mediaCrew = mediaCredits["crew"];
            foreach (string directorName in recommenderContext.userPick.Directors)
            {
                foreach (dynamic crewMember in mediaCrew)
                {
                    if (directorName == crewMember["name"])
                    {
                        media.score = media.score + Director_Present_Score;
                    }
                }
            }
        }

        // TV

        private void applyStillRunningScore(RecMedia media, dynamic mediaDetails, RecommenderContext recommenderContext)
        {
            var tvStatus = mediaDetails["status"];
            if (recommenderContext.userPick.StillRunning == Running.StillRunning && 
              (tvStatus == "Returning Series" || tvStatus == "In Production" || tvStatus == "Pilot"))
            {
                media.score = media.score + Still_Running_Score;
            }
            else if (recommenderContext.userPick.StillRunning == Running.NotRunning && 
              (tvStatus == "Canceled" || tvStatus == "Ended"))
            {
                media.score = media.score + Still_Running_Score;
            }
        }

        private void applySimilarSeriesScore(RecMedia media, List<string> similarTVsIds)
        {
            foreach (string similarTVId in similarTVsIds)
            {
                if (similarTVId == media.id)
                {
                    media.score = media.score + Similar_Series_Score;
                }
            }
        }

        private void applySeriesDurationScore(RecMedia media, dynamic mediaDetails, RecommenderContext recommenderContext)
        {
            var numberOfSeasons = mediaDetails["number_of_seasons"];

            foreach (string seriesDuration in recommenderContext.userPick.SeriesDuration)
            {
                if (
                  (seriesDuration == "1" && numberOfSeasons == 1) ||
                  (seriesDuration == "1 - 3" && numberOfSeasons >= 1 && numberOfSeasons <= 3) ||
                  (seriesDuration == "3 - 7" && numberOfSeasons >= 3 && numberOfSeasons <= 7) ||
                  (seriesDuration == "7 - 10" && numberOfSeasons >= 7 && numberOfSeasons <= 10) ||
                  (seriesDuration == "10+" && numberOfSeasons >= 10)
                )
                {
                    media.score = media.score + Series_Duration_Score;
                }
            }
        }

        private void applySeasonDurationScore(RecMedia media, dynamic mediaDetails, RecommenderContext recommenderContext)
        {
            var numberOfEpisodes = mediaDetails["number_of_episodes"];
            var numberOfSeasons = mediaDetails["number_of_seasons"];
            var episodesPerSeason = numberOfEpisodes / numberOfSeasons;

            foreach (string seasonDuration in recommenderContext.userPick.SeasonDuration)
            {
                if (
                  (seasonDuration == "less than 10" && episodesPerSeason < 10) ||
                  (seasonDuration == "10 - 16" && episodesPerSeason >= 10 && episodesPerSeason <= 16) ||
                  (seasonDuration == "16-25" && episodesPerSeason >= 16 && episodesPerSeason <= 25) ||
                  (seasonDuration == "25+" && episodesPerSeason >= 25)
                )
                {
                    media.score = media.score + Season_Duration_Score;
                }
            }
        }

        private void applyEpisodeDurationScore(RecMedia media, dynamic mediaDetails, RecommenderContext recommenderContext)
        {
            var episodeRunTime = mediaDetails["episode_run_time"][0];
            foreach (string episodeDuration in recommenderContext.userPick.EpisodeDuration)
            {
                if (
                  // imi place ca restul sunt interval dar asta e around 20 minutes :))))
                  (episodeDuration == "around 20 mins" && episodeRunTime >= 15 && episodeRunTime <= 25) ||
                  (episodeDuration == "20 - 30 mins" && episodeRunTime >= 20 && episodeRunTime <= 30) ||
                  (episodeDuration == "30 -  45 mins" && episodeRunTime >= 30 && episodeRunTime <= 45) ||
                  (episodeDuration == "45+ mins" && episodeRunTime >= 45)
                )
                {
                    media.score = media.score + Episode_Duration_Score;
                }
            }
        }

        // Movie

        private void applySimilarMoviesScore(RecMedia media, List<string> similarMoviesIds)
        {
            foreach (string similarMovieIds in similarMoviesIds)
            {
                if (similarMovieIds == media.id)
                {
                    media.score = media.score + Similar_Movies_Score;
                }
            }
        }

        private void applyMovieDurationScore(RecMedia media, dynamic mediaDetails, RecommenderContext recommenderContext)
        {
            var runtime = mediaDetails["runtime"];

            foreach (string movieDuration in recommenderContext.userPick.Durations)
            {
                if (
                  (movieDuration == "1h - 1h30" && runtime >= 60 && runtime <= 90) ||
                  (movieDuration == "1h30 - 2h" && runtime >= 90 && runtime <= 120) ||
                  (movieDuration == "2h - 2h30" && runtime >= 120 && runtime <= 150) ||
                  (movieDuration == "2h30+" && runtime >= 150)
                )
                {
                    media.score = media.score + Movie_Duration_Score;
                }
            }
        }

    }
}
