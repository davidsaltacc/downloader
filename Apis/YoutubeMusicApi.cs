
using Avalonia.Controls;
using downloader.Utils.Songs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Downloader.Apis
{
    internal class YoutubeMusicApi
    {

        public static async /* Task<YoutubeMusicSong> TODO */ void findSong(SpotifySong songData) {

            string artistsNameJoined = string.Join(" ", songData.Artists);

            string songTitleClean = Regex.Replace(songData.Title, @"[^\p{L}]+", " ").Trim();
            string artistsNamesClean = Regex.Replace(artistsNameJoined, @"[^\p{L}]+", " ").Trim();

            if (songTitleClean.Length < songData.Title.Length * 0.4)
            {
                songTitleClean = songData.Title;
            }
            if (artistsNamesClean.Length < artistsNameJoined.Length * 0.6)
            {
                artistsNamesClean = artistsNameJoined;
            }

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
    ""params"": """ + searchParams + @"""
    ""context"": {
        ""user"": {},
        ""client"": {
            ""clientName"": ""WEB_REMIX"",
            ""clientVersion"": ""1." + DateTime.Now.ToString("yyyyMMdd") + @""".01.00""
        }
    }
}

".Replace("\n", "");

            var response = await MainWindow.httpClient.PostAsync("https://music.youtube.com/youtubei/v1/search/?alt=json", new StringContent(postData));
            var data = JsonNode.Parse(await response.Content.ReadAsStringAsync());

            var results = data?["content"]?["tabbedSearchResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?[0]?["musicShelfRenderer"]?["contents"];

            List<YoutubeMusicSong> songsReturned = [];

            foreach (var song in results?.AsArray() ?? [])
            {

                var columns = song?["musicResponsiveListItemRenderer"]?["flexColumns"]?.AsArray().Select(column => column?["musicResponsiveListItemFlexColumnRenderer"]?["text"]?["runs"]?[0]).ToArray();
                var title = columns?[0]?["text"]?.ToString();
                var videoId = columns?[0]?["navigationEndpoint"]?["watchEndpoint"]?["videoId"];

                List<string> artists = [];
                string? album = null;

                foreach (var part in columns?[1]?["text"]?["runs"]?.AsArray() ?? [])
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
                }

                songsReturned.Add(new(album ?? "", [.. artists], title ?? "", -1, -1, -1, "", "https://www.youtube.com/watch?v=" + videoId)); 

            }

            return songsReturned;

        }

    }
}
