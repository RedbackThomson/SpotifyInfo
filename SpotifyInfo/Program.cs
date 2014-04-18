using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Slyngelstat.Spotify.WebApi;

namespace SpotifyInfo
{
    class Program
    {
        private static Process _spotifyProcess;
        private static string _currentArtist = string.Empty;
        private static string _currentTrack = string.Empty;
        private static WebApiClient _webApi;

        private const string OutputLocation = "/output/";
        private const string AlbumArtLocation = OutputLocation + "album_art.jpg";
        private const string TrackLocation = OutputLocation + "track.txt";
        private const string ArtistLocation = OutputLocation + "artist.txt";

        private const string AlbumArtUri = "http://embed.spotify.com/oembed/?url={0}";

        static void Main(string[] args)
        {
            Console.WriteLine("SpotifyInfo by Redback93");

            _webApi = new WebApiClient();
            MakeOutputDirectory();

            _spotifyProcess = GetSpotifyProcess();
            if (_spotifyProcess == null)
                ExitError("Cannot find the Spotify process");

            while (!_spotifyProcess.HasExited)
                RunUpdateCycle();

            ExitError("Spotify has exited.");
        }

        private static Process GetSpotifyProcess()
        {
            var processes = Process.GetProcessesByName("spotify");
            return processes.Length < 1 ? null : processes[0];
        }

        private static void RunUpdateCycle()
        {
            if(NewSong())
            {
                if (_currentArtist != "")
                    Console.WriteLine("New Song: {0} - {1}", _currentArtist, _currentTrack);
                GetAlbumArt();
                SaveTitles();
            }

            Thread.Sleep(150);
        }

        private static void GetAlbumArt()
        {
            if(_currentArtist == "" || _currentTrack == "")
            {
                ResetAlbumArt();
                return;
            }

            var tracks = _webApi.SearchTracks(_currentTrack);
            TrackResult selectedTrack = tracks.Tracks.FirstOrDefault(track => track.Artists.Any(artist => _currentArtist.Contains(artist.Name)));
            if(!selectedTrack.ExternalIds.Any())
            {
                ResetAlbumArt();
                return;
            }

            string trackUri = selectedTrack.Href.Uri;
            SaveAlbumArt(trackUri);
        }

        private static void SaveTitles()
        {
            using(var file = new StreamWriter(Directory.GetCurrentDirectory() + TrackLocation, false))
                file.WriteLine(_currentTrack);

            using (var file = new StreamWriter(Directory.GetCurrentDirectory() + ArtistLocation, false))
                file.WriteLine(_currentArtist);
        }

        private static void SaveAlbumArt(string trackUri)
        {
            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
            var embedUrl = string.Format(AlbumArtUri, trackUri);
            var trackEmbed = client.DownloadString(embedUrl);
            dynamic jsonEmbed = JsonConvert.DeserializeObject(trackEmbed);
            client.DownloadFileAsync(new Uri((string)jsonEmbed.thumbnail_url), Directory.GetCurrentDirectory() + AlbumArtLocation);
        }

        private static void MakeOutputDirectory()
        {
            if (!Directory.Exists(Directory.GetCurrentDirectory() + OutputLocation))
                Directory.CreateDirectory(Directory.GetCurrentDirectory() + OutputLocation);
        }

        private static void ResetAlbumArt()
        {
            var defaultArt = Properties.Resources.default_art;
            try
            {
                defaultArt.Save(Directory.GetCurrentDirectory() + AlbumArtLocation);
            }
            catch(Exception)
            {
            }
            //Directory.GetCurrentDirectory() +
            //AlbumArtLocation;
        }

        private static bool NewSong()
        {
            string _oldTrack = string.Copy(_currentTrack);
            string _oldArtist = string.Copy(_currentArtist);

            GetCurrentWindowInfo();

            return (_oldArtist != _currentArtist || _oldTrack != _currentTrack);
        }

        private static void GetCurrentWindowInfo()
        {
            _spotifyProcess = Process.GetProcessById(_spotifyProcess.Id);
            string title = _spotifyProcess.MainWindowTitle;

            //Check for no songs playing
            if(title == "Spotify" || title == "")
            {
                _currentTrack = "";
                _currentArtist = "";
                return;
            }

            //Take off the inital "Spotify - "
            title = title.Replace("Spotify - ", "");

            //Split by the last hyphen
            const string denominator = " – ";
            int lastHyphen = title.LastIndexOf(denominator);
            string artist = title.Substring(0, lastHyphen);
            string track = title.Substring(lastHyphen + denominator.Length,
                                           title.Length - (lastHyphen + denominator.Length));
            _currentArtist = artist;
            _currentTrack = track;
        }

        private static void ExitError(string message)
        {
            Console.WriteLine("Error: " + message);
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
