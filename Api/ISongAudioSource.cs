using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Downloader.Api.Apis;
using Downloader.Utils.Songs;

namespace Downloader.Api;

public interface ISongAudioSource : ISongApi
{
    
    Task<Song?> FindSong(Song originalSong);
    Task<string?> DownloadSong(Song song, string folder, Action<int> onProgressUpdate);

    public static ISongAudioSource? FromISongApi(ISongApi api)
    {
        return api is ISongAudioSource api2 ? api2 : null;
    }

    public static readonly List<ISongAudioSource> AllSongAudioSources =
        AllApis.FindAll(s => s is ISongAudioSource).ConvertAll(s => (ISongAudioSource) s);

}