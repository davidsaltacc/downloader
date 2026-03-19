using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Downloader.Utils;

namespace Downloader.Api.Apis;

public class TidalApi : ISongAudioSource
{

    private static string? API_URL;

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

            return data?["api"]?.AsArray()
                .Where(u => u != null)
                .Select(u => u.AsObject().ToString())
                .ToArray();

        }
        
        foreach (var listUrl in apiUrlLists)
        {
            
        }
    }

    public string GetName()
    {
        return "Tidal (Hi-Fi API)";
    }

    public string GetId()
    {
        return "tidal";
    }

    public async Task<Song?> FindSong(Song originalSong)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> DownloadSong(Song song, string folder, Action<int> onProgressUpdate)
    {
        throw new NotImplementedException();
    }
    
}