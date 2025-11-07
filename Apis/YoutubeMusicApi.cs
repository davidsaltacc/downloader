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
    internal class YoutubeMusicApi
    {

        public static async Task<YoutubeMusicSong?> findSong(Song songData)
        {

            string artistsNameJoined = string.Join(" ", songData.Artists);

            string songTitleClean = Regex.Replace(songData.Title, @"[\p{S}]+", " ").Trim();
            string albumTitleClean = Regex.Replace(songData.Album, @"[\p{S}]+", " ").Trim();
            string artistsNamesClean = Regex.Replace(artistsNameJoined, @"[\p{S}]+", " ").Trim();

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

            var results = scoreFoundSongs((await Task.WhenAll([
                search(artistsNamesClean + " " + songTitleClean, SearchFor.Songs),
                search(artistsNamesClean + " " + songTitleClean + " " + albumTitleClean, SearchFor.Songs),
                search(artistsNameJoined + " " + songData.Title + " " + songData.Album, SearchFor.Songs)
            ])).SelectMany(x => x).Distinct().ToList(), songData);

            YoutubeMusicSong finalSong = results.OrderBy(x => -x.Key).ToList()[0].Value;
            return finalSong == null
                ? null
                : new(songData.Album, songData.Artists, songData.Title, songData.durationMs, songData.indexOnDisk,
                    songData.diskIndex, songData.releaseYear, songData.imageUrl, finalSong.youtubeSongUrl);

        }

        public static List<KeyValuePair<float, YoutubeMusicSong>> scoreFoundSongs(List<YoutubeMusicSong> songs, Song originalSong)
        {
            List<KeyValuePair<float, YoutubeMusicSong>> scored = [];

            foreach (var song in songs)
            {
                
                float score = 0f;

                score += Process.ExtractAll(originalSong.Title, [ song.Title ], s => s).ToArray()[0].Score / 100f;
                score += Process.ExtractAll(originalSong.Album, [ song.Album ], s => s).ToArray()[0].Score / 100f * 0.65f;
                if (song.durationMs <= 0) {
                    score += (15000 - Math.Abs(song.durationMs - originalSong.durationMs)) / 15000f;
                }
                score += song.Artists.Select(artist => Process.ExtractOne(artist, originalSong.Artists, s => s).Score).Sum() /
                         (float) Math.Max(song.Artists.Length, originalSong.Artists.Length) / 100f;

                score /= (song.durationMs <= 0 ? 2f : 3f) + 0.65f;
                
                scored.Add(new( score, song ));

            }

            return scored;
        }

        public enum SearchFor {
            Songs = 18761, // II
            Albums = 18777, // IY
            Artists = 18791 // Ig
        }

        public static async Task<List<YoutubeMusicSong>> search(string query, SearchFor searchFor) 
        {

            string searchParams = "EgWKAQ"; // enable filter
            searchParams += (char) ((int) searchFor >> 8) + "" + (char) ((int) searchFor & 255);
            searchParams += "AUICCAFqDBAOEAoQAxAEEAkQBQ%3D%3D"; // do not ignore spelling

            string postData = @"

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

            var response = await MainWindow.httpClient.PostAsync("https://music.youtube.com/youtubei/v1/search/?alt=json", new StringContent(postData));
            var data = JsonNode.Parse(await response.Content.ReadAsStringAsync());

            var contents = data?["contents"]?["tabbedSearchResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"];
            int musicShelfIndex = 0;
            int i = 0;
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

                songsReturned.Add(new(album ?? "", [.. artists], title ?? "",  (int) Math.Floor(duration?.TotalMilliseconds ?? -1), -1, -1, -1, "", "https://www.youtube.com/watch?v=" + videoId)); 

            }

            return songsReturned;

        }

    }
}
