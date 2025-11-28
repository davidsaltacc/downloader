using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Downloader.Utils;
using Downloader.Utils.Songs;
using SoftCircuits.HtmlMonkey;

namespace Downloader.Api.Apis;

public class SoundCloudApi : ISongAudioSource
{
    
    private static SoundCloudApi? _instance = null;
    public static SoundCloudApi Instance
    {
        get
        {
            _instance ??= new SoundCloudApi();
            return _instance;
        }
    }

    private string? _clientId;
    
    public async Task Init()
    {
        var document = await HtmlDocument.FromHtmlAsync(await Get("https://soundcloud.com"));
        var allScripts = document.Find("script");
        foreach (var script in allScripts)
        {
            var scriptUrl = script.Attributes["src"]?.Value;
            if (scriptUrl != null)
            {
                var scriptContent = await Get(scriptUrl);
                var match = Regex.Match(scriptContent, @"client_id=([a-zA-Z0-9]+)");
                if (!match.Success)
                {
                    continue;
                }
                _clientId = match.Groups[1].Value;
                break;
            }
        }
        
        return;

        async Task<string> Get(string url)
        {
            var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(url),
                Headers = {
                    { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" }
                }
            });
        
            return await response.Content.ReadAsStringAsync();
        }
    } 
    
    private async Task<HttpResponseMessage> SendApiRequest(string endpoint, Dictionary<string, string> parameters)
    {
        parameters["client_id"] = _clientId ?? "";
        return await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri("https://api-v2.soundcloud.com/" + endpoint + "?" + String.Join("&", new List<string>(parameters.Keys).ConvertAll(s => s + "=" + Uri.EscapeDataString(parameters[s])))),
            Headers = {
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.3" }
            }
        });
            
    }

    public string GetName()
    {
        return "SoundCloud";
    }

    public string GetId()
    {
        return "soundcloud";
    }

    public async Task<Song?> FindSong(Song originalSong)
    {
            if (originalSong.SourceApi == GetId())
            {
                return originalSong;
            }

            if (originalSong.Title.Length == 0)
            {
                return null;
            }

            var artistsNameJoined = string.Join(" ", originalSong.Artists);

            var songTitleClean = Regex.Replace(originalSong.Title, @"[\p{S}]+", " ").Trim();
            var albumTitleClean = Regex.Replace(originalSong.Album, @"[\p{S}]+", " ").Trim();
            var artistsNamesClean = Regex.Replace(artistsNameJoined, @"[\p{S}]+", " ").Trim();

            if (songTitleClean.Length < originalSong.Title.Length * 0.4)
            {
                songTitleClean = originalSong.Title;
            }

            if (albumTitleClean.Length < originalSong.Album.Length * 0.4)
            {
                albumTitleClean = originalSong.Album;
            }

            if (artistsNamesClean.Length < artistsNameJoined.Length * 0.6)
            {
                artistsNamesClean = artistsNameJoined;
            }

            var results = Utils.Utils.ScoreFoundSongs((await Task.WhenAll([
                Search(artistsNamesClean + " " + songTitleClean),
                Search(artistsNamesClean + " " + songTitleClean),
                Search(artistsNamesClean + " " + songTitleClean + " " + albumTitleClean),
                Search(artistsNameJoined + " " + originalSong.Title + " "),
                Search(artistsNameJoined + " " + originalSong.Title + " " + originalSong.Album)
            ])).SelectMany(x => x).Distinct().ToList(), originalSong);
            // TODO custom scoring -> allow "artist - title" in song titles, because a lot of them are reuploads

            if (results.Count == 0)
            {
                return null;
            }

            var finalSong = results.OrderBy(x => -x.Key).ToList()[0].Value;
            return new Song(originalSong.Album, originalSong.Artists, originalSong.Title, originalSong.DurationMs, originalSong.IndexOnDisk,
                originalSong.DiskIndex, originalSong.ReleaseYear, originalSong.ImageUrl, finalSong.SongUrl, GetId());
    }

    private Song? ParseSong(JsonNode? songData)
    {
        if (songData == null)
        {
            return null;
        }

        var song = new Song(
            songData["publisher_metadata"]?["album_title"]?.ToString() ?? "",
            [songData["user"]?["username"]?.ToString() ?? ""],
            songData["title"]?.ToString() ?? "",
            (songData["full_duration"] ?? songData["duration"])?.GetValue<int>() ?? 0,
            -1,
            -1,
            DateTime.TryParse(songData["release_date"]?.ToString(), out var time) ? time.Year : -1,
            (songData["artwork_url"]?.ToString() ?? "").Replace("-large",
                "-t500x500"), // for some reason apparently anything besides 500x500 and "large" (in reality tiny) are not supported
            songData["permalink_url"]?.ToString() ?? "",
            GetId()
        );
        if (song.Title.Length > 0 && song.SongUrl.Length > 0)
        {
            return song;
        }
        return null;
    }
    
    private async Task<List<Song>> Search(string query)
    {
        var response = await (await SendApiRequest("search/tracks", new Dictionary<string, string>
        {
            { "q", query },
            { "limit", "25" }
        })).Content.ReadAsStringAsync();

        List<Song> songs = [];

        var results = JsonNode.Parse(response)?["collection"]?.AsArray();
        
        foreach (var song in results ?? [])
        {
            if (song?["monetization_model"]?.ToString() != "BLACKBOX")
            {
                continue;
            }

            var parsedSong = ParseSong(song);
            if (parsedSong != null)
            {
                songs.Add(parsedSong);
            }
        }
        
        return songs;
    }

    public async Task<string?> DownloadSong(Song song, string folder, Action<int> onProgressUpdate)
    {
        return await YtDlpApi.DownloadSong(song, folder, onProgressUpdate, new Uri(song.SongUrl).AbsolutePath);
    }
}