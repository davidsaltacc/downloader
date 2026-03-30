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

public class TidalApi : ISongAudioSource
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

    private static readonly List<string> _blacklistedApis = [
    ];
    private List<string> _apiUrls = [];

    public async Task Init()
    {

        _apiUrls = [];

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

            if (data?["api"] == null)
            {
                return [];
            }

            return data["api"]?.AsArray()
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

                if ((versionMaj == "2" && Int32.Parse(versionMin ?? "0") < 7) || Int32.Parse(versionMaj ?? "0") < 2) // only versions 2.7 and above
                {
                    continue;
                }

                if (apiUrl == null)
                {
                    continue;
                }

                if (_blacklistedApis.FirstOrDefault(part => apiUrl.Contains(part)) != null)
                {
                    return;
                }
                
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(20));

                if (!new Uri(apiUrl).Host.EndsWith("monochrome.tf")) // only allow monochrome instances as they are the most maintained and therefore less prone to issues
                {
                    continue;
                }
                
                var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(apiUrl + "/track/?id=55391801")
                }, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                if (await ApiRequest("/search/?s=" + HttpUtility.UrlEncode("song"), apiUrl) == null)
                {
                    continue;
                }

                if (JsonNode.Parse(await response.Content.ReadAsStringAsync())?["data"]?["assetPresentation"]?
                        .ToString() != "FULL") // downloading not supported
                {
                    continue;
                }
                
                _apiUrls.Add(apiUrl);
            }
        }
        
    }

    private static readonly Random _random = new Random();

    private async Task<JsonNode?> ApiRequest(string endpoint, string? fixedApiUrl = null, int maxRetries = 15)
    {

        async Task<JsonNode?> GetResponse()
        {
            if (_apiUrls.Count == 0 && fixedApiUrl == null)
            {
                return null;
            }

            var apiUrl = fixedApiUrl ?? _apiUrls[_random.Next(0, _apiUrls.Count)]; // cycle to avoid rate limits 
        
            var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(apiUrl + endpoint)
            });

            if (!response.IsSuccessStatusCode)
            {
                Logger.Log("Noticed code " + response.StatusCode + " on api url " + apiUrl + endpoint);
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

        var response = await GetResponse();
        if (response == null)
        {
            var retries = 0;
            while (response == null && retries < maxRetries) {
                await Task.Delay(5000);
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

    private async Task<Song?> ParseSong(JsonNode? json)
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
                int.TryParse((await ApiRequest("/album/?id=" + json?["album"]?["id"]))?["data"]?["releaseDate"]?.ToString().Split("-")[0], out int year) ? year : -1,
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

        var response = await ApiRequest("/search/?s=" + HttpUtility.UrlEncode(query));

        if (response == null)
        {
            return [];
        }

        var songs = response["data"]?["items"];

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

        var response = await ApiRequest("/track/?id=" + trackId + "&quality=" + quality);
        
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
    
}