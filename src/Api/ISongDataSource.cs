using System.Collections.Generic;
using System.Threading.Tasks;
using Downloader.Api.Apis;
using Downloader.Utils;

namespace Downloader.Api;

public interface ISongDataSource : ISongApi
{
    
    Task<Song[]> GetSongs(string url);
    
    public static ISongDataSource? FromISongApi(ISongApi api)
    {
        return api is ISongDataSource api2 ? api2 : null;
    }
    
    public static readonly List<ISongDataSource> AllSongDataSources =
        AllApis.FindAll(s => s is ISongDataSource).ConvertAll(s => (ISongDataSource) s);

}