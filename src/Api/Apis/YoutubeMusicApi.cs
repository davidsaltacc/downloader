using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Downloader.Utils;

namespace Downloader.Api.Apis
{
    internal class YoutubeMusicApi : ISongDataSource, ISongAudioSource
    {
        
        private YoutubeMusicApi() {}

        private static YoutubeMusicApi? _instance = null;
        public static YoutubeMusicApi Instance
        {
            get
            {
                _instance ??= new YoutubeMusicApi();
                return _instance;
            }
        }

        private string? _visitorId;
        private string? _clientName;
        private string? _clientVersion;

        public async Task Init()
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
        
        private async Task<HttpResponseMessage?> GetAlbumDataFromBrowseId(string browseId)
        {
            var response = await SendApiRequest("browse",
@"{
    ""browseId"": """ + browseId + @"""
}");
            return response;
        }
        
        private async Task<HttpResponseMessage?> GetAlbumDataFromSong(string videoId)
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
            var items = Helpers.NavigateJsonNode(
                content,
                "contents", "singleColumnMusicWatchNextResultsRenderer", "tabbedRenderer", 
                "watchNextTabbedResultsRenderer", "tabs", 0, "tabRenderer", "content", "musicQueueRenderer", "content",
                "playlistPanelRenderer", "contents", 0, "playlistPanelVideoRenderer", "menu", "menuRenderer", "items"
            )?.AsArray();

            string? browseId = null;
            
            foreach (var item in (items ?? [])) 
            {
                var navItemRenderer = item?["menuNavigationItemRenderer"];
                if (navItemRenderer?["icon"]?["iconType"]?.ToString().Contains("album", StringComparison.OrdinalIgnoreCase) ?? false) 
                {
                    browseId = navItemRenderer["navigationEndpoint"]?["browseEndpoint"]?["browseId"]?.ToString() ?? browseId;
                }
            }

            if (browseId == null)
            {
                return null;
            }

            return await GetAlbumDataFromBrowseId(browseId);
            
        }

        private JsonArray? GetSongsFromAlbumData(JsonNode? albumData)
        {
            return Helpers.NavigateJsonNode(
                albumData,
                "contents", "twoColumnBrowseResultsRenderer", "secondaryContents", "sectionListRenderer",
                "contents", 0, "musicShelfRenderer", "contents"
            )?.AsArray();
        }

        private async Task<Song?> GetSong(string url, bool skipQueryingAlbum = false)
        {

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
            
            var albumData = skipQueryingAlbum ? null : await GetAlbumDataFromSong(videoId);
            var albumContent = skipQueryingAlbum ? null : JsonNode.Parse(await (albumData?.Content.ReadAsStringAsync() ?? Task.Run(() => "{}")));
            var songsData = skipQueryingAlbum ? null : GetSongsFromAlbumData(albumContent);

            var albumName = skipQueryingAlbum ? "" : Helpers.NavigateJsonNode(
                albumContent,
                "contents", "twoColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content", 
                "sectionListRenderer", "contents", 0, "musicResponsiveHeaderRenderer", "title", "runs", 0, "text"
            )?.ToString();
            int? releaseYear = null;
            if (!skipQueryingAlbum && int.TryParse(Helpers.NavigateJsonNode(
                    albumContent,
                    "contents", "twoColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content", 
                    "sectionListRenderer", "contents", 0, "musicResponsiveHeaderRenderer", "subtitle", "runs", 2, "text"
            )?.ToString(), CultureInfo.InvariantCulture, out var yearParsed))
            {
                releaseYear = yearParsed;
            }

            int? indexOnDisc = null;
            if (!skipQueryingAlbum)
            {
                var i = 0;
                foreach (var songData in songsData ?? [])
                {
                    if (videoId ==
                        Helpers.NavigateJsonNode(
                            songData,
                            "musicResponsiveListItemRenderer", "overlay", "musicItemThumbnailOverlayRenderer",
                            "content", "musicPlayButtonRenderer", "playNavigationEndpoint", "watchEndpoint", "videoId"
                        )?.ToString())
                    {
                        indexOnDisc = i;
                        break;
                    } 
                    i++;
                }
            }
            
            return new Song(albumName ?? "", [ author ?? "" ], title ?? videoId, (int) duration.TotalMilliseconds, indexOnDisc ?? -1, 0, releaseYear ?? -1, thumbnail, url, GetId());

        }

