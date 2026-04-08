using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;
using Downloader.Utils;

namespace Downloader.Api.Apis;

public class TidalApi : ISongAudioSource, ISongDataSource
{

    private readonly bool _isLosslessInstance;
    private readonly bool _isHighQualityInstance;

    private TidalApi(bool lossless, bool high = true)
    {
        _isLosslessInstance = lossless;
        _isHighQualityInstance = high;
    }

    private static TidalApi? _instanceLossless;
    private static TidalApi? _instanceHigh;
    private static TidalApi? _instanceLower;
    
    public static TidalApi InstanceLossless
    {
        get
        {
            _instanceLossless ??= new TidalApi(true);
            return _instanceLossless;
        }
    }
    public static TidalApi InstanceHighQuality
    {
        get
        {
            _instanceHigh ??= new TidalApi(false, true);
            return _instanceHigh;
        }
    }
    public static TidalApi InstanceLowerQuality
    {
        get
        {
            _instanceLower ??= new TidalApi(false, false);
            return _instanceLower;
        }
    }

    private string? _tidalAccessToken;
    private List<string> _hifiApiUrls = [];
    private readonly Dictionary<string, int> _apiUrlsRequestCounter = new();
    private readonly Dictionary<string, int> _apiUrlsIssueCounter = new();

