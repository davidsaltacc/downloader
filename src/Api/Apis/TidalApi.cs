using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
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
            foreach (var apiUrl in await getUrlsInList(listUrl))
            {
                var originalSpan = MainWindow.HttpClient.Timeout;
                MainWindow.HttpClient.Timeout = TimeSpan.FromSeconds(20);
                var response = await MainWindow.HttpClient.SendAsync(new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(apiUrl)
                });
                MainWindow.HttpClient.Timeout = originalSpan;
                
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

    public string GetName()
    {
        return "Tidal (Hi-Fi API) " + (_isLosslessInstance ? "(Lossless)" : "(High Quality)");
    }

    public string GetId()
    {
        return "tidal-" + (_isLosslessInstance ? "lossless" : "not-lossless");
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