using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FilmrecAPI.Models
{
    public enum Media
    {
        Film,
        Series,
        None
    }
    public enum Running
    {
        StillRunning,
        None,
        NotRunning
    }
    public class UserPick
    {
        private Media mediaType;
        public Media MediaType { get { return mediaType; } set { mediaType = value; } }

        private List<string> genres;
        public List<string> Genres { get { return genres; } set { genres = value; } }

        private List<string> actors;
        public List<string> Actors { get { return actors; } set { actors = value; } }
        private List<string> directors;
        public List<string> Directors { get { return directors; } set { directors = value; } }

        //series
        private Running stillRunning;
        public Running StillRunning { get { return stillRunning; } set { stillRunning = value; } }

        private List<string> seriesDuration;
        public List<string> SeriesDuration { get { return seriesDuration; } set { seriesDuration = value; } }

        private List<string> seasonDuration;
        public List<string> SeasonDuration { get { return seasonDuration; } set { seasonDuration = value; } }

        private List<string> episodeDuration;
        public List<string> EpisodeDuration { get { return episodeDuration; } set { episodeDuration = value; } }

        private List<string> similarSeries;
        public List<string> SimilarSeries { get { return similarSeries; } set { similarSeries = value; } }

        //film
        private List<string> durations;
        public List<string> Durations { get { return durations; } set { durations = value; } }

        private List<string> similarFilms;
        public List<string> SimilarFilms { get { return similarFilms; } set { similarFilms = value; } }

        private string DisplayList(List<string> v)
        {
            string outString = "";
            for (int i = 0; i < v.Count; i++)
            {
                outString += v[i] + "\n";
            }
            return outString;
        }
        public override string ToString()
        {
            String media = "Media: " + MediaType.ToString() + "\n";
            String genre = "Genres: " + DisplayList(Genres) + "\n";
            String act = "Actors: " + DisplayList(Actors) + "\n";
            String dir = "Directors: " + DisplayList(Directors) + "\n";
            String length = "Film length: " + DisplayList(Durations) + "\n";
            String simFilms = "Similar films" + DisplayList(SimilarFilms) + "\n";
            String seriesRunning = "Series status: " + StillRunning.ToString() + "\n";
            String seasons = "Number of season: " + DisplayList(SeriesDuration) + "\n";
            String episodes = "Number of episodes per season: " + DisplayList(SeasonDuration) + "\n";
            String minutes = "Episode length: " + DisplayList(EpisodeDuration) + "\n";
            String simShows = "Similar shows: " + DisplayList(SimilarSeries) + "\n";

            return media + genre + act + dir + length + simFilms + seriesRunning + seasons + episodes + minutes + simShows;
        }
    }
}
