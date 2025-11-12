using System.Threading.Tasks;
using Downloader.Utils.Songs;

namespace Downloader.Apis;

public interface ISongDataSource<T> where T : Song
{
    
    Task<T[]> GetSongs(string url);

}