using System.Collections.Generic;
using System.Threading.Tasks;
using Downloader.Api.Apis;

namespace Downloader.Api;

public interface ISongApi
{
    
    public Task Init();
    public string GetName();
    public string GetId();
    public bool UrlPartOfPlatform(string url);

    public static readonly List<ISongApi> AllApis = [
        SpotifyApi.Instance, 
        TidalApi.InstanceLossless,
        TidalApi.InstanceHighQuality,
        TidalApi.InstanceLowerQuality,
        YoutubeMusicApi.Instance,
        SoundCloudApi.Instance
    ];

    public static ISongApi? GetApiById(string id)
    {
        foreach (var api in AllApis)
        {
            if (api.GetId() == id)
            {
                return api;
            }
        }

        return null;
    }

}