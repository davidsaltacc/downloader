using downloader.Utils.Songs;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Downloader.Apis
{
    internal abstract class YoutubeMusicApi
    {

        private static string? _visitorId;
        private static string? _clientName;
        private static string? _clientVersion;

        public static async Task Init()
        {
            
            var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri("https://music.youtube.com"),
                Headers = {
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" },
                    { "Cookie", "SOCS=CAI" }
                }
            });

            var responseContent = await response.Content.ReadAsStringAsync();
            var matches = Regex.Matches(responseContent, @"ytcfg\.set\s*\(\s*({.+?})\s*\)\s*;"); // thank you sigma67/ytmusicapi for this regex 
            var data = JsonNode.Parse(matches[0].Groups[1].Value);

            _visitorId = data?["VISITOR_DATA"]?.ToString();
            _clientName = data?["INNERTUBE_CLIENT_NAME"]?.ToString();
            _clientVersion = data?["INNERTUBE_CLIENT_VERSION"]?.ToString();

        }
        
        private static async Task<HttpResponseMessage?> GetAlbumDataFromBrowseId(string browseId)
        {
            return await SendApiRequest("browse",
@"{
    ""browseId"": """ + browseId + @"""
}");;
        }
        
        private static async Task<HttpResponseMessage?> GetAlbumDataFromSong(string videoId)
        {
            
            var response = await SendApiRequest("next", 
@"{
    ""video_id"": """ + videoId + @"""
}");
            
            if (response == null)
            {
                return null;
            }

            var content = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            var items = content?["contents"]?["singleColumnMusicWatchNextResultsRenderer"]?
                ["tabbedRenderer"]?["watchNextTabbedResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["musicQueueRenderer"]?["content"]?
                ["playlistPanelRenderer"]?["contents"]?[0]?["playlistPanelVideoRenderer"]?["menu"]?["menuRenderer"]?["items"]?.AsArray();

            string browseId = null;
            
            foreach (var item in (items ?? [])) 
            {
                var navItemRenderer = item?["menuNavigationItemRenderer"];
                if (navItemRenderer?["icon"]?["iconType"]?.ToString().Contains("album", StringComparison.OrdinalIgnoreCase) ?? false) 
                {
                    browseId = navItemRenderer?["navigationEndpoint"]?["browseEndpoint"]?["browseId"].ToString() ?? browseId;
                }
            }

            if (browseId == null)
            {
                return null;
            }

            return await GetAlbumDataFromBrowseId(browseId);
            
        }

        public static async Task<YoutubeMusicSong?> GetSong(string url, bool skipQueryingAlbum = false, int indexOnDisc = -1)
        {

            if (skipQueryingAlbum && indexOnDisc == -1)
            {
                throw new Exception("Can't skip album while getting song and not provide song index on disc.");
            }

            var videoId = HttpUtility.ParseQueryString(new Uri(url).Query)["v"];
            if (videoId == null)
            {
                return null;
            }

            var responseSong = await SendApiRequest("player",
                @"{
    ""playbackContext"": { ""contentPlaybackContext"": {""signatureTimestamp"": " +
                ((DateTime.Today - DateTime.UnixEpoch).Days - 1) + @"} },
    ""video_id"": """ + videoId + @"""
}");
            if (responseSong == null)
            {
                return null;
            }

            var content = JsonNode.Parse(await responseSong.Content.ReadAsStringAsync());

            var title = content?["videoDetails"]?["title"]?.ToString();
            var author = content?["videoDetails"]?["author"]?.ToString();
            var duration = TimeSpan.FromSeconds(int.Parse(
                content?["microformat"]?["microformatDataRenderer"]?["videoDetails"]?["durationSeconds"]?.ToString() ??
                "0"));
            var thumbnail =
                Regex.Replace(
                    content?["videoDetails"]?["thumbnail"]?["thumbnails"]?.AsArray()[0]?["url"]?.ToString() ?? "",
                    @"=w\d+-h\d+-", "=w2048-h2048-"); // force the size to the maximum by supplying a huge resolution

            var albumData = await GetAlbumDataFromSong(videoId);
            var albumContent = JsonNode.Parse(await (albumData?.Content.ReadAsStringAsync() ?? Task.Run(() => "{}")));
            var songsData =
                albumContent?["contents"]?["twoColumnBrowseResultsRenderer"]?["secondaryContents"]?[
                    "sectionListRenderer"]?["contents"]?[0]?["musicShelfRenderer"]?["contents"]?.AsArray();

            var albumName = albumContent?["contents"]?["twoColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?[0]?["musicResponsiveHeaderRenderer"]?["title"]?["runs"]?[0]?["text"]?.ToString();
            int? releaseYear = null;
            if (int.TryParse(albumContent?["contents"]?["twoColumnBrowseResultsRenderer"]?["tabs"]?[0]?["tabRenderer"]?["content"]?["sectionListRenderer"]?["contents"]?[0]?["musicResponsiveHeaderRenderer"]?["subtitle"]?["runs"]?[2]?["text"]?.ToString(), CultureInfo.InvariantCulture, out var yearParsed))
            {
                releaseYear = yearParsed;
            }

            if (indexOnDisc == -1)
            {
                int i = 0;
                foreach (var songData in songsData ?? [])
                {
                    if (videoId ==
                        songData?["musicResponsiveListItemRenderer"]?["overlay"]?["musicItemThumbnailOverlayRenderer"]?
                            ["content"]?["musicPlayButtonRenderer"]?["playNavigationEndpoint"]?["watchEndpoint"]?[
                                "videoId"]?.ToString())
                    {
                        indexOnDisc = i;
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }
            }
            
            return new YoutubeMusicSong(albumName ?? "", [ author ?? "" ], title ?? videoId, (int) duration.TotalMilliseconds, indexOnDisc, 0, releaseYear ?? -1, thumbnail, url);

        }

        private static async Task<HttpResponseMessage?> SendApiRequest(string endpoint, string data)
        {
            
            var finalData = JsonNode.Parse(data);
            
            if (finalData == null)
            {
                return null;
            }
            
            finalData["context"] = JsonNode.Parse(@"{
                ""user"": {},
                ""client"": {
                    ""clientName"": """ + _clientName + @""",
                    ""clientVersion"": """ + _clientVersion + @""",
                    ""hl"": ""en""
                }
            }");
            
            return await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://music.youtube.com/youtubei/v1/" + endpoint + "/?alt=json"),
                Headers = {
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" },
                    { "Cookie", "SOCS=CAI" },
                    { "X-Goog-Visitor-Id", _visitorId }
                },
                Content = new StringContent(finalData.ToJsonString())
            });
            
        }

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
                var max = 0f;

                score += FuzzySharp.Process.ExtractOne(originalSong.Title, [ song.Title ], s => s).Score / 100f;
                max += 1;
                
                score += FuzzySharp.Process.ExtractOne(originalSong.Album, [ song.Album ], s => s).Score / 100f * 0.65f;
                max += 0.65f;
                
                if (song.DurationMs > 0) {
                    score += (15000 - Math.Abs(song.DurationMs - originalSong.DurationMs)) / 15000f;
                    max += 1f;
                }
                
                score += song.Artists.Select(artist => FuzzySharp.Process.ExtractOne(artist, originalSong.Artists, s => s).Score).Sum() /
                         (float) Math.Max(song.Artists.Length, originalSong.Artists.Length) / 100f;
                max += 1;

                score /= max;
                
                scored.Add(new KeyValuePair<float, YoutubeMusicSong>( score, song ));

            }

            return scored;
        }

        private enum SearchFor {
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
    ""params"": """ + searchParams + @"""
}

".Replace("\n", "");

            var response = await SendApiRequest("search", postData);

            if (response == null)
            {
                return [];
            }

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