        private async Task<HttpResponseMessage?> SendApiRequest(string endpoint, string data)
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

        public async Task<Song?> FindSong(Song songData)
        {

            if (songData.SourceApi == GetId())
            {
                // return song;
                // ya'd think. but for some reason, some videos are age-restricted, though their songs are not
                // also some songs only exists as videos but not songs
            }

            if (songData.Title.Length == 0)
            {
                return null;
            }

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

            var results = Helpers.ScoreFoundSongs((await Task.WhenAll([
                Search(artistsNamesClean + " " + songTitleClean, SearchFor.Songs),
                Search(artistsNamesClean + " " + songTitleClean, SearchFor.Videos),
                Search(artistsNamesClean + " " + songTitleClean + " " + albumTitleClean, SearchFor.Songs),
                Search(artistsNameJoined + " " + songData.Title + " ", SearchFor.Videos),
                Search(artistsNameJoined + " " + songData.Title + " " + songData.Album, SearchFor.Songs)
            ])).SelectMany(x => x).Distinct().ToList(), songData, false, true);

            if (results.Count == 0)
            {
                return null;
            }

            var finalSong = results.OrderBy(x => -x.Key).ToList()[0].Value;
            return new Song(songData.Album, songData.Artists, songData.Title, songData.DurationMs, songData.IndexOnDisk,
                    songData.DiskIndex, songData.ReleaseYear, songData.ImageUrl, finalSong.SongUrl, GetId());

        }

        private enum SearchFor {
            Songs = 18761, // II
            Albums = 18777, // IY
            Artists = 18791, // Ig
            Videos = 18769 // IQ
        }

        private async Task<List<Song>> Search(string query, SearchFor searchFor) 
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

            var contents = Helpers.NavigateJsonNode(
                data,
                "contents", "tabbedSearchResultsRenderer", "tabs", 0, "tabRenderer", "content", "sectionListRenderer", "contents"
            );
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

            List<Song> songsReturned = [];

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

