using System.Collections.Generic;
using System.Threading.Tasks;

namespace Downloader.Api.Apis;

public interface ISongApi
{
    
    public Task Init();
    public string GetName();
    public string GetId();

    public static readonly List<ISongApi> AllApis = [
        SpotifyApi.Instance, 
        YoutubeMusicApi.Instance
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