using System;
using System.Threading.Tasks;
using Downloader.Utils.Songs;

namespace Downloader.Apis;

public interface ISongAudioSource<T> : ISongApi where T : Song 
{
    
    string GetSongSourceUrl(T song);
    Task<T?> FindSong(Song originalSong);
    Task<string?> DownloadSong(T song, string folder, Action<int> onProgressUpdate);
    
}