using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Downloader.Utils;

namespace Downloader.Api.Apis;

public class TidalApi : ISongAudioSource
{

    private readonly bool _isLosslessInstance;

    private TidalApi(bool lossless)
    {
        _isLosslessInstance = lossless;
    }

    private static TidalApi? _instanceLossless;
    private static TidalApi? _instanceNotLossless;
    
    public static TidalApi InstanceLossless
    {
        get
        {
            _instanceLossless ??= new TidalApi(true);
            return _instanceLossless;
        }
    }
    public static TidalApi InstanceNotLossless
    {
        get
        {
            _instanceNotLossless ??= new TidalApi(false);
            return _instanceNotLossless;
        }
    }

    private static string? _apiUrl;

    public async Task Init()
    {
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

                if (apiUrl == null)
                {
                    continue;
                }
                
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                
                var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(apiUrl)
                }, cts.Token);
                
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }
                
                _apiUrl = apiUrl;
                return;
            }
        }

        _apiUrl = null;
        
    }

    private async Task<JsonNode?> ApiRequest(string endpoint)
    {

        if (_apiUrl == null)
        {
            return null;
        }
        
        var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(_apiUrl + endpoint)
        });
        
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

    public string GetName()
    {
        return "Tidal (Hi-Fi API) " + (_isLosslessInstance ? "(Lossless)" : "(High Quality)");
    }

    public string GetId()
    {
        return "tidal-" + (_isLosslessInstance ? "lossless" : "not-lossless");
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

        var songs = response?["data"]?["items"];

        if (songs == null)
        {
            return [];
        }

        return (await Task.WhenAll(songs.AsArray().Select(s => ParseSong(s)))).Where(s => s != null).Select(s => (Song) s).ToList();

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

        var results = Helpers.ScoreFoundSongs((await Task.WhenAll(tasks)).SelectMany(x => x).Distinct().ToList(), originalSong, false);

        if (results.Count == 0)
        {
            return null;
        }

        var finalSong = results.OrderBy(x => -x.Key).ToList()[0].Value;
        return new Song(finalSong.Album, finalSong.Artists, finalSong.Title, finalSong.DurationMs, finalSong.IndexOnDisk,
            finalSong.DiskIndex, finalSong.ReleaseYear, finalSong.ImageUrl, finalSong.SongUrl, GetId());
        
    }

    public async Task<string?> DownloadSong(Song song, string folder, Action<int> onProgressUpdate)
    {

        if (!UrlPartOfPlatform(song.SongUrl))
        {
            throw new Exception("Tried to download non-tidal song over tidal api (this should not happen)");
        }

        string trackId = song.SongUrl.Split("/track/")[1];
        string quality = _isLosslessInstance ? "HI_RES_LOSSLESS" : "HIGH";

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

            var downloaded = await Helpers.DownloadFile(decodedManifest["urls"]?[0]?.ToString(), folder);

            return downloaded; 

        }
        if (response["data"]?["manifestMimeType"]?.ToString() == "application/dash+xml")
        {

            var manifest = response["data"]?["manifest"]?.ToString();
            if (manifest == null)
            {
                return null;
            }

            var decodedManifest = Encoding.UTF8.GetString(Convert.FromBase64String(manifest));
            var path = Path.Join(folder, trackId + "-manifest.mpd");
            await File.WriteAllTextAsync(path, decodedManifest);

            var downloaded = await YtDlpApi.DownloadSong(song, path, folder, onProgressUpdate, trackId);
            File.Delete(path);

            return downloaded;

        }

        return null;

    }
    
}