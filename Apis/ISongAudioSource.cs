using System.Threading.Tasks;
using Downloader.Utils.Songs;

namespace Downloader.Apis;

public interface ISongAudioSource<T> where T : Song
{
    
    Task<T> FindSong(Song originalSong);
    Task<string> DownloadSong(T song);
    
}