                songsReturned.Add(new Song(album ?? "", [.. artists], title ?? "",  (int) Math.Floor(duration?.TotalMilliseconds ?? -1), -1, -1, -1, "", "https://www.youtube.com/watch?v=" + videoId, GetId())); 

            }

            return songsReturned;

        }

        private async Task<string?> GetAlbumBrowseId(string url)
        {
            var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
                Headers = {
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" },
                    { "Cookie", "SOCS=CAI" }
                }
            });
            var content = (await response.Content.ReadAsStringAsync()).Replace("\\\"", "\"");
            var match = Regex.Match(content, @"""(MPRE.+?)""");
            return new List<Group>(match.Groups).ElementAtOrDefault(1)?.Value;
        }

        private async Task<Song[]> GetSongsInAlbum(string browseId)
        {
            var albumData = await GetAlbumDataFromBrowseId(browseId);
            if (albumData == null)
            {
                return [];
            }
            var albumContent = JsonNode.Parse(await (albumData?.Content.ReadAsStringAsync() ?? Task.Run(() => "{}")));
            var songsData = GetSongsFromAlbumData(albumContent);

            return songsData?.Select(async (song, index) =>
            {
                var songData = await GetSong("https://music.youtube.com/watch?v=" + Helpers.NavigateJsonNode(
                    song,
                    "musicResponsiveListItemRenderer", "overlay", "musicItemThumbnailOverlayRenderer",
                    "content", "musicPlayButtonRenderer", "playNavigationEndpoint", "watchEndpoint", "videoId"
                ), true);
                
                if (songData == null)
                {
                    return new Song("", [""], "", 0, 0, 0, 0, "", "", GetId());
                }
                
                songData.Title = Helpers.NavigateJsonNode(
                    song,
                    "musicResponsiveListItemRenderer", "flexColumns",
                    0, "musicResponsiveListItemFlexColumnRenderer", "text", "runs", 0, "text"
                )?.ToString() ?? songData.Title;
                songData.Album = Helpers.NavigateJsonNode(
                    albumContent,
                    "contents", "twoColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content",
                    "sectionListRenderer", "contents", 0, "musicResponsiveHeaderRenderer", "title", "runs", 0, "text"
                )?.ToString() ?? "";
                if (int.TryParse(Helpers.NavigateJsonNode(
                        albumContent,
                        "contents", "twoColumnBrowseResultsRenderer", "tabs", 0, "tabRenderer", "content", 
                        "sectionListRenderer", "contents", 0, "musicResponsiveHeaderRenderer", "title", "runs", 0, "text",
                        "subtitle", "runs", 2, "text")?.ToString(), CultureInfo.InvariantCulture, out var yearParsed))
                {
                    songData.ReleaseYear = yearParsed;
                }
                songData.IndexOnDisk = index;
                songData.DiskIndex = 0;
                return songData;
            }).Select(t => t.Result).Where(s => s.SongUrl.Length > 0 && s.Title.Length > 0 && s.DurationMs > 0).ToArray() ?? [];

        }

        private async Task<Song[]> GetSongsInPlaylist(string playlistId)
        {
            var browseId = playlistId.StartsWith("VL") ? playlistId : ("VL" + playlistId);
            var response = await SendApiRequest("browse", @"{ ""browseId"": """ + browseId + @""" }");
            if (response == null)
            {
                return [];
            }

            var data = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            List<Song> songs = [];

            await AddSongs(data, false);

            while (true)
            {
                var continuation = Helpers.NavigateJsonNode(
                    data, "contents", "twoColumnBrowseResultsRenderer", "secondaryContents", 
                    "sectionListRenderer", "contents", 0, "musicPlaylistShelfRenderer", "contents", -1,
                    "continuationItemRenderer", "continuationEndpoint", "continuationCommand", "token"
                )?.ToString();
                
                if (continuation == null)
                {
                    break;
                }
                
                response = await SendApiRequest("browse", @"{ ""continuation"": """ + continuation + @""" }");
                if (response == null)
                {
                    break;
                }
                data = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                
                await AddSongs(data, true);

            }
            
            return songs.ToArray();
            
            async Task AddSongs(JsonNode? data, bool continuation)
            {
                foreach (var jsonNode in ((continuation ? Helpers.NavigateJsonNode(
                             data, "onResponseReceivedActions", 0, "appendContinuationItemsAction", "continuationItems"
                         ) : Helpers.NavigateJsonNode(
                             data, "contents", "twoColumnBrowseResultsRenderer", "secondaryContents", 
                             "sectionListRenderer", "contents", 0, "musicPlaylistShelfRenderer", "contents"
                         ))?.AsArray() ?? []))
                {
                    var song = await GetSong("https://music.youtube.com/watch?v=" + Helpers.NavigateJsonNode(
                        jsonNode, "musicResponsiveListItemRenderer", "flexColumns", 0,
                        "musicResponsiveListItemFlexColumnRenderer", "text", "runs", 0, "navigationEndpoint",
                        "watchEndpoint", "videoId"
                    ));
                    if (song != null)
                    {
                        songs.Add(song);
                    }
                }
            }
        }

        public async Task<Song[]> _GetSongs(string url)
        {
            var uri = new Uri(url);
            
            if (uri.AbsolutePath.StartsWith("/watch"))
            {
                var song = await GetSong(url);
                return song == null ? [] : [ song ];
            }

            var browseId = await GetAlbumBrowseId(url);
            if (browseId != null)
            {
                return await GetSongsInAlbum(browseId);
            }
            
            var playlistId = HttpUtility.ParseQueryString(uri.Query).Get("list");
            if (playlistId == null)
            {
                return [];
            }
            return await GetSongsInPlaylist(playlistId);
            
        }

        public async Task<Song[]> GetSongs(string url)
        {
            return (await _GetSongs(url)).Where(song => song.Title.Length > 0 && song.SongUrl.Length > 0).ToArray();
        }

        public async Task<string?> DownloadSong(Song song, string folder, Action<int> onProgressUpdate)
        {
            return await YtDlpApi.DownloadSong(song, song.SongUrl, folder, onProgressUpdate, HttpUtility.ParseQueryString(new Uri(song.SongUrl).Query).Get("v"));
        }
        
        public bool UrlPartOfPlatform(string url)
        {
            return new Uri(url).Host.Contains("music.youtube", StringComparison.OrdinalIgnoreCase);
        }
        
        public string GetName()
        {
            return "Youtube Music";
        }
        
        public string GetId()
        {
            return "ytmusic";
        }
        
    }
}