    public async Task Init()
    {

        var content = await (await MainWindow.HttpClient.GetAsync(
            "https://raw.githubusercontent.com/monochrome-music/monochrome/refs/heads/main/functions/track/%5Bid%5D.js")).Content.ReadAsStringAsync();

        var clientId = content
            .Split("\n")
            .First(s => s.Replace(" ", "").Contains("CLIENT_ID="))
            .Split("'")[1];
        
        var clientSecret = content
            .Split("\n")
            .First(s => s.Replace(" ", "").Contains("CLIENT_SECRET="))
            .Split("'")[1];

        var tokenResponse = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("https://auth.tidal.com/v1/oauth2/token"),
            Headers =
            {
                { "Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret)) }
            },
            Content = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            ])
        });

        _tidalAccessToken = JsonNode.Parse(await tokenResponse.Content.ReadAsStringAsync())?["access_token"]?.ToString();
        
        _hifiApiUrls = [];

        string[] apiUrlLists =
            [ "https://tidal-uptime.jiffy-puffs-1j.workers.dev/", "https://tidal-uptime.props-76styles.workers.dev/" ];

        async Task<string[]> getUrlsInList(string url)
        {
            var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url)
            });
        
            var content = await response.Content.ReadAsStringAsync();
            var data = JsonNode.Parse(content);

            if (data?["streaming"] == null)
            {
                return [];
            }

            return data["streaming"]?.AsArray()
                .Where(u => u != null)
                .Select(u => u.AsObject().ToString())
                .ToArray();

        }
        
        foreach (var listUrl in apiUrlLists)
        {
            foreach (var apiUrlJson in await getUrlsInList(listUrl))
            {

                var apiUrlData = JsonNode.Parse(apiUrlJson);
                var apiUrl = apiUrlData?["url"]?.ToString();
                var version = apiUrlData?["version"]?.ToString();
                var versionMaj = version?.Split(".")[0];
                var versionMin = version?.Split(".")[1];

                if ((versionMaj == "2" && Int32.Parse(versionMin ?? "0") < 6) || Int32.Parse(versionMaj ?? "0") < 2) // only versions 2.6 and above
                {
                    continue;
                }

                if (apiUrl == null)
                {
                    continue;
                }
                
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                HttpResponseMessage response;
                try
                {
                    response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri(apiUrl + "/track/?id=55391801")
                    }, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                if (await ApiRequestHiFi("/search/?s=" + HttpUtility.UrlEncode("song"), apiUrl) == null)
                {
                    continue;
                }

                if (JsonNode.Parse(await response.Content.ReadAsStringAsync())?["data"]?["assetPresentation"]?
                        .ToString() != "FULL") // downloading not supported
                {
                    continue;
                }
                
                _hifiApiUrls.Add(apiUrl);
            }
        }

        _hifiApiUrls = _hifiApiUrls.Distinct().ToList();

    }
    
    private async Task<JsonNode?> SearchForTrack(string name)
    {
        return (await ApiRequestDirect("/search/?query=" + HttpUtility.UrlEncode(name) + "&limit=25&offset=0&types=TRACKS&countryCode=US"))?["tracks"] 
               ?? (await ApiRequestHiFi("/search/?s=" + HttpUtility.UrlEncode(name)))?["data"];
    }
    
    private async Task<JsonNode?> GetAlbumMetadata(string id)
    {
        return await ApiRequestDirect("/albums/" + HttpUtility.UrlEncode(id) + "?countryCode=US") 
               ?? (await ApiRequestHiFi("/album/?id=" + HttpUtility.UrlEncode(id) + "&offset=0&limit=1"))?["data"];
    }
    
    private async Task<JsonNode?> GetPlaylistMetadata(string uuid)
    {
        return await ApiRequestDirect("/playlists/" + HttpUtility.UrlEncode(uuid) + "?countryCode=US") 
               ?? (await ApiRequestHiFi("/playlist/?id=" + HttpUtility.UrlEncode(uuid) + "&offset=0&limit=1"))?["playlist"];
    }
    
    private async Task<JsonNode?> GetTrackMetadata(string id)
    {
        return await ApiRequestDirect("/tracks/" + HttpUtility.UrlEncode(id) + "?countryCode=US") 
               ?? (await ApiRequestHiFi("/info/?id=" + HttpUtility.UrlEncode(id)))?["data"];
    }
    
    private async Task<List<JsonNode>> GetTracksInPlaylist(string uuid)
    {

        var metadata = await GetPlaylistMetadata(uuid);
        if (metadata == null)
        {
            return [];
        }

        var trackCount = Int32.Parse(metadata["numberOfTracks"]?.ToString() ?? "-1");

        List<JsonNode?> allSongs = [];
        var offset = 0;

        if (trackCount == -1)
        {
            allSongs.AddRange((await GetItems(0, 100))?.AsArray() ?? []);
        }
        else
        {
            while (allSongs.Count < trackCount)
            {
                var items = await GetItems(offset, 100);
                allSongs.AddRange(items?.AsArray() ?? []);
                offset += 100;
            }
        }

        return allSongs.Where(s => s != null).ToList()!;

        async Task<JsonNode?> GetItems(int off, int limit)
        {
            return (await ApiRequestDirect("/playlists/" + HttpUtility.UrlEncode(uuid) + "/items?countryCode=US&offset=" + off + "&limit=" + limit))?["items"]
                ?? (await ApiRequestHiFi("/playlist/?id=" + HttpUtility.UrlEncode(uuid) + "&offset=" + off + "&limit=" + limit))?["items"];
        }

    }
    
    private async Task<(List<JsonNode>, int)> GetTracksInAlbum(string uuid)
    {

        var metadata = await GetAlbumMetadata(uuid);
        if (metadata == null)
        {
            return ([], -1);
        }

        var trackCount = Int32.Parse(metadata["numberOfTracks"]?.ToString() ?? "-1");

        List<JsonNode?> allSongs = [];
        var offset = 0;

        if (trackCount == -1)
        {
            allSongs.AddRange((await GetItems(0, 100))?.AsArray() ?? []);
        }
        else
        {
            while (allSongs.Count < trackCount)
            {
                var items = await GetItems(offset, 100);
                allSongs.AddRange(items?.AsArray() ?? []);
                offset += 100;
            }
        }

        return (
            allSongs.Where(s => s != null).ToList(),
            int.TryParse(metadata["releaseDate"]?.ToString().Split("-")[0], out var year) ? year : -1 // return year to save a few duplicate requests
        )!;

        async Task<JsonNode?> GetItems(int off, int limit)
        {
            return (await ApiRequestDirect("/albums/" + HttpUtility.UrlEncode(uuid) + "/items?countryCode=US&offset=" + off + "&limit=" + limit))?["items"]
                   ?? (await ApiRequestHiFi("/album/?id=" + HttpUtility.UrlEncode(uuid) + "&offset=" + off + "&limit=" + limit))?["data"]?["items"];
        }

    }

    private async Task<JsonNode?> ApiRequestDirect(string endpoint)
    {

        if (_tidalAccessToken == null)
        {
            return null;
        }

        var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
        {
            RequestUri = new Uri("https://api.tidal.com/v1" + endpoint),
            Method = HttpMethod.Get,
            Headers =
            {
                { "Authorization", "Bearer " + _tidalAccessToken }
            }
        });

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        
        var content = await response.Content.ReadAsStringAsync();
        
        try
        {
            return JsonNode.Parse(content);
        }
        catch
        {
            return null;
        }
        
    }
    
    private static readonly Random _random = new();

    private async Task<JsonNode?> ApiRequestHiFi(string endpoint, string? fixedApiUrl = null, int maxRetries = 15)
    {

        async Task<JsonNode?> GetResponse()
        {
            if (_hifiApiUrls.Count == 0 && fixedApiUrl == null)
            {
                return null;
            }

            var apiUrl = fixedApiUrl ?? _hifiApiUrls[_random.Next(0, _hifiApiUrls.Count)]; // cycle to avoid rate limits 
        
            var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(apiUrl + endpoint)
            });

            if (fixedApiUrl == null)
            {
                _apiUrlsRequestCounter.TryAdd(apiUrl, 0);
                _apiUrlsRequestCounter[apiUrl] += 1;
            
                if (!response.IsSuccessStatusCode)
                {
                    _apiUrlsIssueCounter.TryAdd(apiUrl, 0);
                    _apiUrlsIssueCounter[apiUrl] += 1;
                    if (_apiUrlsIssueCounter[apiUrl] > 10 && _hifiApiUrls.Count >= 4 && (float) _apiUrlsIssueCounter[apiUrl] / _apiUrlsRequestCounter[apiUrl] > 0.75)
                    { // if over 75% of requests in the last 10 tries for this api url fail, and there are still more than 3 other api urls left, remove this one from being used
                        _hifiApiUrls.Remove(apiUrl);
                    }
                    Logger.Log("Noticed code " + response.StatusCode + " on api url " + apiUrl + endpoint);
                    return null;
                }

                if (_apiUrlsRequestCounter[apiUrl] > 10)
                {
                    _apiUrlsRequestCounter[apiUrl] = 0;
                    _apiUrlsIssueCounter[apiUrl] = 0;
                }
            }
            else
            {
                if (!response.IsSuccessStatusCode)
                {
                    Logger.Log("Noticed code " + response.StatusCode + " on api url " + apiUrl + endpoint);
                    return null;
                }
            }
        
            var content = await response.Content.ReadAsStringAsync();
            try
            {
                return JsonNode.Parse(content);
            }
            catch
            {
                return null;
            }
        }

        var response = await GetResponse();
        if (response == null)
        {
            var retries = 0;
            while (response == null && retries < maxRetries) {
                await Task.Delay(2000);
                response = await GetResponse();
                retries++;
            }
        }

        return response;

    }

    public string GetName()
    {
        return "Tidal (Hi-Fi API) " + (_isLosslessInstance ? "(Lossless)" :  _isHighQualityInstance ? "(High Quality)" : "(Lower Quality)");
    }

    public string GetId()
    {
        return "tidal-" + (_isLosslessInstance ? "lossless" : _isHighQualityInstance ? "high" : "lower");
    }

    public bool UrlPartOfPlatform(string url)
    {
        return new Uri(url).Host.Contains("tidal", StringComparison.OrdinalIgnoreCase);
    }

    public bool NeedsDependency(Dependency dependency, bool isAudioSource)
    {
        return false; // no direct dependencies
    }

    private async Task<Song?> ParseSong(JsonNode? json, int? knownYear = null)
    {
        try
        {
            return new Song(
                json?["album"]?["title"]?.ToString() ?? "",
                json?["artists"]?.AsArray().Select(a => a?["name"]?.ToString()).Where(s => s != null).Select(s => (string) s).ToArray(),
                json?["title"]?.ToString()!,
                (json?["duration"]?.GetValue<int>() ?? -1/1000) * 1000,
                json?["trackNumber"]?.GetValue<int>() ?? -1,
                json?["volumeNumber"]?.GetValue<int>() ?? -1,
                knownYear ?? (int.TryParse((await GetAlbumMetadata(json?["album"]?["id"]?.ToString() ?? ""))?["releaseDate"]?.ToString().Split("-")[0], out int year) ? year : -1),
                "https://resources.tidal.com/images/" + json?["album"]?["cover"]?.ToString().Replace("-", "/") + "/1280x1280.jpg",
                json?["url"]?.ToString() ?? "",
                GetId()
            );
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<Song>> Search(string query)
    {

        var response = await SearchForTrack(query);

        if (response == null)
        {
            return [];
        }

        var songs = response["items"];

        if (songs == null)
        {
            return [];
        }

        return (await Task.WhenAll(songs.AsArray().Select(s => ParseSong(s)))).Where(s => s != null).Select(s => s!).ToList();

    }

    public async Task<Song?> FindSong(Song originalSong)
    {

        if (originalSong.SourceApi.StartsWith("tidal-"))
        {
            return originalSong;
        }

        if (originalSong.Title.Length == 0)
        {
            return null;
        }
        
        var artistsNameJoined = string.Join(" ", originalSong.Artists);

        var songTitleClean = Regex.Replace(originalSong.Title, @"[\p{S}]+", " ").Trim();
        var artistsNamesClean = Regex.Replace(artistsNameJoined, @"[\p{S}]+", " ").Trim();

        if (songTitleClean.Length < originalSong.Title.Length * 0.4)
        {
            songTitleClean = originalSong.Title;
        }

        if (artistsNamesClean.Length < artistsNameJoined.Length * 0.6)
        {
            artistsNamesClean = artistsNameJoined;
        }

        List<Task<List<Song>>> tasks = [
            Search(artistsNamesClean + " " + songTitleClean)
        ];
        if (artistsNamesClean != artistsNameJoined || songTitleClean != originalSong.Title)
        {
            tasks.Add(Search(artistsNameJoined + " " + originalSong.Title));
        }

        var results = Helpers.ScoreFoundSongs((await Task.WhenAll(tasks)).SelectMany(x => x).Distinct().ToList(), originalSong, false, true);

        if (results.Count == 0)
        {
            return null;
        }

        var finalSong = results.OrderBy(x => -x.Key).ToList()[0].Value;
        return new Song(finalSong.Album, finalSong.Artists, finalSong.Title, finalSong.DurationMs, finalSong.IndexOnDisk,
            finalSong.DiskIndex, finalSong.ReleaseYear, finalSong.ImageUrl, finalSong.SongUrl, GetId());
        
    }

    public async Task<string?> DownloadSong(Song? song, string folder, Action<int> onProgressUpdate)
    {

        if (song == null)
        {
            return null;
        }
        if (!UrlPartOfPlatform(song.SongUrl))
        {
            throw new Exception("Tried to download non-tidal song over tidal api (this should not happen)");
        }

        var trackId = song.SongUrl.Split("/track/")[1];
        var quality = _isLosslessInstance ? "HI_RES_LOSSLESS" : (_isHighQualityInstance ? "HIGH" : "LOW");

        var response = await ApiRequestHiFi("/track/?id=" + trackId + "&quality=" + quality);
        
        if (response?["data"] == null)
        {
            return null;
        }

        if (response["data"]?["manifestMimeType"]?.ToString() == "application/vnd.tidal.bts")
        {
            
            var decodedManifest = JsonNode.Parse(Convert.FromBase64String(response["data"]?["manifest"]?.ToString() ?? Convert.ToBase64String("{}"u8.ToArray())));

            if (decodedManifest?["urls"] == null || decodedManifest["urls"]?.AsArray().Count < 1)
            {
                return null;
            }

            var downloaded = await Helpers.DownloadFile(decodedManifest["urls"]?[0]?.ToString(), folder, onProgressUpdate);

            return downloaded; 

        }
        if (response["data"]?["manifestMimeType"]?.ToString() == "application/dash+xml")
        { 
            // apparently yt-dlp seems to support mpd manifests, but it doesn't like local files, plus there may or may not be some quirks to tidal/hi-fi's returned manifests, so i do it myself here
            // mpd manifest decoding mostly ported over from binimum/tidal-ui
            
            var manifest = response["data"]?["manifest"]?.ToString();
            if (manifest == null)
            {
                return null;
            }

            var decodedManifest = Encoding.UTF8.GetString(Convert.FromBase64String(manifest));
            
            
            var mpd = XDocument.Parse(decodedManifest);
            var ns = mpd.Root?.Name.Namespace;

            bool ValidMediaUrl(string? url)
            {
                if (url == null)
                {
                    return false;
                }

                url = url.ToLower();
                if (url.Contains("w3.org") || url.Contains("xmlschema") || url.Contains("xmlns"))
                {
                    return false;
                }
                if (url.Contains(".flac") || url.Contains(".mp4") || url.Contains(".m4a") ||
                    url.Contains(".aac") || url.Contains("token=") || url.Contains("/audio/"))
                {
                    return true;
                }
                if (Regex.Match(url, @"\/[^\/]+\.[a-z0-9]{2,5}(\?|$)", RegexOptions.IgnoreCase).Success ||
                    Regex.Match(url, @"^[a-z0-9_-]+\/", RegexOptions.IgnoreCase).Success ||
                    Regex.Match(url, @"\/[a-z0-9_-]+$", RegexOptions.IgnoreCase).Success)
                {
                    return true;
                }

                return false;
            }

            if (decodedManifest.Contains("<SegmentTemplate"))
            {
                
                var parsedBaseUrl = mpd.Descendants(ns + "BaseURL").FirstOrDefault()?.Value.Trim();
                var baseUrl = parsedBaseUrl != null && ValidMediaUrl(parsedBaseUrl) ? parsedBaseUrl : null;

                XElement? template = null;
                string? codec = null;

                foreach (var rep in mpd.Descendants(ns + "Representation"))
                {
                    var cTemplate = rep.Descendants(ns + "SegmentTemplate").FirstOrDefault();
                    if (cTemplate == null)
                    {
                        continue;
                    }

                    var codecs = rep.Attribute("codecs")?.Value.ToLower() ?? "";

                    if (codecs.Contains("flac") || template == null)
                    {
                        template = cTemplate;
                        codec = codecs.Length > 0 ? codecs : null;
                        if (codecs.Contains("flac"))
                        {
                            break;
                        }
                    }

                }

                template ??= mpd.Descendants(ns + "SegmentTemplate").FirstOrDefault();

                if (template == null)
                {
                    return null;
                }

                var initUrl = template.Attribute("initialization")?.Value.Trim();
                var mediaUrlTemplate = template.Attribute("media")?.Value.Trim();

                if (initUrl == null || mediaUrlTemplate == null)
                {
                    return null;
                }

                var segmentNumber = Int32.Parse(template.Attribute("startNumber")?.Value ?? "1");
                var timelineParent = template.Descendants(ns + "SegmentTimeline").FirstOrDefault();
                var timeline = new List<KeyValuePair<int, int>>();

                if (timelineParent != null)
                {
                    var segments = timelineParent.Descendants(ns + "S");
                    foreach (var segment in segments)
                    {
                        var duration = Int32.Parse(segment.Attribute("d")?.Value ?? "0");
                        if (duration <= 0)
                        {
                            continue; 
                        } 
                        var repeat = Int32.Parse(segment.Attribute("r")?.Value ?? "0");
                        timeline.Add(new KeyValuePair<int, int>( duration, repeat ));
                    }
                }

                if (timeline.Count == 0)
                {
                    timeline.Add(new KeyValuePair<int, int>(0, 0));
                }

                string ResolveUrl(string url)
                {
                    if (Regex.Match(url, @"^https?:\/\/", RegexOptions.IgnoreCase).Success)
                    {
                        return url;
                    }

                    if (baseUrl != null)
                    {
                        try
                        {
                            return new Uri(new Uri(baseUrl), url).ToString();
                        }
                        catch
                        {
                            return Regex.Replace(baseUrl, @"\/+$", "") + "/" + Regex.Replace(url, @"^\/+", "");
                        }
                    }

                    return url;
                }

                List<string> allUrls = [
                    ResolveUrl(initUrl)
                ];

                foreach (var entry in timeline)
                {
                    var repeat = entry.Value;
                    var count = Math.Max(1, repeat + 1);
                    for (var i = 0; i < count; i += 1) {
                        allUrls.Add(ResolveUrl(mediaUrlTemplate.Replace("$Number$", segmentNumber.ToString())));
                        segmentNumber += 1;
                    }
                }
                
                using HttpClient client = new();

                var fName = trackId + ".flac";
                var path = Path.Join(folder, trackId + ".flac");
                await using var file = File.Create(path);

                var j = 0;
                foreach (var url in allUrls)
                {
                    await Helpers.DownloadFileToStream(url, file, progress =>
                    {
                        if (j > 0)
                        {
                            onProgressUpdate((int) Math.Round(progress / (allUrls.Count - 1d) + (j - 1d) * (100d / (allUrls.Count - 1d))));
                        }
                    });
                    j++;
                }

                return Path.Join(folder, fName);

            }

            int ScoreUrl(string? url)
            {
                if (url == null)
                {
                    return -1;
                }

                url = url.ToLower();
                var score = 0;
                
                if (url.Contains("flac")) {
                    score += 3;
                }
                if (url.Contains("hires")) {
                    score += 1;
                }
                if (url.EndsWith(".flac")) {
                    score += 4;
                }
                if (url.Contains("token=")) {
                    score += 1;
                }

                return score;
            }

            string? BestUrl(List<string> urls)
            {
                return urls
                    .Where(u => u.Length > 0 && ValidMediaUrl(u))
                    .OrderByDescending(u => ScoreUrl(u)).FirstOrDefault();
            }

            string? finalDownloadUrl = null;

            var baseUrls = mpd.Descendants(ns + "BaseURL").Select(u => u.Value.Trim()).ToList();
            if (baseUrls.Count > 0)
            {
                finalDownloadUrl = BestUrl(baseUrls);
            }
            
            if (finalDownloadUrl == null)
            {

                var matches = Regex.Matches(
                    decodedManifest, 
                    @"https?:\/\/[\w\-.~:?#[\]@!$&'()*+,;=%/]+",
                    RegexOptions.Multiline);

                foreach (var match in matches.ToList())
                {
                    var url = match.Value;
                    if (!url.Contains("$Number$") &&
                        !Regex.Match(url, @"\/\d+\.mp4").Success &&
                        ValidMediaUrl(url))
                    {
                        finalDownloadUrl = url;
                    }
                }

            }

            if (finalDownloadUrl != null)
            {
                return await Helpers.DownloadFile(finalDownloadUrl, folder, onProgressUpdate);
            }

        }

        return null;

    }

    public async Task<Song[]> GetSongs(string url)
    {

        var uri = new Uri(url);

        if (uri.AbsolutePath.Split("/")[1] == "playlist")
        {
            return (await GetTracksInPlaylist(uri.AbsolutePath.Split("/")[2]))
                .Select(async s => await ParseSong(s["item"]))
                .Select(s => s.Result)
                .Where(s => s != null)
                .ToArray()!;
        }
        
        if (uri.AbsolutePath.Split("/")[1] == "album" && uri.AbsolutePath.Split("/").Length <= 3)
        {
            var (albumTracks, year) = await GetTracksInAlbum(uri.AbsolutePath.Split("/")[2]);
            return albumTracks
                .Select(async s => await ParseSong(s["item"], year == -1 ? null : year))
                .Select(s => s.Result)
                .Where(s => s != null)
                .ToArray()!;
        }
        
        if ((uri.AbsolutePath.Split("/")[1] == "album" && uri.AbsolutePath.Split("/").Length > 3 && uri.AbsolutePath.Contains("track")) || uri.AbsolutePath.Split("/")[1] == "track")
        {
            var id = uri.AbsolutePath.Split("/")[1] == "track"
                ? uri.AbsolutePath.Split("/")[2]
                : uri.AbsolutePath.Split("/")[4];
            var meta = await GetTrackMetadata(id);
            if (meta == null)
            {
                return [];
            }
            var song = await ParseSong(meta);
            return song == null ? [] : [ song ];
        }

        return [];

    }
    
}