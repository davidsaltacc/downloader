using downloader.Utils.Songs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FuzzySharp;

namespace Downloader.Apis
{
    internal abstract class YoutubeMusicApi
    {

        public static async Task<YoutubeMusicSong?> FindSong(Song songData)
        {

            var artistsNameJoined = string.Join(" ", songData.Artists);

            var songTitleClean = Regex.Replace(songData.Title, @"[\p{S}]+", " ").Trim();
            var albumTitleClean = Regex.Replace(songData.Album, @"[\p{S}]+", " ").Trim();
            var artistsNamesClean = Regex.Replace(artistsNameJoined, @"[\p{S}]+", " ").Trim();

            if (songTitleClean.Length < songData.Title.Length * 0.4)
            {
                songTitleClean = songData.Title;
            }

            if (albumTitleClean.Length < songData.Album.Length * 0.4)
            {
                albumTitleClean = songData.Album;
            }

            if (artistsNamesClean.Length < artistsNameJoined.Length * 0.6)
            {
                artistsNamesClean = artistsNameJoined;
            }

            var results = ScoreFoundSongs((await Task.WhenAll([
                Search(artistsNamesClean + " " + songTitleClean, SearchFor.Songs),
                Search(artistsNamesClean + " " + songTitleClean + " " + albumTitleClean, SearchFor.Songs),
                Search(artistsNameJoined + " " + songData.Title + " " + songData.Album, SearchFor.Songs)
            ])).SelectMany(x => x).Distinct().ToList(), songData);

            var finalSong = results.OrderBy(x => -x.Key).ToList()[0].Value;
            return new YoutubeMusicSong(songData.Album, songData.Artists, songData.Title, songData.DurationMs, songData.IndexOnDisk,
                    songData.DiskIndex, songData.ReleaseYear, songData.ImageUrl, finalSong.YoutubeSongUrl);

        }

        private static List<KeyValuePair<float, YoutubeMusicSong>> ScoreFoundSongs(List<YoutubeMusicSong> songs, Song originalSong)
        {
            List<KeyValuePair<float, YoutubeMusicSong>> scored = [];

            foreach (var song in songs)
            {
                
                var score = 0f;

                score += Process.ExtractAll(originalSong.Title, [ song.Title ], s => s).ToArray()[0].Score / 100f;
                score += Process.ExtractAll(originalSong.Album, [ song.Album ], s => s).ToArray()[0].Score / 100f * 0.65f;
                if (song.DurationMs <= 0) {
                    score += (15000 - Math.Abs(song.DurationMs - originalSong.DurationMs)) / 15000f;
                }
                score += song.Artists.Select(artist => Process.ExtractOne(artist, originalSong.Artists, s => s).Score).Sum() /
                         (float) Math.Max(song.Artists.Length, originalSong.Artists.Length) / 100f;

                score /= (song.DurationMs <= 0 ? 2f : 3f) + 0.65f;
                
                scored.Add(new KeyValuePair<float, YoutubeMusicSong>( score, song ));

            }

            return scored;
        }

        public enum SearchFor {
            Songs = 18761, // II
            Albums = 18777, // IY
            Artists = 18791 // Ig
        }

        private static async Task<List<YoutubeMusicSong>> Search(string query, SearchFor searchFor) 
        {

            var searchParams = "EgWKAQ"; // enable filter
            searchParams += (char) ((int) searchFor >> 8) + "" + (char) ((int) searchFor & 255);
            searchParams += "AUICCAFqDBAOEAoQAxAEEAkQBQ%3D%3D"; // do not ignore spelling

            var postData = @"

{
    ""query"": """ + query + @""",
    ""params"": """ + searchParams + @""",
    ""context"": {
        ""user"": {},
        ""client"": {
            ""clientName"": ""WEB_REMIX"",
            ""clientVersion"": ""1." + DateTime.Now.ToString("yyyyMMdd") + @".01.00""
        }
    }
}

".Replace("\n", "");

            var response = await MainWindow.HttpClient.PostAsync("https://music.youtube.com/youtubei/v1/search/?alt=json", new StringContent(postData));
            var data = JsonNode.Parse(await response.Content.ReadAsStringAsync());

            var contents = data?["contents"]?["tabbedSearchResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"];
            var musicShelfIndex = 0;
            var i = 0;
            foreach (var item in contents?.AsArray() ?? [])
            {
                if (item?["musicShelfRenderer"] != null)
                {
                    musicShelfIndex = i;
                }
                i++;
            }
            var results = contents?[musicShelfIndex]?["musicShelfRenderer"]?["contents"];

            List<YoutubeMusicSong> songsReturned = [];

            foreach (var song in results?.AsArray() ?? [])
            {

                var columns = song?["musicResponsiveListItemRenderer"]?["flexColumns"]?.AsArray().Select(column => column?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]).ToArray();
                var title = columns?[0]?[0]?["text"]?.ToString();
                var videoId = columns?[0]?[0]?["navigationEndpoint"]?["watchEndpoint"]?["videoId"];

                List<string> artists = [];
                string? album = null;
                TimeSpan? duration = null;

                foreach (var part in columns?[1]?.AsArray() ?? [])
                {
                    var text = part?["text"]?.ToString();
                    var type = part?["navigationEndpoint"]?["browseEndpoint"]?["browseEndpointContextSupportedConfigs"]?["browseEndpointContextMusicConfig"]?["pageType"]?.ToString().ToLower();
                    
                    if (type?.EndsWith("album") ?? false)
                    {
                        album = text;
                    }
                    if (type?.EndsWith("artist") ?? false)
                    {
                        artists.Add(text ?? "");
                    }
                    if (type == null && !(text?.Contains('•') ?? true) && TimeSpan.TryParse(text?.Split(":").Length == 2 ? "00:" + text : text, CultureInfo.InvariantCulture, out var span))
                    {
                        duration = span;
                    }
                }

                songsReturned.Add(new YoutubeMusicSong(album ?? "", [.. artists], title ?? "",  (int) Math.Floor(duration?.TotalMilliseconds ?? -1), -1, -1, -1, "", "https://www.youtube.com/watch?v=" + videoId)); 

            }

            return songsReturned;

        }

    }
}
